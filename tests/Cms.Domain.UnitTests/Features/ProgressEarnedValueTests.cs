using Cms.Domain.Features.Progress;

namespace Cms.Domain.UnitTests.Features;

/// <summary>
/// The earned-value formulas checked against figures worked out by hand. Every amount is in centavos: 100,000,000 is 1,000,000.00.
/// </summary>
public sealed class ProgressEarnedValueTests
{
    [Fact]
    public void A_project_ahead_of_schedule_and_over_budget_is_worked_out_exactly()
    {
        // Budget 1,000,000.00, 40% of the work done, 450,000.00 spent, and 30 of the 100 planned days gone (30% planned).
        var start = new DateOnly(2026, 1, 1);
        var planned = PlannedProgress.From(start, start.AddDays(100), start.AddDays(30));

        var figures = EarnedValueCalculator.Calculate(100_000_000, 40_000_000, 45_000_000, planned);

        Assert.Equal(100_000_000, figures.BudgetAtCompletionMinor);
        Assert.Equal(40_000_000, figures.EarnedMinor);
        Assert.Equal(45_000_000, figures.ActualMinor);
        Assert.Equal(30_000_000, figures.PlannedMinor);
        Assert.Equal(-5_000_000, figures.CostVarianceMinor);       // earned 400,000 for 450,000 spent
        Assert.Equal(10_000_000, figures.ScheduleVarianceMinor);   // 100,000 ahead of the plan
        Assert.Equal(8_889, figures.CostIndexBps);                 // 40 / 45 = 0.88889: each peso spent earns 89 centavos
        Assert.Equal(13_333, figures.ScheduleIndexBps);            // 40 / 30 = 1.33333
        Assert.Equal(112_500_000, figures.EstimateAtCompletionMinor); // 1,000,000 x 450,000 / 400,000
        Assert.Equal(67_500_000, figures.EstimateToCompleteMinor);    // 1,125,000 - 450,000
        Assert.Equal(-12_500_000, figures.VarianceAtCompletionMinor); // 125,000 over
        Assert.Equal(4_000, figures.PercentCompleteBps);
        Assert.Equal(3_000, figures.PlannedPercentBps);
    }

    [Fact]
    public void A_project_behind_schedule_and_under_budget_is_worked_out_exactly()
    {
        // Budget 2,500,000.00, 20% done, 400,000.00 spent, halfway through the planned time.
        var start = new DateOnly(2026, 3, 1);
        var planned = PlannedProgress.From(start, start.AddDays(200), start.AddDays(100));

        var figures = EarnedValueCalculator.Calculate(250_000_000, 50_000_000, 40_000_000, planned);

        Assert.Equal(125_000_000, figures.PlannedMinor);
        Assert.Equal(10_000_000, figures.CostVarianceMinor);
        Assert.Equal(-75_000_000, figures.ScheduleVarianceMinor);
        Assert.Equal(12_500, figures.CostIndexBps);   // each peso spent earns 1.25 pesos of work
        Assert.Equal(4_000, figures.ScheduleIndexBps);
        Assert.Equal(200_000_000, figures.EstimateAtCompletionMinor);
        Assert.Equal(160_000_000, figures.EstimateToCompleteMinor);
        Assert.Equal(50_000_000, figures.VarianceAtCompletionMinor);
        Assert.Equal(2_000, figures.PercentCompleteBps);
        Assert.Equal(5_000, figures.PlannedPercentBps);
    }

    [Fact]
    public void The_forecast_is_rounded_once_half_away_from_zero()
    {
        // 1,000 x 500 / 333 = 1,501.50 centavos: rounds to 1,502. The cost index is 333 / 500 = 0.666 exactly.
        var figures = EarnedValueCalculator.Calculate(1_000, 333, 500, null);
        Assert.Equal(1_502, figures.EstimateAtCompletionMinor);
        Assert.Equal(6_660, figures.CostIndexBps);
        Assert.Equal(1_002, figures.EstimateToCompleteMinor);

        // 3 x 1 / 2 = 1.5: a half rounds up.
        Assert.Equal(2, EarnedValueCalculator.Calculate(3, 2, 1, null).EstimateAtCompletionMinor);
    }

