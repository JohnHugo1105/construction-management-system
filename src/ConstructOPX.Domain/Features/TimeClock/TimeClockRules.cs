namespace ConstructOPX.Domain.Features.TimeClock;

/// <summary>
/// The company settings a day of scans is worked out with, taken from the company at the time. <see cref="DayStartMinutes"/> is minutes after
/// midnight (240 is 04:00): a scan before it belongs to the previous date, so a night shift that ends at 2 am is one day.
/// </summary>
public sealed record TimeClockSettings(string TimeZoneId, int DayStartMinutes, int UnpaidBreakMinutes, int RoundingMinutes, decimal HoursPerDay)
{
    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}

/// <summary>Why a scan sent by a machine was refused. The wording a person sees is in <see cref="TimeClockRules.Explain"/>.</summary>
public enum PunchRejection
{
    /// <summary>An unknown code, a replaced (reissued) badge, or a worker who is not active. The three answer alike on purpose.</summary>
    BadgeNotActive,

    /// <summary>The machine's time is more than 5 minutes ahead of the server, or more than 7 days behind.</summary>
    ClockWrong,

    /// <summary>The scan itself could not be read (no id, an id that is too long, a direction that is not In, Out or Auto).</summary>
    Unreadable,

    /// <summary>The time clock's project is archived.</summary>
    ProjectClosed
}

/// <summary>The fixed rules of the time clock (company-specific figures come from <see cref="TimeClockSettings"/>).</summary>
public static class TimeClockRules
{
    /// <summary>A second scan by the same worker on the same machine within this time of the last is "already recorded".</summary>
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

    /// <summary>A scan stamped further ahead of the server's clock than this is refused (the machine's clock is wrong).</summary>
    public static readonly TimeSpan MaximumAhead = TimeSpan.FromMinutes(5);

    /// <summary>A scan older than this is refused: a machine that was offline sends its queue within a week.</summary>
    public static readonly TimeSpan MaximumBehind = TimeSpan.FromDays(7);

    /// <summary>A single In-to-Out span at least this long has the unpaid break taken off.</summary>
    public static readonly TimeSpan BreakThreshold = TimeSpan.FromHours(5);

    /// <summary>More worked time than this in one day is flagged for review.</summary>
    public static readonly TimeSpan TooLong = TimeSpan.FromHours(16);

    public const int MaximumBatch = 200;
    public const int MaximumImportRows = 5_000;
    public const int MaximumClientPunchIdLength = 80;

    /// <summary>The company default for the start of a work day: 04:00.</summary>
    public const int DefaultDayStartMinutes = 240;

    public const int DefaultUnpaidBreakMinutes = 60;
    public const int DefaultRoundingMinutes = 15;

    public const int MaximumDayStartMinutes = 720;
    public const int MaximumUnpaidBreakMinutes = 180;
    public const int MaximumRoundingMinutes = 60;

    /// <summary>
    /// The work date of a scan: its date in the company's time zone, except that a scan before the day-start belongs to the previous date.
    /// </summary>
    public static DateOnly WorkDateOf(DateTimeOffset at, TimeClockSettings settings)
    {
        var local = TimeZoneInfo.ConvertTime(at, settings.TimeZone).DateTime;
        var date = DateOnly.FromDateTime(local);
        return local.TimeOfDay < TimeSpan.FromMinutes(settings.DayStartMinutes) ? date.AddDays(-1) : date;
    }

    /// <summary>Null when the machine's time is believable; otherwise why the scan is refused.</summary>
    public static PunchRejection? CheckClock(DateTimeOffset punchedAt, DateTimeOffset now) =>
        punchedAt - now > MaximumAhead || now - punchedAt > MaximumBehind ? PunchRejection.ClockWrong : null;

    /// <summary>What a person at the machine (or reading an import's result) is told. The same words for every kind of inactive badge.</summary>
    public static string Explain(PunchRejection rejection) => rejection switch
    {
        PunchRejection.BadgeNotActive => "This badge isn't active. See your supervisor.",
        PunchRejection.ClockWrong => "The time on this machine is wrong, so the scan wasn't kept. Ask your supervisor to set the clock.",
        PunchRejection.ProjectClosed => "This time clock's project is archived. Ask your administrator to move the time clock.",
        _ => "This scan couldn't be read. Scan again."
    };

    /// <summary>The direction a scan is taken as: an explicit In or Out stays; an automatic one is the opposite of the worker's last scan that day.</summary>
    public static PunchDirection Resolve(PunchDirection? requested, PunchDirection? lastThatDay) =>
        requested ?? (lastThatDay == PunchDirection.In ? PunchDirection.Out : PunchDirection.In);
}
