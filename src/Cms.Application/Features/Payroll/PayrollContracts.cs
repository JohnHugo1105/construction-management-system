using Cms.Application.Common;
using Cms.Domain.Features.Payroll;

namespace Cms.Application.Features.Payroll;

public interface IPayrollRepository
{
    /// <summary>Loads a run with its lines, deductions and project shares, tracked for change. Null when it isn't in the organization.</summary>
    Task<PayrollRun?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The run a form submission already created, so a double click returns it instead of reserving attendance twice.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// The regular holidays among <paramref name="dates"/> that workers may be paid for without working them: for each date, every
    /// posting (worker, project, sub-project) covering it, without the workers who have attendance anywhere that day and without the
    /// holidays another standing payroll (not <paramref name="excludingRunId"/>) already paid. A fixed number of statements.
    /// </summary>
    Task<IReadOnlyList<UnworkedHolidayFact>> ListUnworkedHolidayCandidatesAsync(IReadOnlyCollection<DateOnly> dates, Guid? excludingRunId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Each worker's basic pay in <paramref name="year"/> by project, from the approved regular payrolls whose period ends in that year,
    /// without the workers already in a standing 13th-month payroll for the year (other than <paramref name="excludingRunId"/>). Workers in
    /// name order. A fixed number of statements.
    /// </summary>
    Task<IReadOnlyList<ThirteenthMonthBasis>> ThirteenthMonthBasisAsync(int year, Guid? excludingRunId, CancellationToken cancellationToken);

    /// <summary>
    /// The workers of payroll <paramref name="runId"/> who are already in a standing (not voided) 13th-month payroll for
    /// <paramref name="year"/>, each saying whether that 13th-month payroll is approved. One statement however many workers.
    /// </summary>
    Task<IReadOnlyList<ThirteenthMonthClaim>> ThirteenthMonthClaimsAsync(Guid runId, int year, CancellationToken cancellationToken);

    /// <summary>
    /// The archived projects, and the sub-projects archived on their own, among <paramref name="places"/>, as names people read ("Tower A",
    /// "the Phase 2 sub-project of Tower A"), in name order. One statement.
    /// </summary>
    Task<IReadOnlyList<string>> ArchivedPlacesAsync(IReadOnlyCollection<(Guid ProjectId, Guid? SubProjectId)> places, CancellationToken cancellationToken);

    void Add(PayrollRun run);
}

/// <summary>The company's pay rules and holiday calendar, tracked for change.</summary>
public interface IPayRulesRepository
{
    /// <summary>The company's saved pay rules, or null while the defaults apply.</summary>
    Task<PayRules?> GetRulesAsync(CancellationToken cancellationToken);

    void AddRules(PayRules rules);

    Task<CompanyHoliday?> GetHolidayAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HolidayExistsOnAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>The holidays from <paramref name="from"/> to <paramref name="to"/> (both included), one statement.</summary>
    Task<IReadOnlyDictionary<DateOnly, HolidayType>> HolidaysBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Whether an approved (not voided) payroll for a pay period includes the date.</summary>
    Task<bool> ApprovedPayrollCoversAsync(DateOnly date, CancellationToken cancellationToken);

    void AddHoliday(CompanyHoliday holiday);

    void RemoveHoliday(CompanyHoliday holiday);
}

public interface IPayRulesReader
{
    /// <summary>A year's holidays in date order, each saying whether an approved payroll covers it. One statement.</summary>
    Task<IReadOnlyList<CompanyHolidayDto>> HolidaysAsync(int year, CancellationToken cancellationToken);
}

/// <param name="OvertimeMultiplierBps">The ordinary-day overtime multiple, kept in the company settings (shown here, changed there).</param>
/// <param name="IsDefault">True while the company has not saved its own rules; <c>Version</c> is then 0.</param>
public sealed record PayRulesDto(int RestDayBps, int SpecialDayBps, int SpecialRestDayBps, int RegularHolidayBps, int RegularHolidayRestDayBps,
    int PremiumDayOvertimeExtraBps, int NightDifferentialBps, int OvertimeMultiplierBps, bool IsDefault, int Version);

public sealed record SavePayRulesRequest(int RestDayBps, int SpecialDayBps, int SpecialRestDayBps, int RegularHolidayBps, int RegularHolidayRestDayBps,
    int PremiumDayOvertimeExtraBps, int NightDifferentialBps, int Version);

/// <param name="Type">Regular or Special.</param>
/// <param name="CoveredByApprovedPayroll">An approved payroll covers the date, so the holiday can't be changed or removed until it is voided.</param>
public sealed record CompanyHolidayDto(Guid Id, DateOnly Date, string Name, string Type, bool CoveredByApprovedPayroll, int Version);

public sealed record HolidayCalendarDto(int Year, IReadOnlyList<CompanyHolidayDto> Holidays);

public sealed record AddHolidayRequest(DateOnly Date, string? Name, string? Type);

public sealed record ChangeHolidayRequest(string? Name, string? Type, int Version);

/// <summary>A worker already in a standing 13th-month payroll for a year, and whether that payroll is approved.</summary>
public sealed record ThirteenthMonthClaim(Guid WorkerId, bool Approved);

public interface IPayrollReader
{
    Task<PagedResult<PayrollRunListItemDto>> ListAsync(PayrollRunListQuery query, PageRequest page, CancellationToken cancellationToken);

