using ConstructOPX.Domain.Features.Progress;

namespace ConstructOPX.Domain.Features.Schedule;

/// <summary>An activity's dates and weight, the only things the planned value needs.</summary>
/// <param name="WeightMinor">The sum of the linked budget lines' budget on the company's comparison basis.</param>
public readonly record struct ScheduledWork(DateOnly Start, DateOnly Finish, long WeightMinor);

/// <summary>One linked budget line of an activity: its budget on the comparison basis and its current percent complete.</summary>
public readonly record struct LinkedLineProgress(long BudgetMinor, int PercentBps);

/// <summary>
/// The schedule's arithmetic, in one place. Every amount is whole minor units. An activity's planned value is its weight times the share of its
/// planned days that have passed, multiplied first, divided last and rounded once, half away from zero (an <see cref="Int128"/> holds the
/// product); the project's planned value adds the rounded activity figures, so the total always equals the sum of what is shown.
/// <list type="bullet">
/// <item>Both the start and the finish are working days of the activity: 1 Mar to 10 Mar is 10 days, and a one-day activity starts and
/// finishes on the same day.</item>
/// <item>"By a date" means by the end of that day: on the start day one day of the activity is planned to be done, on the finish day all of it.
/// Before the start nothing is planned and after the finish all of it, so the share is clamped to 0 and 1.</item>
/// <item>Budget lines in no activity are planned on the straight line over the project's dates (<see cref="StraightLineValue"/>).</item>
/// </list>
/// </summary>
public static class SchedulePlan
{
    /// <summary>How many days the activity is planned to take, both ends counted.</summary>
    public static int PlannedDays(DateOnly start, DateOnly finish)
    {
        if (finish < start) throw new ArgumentOutOfRangeException(nameof(finish), "The finish can't be before the start.");
        return finish.DayNumber - start.DayNumber + 1;
    }

    /// <summary>How many of the planned days have passed by the end of <paramref name="asOf"/>: 0 before the start, all of them from the finish on.</summary>
    public static int ElapsedDays(DateOnly start, DateOnly finish, DateOnly asOf) =>
        Math.Clamp(asOf.DayNumber - start.DayNumber + 1, 0, PlannedDays(start, finish));

    /// <summary>What one activity should have done by the date: <c>round(weight x elapsed days / planned days)</c>.</summary>
    public static long ActivityPlannedValue(ScheduledWork work, DateOnly asOf)
    {
        if (work.WeightMinor < 0) throw new ArgumentOutOfRangeException(nameof(work), "An activity's weight can't be negative.");
        var days = PlannedDays(work.Start, work.Finish);
        var elapsed = ElapsedDays(work.Start, work.Finish, asOf);
        if (elapsed == days) return work.WeightMinor;
        return RoundedShare(work.WeightMinor, elapsed, days);
    }

    /// <summary>What the whole schedule should have done by the date: the rounded activity figures, added.</summary>
    public static long PlannedValue(IEnumerable<ScheduledWork> work, DateOnly asOf)
    {
        long total = 0;
        foreach (var item in work) total = checked(total + ActivityPlannedValue(item, asOf));
        return total;
    }

    /// <summary>
    /// What budget lines that no activity covers should have done by the date: the Progress tab's straight line, <c>round(weight x days gone /
    /// days between start and end)</c>. Nothing before the start, all of it on or after the end. It keeps the planned value and the earned value
    /// over the same lines, so a schedule that covers only some of the budget does not make the rest look finished.
    /// </summary>
    public static long StraightLineValue(long weightMinor, DateOnly start, DateOnly end, DateOnly asOf)
    {
        if (weightMinor < 0) throw new ArgumentOutOfRangeException(nameof(weightMinor), "A weight can't be negative.");
        var share = PlannedProgress.From(start, end, asOf)!.Value;
        return share.ElapsedDays == share.TotalDays ? weightMinor : RoundedShare(weightMinor, share.ElapsedDays, share.TotalDays);
    }

    /// <summary>
    /// An activity's percent complete in basis points: the budget-weighted average of its linked lines' percent, rounded once. Lines whose budget
    /// is all zero are averaged plainly. Null when no line is linked. It reads 100% only when every linked line is at 100%: an average that rounds
    /// up to 100% while a line is still open is shown as 99.99%.
    /// </summary>
    public static int? PercentComplete(IReadOnlyCollection<LinkedLineProgress> lines)
    {
        if (lines.Count == 0) return null;
        Int128 weight = 0, weighted = 0;
        foreach (var line in lines)
        {
            if (line.BudgetMinor < 0) throw new ArgumentOutOfRangeException(nameof(lines), "A budget can't be negative.");
            if (line.PercentBps is < 0 or > ProgressEntry.FullBps) throw new ArgumentOutOfRangeException(nameof(lines), "A percent is 0 to 10,000.");
            weight += line.BudgetMinor;
            weighted += (Int128)line.BudgetMinor * line.PercentBps;
        }

        var percent = weight > 0
            ? (int)RoundedShare(weighted, 1, weight)
            : (int)RoundedShare(lines.Sum(line => (long)line.PercentBps), 1, lines.Count);
        return percent == ProgressEntry.FullBps && !IsComplete(lines) ? ProgressEntry.FullBps - 1 : percent;
    }

    /// <summary>Every linked line is done. An activity with no linked line is not complete (it has nothing measured).</summary>
    public static bool IsComplete(IReadOnlyCollection<LinkedLineProgress> lines) =>
        lines.Count > 0 && lines.All(line => line.PercentBps >= ProgressEntry.FullBps);

    /// <summary>
    /// Behind: the planned finish is before today and the activity is below 100% (some linked line is not done). An activity with no linked line
    /// is not judged: it is flagged to have lines linked instead.
    /// </summary>
    public static bool IsBehind(DateOnly plannedFinish, DateOnly today, IReadOnlyCollection<LinkedLineProgress> lines) =>
        plannedFinish < today && lines.Count > 0 && !IsComplete(lines);

    /// <summary>Days the planned finish moved since the baseline: positive is later, negative earlier, null without a baseline.</summary>
    public static int? SlipDays(DateOnly plannedFinish, DateOnly? baselineFinish) =>
        baselineFinish is { } baseline ? plannedFinish.DayNumber - baseline.DayNumber : null;

    /// <summary><c>round(a x b / c)</c> half away from zero for non-negative values, multiplied in an <see cref="Int128"/> first.</summary>
    private static long RoundedShare(Int128 a, long b, Int128 c)
    {
        var (quotient, remainder) = Int128.DivRem(a * b, c);
        if (remainder * 2 >= c) quotient++;
        return (long)quotient;
    }
}
