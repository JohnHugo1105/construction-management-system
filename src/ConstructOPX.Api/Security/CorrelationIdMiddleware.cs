using System.Security.Claims;

namespace ConstructOPX.Api.Security;

/// <summary>
/// Gives every request a correlation id (the caller's, if it sent a sane one) and puts it, with the tenant and user, on every log line
/// written while the request runs. It is returned in the <c>X-Correlation-Id</c> header and in error responses, so a person can quote it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string ItemKey = "constructopx.correlation-id";
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 64 && supplied.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            ? supplied
            : Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = id;
        context.Response.Headers[HeaderName] = id;

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = id,
            ["TenantId"] = context.User.FindFirstValue(ClaimNames.TenantId),
            ["UserId"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        }))
        {
            await next(context);
        }
    }
}
