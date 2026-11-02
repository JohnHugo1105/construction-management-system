using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Domain.Features.Organizations;

/// <summary>Which amounts the dashboard and variance reports compare.</summary>
public enum VarianceBasis
{
    /// <summary>Budget and actual cost including VAT (what the retired desktop app showed).</summary>
    IncludingVat = 0,

    /// <summary>Budget and actual cost before VAT, for contractors who recover the VAT they pay.</summary>
    ExcludingVat = 1
}

/// <summary>
/// The company that owns the data. Its id is the tenant id stamped on every record. Settings here replace values the
/// retired desktop app hard-coded (12% VAT, a fixed 8-hour day).
/// </summary>
public sealed class Organization : AggregateRoot
{
    public const string DefaultCurrency = "PHP";
    public const int DefaultVatRateBps = 1_200;
    public const string DefaultTimeZoneId = "Asia/Manila";

    private Organization() { }

    public string Name { get; private set; } = "";
    public string CurrencyCode { get; private set; } = DefaultCurrency;

    /// <summary>The VAT rate in basis points (1,200 is 12%). Each cost line keeps the rate it was entered with.</summary>
    public int VatRateBps { get; private set; } = DefaultVatRateBps;

    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    /// <summary>Paid hours in a working day. A daily rate is divided by this to get the hourly rate.</summary>
    public decimal HoursPerDay { get; private set; } = 8m;

    /// <summary>Overtime pay as a multiple of the hourly rate, in basis points (12,500 is 125%).</summary>
    public int OvertimeMultiplierBps { get; private set; } = 12_500;

    public VarianceBasis VarianceBasis { get; private set; } = VarianceBasis.IncludingVat;

    public const int DefaultPayrollRunsPerMonth = 2;

    /// <summary>
    /// How many pay runs the company makes in a month (1 to 4). A monthly contribution or tax table is applied to one run's pay times
    /// this number, and the month's amount is split across the runs.
    /// </summary>
    public int PayrollRunsPerMonth { get; private set; } = DefaultPayrollRunsPerMonth;

    /// <summary>When a work day starts for the time clock, in minutes after midnight (240 is 04:00): a scan before it belongs to the previous date.</summary>
    public int TimeClockDayStartMinutes { get; private set; } = TimeClockRules.DefaultDayStartMinutes;

    /// <summary>The unpaid break taken off a single time-clock span of 5 hours or more, in minutes (0 turns it off).</summary>
    public int UnpaidBreakMinutes { get; private set; } = TimeClockRules.DefaultUnpaidBreakMinutes;

    /// <summary>A time-clock day's worked minutes are rounded to the nearest this many minutes, once, on the total (0 turns it off).</summary>
    public int PunchRoundingMinutes { get; private set; } = TimeClockRules.DefaultRoundingMinutes;

    /// <summary>The settings a day of time-clock scans is worked out with.</summary>
    public TimeClockSettings TimeClock => new(TimeZoneId, TimeClockDayStartMinutes, UnpaidBreakMinutes, PunchRoundingMinutes, HoursPerDay);

    public static Organization Create(Guid id, string name, DateTimeOffset now)
    {
        var organization = new Organization { Name = Guard.RequiredText(name, "name", "the company name", 120) };
        organization.Initialize(id, id, now);
        return organization;
    }

    public void UpdateSettings(string name, string currencyCode, int vatRateBps, string timeZoneId, decimal hoursPerDay,
        int overtimeMultiplierBps, VarianceBasis varianceBasis, DateTimeOffset now, int? payrollRunsPerMonth = null, int? timeClockDayStartMinutes = null,
        int? unpaidBreakMinutes = null, int? punchRoundingMinutes = null)
    {
        var runsPerMonth = payrollRunsPerMonth ?? PayrollRunsPerMonth;
        // Left out, a time clock setting stays as it is, so a client that doesn't know about it never resets it.
        var dayStart = timeClockDayStartMinutes ?? TimeClockDayStartMinutes;
        var breakMinutes = unpaidBreakMinutes ?? UnpaidBreakMinutes;
        var rounding = punchRoundingMinutes ?? PunchRoundingMinutes;
        var errors = new ValidationErrors();
        var cleanName = (name ?? "").Trim();
        if (cleanName.Length is 0 or > 120) errors.Add("name", "Enter the company name (up to 120 characters).");
        var currency = (currencyCode ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)) errors.Add("currencyCode", "Use a 3-letter currency code such as PHP.");
        if (vatRateBps is < 0 or > LineAmounts.MaximumVatRateBps) errors.Add("vatRateBps", "Enter a VAT rate between 0% and 100%.");
        if (hoursPerDay is < 1m or > 24m || decimal.Round(hoursPerDay, 2) != hoursPerDay) errors.Add("hoursPerDay", "Enter paid hours per day between 1 and 24.");
        if (overtimeMultiplierBps is < 10_000 or > 30_000) errors.Add("overtimeMultiplierBps", "Enter an overtime rate between 100% and 300% of the hourly rate.");
        if (!Enum.IsDefined(varianceBasis)) errors.Add("varianceBasis", "Choose whether to compare amounts with or without VAT.");
        if (!IsKnownTimeZone(timeZoneId)) errors.Add("timeZoneId", "Choose a valid time zone.");
        if (runsPerMonth is < 1 or > 4) errors.Add("payrollRunsPerMonth", "Enter how many pay runs you make in a month, from 1 to 4.");
        if (dayStart is < 0 or > TimeClockRules.MaximumDayStartMinutes)
            errors.Add("timeClockDayStartMinutes", "Choose a day start between midnight and noon.");
        if (breakMinutes is < 0 or > TimeClockRules.MaximumUnpaidBreakMinutes)
            errors.Add("unpaidBreakMinutes", "Enter an unpaid break from 0 to 180 minutes. Use 0 to take no break off.");
        if (rounding is < 0 or > TimeClockRules.MaximumRoundingMinutes)
            errors.Add("punchRoundingMinutes", "Enter rounding from 0 to 60 minutes. Use 0 to keep the exact minutes.");
        errors.ThrowIfAny();

        Name = cleanName;
        CurrencyCode = currency;
        VatRateBps = vatRateBps;
        TimeZoneId = timeZoneId.Trim();
        HoursPerDay = hoursPerDay;
        OvertimeMultiplierBps = overtimeMultiplierBps;
        VarianceBasis = varianceBasis;
        PayrollRunsPerMonth = runsPerMonth;
        TimeClockDayStartMinutes = dayStart;
        UnpaidBreakMinutes = breakMinutes;
        PunchRoundingMinutes = rounding;
        Touch(now);
    }

    /// <summary>Today's calendar date in the organization's business time zone (never the UTC day).</summary>
    public DateOnly TodayLocal(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);

    private static bool IsKnownTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
