using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

/// <summary>Budget against actual cost: the dashboard of every project and the cost summary of one. All need costs.read.</summary>
public sealed class ReportEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        api.MapFeature("/reports", "Reports").MapGet("/dashboard", async ([FromQuery] bool? includeArchived, CostReportService reports, CancellationToken ct) =>
            Results.Ok(await reports.DashboardAsync(includeArchived ?? false, ct)))
            .RequirePermission(Permissions.CostsRead);

        var summary = api.MapFeature("/projects/{id:guid}/cost-summary", "Reports");

        summary.MapGet("", async (Guid id, CostReportService reports, CancellationToken ct) => Results.Ok(await reports.ProjectSummaryAsync(id, ct)))
            .RequirePermission(Permissions.CostsRead);

        summary.MapGet("/export", async (Guid id, CostReportService reports, CancellationToken ct) =>
        {
            var file = await reports.ExportSummaryAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission(Permissions.CostsRead);
    }
}