    Task<PayrollRunDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PayrollRunListQuery(string? Status);

/// <param name="Kind">Regular (a pay period) or ThirteenthMonth (<c>Year</c> says which).</param>
public sealed record PayrollRunListItemDto(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int WorkerCount, long TotalGrossMinor,
    long TotalDeductionsMinor, long TotalNetMinor, DateTimeOffset CreatedAtUtc, DateTimeOffset? ApprovedAtUtc, int Version, string Kind = "Regular",
    int? Year = null);

/// <summary>
/// One deduction. <c>Source</c> is Manual (typed by a person) or Statutory (worked out from <c>Agency</c>'s table). For a statutory one,
/// <c>CalculatedMinor</c> is what the table gave, <c>IsAdjusted</c> says a person changed it (with <c>AdjustmentReason</c>), <c>IsLimited</c>
/// says it was cut because the deductions would have been more than the pay, and <c>EmployerShareMinor</c> is what the employer adds
/// (not taken from pay, not charged to a project).
/// </summary>
public sealed record PayrollDeductionDto(Guid Id, string Description, long AmountMinor, string Source = "Manual", string? Agency = null,
    long EmployerShareMinor = 0, long? CalculatedMinor = null, bool IsAdjusted = false, bool IsLimited = false, string? AdjustmentReason = null);

/// <summary>The part of one worker's pay that belongs to one project (and sub-project). The shares add up to the line's gross pay.</summary>
public sealed record PayrollAllocationDto(Guid ProjectId, string ProjectName, Guid? SubProjectId, string? SubProjectName, decimal RegularHours,
    decimal OvertimeHours, long GrossMinor);

/// <summary>
/// One worker's pay. The gross is the sum of <c>RegularPayMinor</c> (the plain-rate pay for the regular hours), <c>PremiumPayMinor</c> (what
/// rest days and holidays add to them), <c>OvertimePayMinor</c> (including the premium on overtime on those days), <c>NightPayMinor</c> (the
/// night differential), <c>HolidayPayMinor</c> (regular holidays paid without being worked, on <c>HolidayDates</c>) and
/// <c>ThirteenthMonthPayMinor</c> (a 13th-month payroll only, a twelfth of <c>ThirteenthMonthBasisMinor</c>).
/// </summary>
public sealed record PayrollLineDto(Guid WorkerId, string WorkerName, string EmployeeNumber, string Designation, decimal RegularHours,
    decimal OvertimeHours, long RegularPayMinor, long OvertimePayMinor, long GrossPayMinor, long DeductionsMinor, long NetPayMinor,
    IReadOnlyList<PayrollDeductionDto> Deductions, IReadOnlyList<PayrollAllocationDto> Allocations, long EmployerSharesMinor = 0,
    bool StatutoryLimited = false, decimal NightHours = 0m, long PremiumPayMinor = 0, long NightPayMinor = 0, long HolidayPayMinor = 0,
    long ThirteenthMonthPayMinor = 0, long ThirteenthMonthBasisMinor = 0, IReadOnlyList<DateOnly>? HolidayDates = null);

/// <param name="RunsPerMonth">The company's pay runs per month when the contributions and tax were last worked out.</param>
/// <param name="TotalEmployerSharesMinor">What the employer adds to the contributions. Recorded and exported, not part of net pay or project cost.</param>
/// <param name="Kind">Regular (a pay period) or ThirteenthMonth (<c>Year</c> says which; the period is the whole year).</param>
/// <param name="HolidaysMissingForYear">
/// Set on a regular payroll when the company has no holiday entered for the year the period ends in, so the page can say holidays were
/// not taken into account. Null otherwise.
/// </param>
/// <param name="WorkersOnArchivedProjectsLeftOut">
/// On a draft regular payroll: how many workers paid by the day were posted to an archived project or sub-project on a regular holiday in
/// the period that they did not work, and so were left out of holiday pay (6.12). Worked out when read; zero otherwise.
/// </param>
public sealed record PayrollRunDto(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int WorkerCount, long TotalGrossMinor,
    long TotalDeductionsMinor, long TotalNetMinor, decimal HoursPerDay, int OvertimeMultiplierBps, string? CreatedByName, DateTimeOffset CreatedAtUtc,
    string? ApprovedByName, DateTimeOffset? ApprovedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc, string? VoidReason,
    IReadOnlyList<PayrollLineDto> Lines, int Version, int RunsPerMonth = 2, long TotalEmployerSharesMinor = 0, string Kind = "Regular",
    int? Year = null, int? HolidaysMissingForYear = null, int WorkersOnArchivedProjectsLeftOut = 0);

public sealed record CreatePayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd, string? ClientRequestId);

/// <summary>Prepares the 13th-month pay for a calendar year (not a future one).</summary>
public sealed record CreateThirteenthMonthRunRequest(int Year, string? ClientRequestId);

public sealed record DeductionInput(string? Description, long AmountMinor);

public sealed record SetDeductionsRequest(IReadOnlyList<DeductionInput>? Deductions, int Version);

/// <summary>Changes one worker's calculated contribution or tax. The reason stays on the payroll (never in the activity log).</summary>
public sealed record AdjustStatutoryRequest(long AmountMinor, string? Reason, int Version);

public sealed record ApprovePayrollRunRequest(int Version);

public sealed record VoidPayrollRunRequest(string? Reason, int Version);

/// <summary>A downloadable payroll register.</summary>
public sealed record PayrollExportFile(string FileName, string ContentType, byte[] Content);
