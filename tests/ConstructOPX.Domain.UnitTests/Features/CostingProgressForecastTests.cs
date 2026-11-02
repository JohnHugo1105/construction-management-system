using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>
/// The forecast flag on the dashboard and the project cost summary: it is the Progress tab's forecast at completion, worked out once, on the
/// company's comparison basis, and it never changes the health that only compares money spent with the budget.
/// </summary>
public sealed class CostingProgressForecastTests
{
    private static readonly ProjectHeader Tower = new(Guid.NewGuid(), "Tower", "Active", null);

    private static ProgressForecastInputs Inputs(Guid projectId, long earnedNet, long earnedGross, long actualNet, long actualGross) =>
        new(projectId, new MeasuredAmounts(earnedNet, earnedGross), new MeasuredAmounts(actualNet, actualGross));

    private static DashboardDto Dashboard(VarianceBasis basis, ProjectHeader project, long budgetNet, long budgetGross, long actualNet, long actualGross,
        params ProgressForecastInputs[] progress) =>
        CostReportBuilder.BuildDashboard(basis, [project], [new ProjectAmounts(project.Id, budgetNet, budgetGross)],
            [new ProjectAmounts(project.Id, actualNet, actualGross)], null, progress);

    [Fact]
    public void A_project_with_no_progress_recorded_has_no_forecast()
    {
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 90_000, 90_000);

