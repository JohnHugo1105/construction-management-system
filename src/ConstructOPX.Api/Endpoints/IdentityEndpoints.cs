using ConstructOPX.Api.Security;
using ConstructOPX.Application.Features.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace ConstructOPX.Api.Endpoints;

public sealed class IdentityEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/identity", "Identity");

        // The web app asks these before anyone is signed in.
        group.MapGet("/csrf", (HttpContext context) =>
        {
            SessionCookies.IssueCsrfToken(context);
            return Results.NoContent();
        }).AllowAnonymous();

        // First-time setup is offered only while it is switched on in configuration AND the system has no users, so a production
        // deployment that forgot the switch never shows (or accepts) an open "create the administrator" page.
        group.MapGet("/setup-status", async (AuthService auth, IConfiguration configuration, CancellationToken ct) =>
            Results.Ok(new SetupStatusDto(configuration.GetValue("Bootstrap:Enabled", false) && await auth.NeedsSetupAsync(ct)))).AllowAnonymous();

        group.MapPost("/sign-in", async (SignInRequest request, HttpContext context, AuthService auth, CancellationToken ct) =>
        {
            var signedIn = await auth.SignInAsync(request, ct);
            await SessionCookies.SignInAsync(context, signedIn);
            return Results.Ok(signedIn.Session);
        }).AllowAnonymous().RequireRateLimiting("sign-in");

        // First-time setup: allowed once, while the system has no users, and only when switched on in configuration.
        group.MapPost("/bootstrap", async (BootstrapRequest request, HttpContext context, AuthService auth, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!configuration.GetValue("Bootstrap:Enabled", false)) return Results.NotFound();
            var signedIn = await auth.BootstrapAsync(request, ct);
            await SessionCookies.SignInAsync(context, signedIn);
            return Results.Created("/api/identity/session", signedIn.Session);
        }).AllowAnonymous().RequireRateLimiting("sign-in");

        // Signing out ends the person's sessions everywhere, so it needs the person's own valid session. A request with a dead or missing
        // cookie only has its cookie cleared.
        group.MapPost("/sign-out", async (HttpContext context, AuthService auth, IMemoryCache cache, CancellationToken ct) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var username = context.User.Identity.Name ?? "";
                await auth.SignOutAsync(ct);
                SessionValidator.Forget(cache, username);
            }
            await SessionCookies.SignOutAsync(context);
            return Results.NoContent();
        }).AllowAnonymous();

        group.MapGet("/session", async (AuthService auth, CancellationToken ct) => Results.Ok(await auth.GetSessionAsync(ct)));

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context, AuthService auth, IMemoryCache cache, CancellationToken ct) =>
        {
            var username = context.User.Identity?.Name ?? "";
            var signedIn = await auth.ChangePasswordAsync(request, ct);
            SessionValidator.Forget(cache, username);
            await SessionCookies.SignInAsync(context, signedIn);
            return Results.Ok(signedIn.Session);
        }).RequireRateLimiting("sign-in");
    }

    public sealed record SetupStatusDto(bool NeedsSetup);
}
