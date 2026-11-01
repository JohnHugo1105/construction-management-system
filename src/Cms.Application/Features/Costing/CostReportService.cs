using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;

namespace Cms.Application.Features.Costing;

/// <summary>
/// The reports the retired desktop app never had: how each project stands against its budget, and where a project's money goes. Budget
/// and actual are compared on the company's basis (with or without VAT); removed budget lines and voided cost entries never count.
/// Every report is a fixed number of grouped SQL statements, however many projects or lines exist.
/// </summary>
public sealed class CostReportService(ICostReportReader reader, IProjectRepository projects, IOrganizationRepository organizations, IClock clock)
{
    public async Task<DashboardDto> DashboardAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var headers = await reader.ListProjectsAsync(includeArchived, cancellationToken);
        var budgets = await reader.SumByProjectAsync(CostMeasure.Budget, includeArchived, cancellationToken);
        var actuals = await reader.SumByProjectAsync(CostMeasure.Actual, includeArchived, cancellationToken);
        return CostReportBuilder.BuildDashboard(organization.VarianceBasis, headers, budgets, actuals);
    }

    public async Task<CostSummaryDto> ProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await BuildSummaryAsync(projectId, cancellationToken)).Summary;

    /// <summary>The variance by work-breakdown code as a CSV file, in division and code order, with the unclassified amounts last.</summary>
    public async Task<ExportFile> ExportSummaryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var (summary, organization, projectName) = await BuildSummaryAsync(projectId, cancellationToken);
        var basis = CostingLabels.BasisLabel(organization.VarianceBasis);

        var csv = new CsvExport.Document("Division", "Main scope", "WBS code", "Work item", "Budget", "Actual", "Remaining", "Used %", "Status", "Compared on");
        foreach (var division in summary.ByDivision.OrderBy(row => row.Division ?? int.MaxValue))
        {
            if (division.Division is null)
            {
                csv.Row("", CsvExport.Text(division.Name), "", "", CsvExport.Money(division.BudgetMinor), CsvExport.Money(division.ActualMinor),
                    CsvExport.Money(division.RemainingMinor), CsvExport.Percent(division.UsedBps), HealthLabel(division.Health), basis);
                continue;
            }

            foreach (var wbs in division.Wbs.OrderBy(row => row.WbsCode, StringComparer.Ordinal))
                csv.Row(division.Division.Value.ToString(CultureInfo.InvariantCulture), CsvExport.Text(division.Name), CsvExport.Text(wbs.WbsCode),
                    CsvExport.Text(wbs.Name), CsvExport.Money(wbs.BudgetMinor), CsvExport.Money(wbs.ActualMinor), CsvExport.Money(wbs.RemainingMinor),
                    CsvExport.Percent(wbs.UsedBps), HealthLabel(wbs.Health), basis);
        }

        return new ExportFile(CsvExport.FileName(projectName, "variance", organization.TodayLocal(clock.UtcNow)), CsvExport.ContentType, csv.ToArray());
    }

    private async Task<(CostSummaryDto Summary, Organization Organization, string ProjectName)> BuildSummaryAsync(Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var budget = await reader.SumProjectAsync(projectId, CostMeasure.Budget, cancellationToken);
        var actual = await reader.SumProjectAsync(projectId, CostMeasure.Actual, cancellationToken);

        var header = new ProjectHeader(project.Id, project.Name, project.Status.ToString(), project.ContractPriceMinor);
        var subProjectNames = project.SubProjects.ToDictionary(sub => sub.Id, sub => sub.Name);
        return (CostReportBuilder.BuildSummary(header, subProjectNames, organization.VarianceBasis, budget, actual), organization, project.Name);
    }

    private static string HealthLabel(string health) => CostingLabels.HealthLabel(Enum.Parse<BudgetHealth>(health));
}
