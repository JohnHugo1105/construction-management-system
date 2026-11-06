using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Capabilities;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The System readiness page: what the system depends on that may not be set up (file storage, virus scanning, email), each with its state,
/// what it means and what still works meanwhile. Administrators only (settings.manage). Read-only.
/// </summary>
public sealed class CapabilityEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api) =>
        api.MapFeature("/capabilities", "Capabilities")
            .MapGet("", async (CapabilityService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
            .RequirePermission(Permissions.SettingsManage);
}
