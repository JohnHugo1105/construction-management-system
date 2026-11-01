using Cms.Application.Common;
using Cms.Domain.Features.Billing;

namespace Cms.Application.Features.Billing;

public interface IProjectBillingTermsRepository
{
    /// <summary>The project's saved terms, tracked for change. Null when they were never saved (the defaults apply).</summary>
    Task<ProjectBillingTerms?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(ProjectBillingTerms terms);
}

public interface IClientInvoiceRepository
{
    /// <summary>Loads a bill with its payments, tracked for change. Null when it isn't in the organization.</summary>
    Task<ClientInvoice?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The project's latest issued bill (the one no other bill follows), tracked for change; null when none is issued.</summary>
    Task<ClientInvoice?> GetChainHeadAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>What the project's issued bills add up to now, summed in SQL.</summary>
    Task<BillingLedger> GetLedgerAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Whether the project has a down payment bill that is not void (a draft or an issued one).</summary>
    Task<bool> AnyLiveAdvanceAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The bill a form submission already created, so a double click returns it instead of preparing two drafts.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>The bill whose payment a form submission already recorded, and that payment's id.</summary>
    Task<(Guid InvoiceId, Guid PaymentId)?> FindPaymentByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(ClientInvoice invoice);
}

public interface IBillingReader
{
    /// <summary>A project's money line, terms and bills; null when the project isn't in the organization.</summary>
    Task<ProjectBillingDto?> GetProjectBillingAsync(Guid projectId, DateOnly today, PageRequest page, CancellationToken cancellationToken);

    Task<PagedResult<ClientInvoiceListItemDto>> ListAsync(ClientInvoiceListQuery query, DateOnly today, PageRequest page, CancellationToken cancellationToken);

