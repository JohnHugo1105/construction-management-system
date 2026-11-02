using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Schedule;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>
/// The schedule rules (docs/development/notes/schedule-rules.md): planned value per activity and per project, percent complete, behind, slip,
/// the activity's checks, and where the schedule's planned value lands on the Progress tab. Amounts are awkward on purpose so every rounding is
/// visible: multiply first, divide last, round once half away from zero, then add the rounded figures.
/// </summary>
public sealed class ScheduleTests
{
    private static readonly DateOnly March1 = new(2026, 3, 1);
    private static readonly DateOnly March10 = new(2026, 3, 10); // 1 Mar to 10 Mar is 10 days, both ends counted

    // ---- Planned value of one activity ------------------------------------------------------------------------------------

    [Fact]
    public void An_activity_plans_its_weight_evenly_over_its_days_with_both_ends_counted()
    {
        var work = new ScheduledWork(March1, March10, 1_000_001);

        Assert.Equal(10, SchedulePlan.PlannedDays(March1, March10));
        Assert.Equal(0, SchedulePlan.ActivityPlannedValue(work, new DateOnly(2026, 2, 28)));       // the day before: nothing
        Assert.Equal(100_000, SchedulePlan.ActivityPlannedValue(work, March1));                    // 1,000,001 x 1/10 = 100,000.1
        Assert.Equal(500_001, SchedulePlan.ActivityPlannedValue(work, new DateOnly(2026, 3, 5))); // 500,000.5 rounds away from zero
        Assert.Equal(900_001, SchedulePlan.ActivityPlannedValue(work, new DateOnly(2026, 3, 9))); // 900,000.9
        Assert.Equal(1_000_001, SchedulePlan.ActivityPlannedValue(work, March10));                 // the finish day: all of it
        Assert.Equal(1_000_001, SchedulePlan.ActivityPlannedValue(work, new DateOnly(2027, 1, 1))); // long after: still all of it
    }

    [Fact]
    public void A_one_day_activity_plans_nothing_before_its_day_and_all_of_it_on_and_after()
    {
        var work = new ScheduledWork(March1, March1, 333_333);

        Assert.Equal(1, SchedulePlan.PlannedDays(March1, March1));
        Assert.Equal(0, SchedulePlan.ActivityPlannedValue(work, March1.AddDays(-1)));
        Assert.Equal(333_333, SchedulePlan.ActivityPlannedValue(work, March1));
        Assert.Equal(333_333, SchedulePlan.ActivityPlannedValue(work, March1.AddDays(1)));
    }

