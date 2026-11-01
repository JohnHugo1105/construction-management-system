using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Domain.Common;
using Cms.Domain.Features.Payroll;

namespace Cms.Application.Features.Payroll;

/// <summary>
/// The company's pay rules (premium factors, the overtime extra on premium days, the night differential) and its holiday calendar. The
/// people who run payroll keep them (payrollrules.manage); everyone who reads payroll reads them. A change applies to payrolls worked out
/// or recalculated afterwards; an approved payroll keeps its amounts, so a holiday an approved payroll covers can't be changed or removed.
/// No real holiday is built in. Audit lines carry the factors as percentages and the holiday's date and name, never a pay figure.
/// </summary>
public sealed class PayRulesService(IPayRulesRepository repository, IPayRulesReader reader, IOrganizationRepository organizations,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<PayRulesDto> GetRulesAsync(CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var rules = await repository.GetRulesAsync(cancellationToken);
        return ToDto(rules?.Factors ?? PayFactors.Defaults, organization.OvertimeMultiplierBps, rules is null, SeenVersion(rules));
    }

    /// <summary>
    /// The version a person sees: 0 while the defaults apply, and the stored version plus one once rules are saved (a new record starts at
    /// 0), so someone who opened the defaults can't overwrite rules another person saved in the meantime.
    /// </summary>
    private static int SeenVersion(PayRules? rules) => rules is null ? 0 : rules.Version + 1;

    /// <summary>Saves the pay rules (created the first time). They apply to payrolls worked out or recalculated from now on.</summary>
    public async Task<PayRulesDto> SaveRulesAsync(SavePayRulesRequest request, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var factors = new PayFactors(request.RestDayBps, request.SpecialDayBps, request.SpecialRestDayBps, request.RegularHolidayBps,
            request.RegularHolidayRestDayBps, request.PremiumDayOvertimeExtraBps, request.NightDifferentialBps);
        var rules = await repository.GetRulesAsync(cancellationToken);
        ConcurrencyGuard.Ensure(SeenVersion(rules), request.Version);
        var created = rules is null;
        if (rules is null)
        {
            rules = PayRules.Create(Guid.NewGuid(), currentUser.TenantId, factors, clock.UtcNow);
            repository.AddRules(rules);
        }
        else
        {
            rules.Change(factors, clock.UtcNow);
        }

        audit.Record("PayRules.Changed", nameof(PayRules), rules.Id,
            $"Changed the pay rules: rest day {Percent(factors.RestDayBps)}, special day {Percent(factors.SpecialDayBps)}, special day on a rest day " +
            $"{Percent(factors.SpecialRestDayBps)}, regular holiday {Percent(factors.RegularHolidayBps)}, regular holiday on a rest day " +
            $"{Percent(factors.RegularHolidayRestDayBps)}, overtime extra on those days {Percent(factors.PremiumDayOvertimeExtraBps)}, night differential " +
            $"{Percent(factors.NightDifferentialBps)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(rules.Factors, organization.OvertimeMultiplierBps, false, created ? 1 : SeenVersion(rules));
    }

    public async Task<HolidayCalendarDto> ListHolidaysAsync(int? year, CancellationToken cancellationToken)
    {
        var chosen = year ?? (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow).Year;
        if (chosen is < 1900 or > 9998) throw new ValidationFailedException("year", "Choose a year.");
        return new HolidayCalendarDto(chosen, await reader.HolidaysAsync(chosen, cancellationToken));
    }

    public async Task<CompanyHolidayDto> AddHolidayAsync(AddHolidayRequest request, CancellationToken cancellationToken)
    {
        var holiday = CompanyHoliday.Create(Guid.NewGuid(), currentUser.TenantId, request.Date, request.Name, ParseType(request.Type), clock.UtcNow);
        if (await repository.HolidayExistsOnAsync(holiday.Date, cancellationToken))
            throw new ValidationFailedException("date", $"{Format(holiday.Date)} already has a holiday. Open it to change it.");
        // A holiday added on a day an approved payroll paid as an ordinary day would make that payroll disagree with the calendar.
        await EnsureNoApprovedPayrollAsync(holiday.Date, "adding a holiday on this date", cancellationToken);

        repository.AddHoliday(holiday);
        audit.Record("PayRules.HolidayAdded", nameof(CompanyHoliday), holiday.Id,
            $"Added {DayTypes.Label(holiday.Type).ToLowerInvariant()} {holiday.Name} on {Format(holiday.Date)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(holiday, cancellationToken);
    }

    /// <summary>
    /// Renames a holiday or changes its type. Changing the type of a holiday an approved payroll covers is refused (the payroll was paid
    /// for it); renaming it changes no pay and is allowed.
    /// </summary>
    public async Task<CompanyHolidayDto> ChangeHolidayAsync(Guid id, ChangeHolidayRequest request, CancellationToken cancellationToken)
    {
        var holiday = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(holiday.Version, request.Version);
        var type = ParseType(request.Type);
        if (type != holiday.Type) await EnsureNoApprovedPayrollAsync(holiday.Date, "changing this holiday", cancellationToken);
        holiday.Change(request.Name, type, clock.UtcNow);

        audit.Record("PayRules.HolidayChanged", nameof(CompanyHoliday), holiday.Id,
            $"Changed the holiday on {Format(holiday.Date)} to {DayTypes.Label(holiday.Type).ToLowerInvariant()} {holiday.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(holiday, cancellationToken);
    }

    /// <summary>Removes a holiday from the calendar (it is a calendar entry, not a financial record). Refused while an approved payroll covers it.</summary>
    public async Task RemoveHolidayAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var holiday = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(holiday.Version, request.Version);
        await EnsureNoApprovedPayrollAsync(holiday.Date, "removing this holiday", cancellationToken);

        repository.RemoveHoliday(holiday);
        audit.Record("PayRules.HolidayRemoved", nameof(CompanyHoliday), holiday.Id, $"Removed the holiday {holiday.Name} on {Format(holiday.Date)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNoApprovedPayrollAsync(DateOnly date, string action, CancellationToken cancellationToken)
    {
        if (await repository.ApprovedPayrollCoversAsync(date, cancellationToken))
            throw new DomainRuleException(action == "adding a holiday on this date"
                ? "A payroll that covers this date was approved. Void it before adding a holiday on this date."
                : "A payroll that covers this date was approved. Void it before changing this holiday.");
    }

    private async Task<CompanyHoliday> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetHolidayAsync(id, cancellationToken) ?? throw new NotFoundException("that holiday");

    private async Task<CompanyHolidayDto> ReadAsync(CompanyHoliday holiday, CancellationToken cancellationToken) =>
        (await reader.HolidaysAsync(holiday.Date.Year, cancellationToken)).Single(item => item.Id == holiday.Id);

    /// <summary>Text that names no type becomes an undefined value, so the domain reports it with the other problems.</summary>
    private static HolidayType ParseType(string? text)
    {
        var name = text?.Trim();
        foreach (var candidate in Enum.GetValues<HolidayType>())
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase)) return candidate;
        return (HolidayType)(-1);
    }

    private static PayRulesDto ToDto(PayFactors factors, int overtimeMultiplierBps, bool isDefault, int version) =>
        new(factors.RestDayBps, factors.SpecialDayBps, factors.SpecialRestDayBps, factors.RegularHolidayBps, factors.RegularHolidayRestDayBps,
            factors.PremiumDayOvertimeExtraBps, factors.NightDifferentialBps, overtimeMultiplierBps, isDefault, version);

    private static string Percent(int bps) => (bps / 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
