using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Suppliers;

namespace ConstructOPX.Domain.Features.Purchasing;

public enum PurchaseOrderStatus
{
    /// <summary>Being prepared: the lines can still be replaced. Nothing is committed yet.</summary>
    Draft = 0,

    /// <summary>Approved: the lines are locked and what has not been invoiced yet counts as committed cost.</summary>
    Approved = 1,

    /// <summary>Closed by a person: the supplier delivered less than ordered and the unspent remainder is released. Final.</summary>
    Closed = 2,

    /// <summary>Called off before any invoice was matched to it. Final.</summary>
    Cancelled = 3
}

/// <summary>What a person typed for one line of an order, before it is checked. The item type and VAT treatment are as the shared line rules take them.</summary>
public sealed record PurchaseOrderLineInput(int? Division, string? MainScope, string? SubScope, string? WbsCode, string? Description, ItemType ItemType,
    decimal Quantity, string? Unit, long UnitCostMinor, VatTreatment VatTreatment);

/// <summary>
/// One thing ordered. The money is worked out once by the shared line rules (the same ones budget lines and cost entries use) and kept,
/// with the VAT rate of the day, so a later change of the company rate never alters an order.
/// </summary>
public sealed class PurchaseOrderLine
{
    private PurchaseOrderLine() { }

    internal PurchaseOrderLine(Guid id, Guid tenantId, Guid purchaseOrderId, int lineNumber, CostLineValues values)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        LineNumber = lineNumber;
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
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public int LineNumber { get; private set; }
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

    /// <summary>The company VAT rate when the line was entered.</summary>
    public int VatRateBps { get; private set; }

    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }
}