    [Fact]
    public void The_share_of_days_is_clamped_to_none_before_the_start_and_all_after_the_finish()
    {
        Assert.Equal(0, SchedulePlan.ElapsedDays(March1, March10, new DateOnly(2025, 1, 1)));
        Assert.Equal(1, SchedulePlan.ElapsedDays(March1, March10, March1));
        Assert.Equal(10, SchedulePlan.ElapsedDays(March1, March10, new DateOnly(2030, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchedulePlan.PlannedDays(March10, March1));
    }

    [Fact]
    public void A_weight_whose_product_with_the_days_overflows_a_long_is_still_exact()
    {
        // 900,000,000,000,000,001 x 3,000 days does not fit a long; the product is taken in an Int128 and divided once.
        var start = new DateOnly(2026, 1, 1);
        var work = new ScheduledWork(start, start.AddDays(3_652), 900_000_000_000_000_001); // 3,653 days
        var day = start.AddDays(2_999);                                                       // 3,000 of them gone

        var expected = (long)(((Int128)900_000_000_000_000_001 * 3_000 * 2 + 3_653) / (2 * 3_653));
        Assert.Equal(expected, SchedulePlan.ActivityPlannedValue(work, day));
        Assert.Equal(739_118_532_712_838_763, expected);
    }

    // ---- Planned value of the plan ----------------------------------------------------------------------------------------

    [Fact]
    public void The_plan_adds_the_rounded_activity_figures_and_reaches_the_total_weight_at_the_last_finish()
    {
        var work = new[]
        {
            new ScheduledWork(March1, March10, 1_000_001),             // 10 days
            new ScheduledWork(new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 6), 200_000), // 3 days
            new ScheduledWork(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 14), 77_777)  // 7 days
        };

        // On 5 Mar: 500,000.5 -> 500,001; 2 of 3 days of 200,000 = 133,333.33 -> 133,333; nothing of the third.
        Assert.Equal(633_334, SchedulePlan.PlannedValue(work, new DateOnly(2026, 3, 5)));
        // On 9 Mar: 900,000.9 -> 900,001; all 200,000; 2 of 7 of 77,777 = 22,222 exactly.
        Assert.Equal(1_122_223, SchedulePlan.PlannedValue(work, new DateOnly(2026, 3, 9)));
        Assert.Equal(1_277_778, SchedulePlan.PlannedValue(work, new DateOnly(2026, 3, 14)));
        Assert.Equal(work.Sum(item => item.WeightMinor), SchedulePlan.PlannedValue(work, new DateOnly(2026, 3, 14)));
        Assert.Equal(0, SchedulePlan.PlannedValue(work, new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void An_activity_with_no_linked_lines_weighs_nothing_and_alone_does_not_make_a_schedule()
    {
        var lines = Rows();
        var unlinked = Activity("Mobilisation", March1, March10);
        var linked = Activity("Footings", March1, March10, lines[0].BudgetLineId);

        var alone = ScheduleWeights.From([unlinked], lines, VarianceBasis.IncludingVat);
        Assert.False(alone.UsesSchedule);
        Assert.Equal(0, alone.TotalWeight);

        var both = ScheduleWeights.From([unlinked, linked], lines, VarianceBasis.IncludingVat);
        Assert.True(both.UsesSchedule);
        Assert.Equal([0L, 1_120_003L], both.Activities.Select(item => item.Weight));
        Assert.Equal(1_120_003, SchedulePlan.PlannedValue(both.Planned, March10));
    }

    [Fact]
    public void The_weight_is_the_linked_lines_budget_on_the_comparison_basis_and_a_removed_line_counts_nothing()
    {
        var lines = Rows();
        var removedLine = Guid.NewGuid(); // linked once, no longer an active line: not in the Progress tab's rows
        var activity = Activity("Concrete", March1, March10, lines[0].BudgetLineId, lines[1].BudgetLineId, removedLine);

        Assert.Equal(1_000_003 + 250_001, ScheduleWeights.From([activity], lines, VarianceBasis.ExcludingVat).TotalWeight);
        Assert.Equal(1_120_003 + 280_001, ScheduleWeights.From([activity], lines, VarianceBasis.IncludingVat).TotalWeight);
    }

    [Fact]
    public void The_baseline_plans_from_the_baseline_dates_and_leaves_out_activities_added_after_it()
    {
        var lines = Rows();
        // Baseline 1-10 Mar; it has since slipped to 6-15 Mar. A second activity was added after the baseline was set.
        var slipped = Activity("Footings", new DateOnly(2026, 3, 6), new DateOnly(2026, 3, 15), lines[0].BudgetLineId) with
        {
            BaselineStart = March1, BaselineFinish = March10
        };
        var added = Activity("Columns", March1, March10, lines[1].BudgetLineId);
        var weights = ScheduleWeights.From([slipped, added], lines, VarianceBasis.ExcludingVat);
        var day = new DateOnly(2026, 3, 7);

        Assert.True(weights.HasBaseline);
        // Current: 2 of 10 days of 1,000,003 = 200,000.6 -> 200,001, plus 7 of 10 of 250,001 = 175,000.7 -> 175,001.
        Assert.Equal(375_002, SchedulePlan.PlannedValue(weights.Planned, day));
        // Baseline: 7 of 10 of 1,000,003 = 700,002.1 -> 700,002; the added activity was not in the baseline.
        Assert.Equal(700_002, SchedulePlan.PlannedValue(weights.Baseline, day));
        Assert.Equal(5, SchedulePlan.SlipDays(slipped.PlannedFinish, slipped.BaselineFinish));
        Assert.Null(SchedulePlan.SlipDays(added.PlannedFinish, added.BaselineFinish));
        Assert.Equal(-3, SchedulePlan.SlipDays(new DateOnly(2026, 3, 7), March10));
    }

    // ---- Percent complete and behind ------------------------------------------------------------------------------------------

    [Fact]
    public void Percent_complete_is_the_budget_weighted_average_of_the_linked_lines_rounded_once()
    {
        // (1,000,003 x 50% + 250,001 x 20%) / 1,250,004 = 4,399.9995 bps: rounded once to 4,400.
        Assert.Equal(4_400, SchedulePlan.PercentComplete([new(1_000_003, 5_000), new(250_001, 2_000)]));
        // (3 x 33.33% + 1 x 0%) / 4 = 24.9975% -> 2,500 bps (2,499.75 rounds up).
        Assert.Equal(2_500, SchedulePlan.PercentComplete([new(3, 3_333), new(1, 0)]));
        // Lines with no budget are averaged plainly.
        Assert.Equal(5_000, SchedulePlan.PercentComplete([new(0, 10_000), new(0, 0)]));
        Assert.Null(SchedulePlan.PercentComplete([]));
    }

    [Fact]
    public void Percent_complete_reads_100_only_when_every_line_is_done()
    {
        // 99,999 x 100% + 1 x 0% averages to 99.999%, which would round to 100%: it is held at 99.99% because a line is still open.
        Assert.Equal(9_999, SchedulePlan.PercentComplete([new(99_999, 10_000), new(1, 0)]));
        Assert.Equal(10_000, SchedulePlan.PercentComplete([new(99_999, 10_000), new(1, 10_000)]));
    }

    [Fact]
    public void An_activity_is_behind_when_its_planned_finish_has_passed_and_a_linked_line_is_not_done()
    {
        var today = new DateOnly(2026, 3, 11);
        LinkedLineProgress[] open = [new(100, 10_000), new(100, 9_900)];
        LinkedLineProgress[] done = [new(100, 10_000), new(100, 10_000)];

        Assert.True(SchedulePlan.IsBehind(March10, today, open));
        Assert.False(SchedulePlan.IsBehind(March10, today, done));
        Assert.False(SchedulePlan.IsBehind(today, today, open));   // finishing today is not past its finish yet
        Assert.False(SchedulePlan.IsBehind(March10, today, []));    // nothing linked: flagged to link lines instead
    }

    // ---- The activity's checks --------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_activity_keeps_its_name_dates_and_lines_and_reports_every_problem_at_once()
    {
        var project = TestData.NewProject();
        var line = Guid.NewGuid();
        var activity = ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project, new ScheduleActivityDetails("  Footings ", March1, March10, [line, line]),
            Context((line, "Footings concrete")), 3, TestData.UserId, " req-1 ", TestData.Now);

        Assert.Equal(("Footings", March1, March10, 3, "req-1"), (activity.Name, activity.PlannedStart, activity.PlannedFinish, activity.SortOrder,
            activity.ClientRequestId));
        Assert.Equal([line], activity.LinkedLineIds());
        Assert.Null(activity.BaselineStart);

        var errors = Assert.Throws<ValidationFailedException>(() => ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project,
            new ScheduleActivityDetails(" ", March10, March1, [Guid.NewGuid()]), ScheduleLineContext.Empty, 0, TestData.UserId, null, TestData.Now)).Errors;
        Assert.Equal(["Enter the activity name."], errors["name"]);
        Assert.Equal(["The planned finish can't be before the planned start."], errors["plannedFinish"]);
        Assert.Equal(["That budget line isn't part of this project any more. Reload the page and try again."], errors["budgetLineIds[0]"]);

        var dates = Assert.Throws<ValidationFailedException>(() => ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project,
            new ScheduleActivityDetails("Footings", default, new DateOnly(2101, 1, 1), null), ScheduleLineContext.Empty, 0, TestData.UserId, null,
            TestData.Now)).Errors;
        Assert.Equal(["Choose the planned start."], dates["plannedStart"]);
        Assert.Equal(["Choose a valid date."], dates["plannedFinish"]);
    }

