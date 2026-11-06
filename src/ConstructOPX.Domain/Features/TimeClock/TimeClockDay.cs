namespace ConstructOPX.Domain.Features.TimeClock;

/// <summary>Why a day of scans needs a person to look before it can become attendance. A day can have several.</summary>
[Flags]
public enum TimeClockException
{
    None = 0,

    /// <summary>A time in with no time out after it (two Ins in a row, or the day ends on an In).</summary>
    MissingTimeOut = 1,

    /// <summary>A time out with no time in before it.</summary>
    MissingTimeIn = 2,

    /// <summary>The worker was not posted to this project that day.</summary>
    NotPostedHere = 4,

    /// <summary>More than 16 hours worked in one day.</summary>
    TooLong = 8
}

/// <summary>One scan as the day's calculation needs it.</summary>
public readonly record struct PunchMoment(DateTimeOffset At, PunchDirection Direction);

/// <summary>
/// A worker's day on one project worked out from the scans. Hours are what the scans say after the break and rounding; <see cref="ProposedOvertimeHours"/>
/// is only proposed: it becomes attendance overtime when a reviewer approves it.
/// </summary>
/// <param name="FirstIn">The first time in, if any.</param>
/// <param name="LastOut">The last time out, if any.</param>
/// <param name="BreakMinutes">The unpaid break taken off a single long span, or the gaps the worker scanned out for: for explanation.</param>
/// <param name="WorkedMinutes">The worked time after the break and rounding.</param>
/// <param name="RegularHours">The worked time up to the company's paid hours per day.</param>
/// <param name="ProposedOvertimeHours">The rest of the worked time.</param>
/// <param name="NightHours">The worked time between 22:00 and 06:00, rounded the same way and never more than the hours worked.</param>
public sealed record TimeClockDayResult(DateTimeOffset? FirstIn, DateTimeOffset? LastOut, int BreakMinutes, int WorkedMinutes, decimal RegularHours,
    decimal ProposedOvertimeHours, decimal NightHours, TimeClockException Exceptions)
{
    public decimal TotalHours => RegularHours + ProposedOvertimeHours;

    /// <summary>A day with a missing scan can't be confirmed: the hours would be a guess. The other flags are for the reviewer to judge.</summary>
    public bool HasMissingScan => (Exceptions & (TimeClockException.MissingTimeIn | TimeClockException.MissingTimeOut)) != 0;

    /// <summary>"Confirm all clean rows" confirms a day only with no exception at all and no overtime waiting for approval.</summary>
    public bool IsCleanWithout(bool overtimeApproved) => Exceptions == TimeClockException.None && (ProposedOvertimeHours == 0m || overtimeApproved);

    /// <summary>
    /// The hours the attendance entry gets. Unapproved overtime is left out (overtime needs the employer's authorization), and the night hours are
    /// then held to the hours that remain.
    /// </summary>
    public (decimal Regular, decimal Overtime, decimal Night) ToAttendance(bool approveOvertime)
    {
        var overtime = approveOvertime ? ProposedOvertimeHours : 0m;
        return (RegularHours, overtime, Math.Min(NightHours, RegularHours + overtime));
    }
}

/// <summary>Works out a day from its scans. Pure and deterministic: the same scans and settings always give the same hours.</summary>
public static class TimeClockDay
{
    private static readonly TimeSpan NightStart = TimeSpan.FromHours(22);
    private static readonly TimeSpan NightEnd = TimeSpan.FromHours(6);

