using Cms.Application.Common;
using Cms.Domain.Features.ChangeOrders;
using Cms.Domain.Features.Costing;

namespace Cms.Application.Features.ChangeOrders;

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface IChangeOrderRepository
{
    /// <summary>A change order with its lines, tracked for change. Null when it doesn't exist in the organization.</summary>
    Task<ChangeOrder?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ChangeOrder?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// The number of the most recently approved change order of the project that was approved after the given moment (or at the same
    /// moment with a higher number), other than <paramref name="excludingId"/>. Null when there is none.
    /// </summary>
    Task<string?> LatestApprovedAfterAsync(Guid projectId, Guid excludingId, DateTimeOffset approvedAtUtc, long sequence,
        CancellationToken cancellationToken);

    /// <summary>Whether the project has any change order that is approved now (voided ones don't count).</summary>
    Task<bool> AnyApprovedAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(ChangeOrder order);
}

/// <summary>Loads the budget lines a change order changes, tracked, in one statement.</summary>
public interface IChangeOrderBudgetLineRepository
{
    Task<IReadOnlyList<BudgetLine>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

/// <summary>Reads change orders and the baseline figures straight into DTOs in SQL, in a fixed number of statements.</summary>
public interface IChangeOrderReader
{
    Task<PagedResult<ChangeOrderListItemDto>> ListAsync(Guid projectId, ChangeOrderStatus? status, PageRequest page, CancellationToken cancellationToken);