    [Fact]
    public void A_budget_line_already_in_another_activity_is_refused_naming_that_activity()
    {
        var line = Guid.NewGuid();
        var context = new ScheduleLineContext(new Dictionary<Guid, string> { [line] = "Hollow blocks" },
            new Dictionary<Guid, string> { [line] = "Masonry walls" });

        var errors = Assert.Throws<ValidationFailedException>(() => ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, TestData.NewProject(),
            new ScheduleActivityDetails("Partitions", March1, March10, [line]), context, 0, TestData.UserId, null, TestData.Now)).Errors;

        Assert.Equal(
            ["\"Hollow blocks\" is already in the activity \"Masonry walls\". A budget line can be in one activity only: take it out of that activity first."],
            errors["budgetLineIds[0]"]);
    }

    [Fact]
    public void An_activity_links_at_most_500_lines()
    {
        var ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray();
        var errors = Assert.Throws<ValidationFailedException>(() => ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, TestData.NewProject(),
            new ScheduleActivityDetails("Everything", March1, March10, ids), ScheduleLineContext.Empty, 0, TestData.UserId, null, TestData.Now)).Errors;

        Assert.Equal(["An activity can link at most 500 budget lines. Split it into smaller activities."], errors["budgetLineIds"]);
    }

    [Fact]
    public void Changing_an_activity_replaces_its_lines_and_saving_the_same_details_changes_nothing()
    {
        var project = TestData.NewProject();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        var context = Context((first, "One"), (second, "Two"));
        var activity = ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project, new ScheduleActivityDetails("Slab", March1, March10, [first]), context,
            0, TestData.UserId, null, TestData.Now);

        Assert.False(activity.Update(project, new ScheduleActivityDetails("Slab", March1, March10, [first]), context, null, TestData.Now));
        Assert.True(activity.Update(project, new ScheduleActivityDetails("Slab", March1, March10.AddDays(4), [second]), context, 5, TestData.Now));
        Assert.Equal([second], activity.LinkedLineIds());
        Assert.Equal((March10.AddDays(4), 5), (activity.PlannedFinish, activity.SortOrder));
    }

    [Fact]
    public void Setting_the_baseline_copies_the_planned_dates_and_can_be_repeated()
    {
        var project = TestData.NewProject();
        var activity = ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project, new ScheduleActivityDetails("Slab", March1, March10, null),
            ScheduleLineContext.Empty, 0, TestData.UserId, null, TestData.Now);

        activity.SetBaseline(project, TestData.Now);
        activity.Update(project, new ScheduleActivityDetails("Slab", March1, March10.AddDays(3), null), ScheduleLineContext.Empty, null, TestData.Now);
        Assert.Equal((March1, March10), (activity.BaselineStart!.Value, activity.BaselineFinish!.Value));

        activity.SetBaseline(project, TestData.Now.AddDays(1));
        Assert.Equal((March1, March10.AddDays(3), TestData.Now.AddDays(1)), (activity.BaselineStart!.Value, activity.BaselineFinish!.Value,
            activity.BaselineSetAtUtc!.Value));
    }

    [Fact]
    public void Removing_needs_a_reason_frees_the_lines_and_a_removed_activity_or_an_archived_project_takes_no_change()
    {
        var project = TestData.NewProject();
        var line = Guid.NewGuid();
        var activity = ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, project, new ScheduleActivityDetails("Slab", March1, March10, [line]),
            Context((line, "Slab")), 0, TestData.UserId, null, TestData.Now);

        Assert.Equal(["Enter the reason for removing it."],
            Assert.Throws<ValidationFailedException>(() => activity.Remove(project, " ", TestData.UserId, TestData.Now)).Errors["reason"]);
        activity.Remove(project, "Merged into the slab pour", TestData.UserId, TestData.Now);
        Assert.Equal((ScheduleActivityStatus.Removed, "Merged into the slab pour"), (activity.Status, activity.RemovedReason));
        Assert.Empty(activity.Lines);
        Assert.Equal("This activity was removed from the schedule, so it can't be changed.",
            Assert.Throws<DomainRuleException>(() => activity.SetBaseline(project, TestData.Now)).Message);

        var archived = TestData.NewProject();
        archived.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal("This project is archived. Restore it before making changes.", Assert.Throws<DomainRuleException>(() =>
            ScheduleActivity.Create(Guid.NewGuid(), TestData.Tenant, archived, new ScheduleActivityDetails("Slab", March1, March10, null),
                ScheduleLineContext.Empty, 0, TestData.UserId, null, TestData.Now)).Message);
    }

    // ---- Where the planned value lands --------------------------------------------------------------------------------------

    // The project runs 1 Jan to 11 Apr 2026 (100 days); on 7 Mar the straight line has 65 of them gone.
    private static readonly DateOnly AsOf = new(2026, 3, 7);

    private static Project WorkedProject() =>
        Project.Create(Guid.NewGuid(), TestData.Tenant, "Tower A", null, null, 5_000_000, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 11), TestData.Now);

    [Fact]
    public void Without_a_schedule_the_progress_report_keeps_the_straight_line_planned_value()
    {
        var project = WorkedProject();
        var plain = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, Rows(), Spent);
        var empty = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, Rows(), Spent, []);
        var unweighted = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, Rows(), Spent, [Activity("Mobilisation", March1, March10)]);

        Assert.Equal(812_503, plain.EarnedValue.PlannedMinor);    // 1,250,004 x 65 / 100 = 812,502.6
        Assert.Equal(ProgressReportBuilder.PlannedFromProjectDates, plain.PlannedFrom);
        Assert.Equal(plain, empty with { Divisions = plain.Divisions });
        Assert.Equal(plain.EarnedValue, unweighted.EarnedValue);
        Assert.Equal(ProgressReportBuilder.PlannedFromProjectDates, unweighted.PlannedFrom);
    }

    [Fact]
    public void With_a_schedule_the_planned_value_schedule_index_and_variance_follow_the_activities_and_the_forecast_does_not_move()
    {
        var project = WorkedProject();
        var rows = Rows();
        var schedule = new[]
        {
            Activity("Footings", March1, March10, rows[0].BudgetLineId),   // 7 of 10 days: 700,002.1 -> 700,002
            Activity("Columns", new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 20), rows[1].BudgetLineId) // not started
        };

        var straight = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, rows, Spent).EarnedValue;
        var report = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, rows, Spent, schedule);
        var ev = report.EarnedValue;

        Assert.Equal(ProgressReportBuilder.PlannedFromSchedule, report.PlannedFrom);
        Assert.Equal(700_002, ev.PlannedMinor);
        Assert.Equal(550_002, ev.EarnedMinor);                  // 500,001.5 -> 500,002 (50%) + 50,000.2 -> 50,000 (20%)
        Assert.Equal(-150_000, ev.ScheduleVarianceMinor);
        Assert.Equal(7_857, ev.SpiBps);                        // 550,002 / 700,002 = 0.78571...
        Assert.Equal(5_600, ev.PlannedPercentBps);             // 700,002 / 1,250,004
        // The cost index method uses no planned value: the forecasts and the cost figures are the same as without a schedule.
        Assert.Equal((straight.ForecastMinor, straight.ToCompleteMinor, straight.ForecastVarianceMinor, straight.CpiBps, straight.CostVarianceMinor,
                straight.ForecastMarginMinor),
            (ev.ForecastMinor, ev.ToCompleteMinor, ev.ForecastVarianceMinor, ev.CpiBps, ev.CostVarianceMinor, ev.ForecastMarginMinor));

        // Before anything is planned there is no schedule index (nothing to divide by), as on the straight line.
        var early = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, new DateOnly(2026, 2, 1), rows, Spent, schedule).EarnedValue;
        Assert.Equal((0L, (int?)null), (early.PlannedMinor!.Value, early.SpiBps));
    }

    // ---- The S-curve ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_curve_steps_weekly_from_the_earliest_start_to_the_latest_finish_and_widen_to_stay_within_120_points()
    {
        var steps = ScheduleFigures.Steps([Activity("A", March1, March10), Activity("B", new DateOnly(2026, 2, 20), new DateOnly(2026, 3, 4))
            with { BaselineFinish = new DateOnly(2026, 3, 30) }])!;
        Assert.Equal((new DateOnly(2026, 2, 20), 7, 7), (steps.First, steps.StepDays, steps.Count)); // 20 Feb .. 3 Apr covers 30 Mar
        Assert.Equal(new DateOnly(2026, 4, 3), steps.Last);

        var tenYears = ScheduleFigures.Steps([Activity("Long", new DateOnly(2026, 1, 1), new DateOnly(2036, 1, 1))])!;
        Assert.True(tenYears.Count <= 119);
        Assert.True(tenYears.Last >= new DateOnly(2036, 1, 1));
        Assert.Equal(0, tenYears.StepDays % 7);

        var oneDay = ScheduleFigures.Steps([Activity("Pour", March1, March1)])!;
        Assert.Equal(1, oneDay.Count);
        Assert.Null(ScheduleFigures.Steps([]));
    }

    [Fact]
    public void The_curve_point_for_today_equals_the_progress_report_and_stops_earned_and_actual_at_today()
    {
        var project = WorkedProject();
        var rows = Rows();
        var schedule = new[] { Activity("Footings", March1, March10, rows[0].BudgetLineId), Activity("Columns", new DateOnly(2026, 3, 8),
            new DateOnly(2026, 3, 20), rows[1].BudgetLineId) };
        var steps = ScheduleFigures.Steps(schedule)!; // 1, 8, 15, 22 Mar
        // Footings was at 30% on 1 Mar and 50% by today; Columns 20% this week. 100,000 spent by 1 Mar, 80,000 since.
        ProgressStepRow[] history = [new(rows[0].BudgetLineId, 0, 3_000), new(rows[0].BudgetLineId, 1, 5_000), new(rows[1].BudgetLineId, 1, 2_000)];
        CostStepRow[] costs = [new(0, 100_000, 112_000), new(1, 80_000, 89_600)];

        var curve = ScheduleFigures.Curve(project.Id, VarianceBasis.ExcludingVat, AsOf, schedule, rows, steps, history, costs);
        var report = ProgressReportBuilder.Build(project, VarianceBasis.ExcludingVat, AsOf, rows, new MeasuredAmounts(180_000, 201_600), schedule);

        Assert.Equal([new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 22)],
            curve.Points.Select(point => point.Date));
        var today = curve.Points.Single(point => point.IsToday);
        Assert.Equal(((long?)700_002, (long?)550_002, (long?)180_000), (report.EarnedValue.PlannedMinor, (long?)report.EarnedValue.EarnedMinor,
            (long?)report.EarnedValue.SpentMinor));
        Assert.Equal((report.EarnedValue.PlannedMinor, (long?)report.EarnedValue.EarnedMinor, (long?)report.EarnedValue.SpentMinor),
            ((long?)today.PlannedMinor, today.EarnedMinor, today.ActualMinor));
        // 1 Mar: 1 of 10 days of Footings (100,000.3), 30% of it earned (300,000.9), 100,000 spent.
        Assert.Equal((100_000L, (long?)300_001, (long?)100_000), (curve.Points[0].PlannedMinor, curve.Points[0].EarnedMinor, curve.Points[0].ActualMinor));
        Assert.All(curve.Points.Where(point => point.Date > AsOf), point => Assert.Equal(((long?)null, (long?)null), (point.EarnedMinor, point.ActualMinor)));
        Assert.Equal(1_250_004, curve.Points[^1].PlannedMinor);
        Assert.False(curve.HasBaseline);
        Assert.All(curve.Points, point => Assert.Null(point.BaselinePlannedMinor));
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------

    private static readonly MeasuredAmounts Spent = new(180_000, 201_600);

    // Two lines with 12% VAT and awkward amounts: Footings 50% done, Columns 20%.
    private static ProgressLineRow[] Rows() =>
    [
        new(Guid.NewGuid(), "Footings", 3, "3300", 1_000_003, 1_120_003, 5_000, new DateOnly(2026, 3, 5)),
        new(Guid.NewGuid(), "Columns", 3, "3300", 250_001, 280_001, 2_000, new DateOnly(2026, 3, 7)),
    ];

    private static ScheduleActivityRow Activity(string name, DateOnly start, DateOnly finish, params Guid[] lines) =>
        new(Guid.NewGuid(), name, start, finish, null, null, null, 0, 0, lines);

    private static ScheduleLineContext Context(params (Guid Id, string Description)[] lines) =>
        new(lines.ToDictionary(line => line.Id, line => line.Description), new Dictionary<Guid, string>());
}
