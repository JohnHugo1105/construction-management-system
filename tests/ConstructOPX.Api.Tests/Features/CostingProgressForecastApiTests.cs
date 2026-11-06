using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The forecast flag on the dashboard and the project cost summary, on the real API and a real SQL Server. The figure must be the one the
/// project's Progress tab shows, from the same calculation; the existing health must not move. Amounts are centavos.
/// </summary>
public sealed class CostingProgressForecastApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const string Feb10 = "2026-02-10";

    private readonly ConstructOpxApiFactory _factory;

    public CostingProgressForecastApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private Task<(ApiClient Client, SeededUser User)> AdminAsync() => SignedInAsync(_factory);

    private static Task<BudgetLineDto> AddLine(ApiClient client, Guid projectId, string description, long amount, string code, string vat = "Exempt") =>
        client.PostAsync<BudgetLineDto>($"/api/projects/{projectId}/budget-lines", Line(description, unitCostMinor: amount, wbsCode: code, vatTreatment: vat));

    private static Task<CostEntryDto> AddCost(ApiClient client, Guid projectId, long amount, string vat = "Exempt") =>
        client.PostAsync<CostEntryDto>($"/api/projects/{projectId}/cost-entries", Cost("Spent", unitCostMinor: amount, vatTreatment: vat));

    private static async Task RecordAsync(ApiClient client, Guid projectId, params (Guid LineId, int PercentBps)[] lines)
    {
        var response = await client.PostAsync($"/api/projects/{projectId}/progress",
            new { asOfDate = Feb10, lines = lines.Select(line => new { budgetLineId = line.LineId, percentBps = line.PercentBps }).ToArray() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<ProgressDto> ProgressTabAsync(ApiClient client, Guid projectId) => client.GetAsync<ProgressDto>($"/api/projects/{projectId}/progress");

    /// <summary>
    /// The worked project: Footings 1,000,000 and Columns 800,000 (budget 1,800,000, no VAT), 900,000 spent. Footings are 60% done, so the
    /// work done is worth 600,000. Money alone says 50% used, "On track"; the work says the forecast is 1,800,000 x 900,000 / 600,000 =
    /// 2,700,000, which is 900,000 over.
    /// </summary>
    private static async Task<(ProjectDto Project, BudgetLineDto Footings, BudgetLineDto Columns)> OverrunProjectAsync(ApiClient client, string? name = null)
    {
        var project = await CreateProjectAsync(client, name);
        var footings = await AddLine(client, project.Id, "Footings", 1_000_000, "3100");
        var columns = await AddLine(client, project.Id, "Columns", 800_000, "3200");
        await AddCost(client, project.Id, 900_000);
        await RecordAsync(client, project.Id, (footings.Id, 6_000));
        return (project, footings, columns);
    }

    [SqlServerFact]
    public async Task A_project_that_is_on_track_on_money_alone_is_flagged_by_the_work_done_and_its_health_does_not_move()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, _, _) = await OverrunProjectAsync(client);

        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        var row = dashboard.Projects.Single();
        var summary = await SummaryAsync(client, project.Id);

        // 600,000 of 1,800,000 is 33.33% done.
        Assert.Equal(new ProgressForecastDto(2_700_000, 900_000, 3_333), row.ProgressForecast);
        Assert.Equal(row.ProgressForecast, summary.ProgressForecast);
        // Health only compares money spent with the budget: 900,000 of 1,800,000 is exactly half, so still on track.
        Assert.Equal(("OnTrack", 5_000, 900_000L), (row.Health, row.UsedBps, row.ActualMinor));
        Assert.Equal("OnTrack", summary.Position.Health);
        Assert.Equal(1, dashboard.Totals.ForecastOverBudgetCount);
        Assert.Equal("OnTrack", dashboard.Totals.Health);
    }

    [SqlServerFact]
    public async Task The_dashboard_row_the_cost_summary_and_the_Progress_tab_show_the_same_forecast_on_either_basis()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        // Lines with VAT so that net and gross really differ: an invoice including VAT and a line added on top of VAT, and costs both ways.
        var project = await CreateProjectAsync(client, "One Answer");
        var slab = await AddLine(client, project.Id, "Slab", 1_000_001, "3100", vat: "Exclusive");
        var walls = await AddLine(client, project.Id, "Walls", 777_777, "3200", vat: "Inclusive");
        var roof = await AddLine(client, project.Id, "Roof", 333_333, "3300");
        await AddCost(client, project.Id, 901_234, vat: "Inclusive");
        await AddCost(client, project.Id, 55_555, vat: "Exclusive");
        await RecordAsync(client, project.Id, (slab.Id, 1_234), (walls.Id, 3_333), (roof.Id, 9_999));

        foreach (var basis in new[] { "IncludingVat", "ExcludingVat" })
        {
            await ChangeOrganizationAsync(client, varianceBasis: basis);
            var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
            var row = dashboard.Projects.Single();
            var summary = await SummaryAsync(client, project.Id);
            var tab = (await ProgressTabAsync(client, project.Id)).EarnedValue;

            Assert.Equal(basis, summary.Basis);
            Assert.NotNull(row.ProgressForecast);
            // The anti-disagreement assertion: three screens, one figure, to the centavo.
            Assert.Equal(row.ProgressForecast, summary.ProgressForecast);
            Assert.Equal(tab.ForecastMinor, row.ProgressForecast.EstimateAtCompletionMinor);
            Assert.Equal(-tab.ForecastVarianceMinor, row.ProgressForecast.OverBudgetMinor);
            Assert.Equal(tab.PercentCompleteBps, row.ProgressForecast.PercentCompleteBps);
            // And it is judged against the same budget the dashboard row shows.
            Assert.Equal(row.BudgetMinor, tab.BudgetMinor);
            Assert.Equal(row.ProgressForecast.EstimateAtCompletionMinor - row.BudgetMinor, row.ProgressForecast.OverBudgetMinor);
        }
    }

    [SqlServerFact]
    public async Task A_project_with_no_progress_or_with_all_its_lines_at_zero_has_no_forecast()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var untouched = await CreateProjectAsync(client, "Untouched");
        await AddLine(client, untouched.Id, "Footings", 1_000_000, "3100");
        await AddCost(client, untouched.Id, 400_000);

        var (reset, footings, _) = await OverrunProjectAsync(client, "Reset to zero");
        var back = await client.PostAsync($"/api/projects/{reset.Id}/progress",
            new { asOfDate = Feb10, lines = new[] { new { budgetLineId = footings.Id, percentBps = 0, note = "Pour condemned and removed" } } });
        Assert.Equal(HttpStatusCode.Created, back.StatusCode);

        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");

        Assert.Null(dashboard.Projects.Single(row => row.Id == untouched.Id).ProgressForecast);
        Assert.Null(dashboard.Projects.Single(row => row.Id == reset.Id).ProgressForecast);
        Assert.Null((await SummaryAsync(client, untouched.Id)).ProgressForecast);
        Assert.Null((await SummaryAsync(client, reset.Id)).ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [SqlServerFact]
    public async Task A_forecast_under_the_budget_is_shown_with_nothing_over_and_is_not_counted()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var footings = await AddLine(client, project.Id, "Footings", 1_000_000, "3100");
        await AddCost(client, project.Id, 300_000);
        await RecordAsync(client, project.Id, (footings.Id, 5_000));

        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");

        // 500,000 of work for 300,000 spent: forecast 1,000,000 x 300,000 / 500,000 = 600,000, 400,000 under.
        Assert.Equal(new ProgressForecastDto(600_000, -400_000, 5_000), dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [SqlServerFact]
    public async Task A_removed_line_stops_counting_exactly_as_it_does_on_the_Progress_tab()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns) = await OverrunProjectAsync(client);
        await RecordAsync(client, project.Id, (columns.Id, 2_500));   // another 200,000 of work: 800,000 done

        var done = await client.GetAsync<BudgetLineDto>($"/api/budget-lines/{footings.Id}");
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{footings.Id}/remove", new { reason = "Re-scoped", version = done.Version });

        var row = await DashboardRowAsync(client, project.Id);
        var tab = (await ProgressTabAsync(client, project.Id)).EarnedValue;

        // Footings are gone: the budget is 800,000, 200,000 of work is done, 900,000 is spent, and the forecast is 800,000 x 900,000 / 200,000.
        Assert.Equal(new ProgressForecastDto(3_600_000, 2_800_000, 2_500), row.ProgressForecast);
        Assert.Equal(tab.ForecastMinor, row.ProgressForecast!.EstimateAtCompletionMinor);
        Assert.Equal(-tab.ForecastVarianceMinor, row.ProgressForecast.OverBudgetMinor);
        Assert.Equal(tab.PercentCompleteBps, row.ProgressForecast.PercentCompleteBps);
    }

    [SqlServerFact]
    public async Task An_archived_project_is_handled_as_the_dashboard_handles_archived_projects()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, _, _) = await OverrunProjectAsync(client);
        await ArchiveAsync(client, project);

        // Left out of the list and its count unless archived projects are asked for.
        var active = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Empty(active.Projects);
        Assert.Equal(0, active.Totals.ForecastOverBudgetCount);

        var all = await client.GetAsync<DashboardDto>("/api/reports/dashboard?includeArchived=true");
        var row = all.Projects.Single();
        Assert.Equal("Archived", row.Status);
        Assert.Equal(new ProgressForecastDto(2_700_000, 900_000, 3_333), row.ProgressForecast);
        Assert.Equal(1, all.Totals.ForecastOverBudgetCount);
        // The project's own page still opens, and says the same.
        Assert.Equal(row.ProgressForecast, (await SummaryAsync(client, project.Id)).ProgressForecast);
    }

    [SqlServerFact]
    public async Task Another_companys_progress_never_appears_or_counts()
    {
        var (mine, _) = await AdminAsync();
        using var _m = mine;
        var (theirs, _) = await AdminAsync();
        using var _t = theirs;
        var (theirProject, _, _) = await OverrunProjectAsync(theirs, "Theirs");
        var quiet = await CreateProjectAsync(mine, "Mine");
        await AddLine(mine, quiet.Id, "Footings", 1_000_000, "3100");

        var dashboard = await mine.GetAsync<DashboardDto>("/api/reports/dashboard?includeArchived=true");

        Assert.Equal(["Mine"], dashboard.Projects.Select(row => row.Name));
        Assert.Null(dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
        Assert.Equal(HttpStatusCode.NotFound, (await mine.GetAsync($"/api/projects/{theirProject.Id}/cost-summary")).StatusCode);
        // Theirs still sees its own.
        Assert.Equal(1, (await theirs.GetAsync<DashboardDto>("/api/reports/dashboard")).Totals.ForecastOverBudgetCount);
    }

    [SqlServerFact]
    public async Task The_dashboard_costs_the_same_number_of_statements_for_one_project_with_progress_as_for_ten()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        await OverrunProjectAsync(client, "First");
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode));

        for (var index = 0; index < 9; index++) await OverrunProjectAsync(client, $"More {index}");
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode));
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(10, dashboard.Totals.ForecastOverBudgetCount);

        Assert.Equal(small, large);
        // Before the forecast: the company, the projects, the budget sums, the actual sums and one statement per commitment source. The
        // forecast adds two: the budget lines with their current percent, and the posted cost of the projects that have progress.
        Assert.Equal(4 + _factory.CommitmentSourceCount() + 2, large);
    }

    [SqlServerFact]
    public async Task The_cost_summary_costs_two_statements_more_for_the_forecast_and_no_more_however_much_progress_there_is()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns) = await OverrunProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-summary";
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        for (var step = 1; step <= 6; step++) await RecordAsync(client, project.Id, (footings.Id, 6_000 + step * 100), (columns.Id, step * 100));
        for (var index = 0; index < 20; index++) await AddLine(client, project.Id, $"Extra {index}", 1_000 + index, "3300");
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        Assert.Equal(small, large);
        // The project, the company, three grouped sums each for budget and actual, one statement per commitment source, and the two for the forecast.
        Assert.Equal(8 + _factory.CommitmentSourceCount() + 2, large);
    }
}
