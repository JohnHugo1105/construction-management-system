using System.Security.Claims;
using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Api.Security;

/// <summary>The signed-in person, read from the session cookie's claims. Nobody signed in means an empty tenant, which matches no data.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public Guid TenantId => IsAuthenticated && Guid.TryParse(Principal?.FindFirstValue(ClaimNames.TenantId), out var id) ? id : Guid.Empty;

    public string UserName => Principal?.FindFirstValue(ClaimTypes.Name) ?? "";

    public string DisplayName => Principal?.FindFirstValue(ClaimNames.DisplayName) ?? "";

    public AccessRole Roles => int.TryParse(Principal?.FindFirstValue(ClaimNames.Roles), out var value) ? (AccessRole)value : AccessRole.None;

    public string? CorrelationId => accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string ?? accessor.HttpContext?.TraceIdentifier;
}
