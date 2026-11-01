using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Domain.Common;
using Cms.Domain.Features.Statutory;

namespace Cms.Application.Features.Statutory;

/// <summary>
/// The contribution and tax tables a company keeps current from the official schedules, and the capability check that tells payroll
/// which agencies can be worked out. Nothing here holds a legal rate: until a table is entered, payroll says so and the person enters the
/// amounts by hand, as before.
/// </summary>
public sealed class StatutoryService(IStatutoryScheduleRepository schedules, IStatutoryReader reader, IOrganizationRepository organizations,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<PagedResult<StatutoryScheduleListItemDto>> ListAsync(StatutoryScheduleListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        await reader.ListAsync(query, await TodayAsync(cancellationToken), page, cancellationToken);

    public async Task<StatutoryScheduleDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ReadAsync(id, await TodayAsync(cancellationToken), cancellationToken);

    /// <summary>
    /// Per agency: Available when a table is in force on the day asked about, otherwise Needs setup with the manual fallback. Never an error.
    /// <paramref name="asOf"/> is the day to judge (a pay run is calculated from the tables in force at the END of its period, so the pages ask
    /// about that day, not today); left out it is today.
    /// </summary>
    public async Task<StatutoryStatusDto> StatusAsync(DateOnly? asOf, CancellationToken cancellationToken)
    {
        var today = asOf ?? await TodayAsync(cancellationToken);
        var inForce = (await reader.InForceAsync(today, cancellationToken)).ToDictionary(item => item.Agency, StringComparer.Ordinal);
        var agencies = StatutoryAgencies.InOrder.Select(agency =>
        {
            var label = StatutoryAgencies.Label(agency);
            var what = agency == StatutoryAgency.WithholdingTax ? "Withholding tax is" : $"{label} contributions are";
            if (inForce.TryGetValue(agency.ToString(), out var table))
                return new StatutoryAgencyStatusDto(agency.ToString(), label, "Available",
                    $"{what} worked out from the table in force since {table.EffectiveFrom.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}.", null,
                    table.Id, table.Name, table.EffectiveFrom);
            return new StatutoryAgencyStatusDto(agency.ToString(), label, "NeedsSetup", NotCalculatedMessage(agency),
                "Enter them as deductions, or ask an accountant to set the table up.", null, null, null);
        }).ToArray();
        return new StatutoryStatusDto(today, agencies);
    }

    /// <summary>"SSS contributions were not calculated because no SSS table is set up."</summary>
    public static string NotCalculatedMessage(StatutoryAgency agency) => agency == StatutoryAgency.WithholdingTax
        ? "Withholding tax was not calculated because no withholding tax table is set up."
        : $"{StatutoryAgencies.Label(agency)} contributions were not calculated because no {StatutoryAgencies.Label(agency)} table is set up.";

    public async Task<StatutoryScheduleDto> CreateAsync(SaveStatutoryScheduleRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await schedules.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return EnsureSameRequest(await ReadAsync(already, today, cancellationToken), request);

        var agency = StatutoryText.Agency(request.Agency) ?? (StatutoryAgency)(-1);
        var schedule = StatutorySchedule.Create(Guid.NewGuid(), currentUser.TenantId, agency, request.Name, request.EffectiveFrom, ParseMethod(request),
            ParseBasis(request), Percentage(request), Rows(request), requestId, clock.UtcNow);
        await EnsureDateIsFreeAsync(schedule, null, cancellationToken);

        schedules.Add(schedule);
        // The table's name is typed by a person, so it stays on the table, not in the audit line.
        audit.Record("Statutory.ScheduleAdded", nameof(StatutorySchedule), schedule.Id,
            $"Added a {StatutoryAgencies.Label(schedule.Agency)} table in force from {Day(schedule.EffectiveFrom)}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (requestId is not null)
        {
            // The same form submitted twice at once: the database let only the first one in. Answer with it; otherwise the failure stands.
            if (await schedules.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original)
                return EnsureSameRequest(await ReadAsync(original, today, cancellationToken), request);
            throw;
        }

        return await ReadAsync(schedule.Id, today, cancellationToken);
    }

    public async Task<StatutoryScheduleDto> UpdateAsync(Guid id, SaveStatutoryScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(schedule.Version, request.Version);
        if (await schedules.IsUsedByApprovedPayrollAsync(id, cancellationToken))
            throw new DomainRuleException("An approved payroll used this table, so it can't be changed. Add a new table with the date the change takes effect.");

        schedule.Update(request.Name, request.EffectiveFrom, ParseMethod(request), ParseBasis(request), Percentage(request), Rows(request), clock.UtcNow);
        await EnsureDateIsFreeAsync(schedule, schedule.Id, cancellationToken);
        audit.Record("Statutory.ScheduleChanged", nameof(StatutorySchedule), schedule.Id,
            $"Changed the {StatutoryAgencies.Label(schedule.Agency)} table in force from {Day(schedule.EffectiveFrom)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(schedule.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    /// <summary>Stops a table applying to payrolls calculated from now on. Payrolls already calculated keep their amounts.</summary>
    public async Task<StatutoryScheduleDto> RetireAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var schedule = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(schedule.Version, request.Version);
        schedule.Retire(currentUser.UserId, clock.UtcNow);
        audit.Record("Statutory.ScheduleRetired", nameof(StatutorySchedule), schedule.Id,
            $"Retired the {StatutoryAgencies.Label(schedule.Agency)} table in force from {Day(schedule.EffectiveFrom)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(schedule.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    private async Task EnsureDateIsFreeAsync(StatutorySchedule schedule, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await schedules.EffectiveDateTakenAsync(schedule.Agency, schedule.EffectiveFrom, excludeId, cancellationToken))
            throw new ValidationFailedException("effectiveFrom",
                $"Another {StatutoryAgencies.Label(schedule.Agency)} table already takes effect on {Day(schedule.EffectiveFrom)}. Change that table, retire it, or choose another date.");
    }

    private static StatutoryScheduleDto EnsureSameRequest(StatutoryScheduleDto original, SaveStatutoryScheduleRequest request) =>
        string.Equals(original.Agency, StatutoryText.Agency(request.Agency)?.ToString(), StringComparison.Ordinal) && original.EffectiveFrom == request.EffectiveFrom
            ? original
            : throw new DomainRuleException("That request id was already used for a different table. Reload the page and try again.");

    private static StatutoryMethod ParseMethod(SaveStatutoryScheduleRequest request) =>
        StatutoryText.Parse<StatutoryMethod>(request.Method) ?? (StatutoryMethod)(-1);

    private static StatutoryPeriodBasis ParseBasis(SaveStatutoryScheduleRequest request) =>
        StatutoryText.Parse<StatutoryPeriodBasis>(request.PeriodBasis) ?? (StatutoryPeriodBasis)(-1);

    private static StatutoryPercentageInput? Percentage(SaveStatutoryScheduleRequest request) =>
        request.EmployeeRateBps is null && request.EmployerRateBps is null
            ? null
            : new StatutoryPercentageInput(request.EmployeeRateBps ?? 0, request.EmployerRateBps ?? 0, request.BaseFloorMinor ?? 0, request.BaseCeilingMinor,
                request.EmployeeMinimumMinor, request.EmployeeMaximumMinor, request.EmployerMinimumMinor, request.EmployerMaximumMinor);

    private static List<StatutoryRowInput>? Rows(SaveStatutoryScheduleRequest request) =>
        request.Rows?.Select(row => new StatutoryRowInput(row.FromMinor, row.ToMinor, row.EmployeeMinor, row.EmployerMinor, row.BaseTaxMinor, row.RateBps))
            .ToList();

    private async Task<StatutorySchedule> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await schedules.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that table");

    private async Task<StatutoryScheduleDto> ReadAsync(Guid id, DateOnly today, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, today, cancellationToken) ?? throw new NotFoundException("that table");

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
