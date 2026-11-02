using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// Tasks given to a person. Everything needs tasks.use; a task is seen only by the person who created it, the person it is assigned to and
/// Administrators (anyone else gets 404), and "every task" is for Administrators only. A task never carries an amount.
/// </summary>
public sealed class TaskEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var tasks = api.MapFeature("/tasks", "Tasks");

        tasks.MapGet("", async ([FromQuery] string? scope, [FromQuery] string? status, [FromQuery] Guid? projectId, [AsParameters] PageQuery page,
                WorkTaskService service, CancellationToken ct) => Results.Ok(await service.ListAsync(scope, status, projectId, page.ToRequest(), ct)))
            .RequirePermission(Permissions.TasksUse);

        tasks.MapGet("/{id:guid}", async (Guid id, WorkTaskService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)))
            .RequirePermission(Permissions.TasksUse);

        tasks.MapPost("", async (CreateTaskRequest request, WorkTaskService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/tasks/{created.Id}", created);
        }).RequirePermission(Permissions.TasksUse);

        tasks.MapPost("/{id:guid}/complete", async (Guid id, VersionRequest request, WorkTaskService service, CancellationToken ct) =>
            Results.Ok(await service.CompleteAsync(id, request, ct))).RequirePermission(Permissions.TasksUse);

        tasks.MapPost("/{id:guid}/cancel", async (Guid id, TaskReasonRequest request, WorkTaskService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(id, request, ct))).RequirePermission(Permissions.TasksUse);

        tasks.MapPost("/{id:guid}/reopen", async (Guid id, VersionRequest request, WorkTaskService service, CancellationToken ct) =>
            Results.Ok(await service.ReopenAsync(id, request, ct))).RequirePermission(Permissions.TasksUse);

        tasks.MapPost("/{id:guid}/reassign", async (Guid id, ReassignTaskRequest request, WorkTaskService service, CancellationToken ct) =>
            Results.Ok(await service.ReassignAsync(id, request, ct))).RequirePermission(Permissions.TasksUse);
    }
}