/// <summary>
/// A purchase order to a supplier for a project. It is drafted, approved by someone who may commit the company's money (the lines are then
/// locked), and either cancelled before any invoice is matched, or closed when the supplier delivered less than ordered. What an approved
/// order has not yet been invoiced for is committed cost: it enters a project's forecast until an invoice is matched to its lines, or the
/// order is closed. Orders are never deleted.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot
{
    public const int MaximumLines = 100;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);

    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder() { }

    public string Number { get; private set; } = "";
    public Guid SupplierId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDate { get; private set; }
    public string? SupplierReference { get; private set; }
    public string? Notes { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;

    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? CloseReason { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancelReason { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    public bool IsFinal => Status is PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled;

    /// <summary>"PO-0001": the number shown to people for the nth order of the company.</summary>
    public static string NumberFor(long sequenceValue) => $"PO-{sequenceValue:0000}";

    /// <summary>
    /// Starts a draft. It has no number yet: the use case takes one with <see cref="AssignNumber"/> once the order is known to be valid, so a
    /// form with mistakes never uses up a number.
    /// </summary>
    public static PurchaseOrder CreateDraft(Guid id, Guid tenantId, Supplier supplier, Project project, Guid? subProjectId,
        DateOnly orderDate, DateOnly? expectedDate, string? supplierReference, string? notes, IReadOnlyList<PurchaseOrderLineInput> lines,
        int vatRateBps, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        supplier.EnsureCanBeUsedAs(SupplierKind.Supplier);
        project.EnsureCanReceiveRecordsFor(subProjectId);
        var checkedValues = Validate(orderDate, expectedDate, supplierReference, notes, lines, vatRateBps, today);

        var order = new PurchaseOrder
        {
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        order.Initialize(id, tenantId, now);
        order.Apply(supplier.Id, project.Id, subProjectId, checkedValues);
        return order;
    }

    /// <summary>Gives a new order its number, once.</summary>
    public void AssignNumber(long sequenceValue)
    {
        if (Number.Length > 0) throw new InvalidOperationException("An order's number is assigned once.");
        if (sequenceValue < 1) throw new ArgumentOutOfRangeException(nameof(sequenceValue));
        Number = NumberFor(sequenceValue);
    }

    /// <summary>Replaces everything a person fills in on a draft, lines included. An approved order is locked.</summary>
    public void ReviseDraft(Supplier supplier, Project project, Guid? subProjectId, DateOnly orderDate, DateOnly? expectedDate, string? supplierReference,
        string? notes, IReadOnlyList<PurchaseOrderLineInput> lines, int vatRateBps, DateOnly today, DateTimeOffset now)
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new DomainRuleException(Status == PurchaseOrderStatus.Approved
                ? "This order is approved and its lines are locked. Cancel it, or close it if the supplier delivered less."
                : $"This order was {Status.ToString().ToLowerInvariant()} and can't be changed.");
        supplier.EnsureCanBeUsedAs(SupplierKind.Supplier);
        project.EnsureCanReceiveRecordsFor(subProjectId);
        var checkedValues = Validate(orderDate, expectedDate, supplierReference, notes, lines, vatRateBps, today);
        Apply(supplier.Id, project.Id, subProjectId, checkedValues);
        Touch(now);
    }

    /// <summary>Approves a draft so its lines are locked and what has not been invoiced counts as committed cost.</summary>
    public void Approve(Supplier supplier, Project project, Guid userId, DateTimeOffset now)
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new DomainRuleException(Status == PurchaseOrderStatus.Approved ? "This order is already approved." : $"This order was {Status.ToString().ToLowerInvariant()} and can't be approved.");
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this order.", nameof(project));
        project.EnsureCanReceiveRecordsFor(SubProjectId);
        supplier.EnsureCanBeUsedAs(SupplierKind.Supplier);
        if (_lines.Count == 0) throw new DomainRuleException("Add at least one line before approving the order.");

        Status = PurchaseOrderStatus.Approved;
        ApprovedAtUtc = now;
        ApprovedByUserId = userId;
        Touch(now);
    }

    /// <summary>Calls off a draft, or an approved order that nothing has been invoiced against and nothing has been delivered for. Needs a reason.</summary>
    public void Cancel(Project project, string? reason, bool hasMatchedInvoices, bool hasDeliveries, Guid userId, DateTimeOffset now)
    {
        if (IsFinal) throw new DomainRuleException($"This order was already {Status.ToString().ToLowerInvariant()}.");
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this order.", nameof(project));
        project.EnsureNotArchived();
        if (Status == PurchaseOrderStatus.Approved && hasMatchedInvoices)
            throw new DomainRuleException("Invoices are already matched to this order, so it can't be cancelled. Close it instead to release what was not delivered.");
        if (Status == PurchaseOrderStatus.Approved && hasDeliveries)
            throw new DomainRuleException("Deliveries are already recorded against this order, so it can't be cancelled. Close it instead to release what was not delivered.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for cancelling this order", 300);

        Status = PurchaseOrderStatus.Cancelled;
        CancelReason = cleanReason;
        CancelledAtUtc = now;
        CancelledByUserId = userId;
        Touch(now);
    }

    /// <summary>
    /// Called when a supplier invoice is matched to one of this order's lines. It re-checks that the order is still approved and touches the
    /// order, so an invoice and a cancel (or close) that happen at the same moment collide on the order's version instead of both succeeding.
    /// </summary>
    public void NoteMatchedInvoice(DateTimeOffset now)
    {
        if (Status != PurchaseOrderStatus.Approved)
            throw new DomainRuleException($"{Number} was {Status.ToString().ToLowerInvariant()} while you were recording the invoice. Reload and choose another order, or leave the invoice unmatched.");
        Touch(now);
    }

    /// <summary>Refuses a delivery against an order that is not approved: a draft is not an order yet, and a closed or cancelled one is final.</summary>
    public void EnsureCanReceive()
    {
        if (Status == PurchaseOrderStatus.Approved) return;
        throw new DomainRuleException(Status switch
        {
            PurchaseOrderStatus.Draft => $"{Number} is not approved yet. Approve it before recording a delivery.",
            PurchaseOrderStatus.Closed => "This order is closed.",
            _ => "This order was cancelled."
        });
    }

    /// <summary>
    /// Called when a delivery is recorded against this order. It re-checks that the order is still approved and touches the order, so two
    /// deliveries of the last quantity, or a delivery and a close or cancel, collide on the order's version instead of both succeeding.
    /// </summary>
    public void NoteDeliveryRecorded(DateTimeOffset now)
    {
        EnsureCanReceive();
        Touch(now);
    }

    /// <summary>Closes an approved order: the supplier delivered less, so what was not invoiced is released from committed cost.</summary>
    public void Close(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        if (Status != PurchaseOrderStatus.Approved)
            throw new DomainRuleException(Status == PurchaseOrderStatus.Draft
                ? "Only an approved order can be closed. Cancel a draft you no longer need."
                : $"This order was already {Status.ToString().ToLowerInvariant()}.");
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this order.", nameof(project));
        project.EnsureNotArchived();
        var cleanReason = Guard.OptionalText(reason, "reason", "the reason", 300);

        Status = PurchaseOrderStatus.Closed;
        CloseReason = cleanReason;
        ClosedAtUtc = now;
        ClosedByUserId = userId;
        Touch(now);
    }

    private void Apply(Guid supplierId, Guid projectId, Guid? subProjectId, CheckedOrder values)
    {
        SupplierId = supplierId;
        ProjectId = projectId;
        SubProjectId = subProjectId;
        OrderDate = values.OrderDate;
        ExpectedDate = values.ExpectedDate;
        SupplierReference = values.SupplierReference;
        Notes = values.Notes;

        _lines.Clear();
        var total = LineAmounts.Zero;
        for (var index = 0; index < values.Lines.Count; index++)
        {
            _lines.Add(new PurchaseOrderLine(Guid.NewGuid(), TenantId, Id, index + 1, values.Lines[index]));
            total += values.Lines[index].Amounts;
        }

        NetMinor = total.NetMinor;
        VatMinor = total.VatMinor;
        GrossMinor = total.GrossMinor;
    }

    private sealed record CheckedOrder(DateOnly OrderDate, DateOnly? ExpectedDate, string? SupplierReference, string? Notes, IReadOnlyList<CostLineValues> Lines);

    /// <summary>Checks the whole order and reports every problem together. A problem with a line is named by its number.</summary>
    private static CheckedOrder Validate(DateOnly orderDate, DateOnly? expectedDate, string? supplierReference, string? notes,
        IReadOnlyList<PurchaseOrderLineInput> lines, int vatRateBps, DateOnly today)
    {
        var errors = new ValidationErrors();
        if (orderDate < EarliestDate) errors.Add("orderDate", "Choose the date of the order.");
        else if (orderDate > today) errors.Add("orderDate", "The order date can't be in the future.");
        if (expectedDate is { } expected)
        {
            if (expected < EarliestDate) errors.Add("expectedDate", "Choose a valid date, or leave it empty.");
            else if (orderDate >= EarliestDate && expected < orderDate) errors.Add("expectedDate", "The expected date can't be before the order date.");
        }

        string? cleanReference = null, cleanNotes = null;
        Capture(errors, () => cleanReference = Guard.OptionalText(supplierReference, "supplierReference", "the supplier's reference", 80));
        Capture(errors, () => cleanNotes = Guard.OptionalText(notes, "notes", "the notes", 500));

        var values = new List<CostLineValues>();
        if (lines.Count == 0) errors.Add("lines", "Add at least one line to the order.");
        else if (lines.Count > MaximumLines) errors.Add("lines", $"An order can have at most {MaximumLines} lines. Raise a second order for the rest.");
        else
        {
            var total = LineAmounts.Zero;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                try
                {
                    // An order line has no date of its own: the order's date is checked above, so the line check gets a date that always passes.
                    var checkedLine = CostLineRules.Validate(line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description, line.ItemType,
                        line.Quantity, line.Unit, line.UnitCostMinor, line.VatTreatment, vatRateBps, EarliestDate, null, latestAllowedDate: null);
                    values.Add(checkedLine);
                    total += checkedLine.Amounts;
                }
                catch (ValidationFailedException ex)
                {
                    foreach (var (_, messages) in ex.Errors)
                        foreach (var message in messages) errors.Add("lines", $"Line {index + 1}: {message}");
                }
                catch (OverflowException)
                {
                    errors.Add("lines", "The total of the order is too large. Check the quantities and unit costs.");
                    break;
                }
            }

            if (!errors.Any && total.GrossMinor > MoneyLimits.MaximumAmountMinor)
                errors.Add("lines", "The total of the order is too large. Raise a second order for part of it.");
        }

        errors.ThrowIfAny();
        return new CheckedOrder(orderDate, expectedDate, cleanReference, cleanNotes, values);
    }

    private static void Capture(ValidationErrors errors, Action check)
    {
        try { check(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
        }
    }
}

/// <summary>The rule for matching a supplier invoice (a cost entry) to an order line, kept apart so the cost entry use case and its tests share it.</summary>
public static class PurchaseOrderMatching
{
    /// <summary>Refuses a match to a line of another project's order, or of an order that is not approved.</summary>
    public static void EnsureLineCanBeMatched(Guid costProjectId, Guid orderProjectId, PurchaseOrderStatus orderStatus, string orderNumber)
    {
        if (costProjectId != orderProjectId)
            throw new ValidationFailedException("purchaseOrderLineId", $"{orderNumber} is for another project. Choose a line of an order for this project.");
        if (orderStatus != PurchaseOrderStatus.Approved)
            throw new ValidationFailedException("purchaseOrderLineId", orderStatus switch
            {
                PurchaseOrderStatus.Draft => $"{orderNumber} is not approved yet. Approve it before matching an invoice to it.",
                PurchaseOrderStatus.Closed => $"{orderNumber} is closed. Choose a line of an open order, or record the invoice without an order.",
                _ => $"{orderNumber} was cancelled. Choose a line of an open order, or record the invoice without an order."
            });
    }
}
