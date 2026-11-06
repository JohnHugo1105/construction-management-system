using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Schedule;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The project schedule: planned activities with dates, each covering budget lines, a baseline and the S-curve. Reading needs schedule.read
/// (the people who read the Progress tab: the schedule is weighted by budget amounts); planning needs schedule.write. Every change returns the
/// whole schedule as it now stands. An archived project's schedule is read-only.
/// </summary>
public sealed class ScheduleEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var schedule = api.MapFeature("/projects/{projectId:guid}/schedule", "Schedule");

        schedule.MapGet("", async (Guid projectId, ScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(projectId, ct))).RequirePermission(Permissions.ScheduleRead);

        schedule.MapGet("/s-curve", async (Guid projectId, ScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.CurveAsync(projectId, ct))).RequirePermission(Permissions.ScheduleRead);

        schedule.MapPost("/activities", async (Guid projectId, CreateScheduleActivityRequest request, ScheduleService service, CancellationToken ct) =>
        {
            var saved = await service.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/projects/{projectId}/schedule", saved);
        }).RequirePermission(Permissions.ScheduleWrite);

        schedule.MapPut("/activities/{id:guid}", async (Guid projectId, Guid id, UpdateScheduleActivityRequest request, ScheduleService service,
            CancellationToken ct) => Results.Ok(await service.UpdateAsync(projectId, id, request, ct))).RequirePermission(Permissions.ScheduleWrite);

        // Planning data, not a financial record: the activity is taken out of the plan with a reason, which stays on its row.
        schedule.MapPost("/activities/{id:guid}/remove", async (Guid projectId, Guid id, RemoveScheduleActivityRequest request, ScheduleService service,
            CancellationToken ct) => Results.Ok(await service.RemoveAsync(projectId, id, request, ct))).RequirePermission(Permissions.ScheduleWrite);

        schedule.MapPost("/baseline", async (Guid projectId, SetScheduleBaselineRequest request, ScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.SetBaselineAsync(projectId, request, ct))).RequirePermission(Permissions.ScheduleWrite);
    }
}
