using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Payroll;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

public sealed class AttendanceEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/attendance", "Attendance");

        group.MapGet("", async ([FromQuery] Guid? workerId, [FromQuery] Guid? projectId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                [FromQuery] bool? includeVoided, [AsParameters] PageQuery page, AttendanceService attendance, CancellationToken ct) =>
            Results.Ok(await attendance.ListAsync(new AttendanceListQuery(workerId, projectId, from, to, includeVoided ?? false), page.ToRequest(), ct)))
            .RequirePermission(Permissions.WorkforceRead);

        group.MapPost("", async (RecordAttendanceRequest request, AttendanceService attendance, CancellationToken ct) =>
        {
            var recorded = await attendance.RecordAsync(request, ct);
            return Results.Created($"/api/projects/{recorded.ProjectId}/attendance?date={recorded.WorkDate:O}", recorded);
        }).RequirePermission(Permissions.AttendanceWrite);

        // A crew's day on one project, all or nothing.
        group.MapPost("/batch", async (RecordAttendanceBatchRequest request, AttendanceService attendance, CancellationToken ct) =>
        {
            var recorded = await attendance.RecordBatchAsync(request, ct);
            return Results.Created($"/api/projects/{request.ProjectId}/attendance?date={request.WorkDate:O}", recorded);
        }).RequirePermission(Permissions.AttendanceWrite);

        group.MapPut("/{id:guid}", async (Guid id, ReviseAttendanceRequest request, AttendanceService attendance, CancellationToken ct) =>
            Results.Ok(await attendance.ReviseAsync(id, request, ct))).RequirePermission(Permissions.AttendanceWrite);

        group.MapPost("/{id:guid}/void", async (Guid id, VoidAttendanceRequest request, AttendanceService attendance, CancellationToken ct) =>
            Results.Ok(await attendance.VoidAsync(id, request, ct))).RequirePermission(Permissions.AttendanceWrite);

        // One project's sheet for a day (today when no date is given).
        var sheet = api.MapGroup("/projects/{projectId:guid}/attendance").WithTags("Attendance");
        sheet.MapGet("", async (Guid projectId, [FromQuery] DateOnly? date, AttendanceService attendance, CancellationToken ct) =>
            Results.Ok(await attendance.SheetAsync(projectId, date, ct))).RequirePermission(Permissions.WorkforceRead);
    }
}
