using System.Security.Claims;
using Cms.Application.Features.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Api.Security;

public static class SessionCookies
{
    public const string Scheme = "cms-session";
    public const string CsrfCookieName = "XSRF-TOKEN";

    public static ClaimsPrincipal CreatePrincipal(SessionDto session)
    {
        var roles = (int)AccessRoleParser.Parse(session.Roles);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.Username),
            new(ClaimNames.TenantId, session.TenantId.ToString()),
            new(ClaimNames.DisplayName, session.DisplayName),
            new(ClaimNames.Roles, roles.ToString()),
            new(ClaimNames.MustChangePassword, session.MustChangePassword ? "true" : "false")
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>
    /// Signs the person in and issues an anti-forgery token for their new identity. A token is tied to who is signed in, so one fetched
    /// before sign-in would be refused afterwards; issuing it here means the browser never holds a stale one.
    /// </summary>
    public static async Task SignInAsync(HttpContext context, SessionDto session)
    {
        var principal = CreatePrincipal(session);
        context.RequestServices.GetRequiredService<IMemoryCache>().Remove($"session:{session.Username}");
        await context.SignInAsync(Scheme, principal, new AuthenticationProperties { IsPersistent = false });
        context.User = principal;
        IssueCsrfToken(context);
    }

    public static async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(Scheme);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        IssueCsrfToken(context);
    }

    public static void IssueCsrfToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(CsrfCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // the web app reads it and sends it back in X-XSRF-TOKEN
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
    }
}
