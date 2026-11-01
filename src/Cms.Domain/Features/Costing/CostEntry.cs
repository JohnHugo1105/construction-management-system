using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Costing;

public enum CostEntryStatus
{
    Posted = 0,
    Voided = 1
}

/// <summary>Where a cost entry came from. Only manual entries can be edited; the others are corrected at their source.</summary>
public enum CostOrigin
{
    /// <summary>A person recorded a supplier invoice or receipt.</summary>
    Manual = 0,

    /// <summary>An approved payroll run posted the labor cost.</summary>
    Payroll = 1,

    /// <summary>A stock transfer charged to the project.</summary>
    Inventory = 2,

    /// <summary>A certified subcontractor progress claim posted the work done.</summary>
    Subcontract = 3
}

/// <summary>
/// One actual cost of a project (the desktop app's "journal"). Like a budget line, the money is computed once and stored in minor
/// units. An entry is voided with a reason, never deleted, so totals can always be traced back. A supplier invoice can be
/// recorded only once per project: the retired app let the same invoice in repeatedly.
/// </summary>
public sealed class CostEntry : AggregateRoot
{
    private CostEntry() { }

    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public int? Division { get; private set; }
    public string? MainScope { get; private set; }
    public string? SubScope { get; private set; }
    public string? WbsCode { get; private set; }
    public string Description { get; private set; } = "";
    public ItemType ItemType { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";
    public long UnitCostMinor { get; private set; }
    public VatTreatment VatTreatment { get; private set; }
    public int VatRateBps { get; private set; }
    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string? Remarks { get; private set; }

    public CostOrigin Origin { get; private set; }

    /// <summary>The payroll run or stock movement behind a system-made entry.</summary>
    public Guid? OriginReferenceId { get; private set; }

    /// <summary>The purchase order line this supplier invoice is matched to, so the order's open commitment shrinks as invoices arrive. Manual entries only.</summary>
    public Guid? PurchaseOrderLineId { get; private set; }

    public string SourceDealer { get; private set; } = "";
    public string NormalizedSourceDealer { get; private set; } = "";
    public string? InvoiceNumber { get; private set; }
    public string? NormalizedInvoiceNumber { get; private set; }

    public CostEntryStatus Status { get; private set; } = CostEntryStatus.Posted;
    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    public bool IsVoided => Status == CostEntryStatus.Voided;

    public static string NormalizeInvoice(string invoiceNumber) =>
        new(invoiceNumber.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Records a supplier invoice or receipt. The supplier and invoice number are required, so every cost can be audited.</summary>
    public static CostEntry RecordManual(Guid id, Guid tenantId, Project projectContext, Guid? subProjectId, int? division,
        string? mainScope, string? subScope, string? wbsCode, string? description, ItemType itemType, decimal quantity, string? unit,
        long unitCostMinor, VatTreatment vatTreatment, int vatRateBps, DateOnly entryDate, string? remarks, string? sourceDealer,
        string? invoiceNumber, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        projectContext.EnsureCanReceiveRecordsFor(subProjectId);
        var (values, dealer, invoice) = ValidateManual(division, mainScope, subScope, wbsCode, description, itemType, quantity, unit,
            unitCostMinor, vatTreatment, vatRateBps, entryDate, remarks, sourceDealer, invoiceNumber, today);

        var entry = new CostEntry
        {
            ProjectId = projectContext.Id,
            SubProjectId = subProjectId,
            Origin = CostOrigin.Manual,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        entry.Initialize(id, tenantId, now);
        entry.Apply(values);
        entry.SetSource(dealer, invoice);
        return entry;
    }

    /// <summary>
    /// A cost made by the system (approved payroll, a stock transfer charged to a project). It has no supplier invoice, can't be edited
    /// by hand, and is voided when its source is reversed.
    /// </summary>
    public static CostEntry RecordSystem(Guid id, Guid tenantId, Project projectContext, Guid? subProjectId, CostOrigin origin,
        Guid originReferenceId, string description, ItemType itemType, decimal quantity, string unit, long unitCostMinor,
        VatTreatment vatTreatment, int vatRateBps, DateOnly entryDate, Guid createdByUserId, DateTimeOffset now, string? sourceName = null, CostClassification? classification = null)
    {
        if (origin == CostOrigin.Manual) throw new ArgumentException("Use RecordManual for manual entries.", nameof(origin));
        if (originReferenceId == Guid.Empty) throw new ArgumentException("A system cost needs its source.", nameof(originReferenceId));
        projectContext.EnsureCanReceiveRecordsFor(subProjectId);
        // A system cost may be placed in the work breakdown (a subcontract names its work item); payroll and stock leave it unclassified.
        var values = CostLineRules.Validate(classification?.Division, classification?.MainScope, classification?.SubScope, classification?.WbsCode,
            description, itemType, quantity, unit, unitCostMinor, vatTreatment, vatRateBps, entryDate, null, latestAllowedDate: null);

        var entry = new CostEntry
        {
            ProjectId = projectContext.Id,
            SubProjectId = subProjectId,
            Origin = origin,
            OriginReferenceId = originReferenceId,
            CreatedByUserId = createdByUserId
        };
        entry.Initialize(id, tenantId, now);
        entry.Apply(values);
        // A subcontract cost names the subcontractor it was certified for; payroll and stock name what they are.
        entry.SetSource(origin switch
        {
            CostOrigin.Payroll => "Payroll",
            CostOrigin.Subcontract => string.IsNullOrWhiteSpace(sourceName) ? "Subcontractor" : sourceName.Trim(),
            _ => "Warehouse"
        }, null);
        return entry;
    }

    public void Revise(Project projectContext, Guid? subProjectId, int? division, string? mainScope, string? subScope, string? wbsCode,
        string? description, ItemType itemType, decimal quantity, string? unit, long unitCostMinor, VatTreatment vatTreatment,
        int vatRateBps, DateOnly entryDate, string? remarks, string? sourceDealer, string? invoiceNumber, DateOnly today,
        DateTimeOffset now)
    {
        if (projectContext.Id != ProjectId) throw new ArgumentException("The project doesn't match this entry.", nameof(projectContext));
        if (Origin != CostOrigin.Manual) throw new DomainRuleException("This cost was made by the system. Correct it where it came from.");
        if (IsVoided) throw new DomainRuleException("This cost was voided. Record a new one instead.");
        projectContext.EnsureCanReceiveRecordsFor(subProjectId);
        var (values, dealer, invoice) = ValidateManual(division, mainScope, subScope, wbsCode, description, itemType, quantity, unit,
            unitCostMinor, vatTreatment, vatRateBps, entryDate, remarks, sourceDealer, invoiceNumber, today);
        SubProjectId = subProjectId;
        Apply(values);
        SetSource(dealer, invoice);
        Touch(now);
    }

    /// <summary>
    /// Matches this invoice to a purchase order line, or clears the match (null). The use case has already checked the line belongs to an
    /// approved order of the same project. Only a hand-entered, still posted cost can be matched.
    /// </summary>
    public void LinkToPurchaseOrderLine(Guid? purchaseOrderLineId)
    {
        if (purchaseOrderLineId == Guid.Empty) throw new ArgumentException("A purchase order line needs its id.", nameof(purchaseOrderLineId));
        if (purchaseOrderLineId is not null && Origin != CostOrigin.Manual)
            throw new DomainRuleException("Only a supplier invoice recorded by hand can be matched to a purchase order.");
        PurchaseOrderLineId = purchaseOrderLineId;
    }

    public void Void(Guid userId, string? reason, DateTimeOffset now)
    {
        if (IsVoided) throw new DomainRuleException("This cost was already voided.");
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for voiding this cost", 300);
        Status = CostEntryStatus.Voided;
        VoidedAtUtc = now;
        VoidedByUserId = userId;
        Touch(now);
    }

    /// <summary>Validates the supplier, invoice and every line field together, so one response lists every problem.</summary>
    private static (CostLineValues Values, string Dealer, string Invoice) ValidateManual(int? division, string? mainScope,
        string? subScope, string? wbsCode, string? description, ItemType itemType, decimal quantity, string? unit, long unitCostMinor,
        VatTreatment vatTreatment, int vatRateBps, DateOnly entryDate, string? remarks, string? sourceDealer, string? invoiceNumber,
        DateOnly today)
    {
        var errors = new ValidationErrors();
        string dealer = "", invoice = "";
        try { dealer = Guard.RequiredText(sourceDealer, "sourceDealer", "the supplier or dealer", 120); }
        catch (ValidationFailedException ex) { errors.Add("sourceDealer", ex.Errors["sourceDealer"][0]); }
        try
        {
            invoice = Guard.RequiredText(invoiceNumber, "invoiceNumber", "the invoice or receipt number", 60);
            if (NormalizeInvoice(invoice).Length == 0) errors.Add("invoiceNumber", "The invoice number needs at least one letter or digit.");
        }
        catch (ValidationFailedException ex) { errors.Add("invoiceNumber", ex.Errors["invoiceNumber"][0]); }

        CostLineValues? values = null;
        try
        {
            values = CostLineRules.Validate(division, mainScope, subScope, wbsCode, description, itemType, quantity, unit,
                unitCostMinor, vatTreatment, vatRateBps, entryDate, remarks, today);
        }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
        }

        errors.ThrowIfAny();
        return (values!, dealer, invoice);
    }

    private void SetSource(string dealer, string? invoice)
    {
        SourceDealer = dealer;
        NormalizedSourceDealer = Guard.Normalize(dealer);
        InvoiceNumber = invoice;
        NormalizedInvoiceNumber = invoice is null ? null : NormalizeInvoice(invoice);
    }

    private void Apply(CostLineValues values)
    {
        Division = values.Classification.Division;
        MainScope = values.Classification.MainScope;
        SubScope = values.Classification.SubScope;
        WbsCode = values.Classification.WbsCode;
        Description = values.Description;
        ItemType = values.ItemType;
        Quantity = values.Quantity;
        Unit = values.Unit;
        UnitCostMinor = values.UnitCostMinor;
        VatTreatment = values.VatTreatment;
        VatRateBps = values.VatRateBps;
        NetMinor = values.Amounts.NetMinor;
        VatMinor = values.Amounts.VatMinor;
        GrossMinor = values.Amounts.GrossMinor;
        EntryDate = values.EntryDate;
        Remarks = values.Remarks;
    }
}