        Assert.Null(dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void Progress_recorded_but_no_work_worth_any_money_has_no_forecast()
    {
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 90_000, 90_000, Inputs(Tower.Id, 0, 0, 90_000, 90_000));

        Assert.Null(dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void A_forecast_under_the_budget_is_present_and_not_over()
    {
        // 100,000 budgeted, 50,000 of work done for 40,000 spent: forecast 100,000 x 40,000 / 50,000 = 80,000, 20,000 under.
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 40_000, 40_000, Inputs(Tower.Id, 50_000, 50_000, 40_000, 40_000));

        Assert.Equal(new ProgressForecastDto(80_000, -20_000, 5_000), dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void A_forecast_over_the_budget_is_present_over_and_counted_without_changing_the_health()
    {
        // The project is "On track" on money alone (40,000 of 100,000 spent) but only 10,000 of work is done: forecast 400,000, 300,000 over.
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 40_000, 40_000, Inputs(Tower.Id, 10_000, 10_000, 40_000, 40_000));

        var row = dashboard.Projects.Single();
        Assert.Equal(new ProgressForecastDto(400_000, 300_000, 1_000), row.ProgressForecast);
        Assert.Equal("OnTrack", row.Health);
        Assert.Equal(4_000, row.UsedBps);
        Assert.Equal(1, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void A_forecast_exactly_on_the_budget_is_not_over()
    {
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 50_000, 50_000, Inputs(Tower.Id, 50_000, 50_000, 50_000, 50_000));

        Assert.Equal(new ProgressForecastDto(100_000, 0, 5_000), dashboard.Projects.Single().ProgressForecast);
        Assert.Equal(0, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void The_forecast_uses_the_same_basis_as_the_budget_it_is_judged_against()
    {
        // With VAT: budget 100,000, work done 50,000, spent 56,000: forecast 112,000, 12,000 over.
        // Without VAT: budget 80,000, work done 40,000, spent 50,000: forecast 100,000, 20,000 over.
        var progress = Inputs(Tower.Id, 40_000, 50_000, 50_000, 56_000);

        var gross = Dashboard(VarianceBasis.IncludingVat, Tower, 80_000, 100_000, 50_000, 56_000, progress);
        var net = Dashboard(VarianceBasis.ExcludingVat, Tower, 80_000, 100_000, 50_000, 56_000, progress);

        Assert.Equal(new ProgressForecastDto(112_000, 12_000, 5_000), gross.Projects.Single().ProgressForecast);
        Assert.Equal(new ProgressForecastDto(100_000, 20_000, 5_000), net.Projects.Single().ProgressForecast);
    }

    [Fact]
    public void Work_that_somehow_exceeds_the_budget_shows_no_forecast_instead_of_failing_the_page()
    {
        // The budget and the work done are read in separate statements; a budget line added in between must not throw.
        var dashboard = Dashboard(VarianceBasis.IncludingVat, Tower, 100_000, 100_000, 40_000, 40_000, Inputs(Tower.Id, 100_001, 100_001, 40_000, 40_000));

        Assert.Null(dashboard.Projects.Single().ProgressForecast);
    }

    [Fact]
    public void Only_projects_with_progress_carry_a_forecast_and_the_count_adds_up_the_ones_over()
    {
        var over = new ProjectHeader(Guid.NewGuid(), "Over", "Active", null);
        var under = new ProjectHeader(Guid.NewGuid(), "Under", "Active", null);
        var none = new ProjectHeader(Guid.NewGuid(), "None", "Active", null);

        var dashboard = CostReportBuilder.BuildDashboard(VarianceBasis.IncludingVat, [over, under, none],
            [new ProjectAmounts(over.Id, 100_000, 100_000), new ProjectAmounts(under.Id, 100_000, 100_000), new ProjectAmounts(none.Id, 100_000, 100_000)],
            [new ProjectAmounts(over.Id, 40_000, 40_000), new ProjectAmounts(under.Id, 40_000, 40_000)], null,
            [Inputs(over.Id, 10_000, 10_000, 40_000, 40_000), Inputs(under.Id, 50_000, 50_000, 40_000, 40_000)]);

        Assert.Equal([true, true, false], dashboard.Projects.Select(row => row.ProgressForecast is not null));
        Assert.Equal(1, dashboard.Totals.ForecastOverBudgetCount);
    }

    [Fact]
    public void The_dashboard_row_the_summary_and_the_progress_report_give_one_forecast()
    {
        // Three lines with 12% VAT: the same rows the Progress report is built from. The source rounds each line once, as the report does.
        ProgressLineRow[] rows =
        [
            new(Guid.NewGuid(), "Footings", 3, "3300", 80_000_000, 89_600_000, 5_000, new DateOnly(2026, 1, 20)),
            new(Guid.NewGuid(), "Columns", 3, "3300", 20_000_000, 22_400_000, null, null),
            new(Guid.NewGuid(), "Site office", null, null, 10_000_001, 11_200_001, 3_333, new DateOnly(2026, 1, 25))
        ];
        var earned = new MeasuredAmounts(
            rows.Sum(row => EarnedValueCalculator.LineEarnedValue(row.NetMinor, row.PercentBps ?? 0)),
            rows.Sum(row => EarnedValueCalculator.LineEarnedValue(row.GrossMinor, row.PercentBps ?? 0)));
        var actual = new MeasuredAmounts(45_000_000, 50_400_000);
        var budget = new MeasuredAmounts(rows.Sum(row => row.NetMinor), rows.Sum(row => row.GrossMinor));
        var project = Project.Create(Guid.NewGuid(), TestData.Tenant, "Tower A", null, null, 150_000_000, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 11), TestData.Now);
        var header = new ProjectHeader(project.Id, project.Name, "Planning", project.ContractPriceMinor);
        var inputs = new ProgressForecastInputs(project.Id, earned, actual);

        foreach (var basis in new[] { VarianceBasis.IncludingVat, VarianceBasis.ExcludingVat })
        {
            var report = ProgressReportBuilder.Build(project, basis, new DateOnly(2026, 1, 31), rows, actual).EarnedValue;
            var dashboard = CostReportBuilder.BuildDashboard(basis, [header], [new ProjectAmounts(project.Id, budget.NetMinor, budget.GrossMinor)],
                [new ProjectAmounts(project.Id, actual.NetMinor, actual.GrossMinor)], null, [inputs]);
            var summary = CostReportBuilder.BuildSummary(header, new Dictionary<Guid, string>(), basis,
                new GroupedSums([], [], [new GroupedAmounts(null, null, null, null, ConstructOPX.Domain.Features.Costing.ItemType.Material, budget.NetMinor, budget.GrossMinor)]),
                new GroupedSums([], [], [new GroupedAmounts(null, null, null, null, ConstructOPX.Domain.Features.Costing.ItemType.Material, actual.NetMinor, actual.GrossMinor)]),
                default, inputs);

            var forecast = dashboard.Projects.Single().ProgressForecast!;
            Assert.Equal(report.ForecastMinor, forecast.EstimateAtCompletionMinor);
            Assert.Equal(-report.ForecastVarianceMinor, forecast.OverBudgetMinor);
            Assert.Equal(report.PercentCompleteBps, forecast.PercentCompleteBps);
            Assert.Equal(forecast, summary.ProgressForecast);
        }
    }
}
