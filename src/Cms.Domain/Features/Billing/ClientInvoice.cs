using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// What a project's issued bills add up to right now: the value of the work billed (VAT included), the retention the client held back
/// on those bills, and what was released again; the down payment billed (with the rate its bill keeps) and what the progress bills
/// recovered of it. Drafts and void bills never count.
/// </summary>
public sealed record BillingLedger(long BilledToDateMinor, long RetentionWithheldMinor, long RetentionReleasedMinor, long AdvanceMinor = 0,
    int AdvanceBps = 0, long AdvanceRecoveredMinor = 0)
{
    public static BillingLedger Empty { get; } = new(0, 0, 0);

    /// <summary>Retention the client still holds: withheld on progress bills minus what release bills gave back.</summary>
    public long RetentionHeldMinor => RetentionWithheldMinor - RetentionReleasedMinor;

    /// <summary>The down payment still to be recovered from progress bills.</summary>
    public long AdvanceToRecoverMinor => AdvanceMinor - AdvanceRecoveredMinor;
}

/// <summary>
/// One bill to the client. A <see cref="ClientInvoiceKind.Progress"/> bill states the value of all work billed to date including itself
/// (VAT included, like the contract price) and bills the difference from the previous issued bill, less its share of the down payment;
/// a <see cref="ClientInvoiceKind.Advance"/> bill asks for the down payment before any progress bill; a
/// <see cref="ClientInvoiceKind.RetentionRelease"/> bill gives back retention once the project is completed. A bill is prepared as a
/// draft, issued (numbered and locked) by an approver, and voided with a reason; it is never deleted. Its VAT rate, retention,
/// withholding and payment terms are copied in when it is prepared, so later changes to the company or the project never alter it.
/// Payments the client made against it are recorded by a person and belong to it.
/// </summary>
/// <remarks>
/// The issued bills of a project form one chain: each issued bill points at the one issued before it (<see cref="PreviousInvoiceId"/>)
/// and the bill before points back (<see cref="NextInvoiceId"/>). Issuing a bill changes the bill before it, and voiding the latest bill
/// changes the one before it, so two people issuing or voiding at the same moment change the same record and the database lets only
/// one of them through: the chain can never fork or skip a voided bill.
/// </remarks>
public sealed class ClientInvoice : AggregateRoot
{
    public const int DescriptionMaxLength = 200;
    public const int ReasonMaxLength = 300;

    /// <summary>A bill is paid in a handful of instalments; this bound keeps one bill's page and checks small.</summary>
    public const int MaximumPayments = 100;

    private readonly List<ClientPayment> _payments = [];

    private ClientInvoice() { }

    public Guid ProjectId { get; private set; }
    public ClientInvoiceKind Kind { get; private set; }
    public ClientInvoiceStatus Status { get; private set; } = ClientInvoiceStatus.Draft;

    /// <summary>"BILL-0001", taken from the company's sequence when the bill is issued. Null while it is a draft.</summary>
    public string? Number { get; private set; }

    public long? SequenceNumber { get; private set; }

    /// <summary>The bill's place among the project's issued bills (1, 2, 3...), set when it is issued.</summary>
    public int? ChainIndex { get; private set; }

    public Guid? PreviousInvoiceId { get; private set; }
    public Guid? NextInvoiceId { get; private set; }

    public DateOnly BillingDate { get; private set; }

    /// <summary>The billing date plus the payment terms the bill was made with.</summary>
    public DateOnly DueDate { get; private set; }

    public string Description { get; private set; } = "";

    /// <summary>Progress bills: the value of all work billed to date including this bill, VAT included. Null on a retention release.</summary>
    public long? CumulativeWorkMinor { get; private set; }

    /// <summary>Progress bills: what had been billed to date when this bill was prepared. Issuing checks it is still true.</summary>
    public long PreviousCumulativeWorkMinor { get; private set; }

    public long GrossMinor { get; private set; }
    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public int VatRateBps { get; private set; }
    public int RetentionBps { get; private set; }
    public long RetentionMinor { get; private set; }
    public int WithholdingBps { get; private set; }
    public long WithholdingMinor { get; private set; }

    /// <summary>Progress bills: the share of the amount before VAT the client withholds as VAT, and that amount.</summary>
    public int VatWithholdingBps { get; private set; }

    public long VatWithholdingMinor { get; private set; }

