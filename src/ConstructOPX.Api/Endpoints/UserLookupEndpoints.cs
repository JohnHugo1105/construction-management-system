using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// Colleagues by name for the task assignee picker and the "@" mention picker (tasks.use). Returns only <c>{ id, displayName }</c> of active
/// colleagues: never a role, an email or a username. With a record, only the people who could read it.
/// </summary>
public sealed class UserLookupEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api) =>
        api.MapFeature("/users/lookup", "Users").MapGet("", async ([FromQuery] string? search, [FromQuery] string? targetKind, [FromQuery] Guid? targetId,
                UserLookupService service, CancellationToken ct) => Results.Ok(await service.LookupAsync(search, targetKind, targetId, ct)))
            .RequirePermission(Permissions.TasksUse);
}