    Task<ClientInvoiceDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken);

    Task<ReceivablesDto> GetReceivablesAsync(DateOnly today, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>
/// What the change-order page needs to say whether added work on a government project passes the agency's limit: read in one statement
/// from the project's billing terms, its baseline and its approved change orders.
/// </summary>
public interface IGovernmentLimitReader
{
    /// <summary>Null when the project isn't in the organization.</summary>
    Task<GovernmentLimitFigures?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <param name="BaselineContractPriceMinor">The contract price frozen when the budget was locked; null while it isn't.</param>
/// <param name="ApprovedAdditiveMinor">The sum of the price increases of the approved change orders (decreases don't count).</param>
public sealed record GovernmentLimitFigures(bool IsGovernmentClient, long? BaselineContractPriceMinor, long ApprovedAdditiveMinor);

public sealed record ClientInvoiceListQuery(Guid? ProjectId, string? Status, bool OverdueOnly, string? Search);

public sealed record BillingTermsDto(int RetentionBps, int CreditableWithholdingBps, int PaymentTermsDays, int AdvanceBps, int VatWithholdingBps,
    bool IsGovernmentClient, bool IsDefault, int Version);

/// <summary>A row of a bills list. Paid, outstanding and overdue are worked out from the bill and its payments, never stored.</summary>
public sealed record ClientInvoiceListItemDto(Guid Id, string? Number, Guid ProjectId, string ProjectName, string Kind, string Status, DateOnly BillingDate,
    DateOnly DueDate, string Description, long GrossMinor, long NetReceivableMinor, long PaidMinor, long OutstandingMinor, bool IsOverdue, int Version);

/// <summary>
/// A project's billing position. <c>BilledToDateMinor</c> is the value of work on issued progress bills (VAT included);
/// <c>RetentionHeldMinor</c> is what the client still holds (withheld less released); <c>OutstandingMinor</c> is what issued bills
/// still ask for; <c>OverdueMinor</c> the part of it past its due date. <c>AdvanceBilledMinor</c> is the issued down payment,
/// <c>AdvanceRecoveredMinor</c> what issued progress bills recovered of it and <c>AdvanceToRecoverMinor</c> the rest.
/// <c>AdvanceBillId</c> is the project's down payment bill that is not void (draft or issued), and <c>AdvanceToBillMinor</c> what a down
/// payment bill would ask for now: present only while one can be prepared (the terms take one, none exists, no progress bill is
/// issued and the project has a contract price).
/// </summary>
public sealed record ProjectBillingDto(Guid ProjectId, string ProjectName, string ProjectStatus, long? ContractPriceMinor, long BilledToDateMinor,
    int? PercentBilledBps, long? RemainingToBillMinor, long RetentionWithheldMinor, long RetentionReleasedMinor, long RetentionHeldMinor, long CollectedMinor,
    long OutstandingMinor, long OverdueMinor, int OverdueCount, bool CanReleaseRetention, long AdvanceBilledMinor, long AdvanceRecoveredMinor,
    long AdvanceToRecoverMinor, Guid? AdvanceBillId, long? AdvanceToBillMinor, BillingTermsDto Terms, int VatRateBps,
    PagedResult<ClientInvoiceListItemDto> Bills);

public sealed record ClientPaymentDto(Guid Id, long AmountMinor, DateOnly PaidOn, string Method, string? Reference, string? Note, string? RecordedByName,
    DateTimeOffset RecordedAtUtc, bool IsVoided, string? VoidedByName, DateTimeOffset? VoidedAtUtc, string? VoidReason);

/// <summary>
/// One bill with its breakdown and payments. <c>IsLatestIssued</c> says it can be voided (when nothing is paid on it);
/// <c>IsStale</c> says a draft's figures no longer follow from what is billed now and it must be saved again before it is issued.
/// <c>AdvanceBps</c> is the down payment rate (the down payment bill's own, or the one a progress bill recovered at);
/// <c>AdvanceRecoveryMinor</c> what a progress bill recovers of the down payment; <c>VatWithholding*</c> the VAT the client withholds.
/// </summary>
public sealed record ClientInvoiceDto(Guid Id, string? Number, Guid ProjectId, string ProjectName, string ProjectStatus, long? ContractPriceMinor, string Kind,
    string Status, DateOnly BillingDate, DateOnly DueDate, string Description, long? CumulativeWorkMinor, long PreviousCumulativeWorkMinor, long GrossMinor,
    long NetMinor, long VatMinor, int VatRateBps, int RetentionBps, long RetentionMinor, int WithholdingBps, long WithholdingMinor, int VatWithholdingBps,
    long VatWithholdingMinor, int AdvanceBps, long AdvanceRecoveryMinor, long NetReceivableMinor, int PaymentTermsDays, long PaidMinor, long OutstandingMinor, bool IsOverdue, bool IsLatestIssued, bool IsStale, string? CreatedByName,
    DateTimeOffset CreatedAtUtc, string? IssuedByName, DateTimeOffset? IssuedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc, string? VoidReason,
    IReadOnlyList<ClientPaymentDto> Payments, int Version);

/// <summary>What a progress bill for this much work completed to date would be, worked out by the same rules as saving it.</summary>
public sealed record BillPreviewDto(long ContractPriceMinor, long PreviousCumulativeWorkMinor, long CumulativeWorkMinor, long GrossMinor, long NetMinor,
    long VatMinor, int VatRateBps, int RetentionBps, long RetentionMinor, int WithholdingBps, long WithholdingMinor, int VatWithholdingBps,
    long VatWithholdingMinor, long AdvanceRecoveryMinor, long NetReceivableMinor, int PaymentTermsDays, DateOnly DueDate);

public sealed record ReceivableRowDto(Guid ProjectId, string ProjectName, string ProjectStatus, long BilledToDateMinor, long CollectedMinor,
    long OutstandingMinor, long OverdueMinor, long RetentionHeldMinor);

/// <summary>Every project with money still to collect or retention held, overdue first, and the company's totals.</summary>
public sealed record ReceivablesDto(long OutstandingMinor, long OverdueMinor, long RetentionHeldMinor, PagedResult<ReceivableRowDto> Rows);

/// <param name="AdvanceBps">The down payment as a share of the contract price; left out, it keeps what is saved (none by default).</param>
/// <param name="VatWithholdingBps">The VAT the client withholds, as a share of the amount before VAT; left out, it keeps what is saved.</param>
/// <param name="IsGovernmentClient">Whether the client is a government agency; left out, it keeps what is saved.</param>
public sealed record SaveBillingTermsRequest(int RetentionBps, int CreditableWithholdingBps, int PaymentTermsDays, int Version, int? AdvanceBps = null,
    int? VatWithholdingBps = null, bool? IsGovernmentClient = null);

/// <param name="Kind">"Progress" (the default), "Advance" (the down payment, worked out from the terms) or "RetentionRelease".</param>
/// <param name="CumulativeWorkMinor">Progress bills: the value of all work completed to date, VAT included.</param>
/// <param name="AmountMinor">Retention releases: the retention to release.</param>
public sealed record CreateClientInvoiceRequest(string? Kind, DateOnly BillingDate, string? Description, long? CumulativeWorkMinor, long? AmountMinor,
    string? ClientRequestId);

public sealed record UpdateClientInvoiceRequest(DateOnly BillingDate, string? Description, long? CumulativeWorkMinor, long? AmountMinor, int Version);

public sealed record BillingReasonRequest(string? Reason, int Version);

public sealed record RecordClientPaymentRequest(long AmountMinor, DateOnly PaidOn, string? Method, string? Reference, string? Note, string? ClientRequestId,
    int Version);