    /// <summary>
    /// The down payment rate. On a down payment bill, the rate it was billed at (recovery always uses it, never the terms as later
    /// changed); on a progress bill, the rate its recovery was worked out at (0 when no down payment was issued).
    /// </summary>
    public int AdvanceBps { get; private set; }

    /// <summary>Progress bills: the part of the down payment this bill recovers. Derived from the chain, never kept on the down payment.</summary>
    public long AdvanceRecoveryMinor { get; private set; }

    /// <summary>What the client is asked to pay for this bill: gross less retention, both withholdings and the down payment recovered.</summary>
    public long NetReceivableMinor { get; private set; }

    public int PaymentTermsDays { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? IssuedByUserId { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<ClientPayment> Payments => _payments;

    public long PaidMinor => _payments.Where(payment => !payment.IsVoided).Sum(payment => payment.AmountMinor);

    /// <summary>What is still to collect. Only an issued bill is owed; it never goes below zero because a payment can't exceed it.</summary>
    public long OutstandingMinor => Status == ClientInvoiceStatus.Issued ? NetReceivableMinor - PaidMinor : 0;

    public bool IsOverdue(DateOnly today) => Status == ClientInvoiceStatus.Issued && OutstandingMinor > 0 && today > DueDate;

    public static string FormatNumber(long sequenceNumber) => $"BILL-{sequenceNumber:D4}";

    // ---- Drafts ------------------------------------------------------------------------------------------------------------

    public static ClientInvoice DraftProgress(Guid id, Guid tenantId, Project project, DateOnly billingDate, string? description, long cumulativeWorkMinor,
        BillingLedger ledger, BillingTermsSnapshot terms, int vatRateBps, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        var invoice = NewDraft(ClientInvoiceKind.Progress, project, createdByUserId, clientRequestId);
        invoice.ApplyProgress(project, billingDate, description, cumulativeWorkMinor, ledger, terms, vatRateBps, today);
        invoice.Initialize(id, tenantId, now);
        return invoice;
    }

    public static ClientInvoice DraftRetentionRelease(Guid id, Guid tenantId, Project project, DateOnly billingDate, string? description, long amountMinor,
        BillingLedger ledger, BillingTermsSnapshot terms, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        var invoice = NewDraft(ClientInvoiceKind.RetentionRelease, project, createdByUserId, clientRequestId);
        invoice.ApplyRelease(project, billingDate, description, amountMinor, ledger, terms, today);
        invoice.Initialize(id, tenantId, now);
        return invoice;
    }

    /// <summary>
    /// Prepares the down payment bill: the terms' share of the contract price. It must come before the first progress bill, and a
    /// project has one live (not void) down payment bill at most.
    /// </summary>
    /// <param name="anotherAdvanceExists">Whether the project already has a down payment bill that is not void (a draft or issued).</param>
    public static ClientInvoice DraftAdvance(Guid id, Guid tenantId, Project project, DateOnly billingDate, string? description, BillingLedger ledger,
        bool anotherAdvanceExists, BillingTermsSnapshot terms, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        if (anotherAdvanceExists) throw new DomainRuleException(AdvanceExistsMessage);
        var invoice = NewDraft(ClientInvoiceKind.Advance, project, createdByUserId, clientRequestId);
        invoice.ApplyAdvance(project, billingDate, description, ledger, terms, today);
        invoice.Initialize(id, tenantId, now);
        return invoice;
    }

    /// <summary>
    /// Saves a draft again with new figures, worked out from what is billed now and the terms and VAT rate in force now. This is also
    /// how a draft made stale by another bill is brought up to date.
    /// </summary>
    /// <param name="amountMinor">Work completed to date for a progress bill; the retention to release for a release; unused for a down
    /// payment, which is worked out from the contract price and the terms.</param>
    public void UpdateDraft(Project project, DateOnly billingDate, string? description, long amountMinor, BillingLedger ledger, BillingTermsSnapshot terms,
        int vatRateBps, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != ClientInvoiceStatus.Draft)
            throw new DomainRuleException(Status == ClientInvoiceStatus.Issued
                ? "An issued bill can't be changed. Void it and prepare a new one if it is wrong."
                : "This bill was voided and can't be changed.");
        switch (Kind)
        {
            case ClientInvoiceKind.Progress:
                ApplyProgress(project, billingDate, description, amountMinor, ledger, terms, vatRateBps, today);
                break;
            case ClientInvoiceKind.Advance:
                ApplyAdvance(project, billingDate, description, ledger, terms, today);
                break;
            default:
                ApplyRelease(project, billingDate, description, amountMinor, ledger, terms, today);
                break;
        }
        Touch(now);
    }

    /// <summary>
    /// Whether a draft's figures still follow from what is billed now. A stale draft is refused at issue. A progress draft is stale when
    /// another bill was issued or voided since it was saved, or when the down payment it should recover changed (one was issued, or the
    /// contract price made it the last bill). A down payment draft is never stale: once progress is billed it can only be discarded.
    /// </summary>
    public bool IsStale(BillingLedger ledger, long? contractPriceMinor) =>
        Status == ClientInvoiceStatus.Draft && Kind switch
        {
            ClientInvoiceKind.Progress => !FollowsFrom(ledger, contractPriceMinor),
            ClientInvoiceKind.Advance => false,
            _ => GrossMinor > ledger.RetentionHeldMinor
        };

    /// <summary>A progress bill's previous work and down payment recovery are what the issued chain gives now.</summary>
    private bool FollowsFrom(BillingLedger ledger, long? contractPriceMinor) =>
        PreviousCumulativeWorkMinor == ledger.BilledToDateMinor &&
        (contractPriceMinor is not { } price || CumulativeWorkMinor is not { } cumulative ||
            AdvanceRecoveryMinor == BillingCalculator.AdvanceRecovery(cumulative, price, ledger));

    /// <summary>Discards a draft with a reason. It stays on record as void, with no number.</summary>
    public void Discard(Project project, string? reason, Guid discardedByUserId, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != ClientInvoiceStatus.Draft)
            throw new DomainRuleException(Status == ClientInvoiceStatus.Issued
                ? "An issued bill can't be discarded. Void it instead."
                : "This draft was already discarded.");
        project.EnsureNotArchived();
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for discarding this draft", ReasonMaxLength);
        Status = ClientInvoiceStatus.Void;
        VoidedByUserId = discardedByUserId;
        VoidedAtUtc = now;
        Touch(now);
    }

