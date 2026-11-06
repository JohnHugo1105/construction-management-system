using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Payroll;

/// <summary>
/// A holiday on the company's calendar: a date, a name and whether it is a regular holiday or a special (non-working) day. One per date.
/// No real holiday is built in: holidays are proclaimed each year, so the people who run payroll enter them. A holiday a non-void
/// approved payroll covers can't be changed or removed (the service checks), so an approved payroll stays explainable.
/// </summary>
public sealed class CompanyHoliday : AggregateRoot
{
    public const int MaximumNameLength = 80;

    private CompanyHoliday() { }

    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = "";
    public HolidayType Type { get; private set; }

    public static CompanyHoliday Create(Guid id, Guid tenantId, DateOnly date, string? name, HolidayType type, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        if (date == default) errors.Add("date", "Choose the date of the holiday.");
        var cleanName = CleanName(errors, name);
        if (!Enum.IsDefined(type)) errors.Add("type", "Choose regular holiday or special day.");
        errors.ThrowIfAny();

        var holiday = new CompanyHoliday { Date = date, Name = cleanName, Type = type };
        holiday.Initialize(id, tenantId, now);
        return holiday;
    }

    public void Change(string? name, HolidayType type, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        var cleanName = CleanName(errors, name);
        if (!Enum.IsDefined(type)) errors.Add("type", "Choose regular holiday or special day.");
        errors.ThrowIfAny();
        Name = cleanName;
        Type = type;
        Touch(now);
    }

    private static string CleanName(ValidationErrors errors, string? name)
    {
        var text = name?.Trim() ?? "";
        if (text.Length == 0) errors.Add("name", "Enter the name of the holiday.");
        else if (text.Length > MaximumNameLength) errors.Add("name", $"The name can be at most {MaximumNameLength} characters.");
        return text;
    }
}
