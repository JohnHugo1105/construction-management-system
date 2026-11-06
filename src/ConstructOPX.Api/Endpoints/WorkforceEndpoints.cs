using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Workforce;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

public sealed class WorkforceEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/workers", "Workforce");

        group.MapGet("", async ([FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? projectId, [AsParameters] PageQuery page,
                WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.ListAsync(new WorkerListQuery(search, status, projectId), page.ToRequest(), ct)))
            .RequirePermission(Permissions.WorkforceRead);

        group.MapGet("/lookup", async ([FromQuery] string? search, [FromQuery] bool? includeLeft, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.LookupAsync(search, includeLeft ?? false, ct))).RequirePermission(Permissions.WorkforceRead);

        group.MapGet("/{id:guid}", async (Guid id, WorkerService workers, CancellationToken ct) => Results.Ok(await workers.GetAsync(id, ct)))
            .RequirePermission(Permissions.WorkforceRead);

        group.MapPost("", async (HireWorkerRequest request, WorkerService workers, CancellationToken ct) =>
        {
            var hired = await workers.HireAsync(request, ct);
            return Results.Created($"/api/workers/{hired.Id}", hired);
        }).RequirePermission(Permissions.WorkforceManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkerRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.UpdateAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/change-rate", async (Guid id, ChangeWorkerRateRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.ChangeRateAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/assign", async (Guid id, AssignWorkerRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.AssignAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        // Posts a whole crew to one project. All or nothing; a refusal lists each worker who couldn't be posted and why.
        group.MapPost("/assign", async (BulkAssignWorkersRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.AssignManyAsync(request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/release", async (Guid id, ReleaseWorkerRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.ReleaseAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/status", async (Guid id, ChangeWorkerStatusRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.ChangeStatusAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/reinstate", async (Guid id, ReinstateWorkerRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.ReinstateAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);

        group.MapPost("/{id:guid}/cases", async (Guid id, RecordWorkerCaseRequest request, WorkerService workers, CancellationToken ct) =>
            Results.Ok(await workers.RecordCaseAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);
    }
}