    // ---- Issue and void (called through BillingChain, which also updates the neighbouring bill) ------------------------------------

    /// <summary>
    /// Checks that the draft can be issued now, before a number is taken. The figures were worked out when the draft was saved;
    /// another bill issued or voided since then makes them wrong, and the draft must be saved again first.
    /// </summary>
    public void EnsureCanIssue(Project project, BillingLedger ledger)
    {
        EnsureProject(project);
        if (Status != ClientInvoiceStatus.Draft)
            throw new DomainRuleException(Status == ClientInvoiceStatus.Issued ? "This bill was already issued." : "A void bill can't be issued.");
        project.EnsureNotArchived();
        if (Kind == ClientInvoiceKind.Progress)
        {
            var price = project.ContractPriceMinor ?? throw new DomainRuleException(NoContractPriceMessage);
            if (PreviousCumulativeWorkMinor != ledger.BilledToDateMinor)
                throw new DomainRuleException(StaleDraftMessage);
            if (CumulativeWorkMinor > price)
                throw new DomainRuleException(
                    "The contract price is now less than the work completed to date on this draft. Open the draft, check the figures and save it again.");
            // A down payment issued since the draft was saved (or a price change that makes this the last bill) changes its recovery.
            if (!FollowsFrom(ledger, price))
                throw new DomainRuleException(StaleDraftMessage);
        }
        else if (Kind == ClientInvoiceKind.Advance)
        {
            if (ledger.BilledToDateMinor > 0) throw new DomainRuleException(AdvanceAfterProgressMessage);
            if (ledger.AdvanceMinor > 0) throw new DomainRuleException(AdvanceExistsMessage);
        }
        else
        {
            EnsureCompleted(project);
            if (GrossMinor > ledger.RetentionHeldMinor)
                throw new DomainRuleException(
                    "Less retention is held now than when this release was prepared. Open the draft, check the amount and save it again.");
        }
    }

    internal void MarkIssued(Project project, BillingLedger ledger, long sequenceNumber, int chainIndex, Guid? previousInvoiceId, Guid issuedByUserId,
        DateTimeOffset now)
    {
        EnsureCanIssue(project, ledger);
        if (sequenceNumber <= 0) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        SequenceNumber = sequenceNumber;
        Number = FormatNumber(sequenceNumber);
        ChainIndex = chainIndex;
        PreviousInvoiceId = previousInvoiceId;
        NextInvoiceId = null;
        Status = ClientInvoiceStatus.Issued;
        IssuedByUserId = issuedByUserId;
        IssuedAtUtc = now;
        Touch(now);
    }

