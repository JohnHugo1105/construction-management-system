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
/// </summary>
public sealed class ScheduleWeights
{
    private ScheduleWeights(IReadOnlyList<(ScheduleActivityRow Activity, long Weight)> activities) => Activities = activities;

    public IReadOnlyList<(ScheduleActivityRow Activity, long Weight)> Activities { get; }

    /// <summary>At least one activity has weight: the planned value comes from the schedule instead of the project's dates.</summary>
    public bool UsesSchedule => Activities.Any(item => item.Weight > 0);

    /// <summary>A baseline exists when at least one activity has baseline dates.</summary>
    public bool HasBaseline => Activities.Any(item => item.Activity.BaselineStart is not null);

    public long TotalWeight => Activities.Sum(item => item.Weight);

    public IEnumerable<ScheduledWork> Planned => Activities.Select(item => new ScheduledWork(item.Activity.PlannedStart, item.Activity.PlannedFinish, item.Weight));

    /// <summary>Only activities that were in the baseline: one added after it has no baseline dates and plans nothing there.</summary>
    public IEnumerable<ScheduledWork> Baseline => Activities
        .Where(item => item.Activity is { BaselineStart: not null, BaselineFinish: not null })
        .Select(item => new ScheduledWork(item.Activity.BaselineStart!.Value, item.Activity.BaselineFinish!.Value, item.Weight));

    public static ScheduleWeights From(IReadOnlyList<ScheduleActivityRow> activities, IReadOnlyList<ProgressLineRow> lines, VarianceBasis basis)
    {
        var budgets = lines.ToDictionary(line => line.BudgetLineId, line => BudgetOn(line, basis));
        return new ScheduleWeights(activities
            .Select(activity => (activity, activity.BudgetLineIds.Sum(id => budgets.GetValueOrDefault(id)))).ToArray());
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
        var weights = ScheduleWeights.From(activities, lines, basis);
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
            SchedulePlan.PlannedValue(weights.Planned, today), weights.HasBaseline ? SchedulePlan.PlannedValue(weights.Baseline, today) : null,
            weights.TotalWeight, lineDtos.Sum(line => line.BudgetMinor), unplanned.Sum(line => line.BudgetMinor), unplanned.Length,
            activities.Max(activity => activity.BaselineSetAtUtc), activityDtos, lineDtos);
    }

    /// <summary>
    /// The points of the curve: from the earliest start (planned or baseline) to the latest finish, every 7 days, or every 14, 21 and so on when
    /// a week apart would give more than 119 points (today makes at most 120). The last point is the first one on or after the latest finish. Null without activities.
    /// </summary>
    public static CurveSteps? Steps(IReadOnlyList<ScheduleActivityRow> activities)
    {
        if (activities.Count == 0) return null;
        var first = activities.Min(activity => Min(activity.PlannedStart, activity.BaselineStart));
        var last = activities.Max(activity => Max(activity.PlannedFinish, activity.BaselineFinish));
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
    public static ScheduleCurveDto Curve(Guid projectId, VarianceBasis basis, DateOnly today, IReadOnlyList<ScheduleActivityRow> activities,
        IReadOnlyList<ProgressLineRow> lines, CurveSteps? steps, IReadOnlyList<ProgressStepRow> progressSteps, IReadOnlyList<CostStepRow> costSteps)
    {
        var weights = ScheduleWeights.From(activities, lines, basis);
        if (steps is null) return new ScheduleCurveDto(projectId, today, basis.ToString(), false, 7, []);

        var budgets = lines.ToDictionary(line => line.BudgetLineId, line => ScheduleWeights.BudgetOn(line, basis));
        var planned = weights.Planned.ToArray();
        var baseline = weights.HasBaseline ? weights.Baseline.ToArray() : null;

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
                points.Add(Point(today, isToday: true, planned, baseline, earned, spent.On(basis)));
            points.Add(Point(date, date == today, planned, baseline, date <= today ? earned : null, date <= today ? spent.On(basis) : null));
        }

        return new ScheduleCurveDto(projectId, today, basis.ToString(), baseline is not null, steps.StepDays, points);
    }

    private static ScheduleCurvePointDto Point(DateOnly date, bool isToday, ScheduledWork[] planned, ScheduledWork[]? baseline, long? earned, long? actual) =>
        new(date, isToday, SchedulePlan.PlannedValue(planned, date), baseline is null ? null : SchedulePlan.PlannedValue(baseline, date), earned, actual);

    /// <summary>The step a date belongs to: the first point on or after it (0 for anything on or before the first point).</summary>
    public static int StepOf(CurveSteps steps, DateOnly date)
    {
        var days = date.DayNumber - steps.First.DayNumber;
        return days <= 0 ? 0 : (days + steps.StepDays - 1) / steps.StepDays;
    }

    private static string DivisionName(int? division) =>
        division is null ? CostReportBuilder.UnclassifiedName : WbsCatalog.MainScopeOf(division.Value) ?? $"Division {division}";

    private static DateOnly Min(DateOnly date, DateOnly? other) => other is { } value && value < date ? value : date;

    private static DateOnly Max(DateOnly date, DateOnly? other) => other is { } value && value > date ? value : date;
}
