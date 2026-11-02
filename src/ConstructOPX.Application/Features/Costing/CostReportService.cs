using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>
/// The reports the retired desktop app never had: how each project stands against its budget, and where a project's money goes. Budget
/// and actual are compared on the company's basis (with or without VAT); removed budget lines and voided cost entries never count.
/// Every report is a fixed number of grouped SQL statements, however many projects or lines exist.
/// </summary>
public sealed class CostReportService(ICostReportReader reader, IProjectRepository projects, IOrganizationRepository organizations, IClock clock,
    IEnumerable<ICommitmentSource> commitmentSources, IProgressForecastSource progressForecast)
{
    public async Task<DashboardDto> DashboardAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var headers = await reader.ListProjectsAsync(includeArchived, cancellationToken);
        var budgets = await reader.SumByProjectAsync(CostMeasure.Budget, includeArchived, cancellationToken);
        var actuals = await reader.SumByProjectAsync(CostMeasure.Actual, includeArchived, cancellationToken);
        var commitments = new List<ProjectAmounts>();
        foreach (var source in commitmentSources) commitments.AddRange(await source.OpenCommitmentsAsync(null, includeArchived, cancellationToken));
        var progress = await progressForecast.ForecastInputsAsync(null, includeArchived, organization.TodayLocal(clock.UtcNow), cancellationToken);
        return CostReportBuilder.BuildDashboard(organization.VarianceBasis, headers, budgets, actuals, commitments, progress);
    }

    public async Task<CostSummaryDto> ProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await BuildSummaryAsync(projectId, withProgressForecast: true, cancellationToken)).Summary;

    /// <summary>The variance by work-breakdown code as a CSV file, in division and code order, with the unclassified amounts last.</summary>
    public async Task<ExportFile> ExportSummaryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var (summary, organization, projectName) = await BuildSummaryAsync(projectId, withProgressForecast: false, cancellationToken);
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
        bool withProgressForecast, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var budget = await reader.SumProjectAsync(projectId, CostMeasure.Budget, cancellationToken);
        var actual = await reader.SumProjectAsync(projectId, CostMeasure.Actual, cancellationToken);
        var commitment = default(MeasuredAmounts);
        foreach (var source in commitmentSources)
            foreach (var open in await source.OpenCommitmentsAsync(projectId, includeArchived: true, cancellationToken))
                commitment += new MeasuredAmounts(open.NetMinor, open.GrossMinor);

        // The export has no use for the forecast, so it does not pay for it.
        var progress = withProgressForecast
            ? (await progressForecast.ForecastInputsAsync(projectId, includeArchived: true, organization.TodayLocal(clock.UtcNow), cancellationToken))
                .SingleOrDefault()
            : null;

        var header = new ProjectHeader(project.Id, project.Name, project.Status.ToString(), project.ContractPriceMinor);
        var subProjectNames = project.SubProjects.ToDictionary(sub => sub.Id, sub => sub.Name);
        return (CostReportBuilder.BuildSummary(header, subProjectNames, organization.VarianceBasis, budget, actual, commitment, progress),
            organization, project.Name);
    }

    private static string HealthLabel(string health) => CostingLabels.HealthLabel(Enum.Parse<BudgetHealth>(health));
}
