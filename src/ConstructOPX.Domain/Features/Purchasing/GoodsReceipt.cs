using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Purchasing;

public enum GoodsReceiptStatus
{
    /// <summary>Recorded: what it names counts as received.</summary>
    Posted = 0,

    /// <summary>Entered by mistake and voided with a reason. It stays on record and counts for nothing.</summary>
    Void = 1
}

/// <summary>How much of an order has arrived, worked out from its lines and the posted delivery receipts. Never stored.</summary>
public enum DeliveryState
{
    NotReceived = 0,
    PartlyReceived = 1,
    FullyReceived = 2
}

/// <summary>What a person typed for one line of a delivery: which order line, and how much of it arrived.</summary>
public sealed record GoodsReceiptLineInput(Guid PurchaseOrderLineId, decimal Quantity);

/// <summary>One order line's share of a delivery. Append-only: a receipt is voided as a whole, never edited.</summary>
public sealed class GoodsReceiptLine
{
    private GoodsReceiptLine() { }

    internal GoodsReceiptLine(Guid id, Guid tenantId, Guid goodsReceiptId, Guid purchaseOrderLineId, decimal quantity)
    {
        Id = id;
        TenantId = tenantId;
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderLineId = purchaseOrderLineId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }

    /// <summary>How much of the order line arrived, in the line's own unit. Greater than zero.</summary>
    public decimal Quantity { get; private set; }
}

