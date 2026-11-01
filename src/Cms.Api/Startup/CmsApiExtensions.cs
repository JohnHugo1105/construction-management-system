using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cms.Api.Endpoints;
using Cms.Api.Errors;
using Cms.Api.Security;
using Cms.Application.Common;
using Cms.Domain.Features.Identity;
using Cms.Infrastructure;
using Cms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Startup;

public static class CmsApiExtensions
{
    public static IServiceCollection AddCmsApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Cookies are Secure everywhere except local development. The reverse proxy terminates TLS and sends X-Forwarded-Proto, which the
        // forwarded-headers middleware turns back into https. Security:RequireSecureCookies=false exists for the plain-HTTP test host only.
        var secureCookies = configuration.GetValue("Security:RequireSecureCookies", !environment.IsDevelopment())
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddApplicationServices();
        services.AddInfrastructure(configuration);

        services.AddAuthentication(SessionCookies.Scheme).AddCookie(SessionCookies.Scheme, options =>
        {
            options.Cookie.Name = "cms.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = secureCookies;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;
            // An API answers 401 or 403, never a redirect to a login page.
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    int.TryParse(context.User.FindFirst(ClaimNames.Roles)?.Value, out var roles) &&
                    Permissions.IsAllowed((AccessRole)roles, permission)));
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "cms.antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = secureCookies;
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var permits = configuration.GetValue("RateLimiting:SignInPerMinute", 10);
            options.AddPolicy("sign-in", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = (context, _) => new ValueTask(ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                "Too many attempts", "Wait a minute, then try again.", "rate_limited"));
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // The API runs behind the reverse proxy in its own network, so the proxy's address is not known in advance.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddOpenApi();
        return services;
    }

    /// <summary>Registers every use-case service (public classes named <c>*Service</c> in the Application assembly).</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var useCases = typeof(IClock).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true } && type.Name.EndsWith("Service", StringComparison.Ordinal));
        foreach (var useCase in useCases) services.AddScoped(useCase);
        return services;
    }

    public static WebApplication UseCmsPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment()) app.UseHsts();

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            await next();
        });

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<PasswordChangeRequiredMiddleware>();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapCmsEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoints();

        var api = app.MapGroup("/api").AddEndpointFilter<PrivateResponseFilter>().AddEndpointFilter<AntiforgeryFilter>();
        var modules = typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IEndpointModule).IsAssignableFrom(type));
        foreach (var module in modules) ((IEndpointModule)Activator.CreateInstance(module)!).Map(api);

        if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
        return app;
    }

    /// <summary>Applies pending migrations at start-up when <c>Database:MigrateOnStartup</c> is true (development and tests; production applies a reviewed script).</summary>
    public static async Task MigrateIfRequestedAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", false)) return;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        // Migrations are the only way a real database changes. Until the first migration has been generated, a development database is
        // created straight from the model so the application can run; once any migration exists this always takes the migration path.
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }
}
