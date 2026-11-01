using System.Security.Claims;
using Cms.Application.Features.Identity;
using Cms.Domain.Features.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Api.Security;

/// <summary>
/// Checks on every request that the person behind the cookie may still sign in, still holds the same roles, and that the session has not
/// been ended (password change or reset, account turned off, signing out). A turned-off account, a changed role or a copied cookie
/// therefore stops working within seconds, not when the cookie expires. The answer is cached briefly to spare the database; the server
/// that made the change forgets it at once, so other servers behind a load balancer would take up to <c>CacheFor</c> longer.
/// </summary>
public static class SessionValidator
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var username = principal?.FindFirstValue(ClaimTypes.Name);
        if (principal is null || username is null || !int.TryParse(principal.FindFirstValue(ClaimNames.Roles), out var rolesInCookie))
        {
            await Reject(context);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();
        var state = await cache.GetOrCreateAsync($"session:{username}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var user = await services.GetRequiredService<IUserRepository>().FindByUsernameAsync(username, context.HttpContext.RequestAborted);
            return user is null ? null : new SessionState(user.Id, user.IsActive, user.Roles, user.MustChangePassword, user.SecurityStamp.ToString("N"));
        });

        var cookieUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var cookieMustChange = principal.FindFirstValue(ClaimNames.MustChangePassword) == "true";
        var cookieStamp = principal.FindFirstValue(ClaimNames.SecurityStamp);
        if (state is null || !state.IsActive || state.UserId.ToString() != cookieUserId || (int)state.Roles != rolesInCookie ||
            state.MustChangePassword != cookieMustChange || !string.Equals(state.SecurityStamp, cookieStamp, StringComparison.Ordinal))
            await Reject(context);
    }

    /// <summary>Forget the cached answer for a person right after their account changes, so the change is immediate on this server.</summary>
    public static void Forget(IMemoryCache cache, string username) => cache.Remove($"session:{username}");

    private static async Task Reject(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(SessionCookies.Scheme);
    }

    private sealed record SessionState(Guid UserId, bool IsActive, AccessRole Roles, bool MustChangePassword, string SecurityStamp);
}
