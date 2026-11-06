using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ConstructOPX.Application.Features.TimeClock;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ConstructOPX.Api.Security;

/// <summary>
/// The second way into the API, for time clock machines only. A machine sends its device key in the <c>X-Device-Key</c> header; the key's SHA-256
/// hash is looked up (the key never reaches the database), compared again in constant time, and the request then belongs to the device's company,
/// and only to it. A device principal carries no roles, so it satisfies no permission policy: it can reach the one route that names the device
/// policy and nothing else, and that route accepts no cookie. An unknown, revoked or malformed key gets the same 401 with no detail.
/// </summary>
public static class DeviceKeyAuthentication
{
    public const string Scheme = "constructopx-device";

    /// <summary>The authorization policy of the machine route. It is not a permission: no person holds it.</summary>
    public const string Policy = "timeclock.device";

    public const string DeviceClaim = "constructopx:device";

    /// <summary>The per-device rate limit policy of the machine route.</summary>
    public const string RateLimitPolicy = "device-punches";

    public const int DefaultRequestsPerMinute = 120;

    /// <summary>
    /// The rate limit partition of a request: a hash of the key it carries (so each device, and each guessed key, has its own budget), or the caller's
    /// address when it carries none. The key itself is not kept in memory.
    /// </summary>
    public static string PartitionOf(HttpContext context)
    {
        var key = context.Request.Headers[DeviceKeys.HeaderName].ToString();
        if (key.Length == 0) return "address:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var bounded = key.Length > 128 ? key[..128] : key;
        return "device:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bounded)));
    }

    /// <summary>The device a machine request authenticated as.</summary>
    public static Guid DeviceId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(DeviceClaim), out var id) ? id : throw new UnauthorizedAccessException("Not a time clock.");
}

public sealed class DeviceKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    ITimeClockDeviceKeyReader devices) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DeviceKeys.HeaderName, out var values)) return AuthenticateResult.NoResult();

        // Every refusal is the same, so a caller learns nothing about why.
        var hash = values.Count == 1 ? DeviceKeys.HashOf(values[0]) : null;
        if (hash is null) return AuthenticateResult.Fail("Refused.");
        var device = await devices.FindByKeyHashAsync(hash, Context.RequestAborted);
        if (device is null || !device.IsActive || !DeviceKeys.Matches(device.KeyHash, hash)) return AuthenticateResult.Fail("Refused.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimNames.TenantId, device.TenantId.ToString()),
            new Claim(DeviceKeyAuthentication.DeviceClaim, device.DeviceId.ToString())
        ], DeviceKeyAuthentication.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DeviceKeyAuthentication.Scheme));
    }
}
