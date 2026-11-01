using Cms.Application.Common;
using Cms.Application.Features.Statutory;
using Cms.Application.Features.Workforce;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

/// <summary>
/// The contribution and tax tables. People who can see payroll read them (and the capability check); only accountants and
/// administrators (statutory.manage) add, change and retire them.
/// </summary>
public sealed class StatutoryEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/statutory", "Statutory");

        group.MapGet("/status", async ([FromQuery] DateOnly? asOf, StatutoryService statutory, CancellationToken ct) => Results.Ok(await statutory.StatusAsync(asOf, ct)))
            .RequirePermission(Permissions.PayrollRead);

        group.MapGet("/schedules", async ([FromQuery] string? agency, [FromQuery] bool? includeRetired, [AsParameters] PageQuery page,
                StatutoryService statutory, CancellationToken ct) =>
            Results.Ok(await statutory.ListAsync(new StatutoryScheduleListQuery(agency, includeRetired ?? false), page.ToRequest(), ct)))
            .RequirePermission(Permissions.PayrollRead);

        group.MapGet("/schedules/{id:guid}", async (Guid id, StatutoryService statutory, CancellationToken ct) => Results.Ok(await statutory.GetAsync(id, ct)))
            .RequirePermission(Permissions.PayrollRead);

        group.MapPost("/schedules", async (SaveStatutoryScheduleRequest request, StatutoryService statutory, CancellationToken ct) =>
        {
            var created = await statutory.CreateAsync(request, ct);
            return Results.Created($"/api/statutory/schedules/{created.Id}", created);
        }).RequirePermission(Permissions.StatutoryManage);

        group.MapPut("/schedules/{id:guid}", async (Guid id, SaveStatutoryScheduleRequest request, StatutoryService statutory, CancellationToken ct) =>
            Results.Ok(await statutory.UpdateAsync(id, request, ct))).RequirePermission(Permissions.StatutoryManage);

        group.MapPost("/schedules/{id:guid}/retire", async (Guid id, VersionRequest request, StatutoryService statutory, CancellationToken ct) =>
            Results.Ok(await statutory.RetireAsync(id, request, ct))).RequirePermission(Permissions.StatutoryManage);

        // A worker's contribution flags decide pay deductions: people who manage workers change them, and the service also asks for
        // payroll.read, because only people who may see pay see the flags.
        var workers = api.MapFeature("/workers", "Workers");
        workers.MapPut("/{id:guid}/statutory", async (Guid id, SetWorkerStatutoryRequest request, WorkerService service, CancellationToken ct) =>
            Results.Ok(await service.SetStatutoryAsync(id, request, ct))).RequirePermission(Permissions.WorkforceManage);
    }
}
