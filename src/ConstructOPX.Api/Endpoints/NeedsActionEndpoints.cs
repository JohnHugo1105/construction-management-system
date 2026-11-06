using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.NeedsAction;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// "Needs your action": what waits on the signed-in person, and the count for the navigation badge. Everyone holds actions.read; which
/// groups come back is decided by the service from the caller's own roles, never from anything in the request.
/// </summary>
public sealed class NeedsActionEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/needs-action", "Needs action");

        group.MapGet("", async (NeedsActionService service, CancellationToken ct) => Results.Ok(await service.GetAsync(ct)))
            .RequirePermission(Permissions.ActionsRead);

        group.MapGet("/count", async (NeedsActionService service, CancellationToken ct) => Results.Ok(await service.CountAsync(ct)))
            .RequirePermission(Permissions.ActionsRead);
    }
}
