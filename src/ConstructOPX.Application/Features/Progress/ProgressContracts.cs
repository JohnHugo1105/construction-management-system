using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;

namespace ConstructOPX.Application.Features.Progress;

// ---- Ports ------------------------------------------------------------------------------------------------------------------

/// <summary>Loads and adds the tracked records recording progress needs.</summary>
public interface IProgressRepository
{
    /// <summary>The active budget lines of the project among <paramref name="ids"/>. A removed line, another project's line and another company's line are absent.</summary>
    Task<IReadOnlyDictionary<Guid, BudgetLine>> GetActiveLinesAsync(Guid projectId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>The latest entry of each of the lines in one statement. A line that was never updated is absent.</summary>
    Task<IReadOnlyDictionary<Guid, LatestProgress>> GetLatestAsync(IReadOnlyCollection<Guid> budgetLineIds, CancellationToken cancellationToken);

    /// <summary>The entries an earlier request with this id recorded, so a repeated request returns them instead of recording again.</summary>
    Task<IReadOnlyList<ProgressEntry>> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(IEnumerable<ProgressEntry> entries);
}

/// <summary>Reads progress straight into rows in SQL, a fixed number of statements however many lines and entries there are.</summary>
public interface IProgressReader
{
    /// <summary>Every active budget line of the project, with the percent that was current on <paramref name="asOf"/>, in work-breakdown order.</summary>
    Task<IReadOnlyList<ProgressLineRow>> ListLinesAsync(Guid projectId, DateOnly asOf, CancellationToken cancellationToken);

    /// <summary>The posted actual cost of the project dated on or before <paramref name="asOf"/>, net and gross.</summary>
    Task<MeasuredAmounts> SumActualAsync(Guid projectId, DateOnly asOf, CancellationToken cancellationToken);

    /// <summary>One page of a line's progress entries, newest first.</summary>
    Task<PagedResult<ProgressHistoryItemDto>> HistoryAsync(Guid budgetLineId, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>One active budget line and where it stood on the date. A line never updated has a null percent and date.</summary>
public sealed record ProgressLineRow(Guid BudgetLineId, string Description, int? Division, string? WbsCode, long NetMinor, long GrossMinor,
    int? PercentBps, DateOnly? LastUpdatedOn);

// ---- Read -------------------------------------------------------------------------------------------------------------------

/// <summary>
/// What earned value says about the project. Amounts are on the company's comparison basis, except the forecast margin, which always uses
/// amounts with VAT because the contract price includes VAT. Every index is in basis points (10,000 is 1.00: each peso spent earns one peso
/// of work).
/// </summary>
/// <param name="BudgetMinor">The budget at completion: every active line.</param>
/// <param name="EarnedMinor">What the work done so far is worth: each line's budget times its percent complete.</param>
/// <param name="SpentMinor">Posted cost on the date.</param>
/// <param name="PlannedMinor">What should be done by the date: from the schedule's activities when the project has a schedule (the budget lines
/// in no activity are spread evenly over the planned time), otherwise all the work spread evenly over the planned time; null without either.</param>
/// <param name="CostVarianceMinor">Earned minus spent; negative means the work cost more than it was worth.</param>
/// <param name="ScheduleVarianceMinor">Earned minus planned; negative means behind schedule. Null without a planned value.</param>
/// <param name="CpiBps">Earned per unit spent; null when nothing is spent.</param>
/// <param name="SpiBps">Earned per unit planned; null without a planned value or before any is planned.</param>
/// <param name="ForecastMinor">The expected cost at completion.</param>
/// <param name="ToCompleteMinor">The expected cost of the rest of the work.</param>
/// <param name="ForecastVarianceMinor">Budget minus forecast; negative means a forecast overrun.</param>
/// <param name="PercentCompleteBps">Earned as a share of the budget; null when nothing is budgeted.</param>
/// <param name="PlannedPercentBps">The share of the planned time that has passed (with a schedule: the planned value as a share of the budget); null
/// without a planned value.</param>
/// <param name="ContractPriceMinor">The contract price with VAT; null when the project has none.</param>
/// <param name="ForecastMarginMinor">Contract price minus the forecast cost, both with VAT; null without a contract price.</param>
public sealed record EarnedValueDto(long BudgetMinor, long EarnedMinor, long SpentMinor, long? PlannedMinor, long CostVarianceMinor,
    long? ScheduleVarianceMinor, int? CpiBps, int? SpiBps, long ForecastMinor, long ToCompleteMinor, long ForecastVarianceMinor,
    int? PercentCompleteBps, int? PlannedPercentBps, long? ContractPriceMinor, long? ForecastMarginMinor);

public sealed record ProgressLineDto(Guid BudgetLineId, string Description, string? WbsCode, long BudgetMinor, int PercentBps, DateOnly? LastUpdatedOn,
    long EarnedMinor);

/// <param name="Division">The division number, or null for lines with no classification (named "Unclassified").</param>
public sealed record ProgressDivisionDto(int? Division, string Name, long BudgetMinor, long EarnedMinor, int? PercentBps,
    IReadOnlyList<ProgressLineDto> Lines);

/// <param name="AsOfDate">The date the figures are for.</param>
/// <param name="Basis">IncludingVat or ExcludingVat: which amounts the budget and spending are compared on.</param>
/// <param name="HasProgress">False until someone has recorded progress on any line.</param>
/// <param name="LastUpdatedOn">The latest date progress was recorded for, or null before any.</param>
/// <param name="PlannedFrom">
/// Where the planned value comes from: "Schedule" when the project has at least one schedule activity with budget lines linked (the planned value,
/// schedule index and schedule variance then follow the activities' dates), "ProjectDates" for the straight line over the project's start and
/// target end dates, or null when there is no planned value.
/// </param>
public sealed record ProgressDto(Guid ProjectId, DateOnly AsOfDate, string Basis, bool HasProgress, DateOnly? LastUpdatedOn, EarnedValueDto EarnedValue,
    IReadOnlyList<ProgressDivisionDto> Divisions, string? PlannedFrom = null);

public sealed record ProgressHistoryItemDto(Guid Id, DateOnly AsOfDate, int PercentBps, string? Note, string? RecordedBy, DateTimeOffset RecordedAtUtc);

// ---- Record -----------------------------------------------------------------------------------------------------------------

/// <param name="BudgetLineId">The line.</param>
/// <param name="PercentBps">How much of the line is done, 0 to 10,000 (100%).</param>
/// <param name="Note">Needed when the percent goes down (for example rework); optional otherwise.</param>
/// <param name="ExpectedPercentBps">The percent the person was looking at. When another person saved a different one meanwhile, the update is refused (409).</param>
public sealed record ProgressLineRequest(Guid BudgetLineId, int? PercentBps, string? Note, int? ExpectedPercentBps = null);

/// <param name="AsOfDate">The date the progress is true on (not in the future).</param>
/// <param name="Lines">Up to 200 lines. A line whose percent did not change is skipped.</param>
/// <param name="ClientRequestId">The web app sends a fresh one per form: sending the same one again returns the first result.</param>
public sealed record RecordProgressRequest(DateOnly AsOfDate, IReadOnlyList<ProgressLineRequest>? Lines, string? ClientRequestId);

public sealed record RecordProgressResult(DateOnly AsOfDate, int LinesUpdated, DateTimeOffset RecordedAtUtc);