    Task<ChangeOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The project's baseline, its active budget lines and its change orders, added up in SQL.</summary>
    Task<BaselineFigures?> BaselineFiguresAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>"CO-001" for a change order id, for the chip on the budget lines it added.</summary>
    Task<string?> NumberOfAsync(Guid changeOrderId, CancellationToken cancellationToken);
}

/// <summary>The raw sums the baseline is built from. Approved and pending sums are the change orders' recorded budget effects.</summary>
public sealed record BaselineFigures(Guid ProjectId, string ProjectName, int ProjectVersion, long? ContractPriceMinor, DateTimeOffset? LockedAtUtc,
    string? LockedBy, long? BaselineBudgetNetMinor, long? BaselineBudgetGrossMinor, long? BaselineContractPriceMinor, string? LastUnlockReason,
    DateTimeOffset? LastUnlockedAtUtc, int ActiveLineCount, long ActiveNetMinor, long ActiveGrossMinor, int ApprovedCount, long ApprovedNetMinor,
    long ApprovedGrossMinor, long ApprovedPriceDeltaMinor, int PendingCount, long PendingNetMinor, long PendingGrossMinor, long PendingPriceDeltaMinor);

// ---- Baseline ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// A project's budget and contract price explained: the original (frozen when the budget was locked), the approved changes, and the revised
/// figures. <c>CurrentBudget*</c> is the sum of the active budget lines now; while locked it equals the revised budget, which
/// <c>Reconciles</c> confirms. While unlocked there is no original: the revised figures are the current ones.
/// </summary>
/// <param name="PendingCount">Submitted change orders waiting for a decision; their effect is not in any total yet.</param>
public sealed record BudgetBaselineDto(Guid ProjectId, bool Locked, DateTimeOffset? LockedAtUtc, string? LockedBy,
    long? OriginalBudgetNetMinor, long? OriginalBudgetGrossMinor, long? OriginalContractPriceMinor,
    int ApprovedCount, long ApprovedBudgetNetMinor, long ApprovedBudgetGrossMinor, long ApprovedContractPriceDeltaMinor,
    long RevisedBudgetNetMinor, long RevisedBudgetGrossMinor, long? RevisedContractPriceMinor,
    int ActiveLineCount, long CurrentBudgetNetMinor, long CurrentBudgetGrossMinor, long? CurrentContractPriceMinor,
    int PendingCount, long PendingBudgetNetMinor, long PendingBudgetGrossMinor, long PendingContractPriceDeltaMinor,
    bool Reconciles, string? LastUnlockReason, DateTimeOffset? LastUnlockedAtUtc, int ProjectVersion);

public sealed record UnlockBudgetRequest(string? Reason, int Version);

// ---- Change orders ----------------------------------------------------------------------------------------------------------

/// <param name="BudgetEffectGrossMinor">The signed change to the budget with VAT: what was applied once approved, an estimate before.</param>
public sealed record ChangeOrderListItemDto(Guid Id, Guid ProjectId, string? Number, string Title, string Reason, string Status, string? VoidedFromStatus,
    int LineCount, long BudgetEffectNetMinor, long BudgetEffectGrossMinor, long ContractPriceDeltaMinor, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc, DateTimeOffset? DecidedAtUtc, DateTimeOffset? VoidedAtUtc, int Version);

/// <summary>
/// One line. <c>Quantity</c>, <c>UnitCostMinor</c> and the money describe the budget line as it will be (Add, Revise) or as it is removed
/// (Omit). <c>Previous*</c> is the line before a Revise or Omit. <c>Effect*</c> is the signed change to the budget.
/// </summary>
public sealed record ChangeOrderLineDto(Guid Id, int LineNumber, string Kind, Guid? BudgetLineId, Guid? SubProjectId, string? SubProjectName,
    int? Division, string? MainScope, string? SubScope, string? WbsCode, string Description, string ItemType, decimal Quantity, string Unit,
    long UnitCostMinor, string VatTreatment, int VatRateBps, DateOnly EntryDate, string? Remarks, long NetMinor, long VatMinor, long GrossMinor,
    decimal? PreviousQuantity, long? PreviousUnitCostMinor, long? PreviousNetMinor, long? PreviousGrossMinor, long EffectNetMinor,
    long EffectVatMinor, long EffectGrossMinor);

/// <param name="VoidBlockedBy">For an approved order: the number of a change order approved after it that must be voided first.</param>
/// <param name="GovernmentLimit">Where added work stands against a government client's limit (advisory). Read-only; filled by the service.</param>
public sealed record ChangeOrderDto(Guid Id, Guid ProjectId, string? Number, string Title, string? Description, string Reason, string Status,
    string? VoidedFromStatus, long ContractPriceDeltaMinor, long BudgetEffectNetMinor, long BudgetEffectVatMinor, long BudgetEffectGrossMinor,
    string? CreatedBy, DateTimeOffset CreatedAtUtc, string? SubmittedBy, DateTimeOffset? SubmittedAtUtc, string? DecidedBy,
    DateTimeOffset? DecidedAtUtc, string? DecisionReason, string? VoidedBy, DateTimeOffset? VoidedAtUtc, string? VoidReason, string? VoidBlockedBy,
    IReadOnlyList<ChangeOrderLineDto> Lines, int Version, GovernmentLimitDto? GovernmentLimit = null);

/// <summary>
/// Added variation orders on a government project against the agency's limit. <c>Applies</c> only for a government client with a locked
/// baseline; then <c>LimitMinor</c> is 10% of the original (baseline) contract price and <c>UsedMinor</c> the approved price increases plus
/// this order's own while it waits for approval. <c>Exceeds</c> is a warning: approving is still allowed (the agency decides).
/// </summary>
public sealed record GovernmentLimitDto(bool Applies, long LimitMinor, long UsedMinor, bool Exceeds)
{
    public static GovernmentLimitDto None { get; } = new(false, 0, 0, false);
}

/// <summary><c>Reason</c> is <c>ClientRequest</c>, <c>DesignChange</c>, <c>SiteCondition</c>, <c>ErrorOrOmission</c> or <c>Other</c>.</summary>
public sealed record CreateChangeOrderRequest(string? Title, string? Description, string? Reason, long ContractPriceDeltaMinor, string? ClientRequestId);

public sealed record UpdateChangeOrderRequest(string? Title, string? Description, string? Reason, long ContractPriceDeltaMinor, int Version);

/// <summary>
/// One line. <c>Kind</c> is <c>Add</c>, <c>Revise</c> or <c>Omit</c>. Add takes the budget line fields (as a budget line does); Revise takes
/// <c>budgetLineId</c>, <c>quantity</c> and <c>unitCostMinor</c>; Omit takes <c>budgetLineId</c>. <c>Version</c> is the change order's.
/// </summary>
public sealed record SaveChangeOrderLineRequest(string? Kind, Guid? BudgetLineId, Guid? SubProjectId, int? Division, string? MainScope, string? SubScope,
    string? WbsCode, string? Description, string? ItemType, decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment, DateOnly EntryDate,
    string? Remarks, int Version);

public sealed record ApproveChangeOrderRequest(string? Note, int Version);

/// <summary>The body of reject, void and discard: why, and the version the person opened.</summary>
public sealed record ChangeOrderReasonRequest(string? Reason, int Version);
