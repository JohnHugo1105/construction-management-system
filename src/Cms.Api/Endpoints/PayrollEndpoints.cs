using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

public sealed class PayrollEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/payroll-runs", "Payroll");

        group.MapGet("", async ([FromQuery] string? status, [AsParameters] PageQuery page, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.ListAsync(new PayrollRunListQuery(status), page.ToRequest(), ct))).RequirePermission(Permissions.PayrollRead);

        group.MapGet("/{id:guid}", async (Guid id, PayrollService payroll, CancellationToken ct) => Results.Ok(await payroll.GetAsync(id, ct)))
            .RequirePermission(Permissions.PayrollRead);

        group.MapGet("/{id:guid}/export", async (Guid id, PayrollService payroll, CancellationToken ct) =>
        {
            var file = await payroll.ExportAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission(Permissions.PayrollRead);

        group.MapPost("", async (CreatePayrollRunRequest request, PayrollService payroll, CancellationToken ct) =>
        {
            var created = await payroll.CreateAsync(request, ct);
            return Results.Created($"/api/payroll-runs/{created.Id}", created);
        }).RequirePermission(Permissions.PayrollRun);

        group.MapPut("/{id:guid}/workers/{workerId:guid}/deductions", async (Guid id, Guid workerId, SetDeductionsRequest request, PayrollService payroll,
                CancellationToken ct) =>
            Results.Ok(await payroll.SetDeductionsAsync(id, workerId, request, ct))).RequirePermission(Permissions.PayrollRun);

        // Contributions and tax: what each agency is owed for the payroll, a person's change to one worker's calculated amount, and
        // working them out again for a draft.
        group.MapGet("/{id:guid}/statutory", async (Guid id, PayrollService payroll, CancellationToken ct) => Results.Ok(await payroll.GetStatutoryAsync(id, ct)))
            .RequirePermission(Permissions.PayrollRead);

        group.MapPost("/{id:guid}/workers/{workerId:guid}/statutory/{agency}/adjust", async (Guid id, Guid workerId, string agency,
                AdjustStatutoryRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.AdjustStatutoryAsync(id, workerId, agency, request, ct))).RequirePermission(Permissions.PayrollRun);

        group.MapPost("/{id:guid}/recalculate-statutory", async (Guid id, VersionRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.RecalculateStatutoryAsync(id, request, ct))).RequirePermission(Permissions.PayrollRun);

        group.MapPost("/{id:guid}/approve", async (Guid id, ApprovePayrollRunRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.ApproveAsync(id, request, ct))).RequirePermission(Permissions.PayrollApprove);

        // A draft needs payroll.run; the service also asks for payroll.approve when the payroll was approved.
        group.MapPost("/{id:guid}/void", async (Guid id, VoidPayrollRunRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.VoidAsync(id, request, ct))).RequirePermission(Permissions.PayrollRun);
    }
}
