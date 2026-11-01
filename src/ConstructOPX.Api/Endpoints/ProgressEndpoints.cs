using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Progress;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// How far along each budget line is and what that says about the project (earned value). Reading needs progress.read; recording needs
/// progress.write. Progress is only ever added: there is no edit or delete.
/// </summary>
public sealed class ProgressEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var inProject = api.MapFeature("/projects/{projectId:guid}/progress", "Progress");

        inProject.MapGet("", async (Guid projectId, [FromQuery] DateOnly? asOf, ProgressService progress, CancellationToken ct) =>
            Results.Ok(await progress.GetAsync(projectId, asOf, ct)))
            .RequirePermission(Permissions.ProgressRead);

        inProject.MapPost("", async (Guid projectId, RecordProgressRequest request, ProgressService progress, CancellationToken ct) =>
        {
            var recorded = await progress.RecordAsync(projectId, request, ct);
            return Results.Created($"/api/projects/{projectId}/progress", recorded);
        }).RequirePermission(Permissions.ProgressWrite);

        api.MapFeature("/budget-lines/{id:guid}/progress", "Progress").MapGet("", async (Guid id, [AsParameters] PageQuery page, ProgressService progress,
                CancellationToken ct) => Results.Ok(await progress.HistoryAsync(id, page.ToRequest(), ct)))
            .RequirePermission(Permissions.ProgressRead);
    }
}
