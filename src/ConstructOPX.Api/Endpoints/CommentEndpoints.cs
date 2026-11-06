using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The discussion on a record. Reading is open to every role at the door (projects.read) because the record's own read permission decides it
/// in the service: purchasing.read for a purchase order, changeorders.read for a change order, billing.read for a bill, subcontracts.read for a
/// subcontract, projects.read for a project, and taking part for a task (a refusal is 403, or 404 for a task or another company's record).
/// Writing and removing also need tasks.use. Comments are never edited or deleted.
/// </summary>
public sealed class CommentEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var comments = api.MapFeature("/comments", "Comments");

        comments.MapGet("", async ([FromQuery] string? kind, [FromQuery] Guid targetId, [AsParameters] PageQuery page, CommentService service,
                CancellationToken ct) => Results.Ok(await service.ListAsync(kind, targetId, page.ToRequest(), ct)))
            .RequirePermission(Permissions.ProjectsRead);

        comments.MapPost("", async (CreateCommentRequest request, CommentService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/comments/{created.Id}", created);
        }).RequirePermission(Permissions.TasksUse);

        comments.MapPost("/{id:guid}/void", async (Guid id, RemoveCommentRequest request, CommentService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveAsync(id, request, ct))).RequirePermission(Permissions.TasksUse);
    }
}
