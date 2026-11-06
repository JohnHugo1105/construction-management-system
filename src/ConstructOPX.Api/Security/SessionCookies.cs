using System.Security.Claims;
using ConstructOPX.Application.Features.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace ConstructOPX.Api.Security;

public static class SessionCookies
{
    public const string Scheme = "constructopx-session";
    public const string CsrfCookieName = "XSRF-TOKEN";

    public static ClaimsPrincipal CreatePrincipal(SignedInSession signedIn)
    {
        var session = signedIn.Session;
        var roles = (int)AccessRoleParser.Parse(session.Roles);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.Username),
            new(ClaimNames.TenantId, session.TenantId.ToString()),
            new(ClaimNames.DisplayName, session.DisplayName),
            new(ClaimNames.Roles, roles.ToString()),
            new(ClaimNames.MustChangePassword, session.MustChangePassword ? "true" : "false"),
            new(ClaimNames.SecurityStamp, signedIn.SecurityStamp.ToString("N"))
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>
    /// Signs the person in and issues an anti-forgery token for their new identity. A token is tied to who is signed in, so one fetched
    /// before sign-in would be refused afterwards; issuing it here means the browser never holds a stale one.
    /// </summary>
    public static async Task SignInAsync(HttpContext context, SignedInSession signedIn)
    {
        var principal = CreatePrincipal(signedIn);
        context.RequestServices.GetRequiredService<IMemoryCache>().Remove($"session:{signedIn.Session.Username}");
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