    public static TimeClockDayResult Work(IEnumerable<PunchMoment> punches, TimeClockSettings settings, bool notPostedHere)
    {
        var ordered = punches.OrderBy(punch => punch.At).ThenBy(punch => punch.Direction).ToArray();
        var exceptions = notPostedHere ? TimeClockException.NotPostedHere : TimeClockException.None;

        // In, Out, In, Out: each In opens a span and the next Out closes it. Anything else is an exception, never a guess.
        var spans = new List<(DateTimeOffset From, DateTimeOffset To)>();
        DateTimeOffset? open = null;
        foreach (var punch in ordered)
        {
            if (punch.Direction == PunchDirection.In)
            {
                if (open is not null) exceptions |= TimeClockException.MissingTimeOut;
                open = punch.At;
            }
            else if (open is { } from)
            {
                spans.Add((from, punch.At));
                open = null;
            }
            else
            {
                exceptions |= TimeClockException.MissingTimeIn;
            }
        }
        if (open is not null) exceptions |= TimeClockException.MissingTimeOut;

        var firstIn = ordered.Where(punch => punch.Direction == PunchDirection.In).Select(punch => (DateTimeOffset?)punch.At).FirstOrDefault();
        var lastOut = ordered.Where(punch => punch.Direction == PunchDirection.Out).Select(punch => (DateTimeOffset?)punch.At).LastOrDefault();

        var workedSeconds = spans.Sum(span => (long)(span.To - span.From).TotalSeconds);
        if (workedSeconds > (long)TimeClockRules.TooLong.TotalSeconds) exceptions |= TimeClockException.TooLong;

        // The break: a single span of 5 hours or more has the company's unpaid break taken off, once. When the worker scanned out and back in,
        // that gap was the break and nothing more is taken off.
        int breakMinutes;
        var paidSeconds = workedSeconds;
        if (spans.Count == 1 && workedSeconds >= (long)TimeClockRules.BreakThreshold.TotalSeconds)
        {
            breakMinutes = settings.UnpaidBreakMinutes;
            paidSeconds = Math.Max(0, workedSeconds - breakMinutes * 60L);
        }
        else
        {
            var gapSeconds = 0L;
            for (var index = 1; index < spans.Count; index++) gapSeconds += (long)(spans[index].From - spans[index - 1].To).TotalSeconds;
            breakMinutes = (int)Math.Round(gapSeconds / 60m, MidpointRounding.AwayFromZero);
        }

        var workedMinutes = RoundMinutes(paidSeconds, settings.RoundingMinutes);
        var totalHours = Hours(workedMinutes);
        var regular = Math.Min(totalHours, settings.HoursPerDay);
        var overtime = totalHours - regular;

        var zone = settings.TimeZone;
        var nightSeconds = spans.Sum(span => NightOverlapSeconds(TimeZoneInfo.ConvertTime(span.From, zone).DateTime,
            TimeZoneInfo.ConvertTime(span.To, zone).DateTime));
        var night = Math.Min(Hours(RoundMinutes(nightSeconds, settings.RoundingMinutes)), totalHours);

        return new TimeClockDayResult(firstIn, lastOut, breakMinutes, workedMinutes, regular, overtime, night, exceptions);
    }

    /// <summary>
    /// Rounds worked seconds to the nearest <paramref name="rounding"/> minutes (half away from zero), once, on the total. With rounding off (0)
    /// the time is rounded to the nearest whole minute.
    /// </summary>
    public static int RoundMinutes(long seconds, int rounding)
    {
        var step = Math.Max(rounding, 1) * 60m;
        return (int)(decimal.Round(seconds / step, 0, MidpointRounding.AwayFromZero) * Math.Max(rounding, 1));
    }

    /// <summary>Minutes as hours to two decimals (attendance keeps two).</summary>
    public static decimal Hours(int minutes) => decimal.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>How many seconds of a span (in local wall time) fall between 22:00 and 06:00.</summary>
    private static long NightOverlapSeconds(DateTime from, DateTime to)
    {
        if (to <= from) return 0;
        var total = 0L;
        // Each night runs from 22:00 on one date to 06:00 on the next; start from the night that began the evening before.
        for (var day = from.Date.AddDays(-1); day <= to.Date; day = day.AddDays(1))
        {
            var start = day + NightStart;
            var end = day.AddDays(1) + NightEnd;
            var overlapStart = from > start ? from : start;
            var overlapEnd = to < end ? to : end;
            if (overlapEnd > overlapStart) total += (long)(overlapEnd - overlapStart).TotalSeconds;
        }
        return total;
    }
}
