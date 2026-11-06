using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;

namespace ConstructOPX.Application.Features.Schedule;

/// <summary>
/// The schedule's weights on one set of line amounts: each activity's weight is the budget of its linked active lines on the company's comparison
/// basis, taken from the same rows the Progress tab reads, so the schedule and the Progress tab can never weigh a line differently.
/// <para>
/// Budget lines in no activity are still planned: on the straight line over the project's start date and target end date. Earned value counts every
/// line, so the planned value has to as well, or one linked line would make the whole project read as ahead of its plan. A project with no target
/// end date has no such line, so while budget is left outside the activities the schedule has no planned value (the Progress tab shows none, as for
/// a project with no schedule) until an end date is set or every line is in an activity.
/// </para>
/// <para>
/// In the baseline, the lines no baseline activity covers are on that same straight line: the lines in no activity, and the lines of an activity
/// added after the baseline (which has no baseline dates), so linking a line to a new activity does not move the baseline.
/// </para>
/// </summary>
public sealed class ScheduleWeights
{
    private readonly ScheduledWork[] planned;
    private readonly ScheduledWork[] baseline;
    private readonly long baselineUncoveredWeight;
    private readonly DateOnly straightStart;
    private readonly DateOnly? straightEnd;

    private ScheduleWeights(IReadOnlyList<(ScheduleActivityRow Activity, long Weight)> activities, long unlinkedWeight, DateOnly straightStart,
        DateOnly? straightEnd)
    {
        Activities = activities;
        UnlinkedWeight = unlinkedWeight;
        this.straightStart = straightStart;
        this.straightEnd = straightEnd;
        baselineUncoveredWeight = unlinkedWeight + activities.Where(item => !InBaseline(item.Activity)).Sum(item => item.Weight);
        planned = activities.Select(item => new ScheduledWork(item.Activity.PlannedStart, item.Activity.PlannedFinish, item.Weight)).ToArray();
        // Only activities that were in the baseline: one added after it has no baseline dates (its lines are on the straight line there).
        baseline = activities
            .Where(item => InBaseline(item.Activity))
            .Select(item => new ScheduledWork(item.Activity.BaselineStart!.Value, item.Activity.BaselineFinish!.Value, item.Weight)).ToArray();
    }

    private static bool InBaseline(ScheduleActivityRow activity) => activity is { BaselineStart: not null, BaselineFinish: not null };

    public IReadOnlyList<(ScheduleActivityRow Activity, long Weight)> Activities { get; }

    /// <summary>The budget of the active lines that no activity covers, on the comparison basis.</summary>
    public long UnlinkedWeight { get; }

    /// <summary>
    /// At least one activity has weight and every budget line outside the activities can be planned (the project has an end date to plan them
    /// to): the planned value comes from the schedule instead of the project's dates alone.
    /// </summary>
    public bool UsesSchedule => Activities.Any(item => item.Weight > 0) && (UnlinkedWeight == 0 || straightEnd is not null);

    /// <summary>A baseline exists when at least one activity has baseline dates.</summary>
    public bool HasBaseline => Activities.Any(item => item.Activity.BaselineStart is not null);

    /// <summary>
    /// The baseline has a planned value: it exists and the lines it leaves out of the activities can be planned. A project with no end date and a
    /// line in an activity added after the baseline has none, so no baseline figure is shown rather than one that stops short of the budget.
    /// </summary>
    public bool PlansBaseline => HasBaseline && UsesSchedule && (baselineUncoveredWeight == 0 || straightEnd is not null);

    /// <summary>The weight of the activities alone, without the lines they leave out.</summary>
    public long TotalWeight => Activities.Sum(item => item.Weight);

    /// <summary>The activities' dates and weights, without the straight line for the lines they leave out.</summary>
    public IEnumerable<ScheduledWork> Planned => planned;

    /// <summary>The baseline activities' dates and weights, without the straight line for the lines they leave out.</summary>
    public IEnumerable<ScheduledWork> Baseline => baseline;

    /// <summary>The dates the straight line runs between; null when it plans nothing (no budget outside the activities, or no schedule in use).</summary>
    public (DateOnly Start, DateOnly End)? StraightLine =>
        UsesSchedule && straightEnd is { } end && (UnlinkedWeight > 0 || (HasBaseline && baselineUncoveredWeight > 0)) ? (straightStart, end) : null;

    /// <summary>What should be done by the end of the date: the activities' rounded figures plus the rounded straight line for the rest.</summary>
    public long PlannedAt(DateOnly date) => !UsesSchedule ? 0 : SchedulePlan.PlannedValue(planned, date) + StraightLineAt(UnlinkedWeight, date);

