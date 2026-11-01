using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Payroll;

/// <summary>
/// The premium factors of a company, in basis points of the ordinary hourly rate (10,000 = 100%). A day factor of 13,000 pays the hours
/// on that day at 130%. Overtime on a premium day is paid at the day's factor times (10,000 + <see cref="PremiumDayOvertimeExtraBps"/>);
/// the night differential is <see cref="NightDifferentialBps"/> of the day's regular-hour rate. Overtime on an ordinary day uses the
/// company's overtime multiple, which lives in the company settings (one answer in one place).
/// </summary>
public sealed record PayFactors(int RestDayBps, int SpecialDayBps, int SpecialRestDayBps, int RegularHolidayBps, int RegularHolidayRestDayBps,
    int PremiumDayOvertimeExtraBps, int NightDifferentialBps)
{
    public const int OrdinaryBps = 10_000;

    /// <summary>What applies while the company has not saved its own rules.</summary>
    public static PayFactors Defaults { get; } = new(13_000, 13_000, 15_000, 20_000, 26_000, 3_000, 1_000);

    /// <summary>The factor of the regular hours on a day of this type (10,000 on an ordinary day).</summary>
    public int DayFactorBps(DayType type) => type switch
    {
        DayType.RestDay => RestDayBps,
        DayType.Special => SpecialDayBps,
        DayType.SpecialRestDay => SpecialRestDayBps,
        DayType.RegularHoliday => RegularHolidayBps,
        DayType.RegularHolidayRestDay => RegularHolidayRestDayBps,
        _ => OrdinaryBps
    };
}

/// <summary>
/// The company's pay rules: the premium factors for rest days and holidays, the extra on overtime on those days, and the night
/// differential. One per company, created the first time someone saves them; until then <see cref="PayFactors.Defaults"/> apply.
/// A change applies to payrolls worked out (or recalculated) afterwards; an approved payroll keeps its amounts.
/// </summary>
public sealed class PayRules : AggregateRoot
{
    public const int MinimumDayFactorBps = 10_000;
    public const int MaximumDayFactorBps = 50_000;
    public const int MaximumExtraBps = 10_000;

    private PayRules() { }

    public int RestDayBps { get; private set; }
    public int SpecialDayBps { get; private set; }
    public int SpecialRestDayBps { get; private set; }
    public int RegularHolidayBps { get; private set; }
    public int RegularHolidayRestDayBps { get; private set; }
    public int PremiumDayOvertimeExtraBps { get; private set; }
    public int NightDifferentialBps { get; private set; }

    public PayFactors Factors => new(RestDayBps, SpecialDayBps, SpecialRestDayBps, RegularHolidayBps, RegularHolidayRestDayBps,
        PremiumDayOvertimeExtraBps, NightDifferentialBps);

    public static PayRules Create(Guid id, Guid tenantId, PayFactors factors, DateTimeOffset now)
    {
        Validate(factors);
        var rules = new PayRules();
        rules.Apply(factors);
        rules.Initialize(id, tenantId, now);
        return rules;
    }

    public void Change(PayFactors factors, DateTimeOffset now)
    {
        Validate(factors);
        Apply(factors);
        Touch(now);
    }

    private void Apply(PayFactors factors)
    {
        RestDayBps = factors.RestDayBps;
        SpecialDayBps = factors.SpecialDayBps;
        SpecialRestDayBps = factors.SpecialRestDayBps;
        RegularHolidayBps = factors.RegularHolidayBps;
        RegularHolidayRestDayBps = factors.RegularHolidayRestDayBps;
        PremiumDayOvertimeExtraBps = factors.PremiumDayOvertimeExtraBps;
        NightDifferentialBps = factors.NightDifferentialBps;
    }

    private static void Validate(PayFactors factors)
    {
        var errors = new ValidationErrors();
        DayFactor(errors, factors.RestDayBps, "restDayBps", "rest day");
        DayFactor(errors, factors.SpecialDayBps, "specialDayBps", "special day");
        DayFactor(errors, factors.SpecialRestDayBps, "specialRestDayBps", "special day on a rest day");
        DayFactor(errors, factors.RegularHolidayBps, "regularHolidayBps", "regular holiday");
        DayFactor(errors, factors.RegularHolidayRestDayBps, "regularHolidayRestDayBps", "regular holiday on a rest day");
        if (factors.PremiumDayOvertimeExtraBps is < 0 or > MaximumExtraBps)
            errors.Add("premiumDayOvertimeExtraBps", "Enter the overtime extra on a premium day between 0% and 100%.");
        if (factors.NightDifferentialBps is < 0 or > MaximumExtraBps)
            errors.Add("nightDifferentialBps", "Enter the night differential between 0% and 100%.");
        errors.ThrowIfAny();
    }

    private static void DayFactor(ValidationErrors errors, int value, string field, string label)
    {
        if (value is < MinimumDayFactorBps or > MaximumDayFactorBps)
            errors.Add(field, $"Enter the pay for a {label} between 100% and 500% of the ordinary rate.");
    }
}
