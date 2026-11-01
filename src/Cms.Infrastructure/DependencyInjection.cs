using Cms.Application.Common;
using Cms.Infrastructure.Persistence;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Cms";

    /// <summary>
    /// Registers persistence and adapters. Classes in this assembly named <c>*Repository</c> or <c>*Reader</c> are registered against the
    /// Application ports they implement, so a feature adds its adapters without editing this file.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"The connection string \"{ConnectionStringName}\" is not configured. Set ConnectionStrings__{ConnectionStringName} or CMS_DATABASE_CONNECTION.");

        // Interceptors registered in the container (tests add a statement counter) are attached to every context.
        services.AddDbContext<CmsDbContext>((provider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(provider.GetServices<IInterceptor>()));
        services.Configure<PasswordHashingOptions>(options =>
        {
            if (int.TryParse(configuration[$"{PasswordHashingOptions.SectionName}:Iterations"], out var iterations)) options.Iterations = iterations;
        });
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditTrail, AuditTrail>();

        var applicationAssembly = typeof(IClock).Assembly;
        var adapters = typeof(CmsDbContext).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && (type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                type.Name.EndsWith("Reader", StringComparison.Ordinal)));
        foreach (var adapter in adapters)
            foreach (var port in adapter.GetInterfaces().Where(item => item.Assembly == applicationAssembly))
                services.AddScoped(port, adapter);

        return services;
    }
}