    /// <summary>The same from the baseline dates; the lines no baseline activity covers are on the straight line, whose dates the baseline does not freeze.</summary>
    public long BaselineAt(DateOnly date) => !PlansBaseline ? 0 : SchedulePlan.PlannedValue(baseline, date) + StraightLineAt(baselineUncoveredWeight, date);

    private long StraightLineAt(long weight, DateOnly date) =>
        weight == 0 ? 0 : SchedulePlan.StraightLineValue(weight, straightStart, straightEnd!.Value, date);

    public static ScheduleWeights From(IReadOnlyList<ScheduleActivityRow> activities, IReadOnlyList<ProgressLineRow> lines, VarianceBasis basis,
        Project project)
    {
        var budgets = lines.ToDictionary(line => line.BudgetLineId, line => BudgetOn(line, basis));
        var covered = activities.SelectMany(activity => activity.BudgetLineIds).ToHashSet();
        var unlinked = lines.Where(line => !covered.Contains(line.BudgetLineId)).Sum(line => BudgetOn(line, basis));
        return new ScheduleWeights(activities
            .Select(activity => (activity, activity.BudgetLineIds.Sum(id => budgets.GetValueOrDefault(id)))).ToArray(), unlinked, project.StartDate,
            project.TargetEndDate);
    }

    public static long BudgetOn(ProgressLineRow line, VarianceBasis basis) => basis == VarianceBasis.ExcludingVat ? line.NetMinor : line.GrossMinor;
}

/// <summary>Turns the reader's rows into the schedule and the S-curve. Nothing here reads the database.</summary>
public static class ScheduleFigures
{
    /// <summary>The most points the S-curve returns: at most 119 weekly (or wider) points and today.</summary>
    public const int MaximumPoints = 120;

    public static ScheduleDto Build(Project project, VarianceBasis basis, DateOnly today, IReadOnlyList<ScheduleActivityRow> activities,
        IReadOnlyList<ProgressLineRow> lines)
    {
        var weights = ScheduleWeights.From(activities, lines, basis, project);
        var lineById = lines.ToDictionary(line => line.BudgetLineId);
        var activityOfLine = new Dictionary<Guid, Guid>();
        foreach (var activity in activities)
            foreach (var lineId in activity.BudgetLineIds.Where(lineById.ContainsKey)) activityOfLine[lineId] = activity.Id;

        var activityDtos = weights.Activities.Select(item =>
        {
            var activity = item.Activity;
            var linked = activity.BudgetLineIds.Where(lineById.ContainsKey).ToArray();
            var progress = linked.Select(id => new LinkedLineProgress(ScheduleWeights.BudgetOn(lineById[id], basis), lineById[id].PercentBps ?? 0)).ToArray();
            return new ScheduleActivityDto(activity.Id, activity.Name, activity.PlannedStart, activity.PlannedFinish,
                SchedulePlan.PlannedDays(activity.PlannedStart, activity.PlannedFinish), activity.BaselineStart, activity.BaselineFinish,
                SchedulePlan.SlipDays(activity.PlannedFinish, activity.BaselineFinish), activity.SortOrder, item.Weight,
                SchedulePlan.PercentComplete(progress), SchedulePlan.IsBehind(activity.PlannedFinish, today, progress), linked.Length == 0, linked,
                activity.Version);
        }).ToArray();

        var lineDtos = lines.Select(line => new ScheduleBudgetLineDto(line.BudgetLineId, line.Description, line.WbsCode, line.Division,
            DivisionName(line.Division), ScheduleWeights.BudgetOn(line, basis), line.PercentBps ?? 0,
            activityOfLine.TryGetValue(line.BudgetLineId, out var owner) ? owner : null)).ToArray();
        var unplanned = lineDtos.Where(line => line.ActivityId is null).ToArray();

        return new ScheduleDto(project.Id, today, basis.ToString(), project.IsArchived, weights.UsesSchedule,
            weights.PlannedAt(today), weights.PlansBaseline ? weights.BaselineAt(today) : null,
            weights.TotalWeight, lineDtos.Sum(line => line.BudgetMinor), unplanned.Sum(line => line.BudgetMinor), unplanned.Length,
            activities.Max(activity => activity.BaselineSetAtUtc), activityDtos, lineDtos);
    }

