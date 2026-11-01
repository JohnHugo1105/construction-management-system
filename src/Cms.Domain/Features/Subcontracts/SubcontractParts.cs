using Cms.Domain.Common;

namespace Cms.Domain.Features.Subcontracts;

/// <summary>
/// A progress claim (payment certificate). A draft can be edited or discarded; certifying it posts its work as project cost and makes its net
/// payable owed; voiding it voids that cost. It is numbered 1, 2, 3 within its subcontract when it is certified, and never deleted.
/// </summary>
public sealed class SubcontractClaim
{
    private SubcontractClaim() { }

    internal SubcontractClaim(Guid id, Guid tenantId, Guid subcontractId, DateOnly claimDate, DateOnly periodEnd, ClaimBreakdown breakdown,
        string? notes, Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        SubcontractId = subcontractId;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = now;
        ClientRequestId = clientRequestId;
        Apply(claimDate, periodEnd, breakdown, notes);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubcontractId { get; private set; }

    /// <summary>1, 2, 3 within the subcontract, given when the claim is certified. Null for a draft (and a discarded draft).</summary>
    public int? ClaimNumber { get; private set; }

    public SubcontractClaimStatus Status { get; private set; } = SubcontractClaimStatus.Draft;
    public DateOnly ClaimDate { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public long CumulativeWorkMinor { get; private set; }

    /// <summary>The certified work the claim was worked out on. Certifying refuses a claim whose chain has moved since.</summary>
    public long PreviousCumulativeMinor { get; private set; }

    public long ThisClaimBaseMinor { get; private set; }
    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }
    public long RetentionMinor { get; private set; }
    public long WithholdingMinor { get; private set; }
    public long NetPayableMinor { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The cost entry the certification posted.</summary>
    public Guid? CostEntryId { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? CertifiedByUserId { get; private set; }
    public DateTimeOffset? CertifiedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    public bool IsDraft => Status == SubcontractClaimStatus.Draft;
    public bool IsCertified => Status == SubcontractClaimStatus.Certified;

    public ClaimBreakdown Breakdown => new(CumulativeWorkMinor, PreviousCumulativeMinor, ThisClaimBaseMinor, NetMinor, VatMinor, GrossMinor,
        RetentionMinor, WithholdingMinor, NetPayableMinor);

    internal void Revise(DateOnly claimDate, DateOnly periodEnd, ClaimBreakdown breakdown, string? notes) =>
        Apply(claimDate, periodEnd, breakdown, notes);

    internal void Certify(int claimNumber, Guid costEntryId, Guid userId, DateTimeOffset now)
    {
        Status = SubcontractClaimStatus.Certified;
        ClaimNumber = claimNumber;
        CostEntryId = costEntryId;
        CertifiedByUserId = userId;
        CertifiedAtUtc = now;
    }

    internal void Void(string reason, Guid userId, DateTimeOffset now)
    {
        Status = SubcontractClaimStatus.Voided;
        VoidReason = reason;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
    }

    private void Apply(DateOnly claimDate, DateOnly periodEnd, ClaimBreakdown breakdown, string? notes)
    {
        ClaimDate = claimDate;
        PeriodEnd = periodEnd;
        CumulativeWorkMinor = breakdown.CumulativeWorkMinor;
        PreviousCumulativeMinor = breakdown.PreviousCumulativeMinor;
        ThisClaimBaseMinor = breakdown.ThisClaimBaseMinor;
        NetMinor = breakdown.NetMinor;
        VatMinor = breakdown.VatMinor;
        GrossMinor = breakdown.GrossMinor;
        RetentionMinor = breakdown.RetentionMinor;
        WithholdingMinor = breakdown.WithholdingMinor;
        NetPayableMinor = breakdown.NetPayableMinor;
        Notes = notes;
    }
}

/// <summary>
/// A payment a person records as made: against a certified claim, or a release of retention. The system never pays anyone; this is the
/// record that it was paid. A mistake is voided with a reason, never deleted.
/// </summary>
public sealed class SubcontractPayment
{
    private SubcontractPayment() { }

    internal SubcontractPayment(Guid id, Guid tenantId, Guid subcontractId, SubcontractPaymentKind kind, Guid? claimId, long amountMinor,
        DateOnly paidOn, SubcontractPaymentMethod method, string? reference, string? note, Guid recordedByUserId, string? clientRequestId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        SubcontractId = subcontractId;
        Kind = kind;
        ClaimId = claimId;
        AmountMinor = amountMinor;
        PaidOn = paidOn;
        Method = method;
        Reference = reference;
        Note = note;
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = now;
        ClientRequestId = clientRequestId;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubcontractId { get; private set; }
    public SubcontractPaymentKind Kind { get; private set; }

    /// <summary>The certified claim paid; null for a retention release.</summary>
    public Guid? ClaimId { get; private set; }

    public long AmountMinor { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public SubcontractPaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }
    public SubcontractPaymentStatus Status { get; private set; } = SubcontractPaymentStatus.Recorded;
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    public bool IsRecorded => Status == SubcontractPaymentStatus.Recorded;

    internal void Void(string reason, Guid userId, DateTimeOffset now)
    {
        Status = SubcontractPaymentStatus.Voided;
        VoidReason = reason;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
    }
}

/// <summary>A change to the contract sum (more or less work), as a signed base amount. Variations are added, never edited or deleted.</summary>
public sealed class SubcontractVariation
{
    private SubcontractVariation() { }

    internal SubcontractVariation(Guid id, Guid tenantId, Guid subcontractId, int number, string description, long amountMinor, DateOnly variationDate,
        Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        SubcontractId = subcontractId;
        Number = number;
        Description = description;
        AmountMinor = amountMinor;
        VariationDate = variationDate;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubcontractId { get; private set; }

    /// <summary>1, 2, 3 within the subcontract.</summary>
    public int Number { get; private set; }

    public string Description { get; private set; } = "";

    /// <summary>Signed: positive adds work, negative omits it. A base amount on the contract's VAT treatment.</summary>
    public long AmountMinor { get; private set; }

    public DateOnly VariationDate { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
