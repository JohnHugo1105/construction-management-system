using Cms.Application.Common;
using Cms.Domain.Features.Subcontracts;

namespace Cms.Application.Features.Subcontracts;

public interface ISubcontractRepository
{
    /// <summary>Loads a subcontract with its claims, payments and variations, tracked for change. Null when it isn't in the organization.</summary>
    Task<Subcontract?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The subcontract an earlier create with this request id made.</summary>
    Task<RequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>The subcontract (and project) whose claim an earlier request with this id drafted.</summary>
    Task<RequestOwner?> FindClaimByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>The subcontract whose payment an earlier request with this id recorded.</summary>
    Task<RequestOwner?> FindPaymentByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(Subcontract subcontract);
}

/// <summary>Who a request id already belongs to: the subcontract, plus the claim or payment it made when it made one.</summary>
public sealed record RequestOwner(Guid SubcontractId, Guid ProjectId, Guid? ChildId, Guid? ClaimId);

public interface ISubcontractReader
{
    Task<PagedResult<SubcontractListItemDto>> ListAsync(SubcontractListFilter filter, PageRequest page, CancellationToken cancellationToken);

    Task<SubcontractDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record SubcontractListQuery(Guid? ProjectId, Guid? SubcontractorId, string? Status, string? Search);

public sealed record SubcontractListFilter(Guid? ProjectId, Guid? SubcontractorId, SubcontractStatus? Status, string? Search);

/// <summary>One row of the list. Base amounts are on the subcontract's own VAT treatment.</summary>
/// <param name="RetentionHeldMinor">Retention still held (held on certified claims minus what was released).</param>
/// <param name="PaidMinor">Every recorded payment: against claims and retention released.</param>
/// <param name="OutstandingMinor">Net payable on certified claims not yet paid.</param>
public sealed record SubcontractListItemDto(Guid Id, string Number, string Title, Guid SubcontractorId, string SubcontractorName, Guid ProjectId,
    string ProjectName, string? SubProjectName, string Status, string VatTreatment, long RevisedSumMinor, long CertifiedWorkMinor,
    int? PercentCertifiedBps, long RetentionHeldMinor, long PaidMinor, long OutstandingMinor, int Version);

/// <summary>A progress claim. Every figure comes from the domain; a draft has no number and <c>IsStale</c> says its chain has moved.</summary>
public sealed record SubcontractClaimDto(Guid Id, int? ClaimNumber, string Status, DateOnly ClaimDate, DateOnly PeriodEnd, long CumulativeWorkMinor,
    long PreviousCumulativeMinor, long ThisClaimBaseMinor, long NetMinor, long VatMinor, long GrossMinor, long RetentionMinor, long WithholdingMinor,
    long NetPayableMinor, long PaidMinor, long OutstandingMinor, bool IsStale, string? Notes, Guid? CostEntryId, string? CreatedByName,
    DateTimeOffset CreatedAtUtc, string? CertifiedByName, DateTimeOffset? CertifiedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc,
    string? VoidReason);

/// <summary>A recorded payment. <c>Kind</c> is Claim (with <c>ClaimNumber</c>) or RetentionRelease.</summary>
public sealed record SubcontractPaymentDto(Guid Id, string Kind, Guid? ClaimId, int? ClaimNumber, long AmountMinor, DateOnly PaidOn, string Method,
    string? Reference, string? Note, string Status, string? RecordedByName, DateTimeOffset RecordedAtUtc, string? VoidedByName,
    DateTimeOffset? VoidedAtUtc, string? VoidReason);

public sealed record SubcontractVariationDto(Guid Id, int Number, string Description, long AmountMinor, DateOnly VariationDate, string? CreatedByName,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// A whole subcontract: terms, the money position, and its claims (newest first), payments (newest first) and variations. Every figure is
/// worked out by the domain and stored; the client only displays it.
/// </summary>
/// <param name="RemainingWorkMinor">Revised sum minus certified work: what is still to be claimed.</param>
/// <param name="RetentionBalanceMinor">Retention held minus released: what is still to be paid out at the end.</param>
/// <param name="ProjectArchived">The project is archived: the subcontract can be read but not changed.</param>
public sealed record SubcontractDto(Guid Id, string Number, string Title, string? Notes, Guid SubcontractorId, string SubcontractorName,
    Guid ProjectId, string ProjectName, bool ProjectArchived, Guid? SubProjectId, string? SubProjectName, string Status, string VatTreatment,
    int VatRateBps, int RetentionBps, int WithholdingBps, long ContractSumMinor, long VariationsMinor, long RevisedSumMinor, long RevisedNetMinor,
    long RevisedVatMinor, long RevisedGrossMinor, long CertifiedWorkMinor, long RemainingWorkMinor, int? PercentCertifiedBps, long CertifiedNetMinor,
    long CertifiedGrossMinor, long RetentionHeldMinor, long RetentionReleasedMinor, long RetentionBalanceMinor, long WithholdingMinor,
    long NetPayableMinor, long PaidOnClaimsMinor, long OutstandingMinor, string? CreatedByName, DateTimeOffset CreatedAtUtc, string? AwardedByName,
    DateTimeOffset? AwardedAtUtc, string? CompletedByName, DateTimeOffset? CompletedAtUtc, string? CancelledByName, DateTimeOffset? CancelledAtUtc,
    string? CancelReason, IReadOnlyList<SubcontractClaimDto> Claims, IReadOnlyList<SubcontractPaymentDto> Payments,
    IReadOnlyList<SubcontractVariationDto> Variations, int Version, string? WbsCode = null, string? WorkItem = null, int? Division = null);

/// <summary>What a claim for the given work to date would be, worked out by the server before anything is saved.</summary>
public sealed record ClaimPreviewDto(long CumulativeWorkMinor, long PreviousCumulativeMinor, long ThisClaimBaseMinor, long NetMinor, long VatMinor,
    long GrossMinor, long RetentionMinor, long WithholdingMinor, long NetPayableMinor);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <param name="VatTreatment">Inclusive (the sum includes VAT), Exclusive (VAT is added) or Exempt.</param>
/// <param name="RetentionBps">Basis points held back from each claim; 1,000 (10%) when not given.</param>
/// <param name="WithholdingBps">Basis points of the amount before VAT withheld as tax; 0 when not given.</param>
public sealed record CreateSubcontractRequest(Guid SubcontractorId, Guid ProjectId, Guid? SubProjectId, string? Title, string? Notes,
    long ContractSumMinor, string? VatTreatment, int? RetentionBps, int? WithholdingBps, string? ClientRequestId, string? WbsCode = null);

public sealed record UpdateSubcontractRequest(Guid SubcontractorId, Guid? SubProjectId, string? Title, string? Notes, long ContractSumMinor,
    string? VatTreatment, int? RetentionBps, int? WithholdingBps, int Version, string? WbsCode = null);

public sealed record SubcontractReasonRequest(string? Reason, int Version);

/// <param name="AmountMinor">Signed base amount: positive adds work, negative omits it.</param>
public sealed record AddVariationRequest(string? Description, long AmountMinor, DateOnly VariationDate, int Version);

/// <param name="CumulativeWorkMinor">The base amount of all work done to date, including this claim.</param>
public sealed record SaveClaimRequest(DateOnly ClaimDate, DateOnly PeriodEnd, long CumulativeWorkMinor, string? Notes, string? ClientRequestId,
    int Version);

/// <param name="Method">BankTransfer, Cheque, Cash or Other.</param>
public sealed record RecordSubcontractPaymentRequest(long AmountMinor, DateOnly PaidOn, string? Method, string? Reference, string? Note,
    string? ClientRequestId, int Version);