    [Fact]
    public void The_planned_value_is_rounded_once_from_the_exact_share_of_time()
    {
        // 10 centavos with 1 of 4 days gone is 2.5: a half rounds up to 3. Rounding the 25% first would also give 3, but 1 of 3 days
        // of 10 is 3.33 (3), and 2 of 3 days is 6.67 (7): the exact fraction, not a rounded 6,667 basis points x 10.
        var start = new DateOnly(2026, 1, 1);
        Assert.Equal(3, EarnedValueCalculator.Calculate(10, 0, 0, PlannedProgress.From(start, start.AddDays(4), start.AddDays(1))).PlannedMinor);
        Assert.Equal(3, EarnedValueCalculator.Calculate(10, 0, 0, PlannedProgress.From(start, start.AddDays(3), start.AddDays(1))).PlannedMinor);
        Assert.Equal(7, EarnedValueCalculator.Calculate(10, 0, 0, PlannedProgress.From(start, start.AddDays(3), start.AddDays(2))).PlannedMinor);
    }

    [Fact]
    public void With_nothing_spent_there_is_no_cost_index_and_the_forecast_is_the_budget()
    {
        var figures = EarnedValueCalculator.Calculate(100_000_000, 10_000_000, 0, null);

        Assert.Null(figures.CostIndexBps);
        Assert.Equal(100_000_000, figures.EstimateAtCompletionMinor);
        Assert.Equal(100_000_000, figures.EstimateToCompleteMinor);
        Assert.Equal(0, figures.VarianceAtCompletionMinor);
        Assert.Equal(10_000_000, figures.CostVarianceMinor);
    }

    [Fact]
    public void With_money_spent_and_nothing_earned_the_cost_index_is_zero_and_the_forecast_stays_the_budget()
    {
        var figures = EarnedValueCalculator.Calculate(100_000_000, 0, 5_000_000, null);

        Assert.Equal(0, figures.CostIndexBps);
        Assert.Equal(100_000_000, figures.EstimateAtCompletionMinor);
        Assert.Equal(95_000_000, figures.EstimateToCompleteMinor);
        Assert.Equal(-5_000_000, figures.CostVarianceMinor);
        Assert.Equal(0, figures.PercentCompleteBps);
    }

    [Fact]
    public void Without_a_target_end_date_there_is_no_planned_value_schedule_variance_or_schedule_index()
    {
        Assert.Null(PlannedProgress.From(new DateOnly(2026, 1, 1), null, new DateOnly(2026, 6, 1)));

        var figures = EarnedValueCalculator.Calculate(100_000_000, 40_000_000, 45_000_000, null);

        Assert.Null(figures.PlannedMinor);
        Assert.Null(figures.ScheduleVarianceMinor);
        Assert.Null(figures.ScheduleIndexBps);
        Assert.Null(figures.PlannedPercentBps);
        Assert.Equal(8_889, figures.CostIndexBps);
    }

    [Fact]
    public void Before_the_start_nothing_is_planned_so_there_is_a_planned_value_of_zero_but_no_schedule_index()
    {
        var start = new DateOnly(2026, 6, 1);
        var planned = PlannedProgress.From(start, start.AddDays(90), start.AddDays(-10));

        var figures = EarnedValueCalculator.Calculate(100_000_000, 0, 0, planned);

        Assert.Equal(0, figures.PlannedMinor);
        Assert.Equal(0, figures.ScheduleVarianceMinor);
        Assert.Null(figures.ScheduleIndexBps);
        Assert.Equal(0, figures.PlannedPercentBps);
    }

    [Fact]
    public void A_budget_of_nothing_has_no_percent_complete_and_a_forecast_of_nothing()
    {
        var figures = EarnedValueCalculator.Calculate(0, 0, 250_000, null);

        Assert.Null(figures.PercentCompleteBps);
        Assert.Equal(0, figures.EstimateAtCompletionMinor);
        Assert.Equal(0, figures.EstimateToCompleteMinor);
        Assert.Equal(0, figures.VarianceAtCompletionMinor);
        Assert.Equal(0, figures.CostIndexBps);
    }

    [Fact]
    public void A_finished_project_that_overspent_forecasts_what_it_spent()
    {
        // Everything is done (earned = budget) and 1,200,000.00 was spent on a 1,000,000.00 budget: the forecast is what was spent.
        var figures = EarnedValueCalculator.Calculate(100_000_000, 100_000_000, 120_000_000, null);

        Assert.Equal(120_000_000, figures.EstimateAtCompletionMinor);
        Assert.Equal(0, figures.EstimateToCompleteMinor);
        Assert.Equal(-20_000_000, figures.VarianceAtCompletionMinor);
        Assert.Equal(8_333, figures.CostIndexBps);
        Assert.Equal(10_000, figures.PercentCompleteBps);
    }