/// <summary>
/// A delivery against an approved purchase order: what the supplier actually brought, on which day, under which delivery receipt of theirs.
/// A receipt is append-only: a mistake is voided with a reason and entered again. It changes no money anywhere: committed cost still falls
/// only when an invoice is matched to the order line, and stock is not updated by it.
/// </summary>
public sealed class GoodsReceipt : AggregateRoot
{
    public const int MaximumNumberLength = 60;
    public const int MaximumNoteLength = 300;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);

    private readonly List<GoodsReceiptLine> _lines = [];

    private GoodsReceipt() { }

    public Guid PurchaseOrderId { get; private set; }

    /// <summary>The order's supplier, kept here so the same delivery receipt number is refused for the same supplier across orders.</summary>
    public Guid SupplierId { get; private set; }

    public Guid ProjectId { get; private set; }
    public DateOnly ReceivedDate { get; private set; }

    /// <summary>The number on the supplier's own delivery receipt, as typed.</summary>
    public string? DeliveryReceiptNumber { get; private set; }

    public string? NormalizedDeliveryReceiptNumber { get; private set; }
    public string? Note { get; private set; }
    public GoodsReceiptStatus Status { get; private set; } = GoodsReceiptStatus.Posted;
    public Guid RecordedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    public IReadOnlyList<GoodsReceiptLine> Lines => _lines;

    public bool IsVoid => Status == GoodsReceiptStatus.Void;

    /// <summary>The form the duplicate check compares: letters and digits only, upper case, so "DR-0012" and "dr 0012" are the same delivery receipt.</summary>
    public static string NormalizeNumber(string number) => new(number.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// Records a delivery against an approved order. <paramref name="alreadyReceived"/> is what posted receipts already name per order line,
    /// and <paramref name="numberAlreadyRecorded"/> says the same delivery receipt number is already on a posted receipt for this supplier
    /// (the use case looks both up). Every problem with what was typed is reported together. The order is touched so a delivery and a close or
    /// cancel (or a second delivery of the last quantity) at the same moment collide on the order's version instead of both succeeding.
    /// </summary>
    public static GoodsReceipt Record(Guid id, Guid tenantId, PurchaseOrder order, Project project, DateOnly receivedDate, string? deliveryReceiptNumber,
        string? note, IReadOnlyList<GoodsReceiptLineInput> lines, IReadOnlyDictionary<Guid, decimal> alreadyReceived, bool numberAlreadyRecorded,
        Guid recordedByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        order.EnsureCanReceive();
        if (project.Id != order.ProjectId) throw new ArgumentException("The project doesn't match this order.", nameof(project));
        project.EnsureCanReceiveRecordsFor(order.SubProjectId);

        var errors = new ValidationErrors();
        if (receivedDate < EarliestDate) errors.Add("receivedDate", "Choose the day the delivery arrived.");
        else if (receivedDate > today) errors.Add("receivedDate", "The delivery date can't be in the future.");

        string? cleanNumber = null, cleanNote = null, normalized = null;
        Capture(errors, () => cleanNumber = Guard.OptionalText(deliveryReceiptNumber, "deliveryReceiptNumber", "the delivery receipt number", MaximumNumberLength));
        Capture(errors, () => cleanNote = Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        if (cleanNumber is not null)
        {
            normalized = NormalizeNumber(cleanNumber);
            if (normalized.Length == 0) errors.Add("deliveryReceiptNumber", "The delivery receipt number needs at least one letter or digit.");
            else if (numberAlreadyRecorded) errors.Add("deliveryReceiptNumber", "This delivery receipt is already recorded.");
        }

        var checkedLines = CheckLines(errors, order, lines, alreadyReceived);
        errors.ThrowIfAny();

        var receipt = new GoodsReceipt
        {
            PurchaseOrderId = order.Id,
            SupplierId = order.SupplierId,
            ProjectId = order.ProjectId,
            ReceivedDate = receivedDate,
            DeliveryReceiptNumber = cleanNumber,
            NormalizedDeliveryReceiptNumber = normalized,
            Note = cleanNote,
            RecordedByUserId = recordedByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        receipt.Initialize(id, tenantId, now);
        foreach (var line in checkedLines) receipt._lines.Add(new GoodsReceiptLine(Guid.NewGuid(), tenantId, id, line.PurchaseOrderLineId, line.Quantity));
        order.NoteDeliveryRecorded(now);
        return receipt;
    }

    /// <summary>
    /// Voids a delivery that was entered by mistake. It stays on record, stops counting as received, and can be entered again. Refused on an
    /// archived project. Needs a reason.
    /// </summary>
    public void Void(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this delivery.", nameof(project));
        if (IsVoid) throw new DomainRuleException("This delivery was already voided.");
        project.EnsureNotArchived();
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for voiding this delivery", 300);

        Status = GoodsReceiptStatus.Void;
        VoidReason = cleanReason;
        VoidedAtUtc = now;
        VoidedByUserId = userId;
        Touch(now);
    }

    private static List<GoodsReceiptLineInput> CheckLines(ValidationErrors errors, PurchaseOrder order, IReadOnlyList<GoodsReceiptLineInput> lines,
        IReadOnlyDictionary<Guid, decimal> alreadyReceived)
    {
        var accepted = new List<GoodsReceiptLineInput>();
        if (lines.Count == 0)
        {
            errors.Add("lines", "Enter how much arrived for at least one line.");
            return accepted;
        }

        if (lines.Count > PurchaseOrder.MaximumLines)
        {
            errors.Add("lines", $"A delivery can list at most {PurchaseOrder.MaximumLines} lines, the most an order can have.");
            return accepted;
        }

        var seen = new HashSet<Guid>();
        for (var index = 0; index < lines.Count; index++)
        {
            var input = lines[index];
            var orderLine = order.Lines.FirstOrDefault(line => line.Id == input.PurchaseOrderLineId);
            if (orderLine is null)
            {
                errors.Add("lines", $"Line {index + 1}: that line isn't on {order.Number}. Reload the order and try again.");
                continue;
            }

            var name = $"Line {orderLine.LineNumber} ({orderLine.Description})";
            if (!seen.Add(orderLine.Id))
            {
                errors.Add("lines", $"{name} is listed twice. Enter the whole quantity once.");
                continue;
            }

            try { Guard.PositiveQuantity(input.Quantity, "lines", "the quantity received"); }
            catch (ValidationFailedException ex)
            {
                errors.Add("lines", $"{name}: {ex.Errors["lines"][0]}");
                continue;
            }

            var left = LeftToReceive(orderLine.Quantity, alreadyReceived.GetValueOrDefault(orderLine.Id));
            if (input.Quantity > left)
            {
                errors.Add("lines", left == 0m
                    ? $"{name} has all been received already."
                    : $"{name}: only {Plain(left)} {orderLine.Unit} {(left == 1m ? "is" : "are")} left to receive. Enter {Plain(left)} or less.");
                continue;
            }

            accepted.Add(input);
        }

        return accepted;
    }

    /// <summary>What is still to arrive on a line: ordered minus received, never below zero.</summary>
    public static decimal LeftToReceive(decimal ordered, decimal received) => Math.Max(0m, ordered - received);

    /// <summary>A quantity without trailing zeros ("2.5", "10") for a message.</summary>
    private static string Plain(decimal quantity) => quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

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

/// <summary>
/// The derived delivery figures of an order, kept in one pure class so the order read, the list and the tests agree: the order's state, what a
/// line's received quantity is worth, and whether an invoice ran ahead of the delivery.
/// </summary>
public static class DeliveryRules
{
    /// <summary>Not received when nothing arrived, fully received when every line arrived in full, partly received otherwise.</summary>
    public static DeliveryState StateOf(IEnumerable<(decimal Ordered, decimal Received)> lines)
    {
        var any = false;
        var all = true;
        var hasLines = false;
        foreach (var (ordered, received) in lines)
        {
            hasLines = true;
            if (received > 0m) any = true;
            if (received < ordered) all = false;
        }

        if (!hasLines || !any) return DeliveryState.NotReceived;
        return all ? DeliveryState.FullyReceived : DeliveryState.PartlyReceived;
    }

    /// <summary>
    /// What the received quantity of a line is worth: the line's unit cost times the quantity, rounded once, on the same net, VAT and
    /// gross basis and with the same VAT rate as the line itself (so receiving the whole line gives exactly the line's own amounts).
    /// </summary>
    public static LineAmounts ReceivedValue(decimal receivedQuantity, long unitCostMinor, VatTreatment vatTreatment, int vatRateBps) =>
        receivedQuantity <= 0m ? LineAmounts.Zero : LineAmounts.Calculate(receivedQuantity, unitCostMinor, vatTreatment, vatRateBps);

    /// <summary>Advisory only: true when more was invoiced to a line (with VAT) than the value of what arrived. The invoice stays valid.</summary>
    public static bool InvoicedAheadOfDelivery(long invoicedGrossMinor, long receivedValueGrossMinor) => invoicedGrossMinor > receivedValueGrossMinor;
}
