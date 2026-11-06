using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The progress report assembled from rows: roll-ups by division, the comparison basis, and the margin that always uses amounts with VAT.</summary>
public sealed class ProgressReportBuilderTests
{
    private static readonly DateOnly AsOf = new(2026, 1, 31);

    // Starts 1 Jan 2026 and ends 11 Apr 2026 (100 days), so the as-of date is 30% of the way.
    private static Project NewProject(long? contractPriceMinor = 150_000_000) =>
        Project.Create(Guid.NewGuid(), TestData.Tenant, "Tower A", null, null, contractPriceMinor, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 11), TestData.Now);

    // Three lines with 12% VAT: a concrete line half done, a concrete line never updated, and an unclassified line fully done.
    private static ProgressLineRow[] Rows() =>
    [
        new(Guid.NewGuid(), "Footings", 3, "3300", 80_000_000, 89_600_000, 5_000, new DateOnly(2026, 1, 20)),
        new(Guid.NewGuid(), "Columns", 3, "3300", 20_000_000, 22_400_000, null, null),
        new(Guid.NewGuid(), "Site office", null, null, 10_000_000, 11_200_000, 10_000, new DateOnly(2026, 1, 25)),
    ];

    private static readonly MeasuredAmounts Spent = new(45_000_000, 50_400_000);

    [Fact]
    public void On_the_net_basis_the_figures_use_amounts_before_vat_and_the_margin_still_uses_amounts_with_vat()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.ExcludingVat, AsOf, Rows(), Spent);

        var ev = report.EarnedValue;
        Assert.Equal(110_000_000, ev.BudgetMinor);
        Assert.Equal(50_000_000, ev.EarnedMinor);     // 40,000,000 + 0 + 10,000,000
        Assert.Equal(45_000_000, ev.SpentMinor);
        Assert.Equal(33_000_000, ev.PlannedMinor);    // 30% of the budget
        Assert.Equal(5_000_000, ev.CostVarianceMinor);
        Assert.Equal(17_000_000, ev.ScheduleVarianceMinor);
        Assert.Equal(11_111, ev.CpiBps);              // 50 / 45
        Assert.Equal(15_152, ev.SpiBps);              // 50 / 33
        Assert.Equal(99_000_000, ev.ForecastMinor);   // 110 x 45 / 50
        Assert.Equal(54_000_000, ev.ToCompleteMinor);
        Assert.Equal(11_000_000, ev.ForecastVarianceMinor);
        Assert.Equal(4_545, ev.PercentCompleteBps);
        Assert.Equal(3_000, ev.PlannedPercentBps);

        // With VAT the budget is 123,200,000, the work earned 56,000,000 and 50,400,000 was spent: forecast 123.2 x 50.4 / 56 = 110,880,000.
        Assert.Equal(150_000_000, ev.ContractPriceMinor);
        Assert.Equal(39_120_000, ev.ForecastMarginMinor);
    }

    [Fact]
    public void On_the_gross_basis_the_figures_and_the_margin_come_from_the_same_calculation()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.IncludingVat, AsOf, Rows(), Spent);

        var ev = report.EarnedValue;
        Assert.Equal(123_200_000, ev.BudgetMinor);
        Assert.Equal(56_000_000, ev.EarnedMinor);
        Assert.Equal(50_400_000, ev.SpentMinor);
        Assert.Equal(110_880_000, ev.ForecastMinor);
        Assert.Equal(39_120_000, ev.ForecastMarginMinor);
        Assert.Equal("IncludingVat", report.Basis);
    }

    [Fact]
    public void Lines_are_grouped_by_division_with_roll_ups_and_unclassified_last()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.ExcludingVat, AsOf, Rows(), Spent);

        Assert.Equal(2, report.Divisions.Count);
        var concrete = report.Divisions[0];
        Assert.Equal(3, concrete.Division);
        Assert.Equal("Concrete", concrete.Name);
        Assert.Equal(100_000_000, concrete.BudgetMinor);
        Assert.Equal(40_000_000, concrete.EarnedMinor);
        Assert.Equal(4_000, concrete.PercentBps);
        Assert.Equal(["Footings", "Columns"], concrete.Lines.Select(line => line.Description));
        Assert.Equal(40_000_000, concrete.Lines[0].EarnedMinor);
        Assert.Equal(5_000, concrete.Lines[0].PercentBps);
        Assert.Equal(new DateOnly(2026, 1, 20), concrete.Lines[0].LastUpdatedOn);
        Assert.Equal(0, concrete.Lines[1].PercentBps);
        Assert.Null(concrete.Lines[1].LastUpdatedOn);

        var unclassified = report.Divisions[1];
        Assert.Null(unclassified.Division);
        Assert.Equal("Unclassified", unclassified.Name);
        Assert.Equal(10_000, unclassified.PercentBps);
    }

    [Fact]
    public void The_division_totals_add_up_to_the_project()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.ExcludingVat, AsOf, Rows(), Spent);

        Assert.Equal(report.EarnedValue.BudgetMinor, report.Divisions.Sum(division => division.BudgetMinor));
        Assert.Equal(report.EarnedValue.EarnedMinor, report.Divisions.Sum(division => division.EarnedMinor));
    }

    [Fact]
    public void Before_any_progress_is_recorded_nothing_is_earned_and_the_forecast_is_the_budget()
    {
        var rows = Rows().Select(row => row with { PercentBps = null, LastUpdatedOn = null }).ToArray();

        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.IncludingVat, AsOf, rows, Spent);

        Assert.False(report.HasProgress);
        Assert.Null(report.LastUpdatedOn);
        Assert.Equal(0, report.EarnedValue.EarnedMinor);
        Assert.Equal(report.EarnedValue.BudgetMinor, report.EarnedValue.ForecastMinor);
        Assert.Equal(0, report.EarnedValue.PercentCompleteBps);
        Assert.Equal(0, report.EarnedValue.CpiBps);
    }

    [Fact]
    public void Once_progress_is_recorded_the_latest_date_is_reported()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.IncludingVat, AsOf, Rows(), Spent);

        Assert.True(report.HasProgress);
        Assert.Equal(new DateOnly(2026, 1, 25), report.LastUpdatedOn);
    }

    [Fact]
    public void A_project_with_no_budget_lines_reports_zeros_and_no_percent()
    {
        var report = ProgressReportBuilder.Build(NewProject(), VarianceBasis.IncludingVat, AsOf, [], default);

        Assert.Empty(report.Divisions);
        Assert.False(report.HasProgress);
        Assert.Equal(0, report.EarnedValue.BudgetMinor);
        Assert.Null(report.EarnedValue.PercentCompleteBps);
        Assert.Equal(150_000_000, report.EarnedValue.ForecastMarginMinor);
    }

    [Fact]
    public void Without_a_contract_price_there_is_no_margin_and_without_a_target_end_there_is_no_schedule()
    {
        var project = Project.Create(Guid.NewGuid(), TestData.Tenant, "Tower B", null, null, null, new DateOnly(2026, 1, 1), null, TestData.Now);

        var ev = ProgressReportBuilder.Build(project, VarianceBasis.IncludingVat, AsOf, Rows(), Spent).EarnedValue;

        Assert.Null(ev.ContractPriceMinor);
        Assert.Null(ev.ForecastMarginMinor);
        Assert.Null(ev.PlannedMinor);
        Assert.Null(ev.ScheduleVarianceMinor);
        Assert.Null(ev.SpiBps);
        Assert.Null(ev.PlannedPercentBps);
    }
}
