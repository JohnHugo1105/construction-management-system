using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Payroll;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

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

        // The 13th-month pay for a calendar year: prepared like a payroll, approved and voided on the same routes as any payroll.
        group.MapPost("/thirteenth-month", async (CreateThirteenthMonthRunRequest request, PayrollService payroll, CancellationToken ct) =>
        {
            var created = await payroll.CreateThirteenthMonthAsync(request, ct);
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

        // Works a draft out again: its pay (pay rules, holidays, rest days) and then its contributions and tax.
        group.MapPost("/{id:guid}/recalculate-statutory", async (Guid id, VersionRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.RecalculateStatutoryAsync(id, request, ct))).RequirePermission(Permissions.PayrollRun);

        group.MapPost("/{id:guid}/approve", async (Guid id, ApprovePayrollRunRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.ApproveAsync(id, request, ct))).RequirePermission(Permissions.PayrollApprove);

        // A draft needs payroll.run; the service also asks for payroll.approve when the payroll was approved.
        group.MapPost("/{id:guid}/void", async (Guid id, VoidPayrollRunRequest request, PayrollService payroll, CancellationToken ct) =>
            Results.Ok(await payroll.VoidAsync(id, request, ct))).RequirePermission(Permissions.PayrollRun);

        // The pay rules (premium factors) and the holiday calendar: read with payroll.read, kept with payrollrules.manage.
        var rules = api.MapFeature("/payroll-rules", "Payroll");
        rules.MapGet("", async (PayRulesService service, CancellationToken ct) => Results.Ok(await service.GetRulesAsync(ct)))
            .RequirePermission(Permissions.PayrollRead);
        rules.MapPut("", async (SavePayRulesRequest request, PayRulesService service, CancellationToken ct) =>
            Results.Ok(await service.SaveRulesAsync(request, ct))).RequirePermission(Permissions.PayrollRulesManage);

        var holidays = api.MapFeature("/payroll-holidays", "Payroll");
        holidays.MapGet("", async ([FromQuery] int? year, PayRulesService service, CancellationToken ct) =>
            Results.Ok(await service.ListHolidaysAsync(year, ct))).RequirePermission(Permissions.PayrollRead);
        holidays.MapPost("", async (AddHolidayRequest request, PayRulesService service, CancellationToken ct) =>
        {
            var created = await service.AddHolidayAsync(request, ct);
            return Results.Created($"/api/payroll-holidays/{created.Id}", created);
        }).RequirePermission(Permissions.PayrollRulesManage);
        holidays.MapPut("/{id:guid}", async (Guid id, ChangeHolidayRequest request, PayRulesService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeHolidayAsync(id, request, ct))).RequirePermission(Permissions.PayrollRulesManage);
        holidays.MapPost("/{id:guid}/remove", async (Guid id, VersionRequest request, PayRulesService service, CancellationToken ct) =>
        {
            await service.RemoveHolidayAsync(id, request, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.PayrollRulesManage);
    }
}
