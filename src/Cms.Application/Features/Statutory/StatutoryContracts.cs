using Cms.Application.Common;
using Cms.Domain.Features.Statutory;

namespace Cms.Application.Features.Statutory;

public interface IStatutoryScheduleRepository
{
    /// <summary>Loads a table with its rows, tracked for change. Null when it isn't in the organization.</summary>
    Task<StatutorySchedule?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The table a form submission already created, so a double click returns it instead of adding a second.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>Whether another active table of the agency already takes effect on that date.</summary>
    Task<bool> EffectiveDateTakenAsync(StatutoryAgency agency, DateOnly effectiveFrom, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Whether a payroll that was approved (even if voided since) took an amount from this table.</summary>
    Task<bool> IsUsedByApprovedPayrollAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The tables in force on a date, at most one per agency (the active one with the latest effective date on or before it), with
    /// their rows, read without tracking. A fixed number of statements however many tables exist.
    /// </summary>
    Task<IReadOnlyList<StatutorySchedule>> ListInForceAsync(DateOnly onDate, CancellationToken cancellationToken);

    /// <summary>Which contributions each worker pays, read in one statement. A worker not in the organization is absent.</summary>
    Task<IReadOnlyDictionary<Guid, StatutoryParticipation>> ParticipationAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken);

    void Add(StatutorySchedule schedule);
}

public interface IStatutoryReader
{
    Task<PagedResult<StatutoryScheduleListItemDto>> ListAsync(StatutoryScheduleListQuery query, DateOnly today, PageRequest page,
        CancellationToken cancellationToken);

    Task<StatutoryScheduleDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken);

    /// <summary>The table in force today for each agency that has one.</summary>
    Task<IReadOnlyList<StatutoryScheduleSummaryDto>> InForceAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>A payroll's contributions and tax: per agency and per worker. Null when the payroll isn't in the organization.</summary>
    Task<PayrollStatutoryDto?> RunStatutoryAsync(Guid runId, CancellationToken cancellationToken);
}

/// <param name="Agency">Sss, PhilHealth, PagIbig or WithholdingTax to narrow the list; empty for all.</param>
public sealed record StatutoryScheduleListQuery(string? Agency, bool IncludeRetired);

public sealed record StatutoryScheduleListItemDto(Guid Id, string Agency, string AgencyLabel, string Name, DateOnly EffectiveFrom, string Status, string Method,
    string PeriodBasis, int RowCount, bool InForce, int Version);

public sealed record StatutoryScheduleSummaryDto(Guid Id, string Agency, string Name, DateOnly EffectiveFrom, string Method, string PeriodBasis);

/// <summary>One row of a bracket table: from <c>FromMinor</c> up to, but not including, <c>ToMinor</c> (null on the last row).</summary>
public sealed record StatutoryRowDto(long FromMinor, long? ToMinor, long EmployeeMinor, long EmployerMinor, long BaseTaxMinor, int RateBps);

/// <param name="UsedByApprovedPayroll">An approved payroll took amounts from this table, so it can't be changed: add a new table instead.</param>
/// <param name="InForce">This is the table that applies today.</param>
public sealed record StatutoryScheduleDto(Guid Id, string Agency, string AgencyLabel, string Name, DateOnly EffectiveFrom, string Status, string Method,
    string PeriodBasis, int? EmployeeRateBps, int? EmployerRateBps, long? BaseFloorMinor, long? BaseCeilingMinor, long? EmployeeMinimumMinor,
    long? EmployeeMaximumMinor, long? EmployerMinimumMinor, long? EmployerMaximumMinor, IReadOnlyList<StatutoryRowDto> Rows, bool UsedByApprovedPayroll,
    bool InForce, DateTimeOffset? RetiredAtUtc, int Version);

public sealed record StatutoryRowRequest(long FromMinor, long? ToMinor, long EmployeeMinor, long EmployerMinor, long BaseTaxMinor, int RateBps);

/// <summary>
/// Creates or changes a table. <c>Agency</c> is read only when creating (a table's agency never changes). The percentage fields are read
/// for a Percentage table and the rows for Brackets and TaxBrackets; the others are ignored.
/// </summary>
public sealed record SaveStatutoryScheduleRequest(string? Agency, string? Name, DateOnly EffectiveFrom, string? Method, string? PeriodBasis,
    int? EmployeeRateBps, int? EmployerRateBps, long? BaseFloorMinor, long? BaseCeilingMinor, long? EmployeeMinimumMinor, long? EmployeeMaximumMinor,
    long? EmployerMinimumMinor, long? EmployerMaximumMinor, IReadOnlyList<StatutoryRowRequest>? Rows, string? ClientRequestId, int Version);

/// <summary>Whether an agency's contributions are worked out for you, in plain words, with what to do otherwise.</summary>
/// <param name="State">Available (a table is in force today) or NeedsSetup (none).</param>
public sealed record StatutoryAgencyStatusDto(string Agency, string Label, string State, string Message, string? Fallback, Guid? ScheduleId,
    string? ScheduleName, DateOnly? EffectiveFrom);

public sealed record StatutoryStatusDto(DateOnly Today, IReadOnlyList<StatutoryAgencyStatusDto> Agencies);

/// <summary>What one agency is owed for one payroll: the workers' shares taken from pay, the employer's shares, and the sum to remit.</summary>
public sealed record StatutoryAgencyTotalDto(string Agency, string Label, IReadOnlyList<string> ScheduleNames, int WorkerCount, long EmployeeMinor,
    long EmployerMinor, long ToRemitMinor, int AdjustedCount, int LimitedCount);

public sealed record StatutoryWorkerShareDto(string Agency, long EmployeeMinor, long EmployerMinor, long? CalculatedMinor, bool IsAdjusted,
    bool IsLimited);

public sealed record StatutoryWorkerDto(Guid WorkerId, string WorkerName, string EmployeeNumber, IReadOnlyList<StatutoryWorkerShareDto> Shares);

public sealed record PayrollStatutoryDto(Guid RunId, string Status, int RunsPerMonth, long TotalEmployeeMinor, long TotalEmployerMinor, long TotalToRemitMinor,
    IReadOnlyList<StatutoryAgencyTotalDto> Agencies, IReadOnlyList<StatutoryWorkerDto> Workers);

public static class StatutoryText
{
    /// <summary>Request text to an agency; an unknown name gives null.</summary>
    public static StatutoryAgency? Agency(string? text) => Parse<StatutoryAgency>(text);

    public static T? Parse<T>(string? text) where T : struct, Enum
    {
        var name = text?.Trim();
        foreach (var candidate in Enum.GetValues<T>())
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase)) return candidate;
        return null;
    }
}