    [Fact]
    public void A_tiny_amount_earned_against_a_large_cost_holds_the_forecast_at_the_ceiling_instead_of_overflowing()
    {
        var figures = EarnedValueCalculator.Calculate(10_000_000_000_000, 1, 10_000_000_000_000, null);

        Assert.Equal(EarnedValueCalculator.MaximumForecastMinor, figures.EstimateAtCompletionMinor);
        Assert.Equal(EarnedValueCalculator.MaximumForecastMinor - 10_000_000_000_000, figures.EstimateToCompleteMinor);
        Assert.Equal(10_000_000_000_000 - EarnedValueCalculator.MaximumForecastMinor, figures.VarianceAtCompletionMinor);
    }

    [Fact]
    public void An_index_too_large_for_an_int_is_held_at_the_largest_value()
    {
        var figures = EarnedValueCalculator.Calculate(10_000_000_000_000, 10_000_000_000_000, 1, null);

        Assert.Equal(int.MaxValue, figures.CostIndexBps);
    }

    [Theory]
    [InlineData(10_001, 3_333, 3_333)]   // 3,333.33
    [InlineData(5, 5_000, 3)]            // 2.5 rounds up
    [InlineData(5, 4_999, 2)]            // 2.4995
    [InlineData(100_000, 0, 0)]
    [InlineData(100_000, 10_000, 100_000)]
    [InlineData(0, 6_000, 0)]
    public void The_value_of_a_line_is_its_budget_times_its_percent_rounded_once(long budget, int percentBps, long expected) =>
        Assert.Equal(expected, EarnedValueCalculator.LineEarnedValue(budget, percentBps));

    [Fact]
    public void Each_line_is_rounded_on_its_own_and_the_project_adds_the_rounded_figures()
    {
        // Three lines of 0.05 each, half done: 0.025 each is 0.03 after rounding, so the project has earned 0.09, not 0.075.
        var lines = new long[] { 5, 5, 5 };
        var earned = lines.Sum(budget => EarnedValueCalculator.LineEarnedValue(budget, 5_000));

        Assert.Equal(9, earned);
        Assert.Equal(15, lines.Sum());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 10_001)]
    public void A_line_with_an_impossible_budget_or_percent_is_refused(long budget, int percentBps) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EarnedValueCalculator.LineEarnedValue(budget, percentBps));

    [Fact]
    public void Earned_value_can_never_exceed_the_budget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EarnedValueCalculator.Calculate(100, 101, 0, null));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-04-11", "2025-12-01", 0, 100)]    // before the start
    [InlineData("2026-01-01", "2026-04-11", "2026-01-01", 0, 100)]    // the start day itself
    [InlineData("2026-01-01", "2026-04-11", "2026-02-20", 50, 100)]   // halfway
    [InlineData("2026-01-01", "2026-04-11", "2026-04-11", 100, 100)]  // the end day
    [InlineData("2026-01-01", "2026-04-11", "2027-01-01", 100, 100)]  // long after the end
    [InlineData("2026-01-01", "2026-01-01", "2025-12-31", 0, 1)]      // a one-day project, before it
    [InlineData("2026-01-01", "2026-01-01", "2026-01-01", 1, 1)]      // a one-day project, on its day
    public void The_planned_share_of_time_is_linear_from_the_start_to_the_target_end(string start, string end, string asOf, int elapsed, int total)
    {
        var planned = PlannedProgress.From(DateOnly.Parse(start), DateOnly.Parse(end), DateOnly.Parse(asOf));

        Assert.Equal(new PlannedProgress(elapsed, total), planned);
    }

    [Fact]
    public void The_forecast_margin_is_the_contract_price_minus_the_forecast_cost_and_is_absent_without_a_price()
    {
        Assert.Equal(87_500_000, EarnedValueCalculator.ForecastMarginMinor(200_000_000, 112_500_000));
        Assert.Equal(-12_500_000, EarnedValueCalculator.ForecastMarginMinor(100_000_000, 112_500_000));
        Assert.Null(EarnedValueCalculator.ForecastMarginMinor(null, 112_500_000));
    }
}