    /// <summary>
    /// The points of the curve: from the earliest start (planned or baseline) to the latest finish, every 7 days, or every 14, 21 and so on when
    /// a week apart would give more than 119 points (today makes at most 120). The last point is the first one on or after the latest finish. Null without activities.
    /// When budget lines are planned on the straight line (<paramref name="straightLine"/>), the curve also covers those dates, so it reaches the
    /// whole budget.
    /// </summary>
    public static CurveSteps? Steps(IReadOnlyList<ScheduleActivityRow> activities, (DateOnly Start, DateOnly End)? straightLine = null)
    {
        if (activities.Count == 0) return null;
        var first = activities.Min(activity => Min(activity.PlannedStart, activity.BaselineStart));
        var last = activities.Max(activity => Max(activity.PlannedFinish, activity.BaselineFinish));
        if (straightLine is { } window)
        {
            // Project dates are not range-checked like the activities' (2000 to 2100): a typo must not stretch the curve past what a date can hold.
            first = Min(first, WithinPlanningYears(window.Start));
            last = Max(last, WithinPlanningYears(window.End));
        }

        var span = last.DayNumber - first.DayNumber;
        const int weekly = MaximumPoints - 1;
        var weeks = (span + 6) / 7;
        var stepDays = 7 * Math.Max(1, (weeks + weekly - 2) / (weekly - 1));
        var intervals = (span + stepDays - 1) / stepDays;
        return new CurveSteps(first, stepDays, intervals + 1);
    }

    /// <summary>
    /// The S-curve from the reader's rows. <paramref name="lines"/> are the Progress tab's rows for today (the line amounts and today's percent);
    /// <paramref name="progressSteps"/> and <paramref name="costSteps"/> are the history in steps, so each point adds up the same rounded line
    /// figures the Progress tab adds up for its date.
    /// </summary>
    public static ScheduleCurveDto Curve(Project project, VarianceBasis basis, DateOnly today, IReadOnlyList<ScheduleActivityRow> activities,
        IReadOnlyList<ProgressLineRow> lines, CurveSteps? steps, IReadOnlyList<ProgressStepRow> progressSteps, IReadOnlyList<CostStepRow> costSteps)
    {
        if (steps is null) return new ScheduleCurveDto(project.Id, today, basis.ToString(), false, 7, []);

        var weights = ScheduleWeights.From(activities, lines, basis, project);
        var budgets = lines.ToDictionary(line => line.BudgetLineId, line => ScheduleWeights.BudgetOn(line, basis));

        // Earned value moves only where a line's percent changed: keep each line's figure and add the difference at the step it changed in.
        var changesByStep = progressSteps.Where(row => budgets.ContainsKey(row.BudgetLineId)).ToLookup(row => row.Step);
        var costByStep = costSteps.ToDictionary(row => row.Step, row => new MeasuredAmounts(row.NetMinor, row.GrossMinor));
        var lineEarned = new Dictionary<Guid, long>();
        long earned = 0;
        var spent = default(MeasuredAmounts);

        var points = new List<ScheduleCurvePointDto>(MaximumPoints);
        for (var step = 0; step <= steps.Count; step++)
        {
            foreach (var change in changesByStep[step])
            {
                var value = EarnedValueCalculator.LineEarnedValue(budgets[change.BudgetLineId], change.PercentBps);
                earned += value - lineEarned.GetValueOrDefault(change.BudgetLineId);
                lineEarned[change.BudgetLineId] = value;
            }

            spent += costByStep.GetValueOrDefault(step);
            if (step == steps.Count) break;

            var date = steps.Point(step);
            // Today between the previous point and this one: everything recorded so far is in, as on the Progress tab.
            if (date > today && step > 0 && steps.Point(step - 1) < today)
                points.Add(Point(today, isToday: true, weights, earned, spent.On(basis)));
            points.Add(Point(date, date == today, weights, date <= today ? earned : null, date <= today ? spent.On(basis) : null));
        }

        return new ScheduleCurveDto(project.Id, today, basis.ToString(), weights.PlansBaseline, steps.StepDays, points);
    }

    private static ScheduleCurvePointDto Point(DateOnly date, bool isToday, ScheduleWeights weights, long? earned, long? actual) =>
        new(date, isToday, weights.PlannedAt(date), weights.PlansBaseline ? weights.BaselineAt(date) : null, earned, actual);

    /// <summary>The step a date belongs to: the first point on or after it (0 for anything on or before the first point).</summary>
    public static int StepOf(CurveSteps steps, DateOnly date)
    {
        var days = date.DayNumber - steps.First.DayNumber;
        return days <= 0 ? 0 : (days + steps.StepDays - 1) / steps.StepDays;
    }

    private static string DivisionName(int? division) =>
        division is null ? CostReportBuilder.UnclassifiedName : WbsCatalog.MainScopeOf(division.Value) ?? $"Division {division}";

    private static DateOnly WithinPlanningYears(DateOnly date) =>
        date < ScheduleActivity.EarliestDate ? ScheduleActivity.EarliestDate : date > ScheduleActivity.LatestDate ? ScheduleActivity.LatestDate : date;

    private static DateOnly Min(DateOnly date, DateOnly? other) => other is { } value && value < date ? value : date;

    private static DateOnly Max(DateOnly date, DateOnly? other) => other is { } value && value > date ? value : date;
}
