using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Suppliers;
using static Cms.Domain.Features.Subcontracts.ClaimBreakdown;

namespace Cms.Domain.Features.Subcontracts;

/// <summary>
/// Work awarded to a subcontractor on a project: the contract sum (a base amount on a VAT treatment, with the company VAT rate kept as it was
/// when the subcontract was made), the retention and withholding rates, its variations, its progress claims and the payments recorded against
/// them. One aggregate, so the chain of claims, what each claim still owes, the retention held and the revised sum are always checked
/// together and saved together: two people certifying or paying at the same moment can't both succeed against the same figures.
/// </summary>
/// <remarks>
/// The totals (revised sum, certified work, retention, net payable, paid) are kept on the record, worked out here from the claims, payments and
/// variations after every change, so lists and the commitment report read one row instead of adding up children.
/// </remarks>
public sealed class Subcontract : AggregateRoot
{
    public const int DefaultRetentionBps = 1_000;
    public const int MaximumRetentionBps = 3_000;
    public const int DefaultWithholdingBps = 0;
    public const int MaximumWithholdingBps = 1_000;
    public const int MaximumClaims = 100;
    public const int MaximumPayments = 300;
    public const int MaximumVariations = 100;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);

    private readonly List<SubcontractClaim> _claims = [];
    private readonly List<SubcontractPayment> _payments = [];
    private readonly List<SubcontractVariation> _variations = [];

    private Subcontract() { }

    /// <summary>"SC-0001", from the company's <c>subcontract</c> sequence.</summary>
    public string Number { get; private set; } = "";

    /// <summary>The firm doing the work (a supplier record of kind Subcontractor).</summary>
    public Guid SupplierId { get; private set; }

    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }

    /// <summary>The awarded contract sum as a base amount on <see cref="VatTreatment"/>.</summary>
    public long ContractSumMinor { get; private set; }

    public VatTreatment VatTreatment { get; private set; }
    public int VatRateBps { get; private set; }
    public int RetentionBps { get; private set; }
    public int WithholdingBps { get; private set; }
    public SubcontractStatus Status { get; private set; } = SubcontractStatus.Draft;

    public long RevisedSumMinor { get; private set; }
    public long RevisedNetMinor { get; private set; }
    public long RevisedVatMinor { get; private set; }
    public long RevisedGrossMinor { get; private set; }

    /// <summary>
    /// The standard work-breakdown code the certified work is counted under (for example 3300 for cast-in-place concrete), or null when the
    /// work is not placed in the breakdown. It decides which division of the project's budget the cost is compared with.
    /// </summary>
    public string? WbsCode { get; private set; }

    /// <summary>Where a certified claim's cost goes in the work breakdown.</summary>
    public CostClassification Classification =>
        WbsCode is null ? CostClassification.Unclassified : CostClassification.Create(null, null, null, WbsCode);

    /// <summary>The base amount of all certified work (the latest certified claim's work to date).</summary>
    public long CertifiedWorkMinor { get; private set; }

    public long CertifiedNetMinor { get; private set; }
    public long CertifiedGrossMinor { get; private set; }

    /// <summary>Retention on certified claims, whether or not some has been released since.</summary>
    public long RetentionHeldMinor { get; private set; }

    public long RetentionReleasedMinor { get; private set; }
    public long WithholdingMinor { get; private set; }

    /// <summary>The net payable of every certified claim.</summary>
    public long NetPayableMinor { get; private set; }

    /// <summary>Payments recorded against claims (retention releases are separate).</summary>
    public long PaidOnClaimsMinor { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public Guid? AwardedByUserId { get; private set; }
    public DateTimeOffset? AwardedAtUtc { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancelReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<SubcontractClaim> Claims => _claims;
    public IReadOnlyList<SubcontractPayment> Payments => _payments;
    public IReadOnlyList<SubcontractVariation> Variations => _variations;

    /// <summary>Net payable on certified claims not yet paid. Never negative.</summary>
    public long OutstandingMinor => NetPayableMinor - PaidOnClaimsMinor;

    /// <summary>Retention still held: held on certified claims minus what was released. Never negative.</summary>
    public long RetentionBalanceMinor => RetentionHeldMinor - RetentionReleasedMinor;

    /// <summary>Certified work as a share of the revised sum, in basis points; null when the revised sum is zero.</summary>
    public static int? PercentCertifiedBps(long certifiedWorkMinor, long revisedSumMinor) =>
        revisedSumMinor <= 0 ? null : (int)LineAmounts.RoundToMinor(certifiedWorkMinor * 10_000m / revisedSumMinor);

    public static string FormatNumber(long sequence) => $"SC-{sequence:D4}";

    // ---- Lifecycle --------------------------------------------------------------------------------------------------------------

    public static Subcontract Create(Guid id, Guid tenantId, Supplier subcontractor, Project project, Guid? subProjectId, string? title, string? notes,
        long contractSumMinor, VatTreatment vatTreatment, int vatRateBps, int retentionBps, int withholdingBps, Guid createdByUserId,
        string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureCanReceiveRecordsFor(subProjectId);
        subcontractor.EnsureCanBeUsedAs(SupplierKind.Subcontractor);
        var subcontract = new Subcontract
        {
            ProjectId = project.Id,
            CreatedByUserId = createdByUserId,
            VatRateBps = vatRateBps,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        subcontract.Initialize(id, tenantId, now);
        subcontract.ApplyTerms(subcontractor.Id, subProjectId, title, notes, contractSumMinor, vatTreatment, retentionBps, withholdingBps);
        return subcontract;
    }

    /// <summary>
    /// Places the work in the standard work breakdown, or clears it. Only while drafting: the work item decides where every certified claim's
    /// cost is counted, so it is not changed once claims can exist.
    /// </summary>
    public void SetWorkItem(string? wbsCode)
    {
        if (Status != SubcontractStatus.Draft) throw new DomainRuleException("The work item can only be changed while the subcontract is a draft.");
        var code = Guard.OptionalText(wbsCode, "wbsCode", "the work item", 20);
        if (code is not null && WbsCatalog.FindByCode(code) is null)
            throw new ValidationFailedException("wbsCode", "Choose a work item from the list, or leave it empty so the cost shows as unclassified.");
        WbsCode = code is null ? null : WbsCatalog.FindByCode(code)!.Code;
    }

    /// <summary>Gives the subcontract its number. Taken after the details pass their checks, so a refused form uses no number.</summary>
    public void AssignNumber(string number)
    {
        if (Number.Length > 0) throw new InvalidOperationException("The subcontract already has a number.");
        Number = Guard.RequiredText(number, "number", "the number", 20);
    }

    /// <summary>Changes a draft. The VAT rate stays the one the subcontract was made with; an awarded subcontract changes by variations.</summary>
    public void UpdateDraft(Supplier subcontractor, Project project, Guid? subProjectId, string? title, string? notes, long contractSumMinor,
        VatTreatment vatTreatment, int retentionBps, int withholdingBps, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != SubcontractStatus.Draft)
            throw new DomainRuleException("Only a draft subcontract can be edited. Record a variation to change the sum of an awarded one.");
        project.EnsureCanReceiveRecordsFor(subProjectId);
        if (subcontractor.Id != SupplierId) subcontractor.EnsureCanBeUsedAs(SupplierKind.Subcontractor);
        ApplyTerms(subcontractor.Id, subProjectId, title, notes, contractSumMinor, vatTreatment, retentionBps, withholdingBps);
        Touch(now);
    }

    /// <summary>Awards the work: the subcontract becomes active and its sum counts as committed cost of the project.</summary>
    public void Award(Supplier subcontractor, Project project, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != SubcontractStatus.Draft) throw new DomainRuleException("Only a draft subcontract can be awarded.");
        if (subcontractor.Id != SupplierId) throw new ArgumentException("The firm doesn't match this subcontract.", nameof(subcontractor));
        project.EnsureCanReceiveRecordsFor(SubProjectId);
        subcontractor.EnsureCanBeUsedAs(SupplierKind.Subcontractor);
        Status = SubcontractStatus.Active;
        AwardedByUserId = userId;
        AwardedAtUtc = now;
        Touch(now);
    }

    /// <summary>The work is finished. No more claims; retention can then be released. A draft claim must be certified or discarded first.</summary>
    public void Complete(Guid userId, DateTimeOffset now)
    {
        if (Status != SubcontractStatus.Active) throw new DomainRuleException("Only an active subcontract can be completed.");
        if (_claims.Any(claim => claim.IsDraft))
            throw new DomainRuleException("A draft claim is still open. Certify or discard it before completing the subcontract.");
        Status = SubcontractStatus.Completed;
        CompletedByUserId = userId;
        CompletedAtUtc = now;
        Touch(now);
    }

    /// <summary>Calls off a draft, or an active subcontract with no certified work. Draft claims are discarded with the same reason.</summary>
    public void Cancel(string? reason, Guid userId, DateTimeOffset now)
    {
        if (Status is SubcontractStatus.Completed or SubcontractStatus.Cancelled)
            throw new DomainRuleException(Status == SubcontractStatus.Cancelled
                ? "This subcontract was already cancelled."
                : "A completed subcontract can't be cancelled.");
        if (_claims.Any(claim => claim.IsCertified))
            throw new DomainRuleException("Work has been certified on this subcontract, so it can't be cancelled. Complete it instead, or void its claims first.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for cancelling", 300);
        foreach (var draft in _claims.Where(claim => claim.IsDraft)) draft.Void(cleanReason, userId, now);
        Status = SubcontractStatus.Cancelled;
        CancelReason = cleanReason;
        CancelledByUserId = userId;
        CancelledAtUtc = now;
        Recalculate();
        Touch(now);
    }

    /// <summary>Adds or omits work. The revised sum stays above zero and never below the work already certified.</summary>
    public SubcontractVariation AddVariation(Guid id, string? description, long amountMinor, DateOnly variationDate, Guid userId, DateOnly today,
        DateTimeOffset now)
    {
        if (Status is not (SubcontractStatus.Draft or SubcontractStatus.Active))
            throw new DomainRuleException("Variations can be added only to a draft or active subcontract.");
        if (_variations.Count >= MaximumVariations)
            throw new DomainRuleException($"A subcontract can have at most {MaximumVariations} variations.");

        var errors = new ValidationErrors();
        var cleanDescription = Field(errors, () => Guard.RequiredText(description, "description", "what the variation changes", 200));
        CheckDate(errors, variationDate, "variationDate", "the date of the variation", today);
        if (amountMinor == 0) errors.Add("amountMinor", "Enter the amount the variation adds, or a negative amount it takes off.");
        else if (amountMinor is > MoneyLimits.MaximumAmountMinor or < -MoneyLimits.MaximumAmountMinor)
            errors.Add("amountMinor", "A variation can be at most ₱100,000,000,000.00.");
        else
        {
            var revised = RevisedSumMinor + amountMinor;
            if (revised <= 0) errors.Add("amountMinor", "The revised contract sum must stay above zero. Cancel the subcontract instead if no work remains.");
            else if (revised < CertifiedWorkMinor)
                errors.Add("amountMinor", $"The revised contract sum can't be less than the {Peso(CertifiedWorkMinor)} of work already certified.");
            else if (LineAmounts.FromBase(revised, VatTreatment, VatRateBps).GrossMinor > MoneyLimits.MaximumAmountMinor)
                errors.Add("amountMinor", "The revised contract sum can be at most ₱100,000,000,000.00.");
        }
        errors.ThrowIfAny();

        var variation = new SubcontractVariation(id, TenantId, Id, _variations.Count + 1, cleanDescription!, amountMinor, variationDate, userId, now);
        _variations.Add(variation);
        Recalculate();
        Touch(now);
        return variation;
    }

    // ---- Claims -----------------------------------------------------------------------------------------------------------------

    /// <summary>What a claim for this much work to date would be, without recording it.</summary>
    public ClaimBreakdown PreviewClaim(long cumulativeWorkMinor)
    {
        EnsureActive("recording claims");
        var errors = new ValidationErrors();
        CheckCumulative(errors, cumulativeWorkMinor);
        errors.ThrowIfAny();
        return BreakdownFor(cumulativeWorkMinor);
    }

    /// <summary>Records a draft claim for the work done to date. Only one draft is open at a time.</summary>
    public SubcontractClaim DraftClaim(Guid id, DateOnly claimDate, DateOnly periodEnd, long cumulativeWorkMinor, string? notes, Guid userId,
        string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        EnsureActive("recording claims");
        if (_claims.Any(claim => claim.IsDraft))
            throw new DomainRuleException("A draft claim is already open. Certify or discard it before starting another.");
        if (_claims.Count >= MaximumClaims) throw new DomainRuleException($"A subcontract can have at most {MaximumClaims} claims.");

        var cleanNotes = ValidateClaim(claimDate, periodEnd, cumulativeWorkMinor, notes, today);
        var claim = new SubcontractClaim(id, TenantId, Id, claimDate, periodEnd, BreakdownFor(cumulativeWorkMinor), cleanNotes, userId,
            Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80), now);
        _claims.Add(claim);
        Touch(now);
        return claim;
    }

    /// <summary>Changes a draft claim. It is worked out again on the certified work as it stands now, which also brings a stale draft up to date.</summary>
    public SubcontractClaim UpdateDraftClaim(Guid claimId, DateOnly claimDate, DateOnly periodEnd, long cumulativeWorkMinor, string? notes,
        DateOnly today, DateTimeOffset now)
    {
        EnsureActive("changing claims");
        var claim = FindClaim(claimId);
        if (!claim.IsDraft) throw new DomainRuleException("Only a draft claim can be edited.");
        var cleanNotes = ValidateClaim(claimDate, periodEnd, cumulativeWorkMinor, notes, today);
        claim.Revise(claimDate, periodEnd, BreakdownFor(cumulativeWorkMinor), cleanNotes);
        Touch(now);
        return claim;
    }

    /// <summary>Checks that a draft claim can be certified now, without changing anything (so a claim number is taken only when it can).</summary>
    public SubcontractClaim EnsureCanCertify(Guid claimId)
    {
        EnsureActive("certifying claims");
        var claim = FindClaim(claimId);
        if (!claim.IsDraft)
            throw new DomainRuleException(claim.IsCertified ? "This claim is already certified." : "This claim was voided. Record a new claim instead.");
        if (claim.PreviousCumulativeMinor != CertifiedWorkMinor)
            throw new DomainRuleException(
                $"This claim was worked out on {Peso(claim.PreviousCumulativeMinor)} of work certified before it, but the certified work is now " +
                $"{Peso(CertifiedWorkMinor)}. Open the claim and save it again to bring it up to date, then certify it.");
        if (claim.CumulativeWorkMinor > RevisedSumMinor)
            throw new DomainRuleException(
                $"The work to date on this claim ({Peso(claim.CumulativeWorkMinor)}) is more than the revised contract sum ({Peso(RevisedSumMinor)}). " +
                "Edit the claim, or record a variation first.");
        return claim;
    }

    /// <summary>Certifies a draft claim: it gets its number and is linked to the cost entry that posts its work to the project in the same save.</summary>
    public SubcontractClaim CertifyClaim(Guid claimId, int claimNumber, Guid costEntryId, Guid userId, DateTimeOffset now)
    {
        var claim = EnsureCanCertify(claimId);
        if (claimNumber <= 0) throw new ArgumentOutOfRangeException(nameof(claimNumber));
        if (costEntryId == Guid.Empty) throw new ArgumentException("A certified claim posts a cost.", nameof(costEntryId));
        claim.Certify(claimNumber, costEntryId, userId, now);
        Recalculate();
        Touch(now);
        return claim;
    }

    /// <summary>Discards a draft claim with a reason. It stays on record and never counted.</summary>
    public void DiscardClaim(Guid claimId, string? reason, Guid userId, DateTimeOffset now)
    {
        var claim = FindClaim(claimId);
        if (!claim.IsDraft)
            throw new DomainRuleException(claim.IsCertified
                ? "This claim is certified. Void it instead, which also voids its cost."
                : "This claim was already voided.");
        claim.Void(Guard.RequiredText(reason, "reason", "the reason for discarding this claim", 300), userId, now);
        Touch(now);
    }

    /// <summary>
    /// Voids a certified claim (its cost entry is voided by the caller in the same save). Only the latest certified claim, only while no payment
    /// is recorded against it, and never so that more retention would have been released than is held.
    /// </summary>
    public SubcontractClaim VoidClaim(Guid claimId, string? reason, Guid userId, DateTimeOffset now)
    {
        var claim = FindClaim(claimId);
        if (!claim.IsCertified)
            throw new DomainRuleException(claim.IsDraft
                ? "This claim is still a draft. Discard it instead."
                : "This claim was already voided.");
        var latest = _claims.Where(item => item.IsCertified).MaxBy(item => item.ClaimNumber)!;
        if (latest.Id != claim.Id)
            throw new DomainRuleException($"Only the latest certified claim can be voided. Void claim {latest.ClaimNumber} first.");
        if (_payments.Any(payment => payment.IsRecorded && payment.ClaimId == claim.Id))
            throw new DomainRuleException("Payments are recorded against this claim. Void them first, then void the claim.");
        if (RetentionHeldMinor - claim.RetentionMinor < RetentionReleasedMinor)
            throw new DomainRuleException("More retention has been released than would still be held. Void the retention payment first.");

        claim.Void(Guard.RequiredText(reason, "reason", "the reason for voiding this claim", 300), userId, now);
        Recalculate();
        Touch(now);
        return claim;
    }

    // ---- Payments ---------------------------------------------------------------------------------------------------------------

    /// <summary>Records that part or all of a certified claim's net payable was paid. Never more than is still owed on the claim.</summary>
    public SubcontractPayment RecordClaimPayment(Guid id, Guid claimId, long amountMinor, DateOnly paidOn, SubcontractPaymentMethod method,
        string? reference, string? note, Guid userId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        if (Status is not (SubcontractStatus.Active or SubcontractStatus.Completed))
            throw new DomainRuleException("Payments can be recorded only on an active or completed subcontract.");
        var claim = FindClaim(claimId);
        if (!claim.IsCertified) throw new DomainRuleException("Certify the claim before recording a payment against it.");
        var outstanding = ClaimOutstandingMinor(claim);
        if (outstanding <= 0) throw new DomainRuleException("This claim is already fully paid.");

        var errors = new ValidationErrors();
        CheckPaymentAmount(errors, amountMinor, outstanding, "still owed on this claim");
        CheckDate(errors, paidOn, "paidOn", "the date it was paid", today);
        if (paidOn != default && paidOn < claim.ClaimDate) errors.Add("paidOn", "The payment can't be dated before the claim.");
        var (cleanReference, cleanNote) = CheckPaymentDetails(errors, method, reference, note);
        errors.ThrowIfAny();
        return AddPayment(new SubcontractPayment(id, TenantId, Id, SubcontractPaymentKind.Claim, claim.Id, amountMinor, paidOn, method, cleanReference,
            cleanNote, userId, Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80), now), now);
    }

    /// <summary>Records that retention held was paid out. Only once the subcontract is completed, and never more than is still held.</summary>
    public SubcontractPayment RecordRetentionRelease(Guid id, long amountMinor, DateOnly paidOn, SubcontractPaymentMethod method, string? reference,
        string? note, Guid userId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        if (Status != SubcontractStatus.Completed)
            throw new DomainRuleException("Retention is released once the subcontract is completed. Complete it first.");
        if (RetentionBalanceMinor <= 0) throw new DomainRuleException("No retention is held on this subcontract.");

        var errors = new ValidationErrors();
        CheckPaymentAmount(errors, amountMinor, RetentionBalanceMinor, "retention still held");
        CheckDate(errors, paidOn, "paidOn", "the date it was paid", today);
        var (cleanReference, cleanNote) = CheckPaymentDetails(errors, method, reference, note);
        errors.ThrowIfAny();
        return AddPayment(new SubcontractPayment(id, TenantId, Id, SubcontractPaymentKind.RetentionRelease, null, amountMinor, paidOn, method,
            cleanReference, cleanNote, userId, Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80), now), now);
    }

    public SubcontractPayment VoidPayment(Guid paymentId, string? reason, Guid userId, DateTimeOffset now)
    {
        var payment = _payments.FirstOrDefault(item => item.Id == paymentId) ?? throw new NotFoundException("that payment");
        if (!payment.IsRecorded) throw new DomainRuleException("This payment was already voided.");
        payment.Void(Guard.RequiredText(reason, "reason", "the reason for voiding this payment", 300), userId, now);
        Recalculate();
        Touch(now);
        return payment;
    }

    /// <summary>A certified claim's net payable minus the payments recorded against it.</summary>
    public long ClaimOutstandingMinor(SubcontractClaim claim) =>
        claim.IsCertified ? claim.NetPayableMinor - _payments.Where(item => item.IsRecorded && item.ClaimId == claim.Id).Sum(item => item.AmountMinor) : 0;

    public SubcontractClaim FindClaim(Guid claimId) =>
        _claims.FirstOrDefault(claim => claim.Id == claimId) ?? throw new NotFoundException("that claim");

    // ---- Internals --------------------------------------------------------------------------------------------------------------

    private void ApplyTerms(Guid supplierId, Guid? subProjectId, string? title, string? notes, long contractSumMinor, VatTreatment vatTreatment,
        int retentionBps, int withholdingBps)
    {
        var errors = new ValidationErrors();
        var cleanTitle = Field(errors, () => Guard.RequiredText(title, "title", "the scope of work", 200));
        var cleanNotes = Field(errors, () => Guard.OptionalText(notes, "notes", "the notes", 500));
        var vatKnown = Enum.IsDefined(vatTreatment);
        if (!vatKnown) errors.Add("vatTreatment", "Choose whether the contract sum includes VAT.");
        if (VatRateBps is < 0 or > LineAmounts.MaximumVatRateBps) errors.Add("vatTreatment", "The VAT rate must be between 0% and 100%.");
        if (retentionBps is < 0 or > MaximumRetentionBps) errors.Add("retentionBps", "Retention can be from 0% to 30%.");
        if (withholdingBps is < 0 or > MaximumWithholdingBps) errors.Add("withholdingBps", "Withholding tax can be from 0% to 10%.");

        var variations = _variations.Sum(item => item.AmountMinor);
        if (contractSumMinor <= 0) errors.Add("contractSumMinor", "Enter the contract sum, more than zero.");
        else if (contractSumMinor > MoneyLimits.MaximumAmountMinor) errors.Add("contractSumMinor", "The contract sum can be at most ₱100,000,000,000.00.");
        else if (contractSumMinor + variations <= 0)
            errors.Add("contractSumMinor", "With its variations the contract sum would fall to zero or less. Enter a larger sum.");
        else if (vatKnown && LineAmounts.FromBase(contractSumMinor + variations, vatTreatment, VatRateBps).GrossMinor > MoneyLimits.MaximumAmountMinor)
            errors.Add("contractSumMinor", "With VAT the contract sum can be at most ₱100,000,000,000.00.");
        errors.ThrowIfAny();

        SupplierId = supplierId;
        SubProjectId = subProjectId;
        Title = cleanTitle!;
        Notes = cleanNotes;
        ContractSumMinor = contractSumMinor;
        VatTreatment = vatTreatment;
        RetentionBps = retentionBps;
        WithholdingBps = withholdingBps;
        Recalculate();
    }

    /// <summary>Works the stored totals out again from the variations, claims and payments. Called after every change.</summary>
    private void Recalculate()
    {
        RevisedSumMinor = ContractSumMinor + _variations.Sum(item => item.AmountMinor);
        var revised = LineAmounts.FromBase(RevisedSumMinor, VatTreatment, VatRateBps);
        RevisedNetMinor = revised.NetMinor;
        RevisedVatMinor = revised.VatMinor;
        RevisedGrossMinor = revised.GrossMinor;

        var certified = _claims.Where(claim => claim.IsCertified).ToArray();
        CertifiedWorkMinor = certified.Sum(claim => claim.ThisClaimBaseMinor);
        CertifiedNetMinor = certified.Sum(claim => claim.NetMinor);
        CertifiedGrossMinor = certified.Sum(claim => claim.GrossMinor);
        RetentionHeldMinor = certified.Sum(claim => claim.RetentionMinor);
        WithholdingMinor = certified.Sum(claim => claim.WithholdingMinor);
        NetPayableMinor = certified.Sum(claim => claim.NetPayableMinor);

        var recorded = _payments.Where(payment => payment.IsRecorded).ToArray();
        PaidOnClaimsMinor = recorded.Where(payment => payment.Kind == SubcontractPaymentKind.Claim).Sum(payment => payment.AmountMinor);
        RetentionReleasedMinor = recorded.Where(payment => payment.Kind == SubcontractPaymentKind.RetentionRelease).Sum(payment => payment.AmountMinor);
    }

    private ClaimBreakdown BreakdownFor(long cumulativeWorkMinor) =>
        ClaimBreakdown.Calculate(cumulativeWorkMinor, CertifiedWorkMinor, VatTreatment, VatRateBps, RetentionBps, WithholdingBps);

    private string? ValidateClaim(DateOnly claimDate, DateOnly periodEnd, long cumulativeWorkMinor, string? notes, DateOnly today)
    {
        var errors = new ValidationErrors();
        CheckDate(errors, claimDate, "claimDate", "the claim date", today);
        if (periodEnd == default) errors.Add("periodEnd", "Choose the last day of the period the claim covers.");
        else if (periodEnd < EarliestDate) errors.Add("periodEnd", "Choose a valid date.");
        else if (claimDate != default && periodEnd > claimDate) errors.Add("periodEnd", "The period can't end after the claim date.");
        CheckCumulative(errors, cumulativeWorkMinor);
        var cleanNotes = Field(errors, () => Guard.OptionalText(notes, "notes", "the notes", 300));
        errors.ThrowIfAny();
        return cleanNotes;
    }

    private void CheckCumulative(ValidationErrors errors, long cumulativeWorkMinor)
    {
        if (cumulativeWorkMinor <= CertifiedWorkMinor)
            errors.Add("cumulativeWorkMinor", CertifiedWorkMinor == 0
                ? "Enter the value of the work done to date, more than zero."
                : $"Enter the value of all work done to date. It must be more than the {Peso(CertifiedWorkMinor)} already certified.");
        else if (cumulativeWorkMinor > RevisedSumMinor)
            errors.Add("cumulativeWorkMinor",
                $"Work done to date can't be more than the revised contract sum of {Peso(RevisedSumMinor)}. Record a variation first if the work grew.");
    }

    private static void CheckPaymentAmount(ValidationErrors errors, long amountMinor, long limitMinor, string what)
    {
        if (amountMinor <= 0) errors.Add("amountMinor", "Enter the amount paid, more than zero.");
        else if (amountMinor > limitMinor) errors.Add("amountMinor", $"That is more than the {Peso(limitMinor)} {what}.");
    }

    private static (string? Reference, string? Note) CheckPaymentDetails(ValidationErrors errors, SubcontractPaymentMethod method, string? reference,
        string? note)
    {
        if (!Enum.IsDefined(method)) errors.Add("method", "Choose how it was paid.");
        var cleanReference = Field(errors, () => Guard.OptionalText(reference, "reference", "the reference", 80));
        var cleanNote = Field(errors, () => Guard.OptionalText(note, "note", "the note", 300));
        return (cleanReference, cleanNote);
    }

    private static void CheckDate(ValidationErrors errors, DateOnly date, string field, string label, DateOnly today)
    {
        if (date == default) errors.Add(field, $"Choose {label}.");
        else if (date < EarliestDate) errors.Add(field, "Choose a valid date.");
        else if (date > today) errors.Add(field, "The date can't be in the future.");
    }

    private SubcontractPayment AddPayment(SubcontractPayment payment, DateTimeOffset now)
    {
        if (_payments.Count >= MaximumPayments) throw new DomainRuleException($"A subcontract can have at most {MaximumPayments} payments.");
        _payments.Add(payment);
        Recalculate();
        Touch(now);
        return payment;
    }

    private void EnsureActive(string doing)
    {
        switch (Status)
        {
            case SubcontractStatus.Active: return;
            case SubcontractStatus.Draft: throw new DomainRuleException($"Award the subcontract before {doing}.");
            case SubcontractStatus.Completed: throw new DomainRuleException($"This subcontract is completed, so it is closed to {doing}.");
            default: throw new DomainRuleException($"This subcontract was cancelled, so it is closed to {doing}.");
        }
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this subcontract.", nameof(project));
    }

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}
