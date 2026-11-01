namespace Cms.Domain.Features.Payroll;

/// <summary>A holiday's kind, as proclaimed: a regular holiday (paid even when not worked) or a special (non-working) day.</summary>
public enum HolidayType
{
    Regular = 0,
    Special = 1
}

/// <summary>What kind of day a work date is for one worker. It decides the premium the hours on it earn.</summary>
public enum DayType
{
    Ordinary = 0,
    RestDay = 1,
    Special = 2,
    SpecialRestDay = 3,
    RegularHoliday = 4,
    RegularHolidayRestDay = 5
}

public static class DayTypes
{
    /// <summary>
    /// The day type of a date for a worker: from the company's holiday on that date (if any) and the worker's rest day (if one is set).
    /// A worker without a rest day has none, so an ordinary weekday or weekend day is ordinary for them.
    /// </summary>
    public static DayType Of(DateOnly date, HolidayType? holiday, DayOfWeek? restDay)
    {
        var isRestDay = restDay is { } rest && date.DayOfWeek == rest;
        return holiday switch
        {
            HolidayType.Regular => isRestDay ? DayType.RegularHolidayRestDay : DayType.RegularHoliday,
            HolidayType.Special => isRestDay ? DayType.SpecialRestDay : DayType.Special,
            _ => isRestDay ? DayType.RestDay : DayType.Ordinary
        };
    }

    /// <summary>The words people use for the day type.</summary>
    public static string Label(DayType type) => type switch
    {
        DayType.RestDay => "Rest day",
        DayType.Special => "Special day",
        DayType.SpecialRestDay => "Special day on a rest day",
        DayType.RegularHoliday => "Regular holiday",
        DayType.RegularHolidayRestDay => "Regular holiday on a rest day",
        _ => "Ordinary day"
    };

    public static string Label(HolidayType type) => type == HolidayType.Regular ? "Regular holiday" : "Special day";
}