    internal void MarkVoided(Project project, string? latestNumber, string? reason, Guid voidedByUserId, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != ClientInvoiceStatus.Issued)
            throw new DomainRuleException(Status == ClientInvoiceStatus.Draft
                ? "A draft isn't voided: discard it instead."
                : "This bill was already voided.");
        project.EnsureNotArchived();
        if (NextInvoiceId is not null)
            throw new DomainRuleException(latestNumber is null
                ? "Only the latest bill of a project can be voided. Void the bills issued after it first."
                : $"Only the latest bill of a project can be voided. Void {latestNumber} first.");
        if (_payments.Any(payment => !payment.IsVoided))
            throw new DomainRuleException("This bill has payments recorded against it. Void those payments first, then void the bill.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for voiding this bill", ReasonMaxLength);

        VoidReason = cleanReason;
        Status = ClientInvoiceStatus.Void;
        VoidedByUserId = voidedByUserId;
        VoidedAtUtc = now;
        Touch(now);
    }

    internal void LinkSuccessor(Guid nextInvoiceId, DateTimeOffset now)
    {
        if (Status != ClientInvoiceStatus.Issued || NextInvoiceId is not null)
            throw new InvalidOperationException("Only the latest issued bill can be followed by another.");
        NextInvoiceId = nextInvoiceId;
        Touch(now);
    }

    internal void UnlinkSuccessor(Guid nextInvoiceId, DateTimeOffset now)
    {
        if (NextInvoiceId != nextInvoiceId) throw new InvalidOperationException("That bill doesn't follow this one.");
        NextInvoiceId = null;
        Touch(now);
    }

    // ---- Payments ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Records money the client paid against this bill. This is a person saying the money was received: the system never collects.
    /// The amount can't be more than what is still to collect, so a bill is never paid more than it asks.
    /// </summary>
    public ClientPayment RecordPayment(Guid paymentId, Project project, long amountMinor, DateOnly paidOn, ClientPaymentMethod method, string? reference,
        string? note, Guid recordedByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        if (Status != ClientInvoiceStatus.Issued)
            throw new DomainRuleException(Status == ClientInvoiceStatus.Draft
                ? "Issue this bill before recording a payment against it."
                : "This bill was voided, so no payment can be recorded against it.");
        var outstanding = OutstandingMinor;
        if (outstanding <= 0) throw new DomainRuleException("This bill is fully paid. There is nothing left to collect on it.");
        if (_payments.Count >= MaximumPayments)
            throw new DomainRuleException($"A bill can have at most {MaximumPayments} payments recorded against it.");

        var errors = new ValidationErrors();
        if (amountMinor <= 0) errors.Add("amountMinor", "Enter the amount received, greater than zero.");
        else if (amountMinor > outstanding) errors.Add("amountMinor", "That is more than what is still to collect on this bill.");
        if (paidOn == default) errors.Add("paidOn", "Choose the date the payment was received.");
        else if (paidOn > today) errors.Add("paidOn", "The payment date can't be in the future.");
        if (!Enum.IsDefined(method)) errors.Add("method", "Choose how the client paid.");
        var cleanReference = OptionalText(errors, reference, "reference", "The reference", 80);
        var cleanNote = OptionalText(errors, note, "note", "The note", ReasonMaxLength);
        var cleanRequestId = OptionalText(errors, clientRequestId, "clientRequestId", "The request id", 80);
        errors.ThrowIfAny();

        var payment = new ClientPayment(paymentId, TenantId, Id, amountMinor, paidOn, method, cleanReference, cleanNote, recordedByUserId, cleanRequestId, now);
        _payments.Add(payment);
        Touch(now);
        return payment;
    }

    /// <summary>Voids a recorded payment with a reason (it was entered by mistake or the cheque bounced). The amount is owed again.</summary>
    public void VoidPayment(Guid paymentId, Project project, string? reason, Guid voidedByUserId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        var payment = _payments.FirstOrDefault(item => item.Id == paymentId) ?? throw new NotFoundException("that payment");
        if (payment.IsVoided) throw new DomainRuleException("This payment was already voided.");
        payment.Void(Guard.RequiredText(reason, "reason", "the reason for voiding this payment", ReasonMaxLength), voidedByUserId, now);
        Touch(now);
    }

    // ---- Rules shared by the drafts ------------------------------------------------------------------------------------------

    public const string NoContractPriceMessage = "This project has no contract price yet. Add it to the project before billing the client.";
    public const string AdvanceAfterProgressMessage = "Bill the down payment before the first progress bill.";
    public const string AdvanceExistsMessage = "This project already has a down payment bill. Open it instead of preparing another.";
    public const string NoAdvanceTermsMessage = "This project's billing terms have no down payment. Set the down payment in the billing terms first.";

    public const string StaleDraftMessage =
        "Another bill was issued or voided after this draft was prepared, so its figures are out of date. Open the draft, check the work completed to date and save it again.";

    private static ClientInvoice NewDraft(ClientInvoiceKind kind, Project project, Guid createdByUserId, string? clientRequestId) => new()
    {
        Kind = kind,
        ProjectId = project.Id,
        CreatedByUserId = createdByUserId,
        ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
    };

    private void ApplyProgress(Project project, DateOnly billingDate, string? description, long cumulativeWorkMinor, BillingLedger ledger,
        BillingTermsSnapshot terms, int vatRateBps, DateOnly today)
    {
        project.EnsureCanReceiveRecordsFor(null);
        var price = project.ContractPriceMinor ?? throw new DomainRuleException(NoContractPriceMessage);
        if (ledger.BilledToDateMinor >= price)
            throw new DomainRuleException("The whole contract price has already been billed. There is nothing more to bill on this project.");

        var errors = new ValidationErrors();
        CheckBillingDate(errors, billingDate, today);
        var cleanDescription = RequiredText(errors, description, "description", "a description of the bill", "The description", DescriptionMaxLength);
        if (cumulativeWorkMinor <= ledger.BilledToDateMinor)
            errors.Add("cumulativeWorkMinor", "Work completed to date must be more than what has already been billed.");
        else if (cumulativeWorkMinor > price)
            errors.Add("cumulativeWorkMinor", "Work completed to date can't be more than the contract price.");
        errors.ThrowIfAny();

        // The recovery uses the rate kept on the issued down payment bill, never the terms as they may have changed since.
        var recovery = BillingCalculator.AdvanceRecovery(cumulativeWorkMinor, price, ledger);
        var amounts = BillingCalculator.Progress(cumulativeWorkMinor - ledger.BilledToDateMinor, vatRateBps, terms.RetentionBps, terms.WithholdingBps,
            terms.VatWithholdingBps, recovery);

        CumulativeWorkMinor = cumulativeWorkMinor;
        PreviousCumulativeWorkMinor = ledger.BilledToDateMinor;
        SetFigures(billingDate, cleanDescription!, amounts, vatRateBps, terms.RetentionBps, terms.WithholdingBps, terms.VatWithholdingBps,
            ledger.AdvanceMinor > 0 ? ledger.AdvanceBps : 0, terms.PaymentTermsDays);
    }

    private void ApplyAdvance(Project project, DateOnly billingDate, string? description, BillingLedger ledger, BillingTermsSnapshot terms, DateOnly today)
    {
        project.EnsureCanReceiveRecordsFor(null);
        var price = project.ContractPriceMinor ?? throw new DomainRuleException(NoContractPriceMessage);
        if (terms.AdvanceBps <= 0) throw new DomainRuleException(NoAdvanceTermsMessage);
        if (ledger.BilledToDateMinor > 0) throw new DomainRuleException(AdvanceAfterProgressMessage);
        if (ledger.AdvanceMinor > 0) throw new DomainRuleException(AdvanceExistsMessage);

        var errors = new ValidationErrors();
        CheckBillingDate(errors, billingDate, today);
        var cleanDescription = RequiredText(errors, description, "description", "a description of the bill", "The description", DescriptionMaxLength);
        errors.ThrowIfAny();

        var amounts = BillingCalculator.Advance(price, terms.AdvanceBps);
        if (amounts.GrossMinor <= 0)
            throw new DomainRuleException("The down payment comes to nothing at this rate. Check the contract price and the down payment rate.");

        CumulativeWorkMinor = null;
        PreviousCumulativeWorkMinor = 0;
        SetFigures(billingDate, cleanDescription!, amounts, 0, 0, 0, 0, terms.AdvanceBps, terms.PaymentTermsDays);
    }

    private void ApplyRelease(Project project, DateOnly billingDate, string? description, long amountMinor, BillingLedger ledger, BillingTermsSnapshot terms,
        DateOnly today)
    {
        project.EnsureCanReceiveRecordsFor(null);
        EnsureCompleted(project);
        if (ledger.RetentionHeldMinor <= 0) throw new DomainRuleException("No retention is held on this project, so there is nothing to release.");

        var errors = new ValidationErrors();
        CheckBillingDate(errors, billingDate, today);
        var cleanDescription = RequiredText(errors, description, "description", "a description of the bill", "The description", DescriptionMaxLength);
        if (amountMinor <= 0) errors.Add("amountMinor", "Enter the amount of retention to release, greater than zero.");
        else if (amountMinor > ledger.RetentionHeldMinor) errors.Add("amountMinor", "That is more than the retention still held on this project.");
        errors.ThrowIfAny();

        CumulativeWorkMinor = null;
        PreviousCumulativeWorkMinor = 0;
        SetFigures(billingDate, cleanDescription!, BillingCalculator.RetentionRelease(amountMinor), 0, 0, 0, 0, 0, terms.PaymentTermsDays);
    }

    private void SetFigures(DateOnly billingDate, string description, BillAmounts amounts, int vatRateBps, int retentionBps, int withholdingBps,
        int vatWithholdingBps, int advanceBps, int paymentTermsDays)
    {
        if (paymentTermsDays is < 0 or > ProjectBillingTerms.MaximumPaymentTermsDays) throw new ArgumentOutOfRangeException(nameof(paymentTermsDays));
        BillingDate = billingDate;
        Description = description;
        GrossMinor = amounts.GrossMinor;
        NetMinor = amounts.NetMinor;
        VatMinor = amounts.VatMinor;
        VatRateBps = vatRateBps;
        RetentionBps = retentionBps;
        RetentionMinor = amounts.RetentionMinor;
        WithholdingBps = withholdingBps;
        WithholdingMinor = amounts.WithholdingMinor;
        VatWithholdingBps = vatWithholdingBps;
        VatWithholdingMinor = amounts.VatWithholdingMinor;
        AdvanceBps = advanceBps;
        AdvanceRecoveryMinor = amounts.AdvanceRecoveryMinor;
        NetReceivableMinor = amounts.NetReceivableMinor;
        PaymentTermsDays = paymentTermsDays;
        DueDate = billingDate.AddDays(paymentTermsDays);
    }

    private static void EnsureCompleted(Project project)
    {
        if (project.Status != ProjectStatus.Completed)
            throw new DomainRuleException("Retention is released once the project is completed. Mark the project completed first.");
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("That is not this bill's project.", nameof(project));
    }

    private static void CheckBillingDate(ValidationErrors errors, DateOnly billingDate, DateOnly today)
    {
        if (billingDate == default) errors.Add("billingDate", "Choose the billing date.");
        else if (billingDate > today) errors.Add("billingDate", "The billing date can't be in the future.");
    }

    private static string? RequiredText(ValidationErrors errors, string? value, string field, string label, string name, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) errors.Add(field, $"Enter {label}.");
        else if (text.Length > maxLength) errors.Add(field, $"{name} can be at most {maxLength} characters.");
        else return text;
        return null;
    }

    private static string? OptionalText(ValidationErrors errors, string? value, string field, string name, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length <= maxLength) return text;
        errors.Add(field, $"{name} can be at most {maxLength} characters.");
        return null;
    }
}

/// <summary>
/// Money the client paid against one issued bill, as recorded by a person. It is voided with a reason, never deleted, and a voided
/// payment no longer counts as collected.
/// </summary>
public sealed class ClientPayment
{
    private ClientPayment() { }

    internal ClientPayment(Guid id, Guid tenantId, Guid clientInvoiceId, long amountMinor, DateOnly paidOn, ClientPaymentMethod method, string? reference,
        string? note, Guid recordedByUserId, string? clientRequestId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ClientInvoiceId = clientInvoiceId;
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
    public Guid ClientInvoiceId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public ClientPaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string? ClientRequestId { get; private set; }
    public bool IsVoided { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }

    internal void Void(string reason, Guid voidedByUserId, DateTimeOffset now)
    {
        IsVoided = true;
        VoidReason = reason;
        VoidedByUserId = voidedByUserId;
        VoidedAtUtc = now;
    }
}
