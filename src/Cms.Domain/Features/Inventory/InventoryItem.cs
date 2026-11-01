using Cms.Domain.Common;
using Cms.Domain.Features.Costing;

namespace Cms.Domain.Features.Inventory;

/// <summary>
/// A tool, piece of equipment or machine, and where its stock is. Balances per location are kept on the item and changed only
/// through methods that also add the matching ledger line, in the same save. A move can never take more than a location holds
/// (the desktop app checked nothing). Tools are counted; equipment and machinery are single, serial-numbered pieces.
/// </summary>
public sealed class InventoryItem : AggregateRoot
{
    private readonly List<StockBalance> _balances = [];
    private readonly List<StockMovement> _movements = [];

    private InventoryItem() { }

    public string Description { get; private set; } = "";
    public InventoryCategory Category { get; private set; }
    public string? Brand { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? NormalizedSerialNumber { get; private set; }
    public string Unit { get; private set; } = "";
    public string? SourceDealer { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly PurchaseDate { get; private set; }

    /// <summary>The cost of one piece, including VAT, in minor units. Used when a transfer is charged to a project.</summary>
    public long UnitCostMinor { get; private set; }

    public string? Remarks { get; private set; }
    public InventoryItemStatus Status { get; private set; } = InventoryItemStatus.Active;
    public string? RetiredReason { get; private set; }

    public IReadOnlyList<StockBalance> Balances => _balances;

    /// <summary>The ledger. It is never loaded as a whole; use the movement queries for history.</summary>
    public IReadOnlyList<StockMovement> Movements => _movements;

    public bool IsUnitTracked => Category != InventoryCategory.Tool;

    public decimal TotalQuantity => _balances.Sum(balance => balance.Quantity);

    public decimal BalanceAt(StockLocation location) =>
        _balances.FirstOrDefault(balance => balance.LocationKey == location.Key)?.Quantity ?? 0m;

    public static string? NormalizeSerial(string? serial) =>
        string.IsNullOrWhiteSpace(serial) ? null : new string(serial.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Registers an item and receives its first stock into the warehouse.</summary>
    public static InventoryItem Register(Guid id, Guid tenantId, string? description, InventoryCategory category, string? brand,
        string? serialNumber, string? unit, string? sourceDealer, string? invoiceNumber, DateOnly purchaseDate, long unitCostMinor,
        decimal quantity, string? remarks, Guid recordedByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        var item = new InventoryItem { Category = category };
        item.ApplyDetails(description, brand, serialNumber, unit, sourceDealer, invoiceNumber, purchaseDate, unitCostMinor, remarks, today);
        item.Initialize(id, tenantId, now);
        item.Receive(quantity, purchaseDate, "First stock received", recordedByUserId, clientRequestId, today, now, firstReceipt: true);
        return item;
    }

    public void UpdateDetails(string? description, string? brand, string? serialNumber, string? unit, string? sourceDealer,
        string? invoiceNumber, DateOnly purchaseDate, long unitCostMinor, string? remarks, DateOnly today, DateTimeOffset now)
    {
        if (Status == InventoryItemStatus.Retired) throw new DomainRuleException("This item is retired and can't be edited.");
        ApplyDetails(description, brand, serialNumber, unit, sourceDealer, invoiceNumber, purchaseDate, unitCostMinor, remarks, today);
        Touch(now);
    }

    /// <summary>Stock arrives in the warehouse. A tool can be restocked; a serial-numbered piece is received once.</summary>
    public StockMovement Receive(decimal quantity, DateOnly date, string? note, Guid recordedByUserId, string? clientRequestId,
        DateOnly today, DateTimeOffset now, bool firstReceipt = false)
    {
        EnsureActive();
        var qty = ValidateQuantity(quantity);
        if (IsUnitTracked && !firstReceipt) throw new DomainRuleException("This is a single piece, so it can't be restocked. Register another item for another piece.");
        ValidateDate(date, today);
        return Apply(MovementType.Purchase, null, StockLocation.Warehouse, qty, date, 0, note, null, recordedByUserId, clientRequestId, now);
    }

    /// <summary>
    /// Moves stock between locations. The source must hold the quantity. When <paramref name="chargeToProject"/> is set the move is
    /// charged to the destination project at unit cost times quantity; moving stock back to the warehouse is never charged.
    /// </summary>
    public StockMovement Transfer(StockLocation from, StockLocation to, decimal quantity, DateOnly date, bool chargeToProject,
        string? note, Guid recordedByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        EnsureActive();
        var qty = ValidateQuantity(quantity);
        ValidateDate(date, today);
        if (from.Key == to.Key) throw new ValidationFailedException("toLocation", "Choose a different place to move it to.");
        EnsureAvailable(from, qty);

        long charge = 0;
        if (chargeToProject)
        {
            if (to.IsWarehouse) throw new ValidationFailedException("chargeToProject", "Moving stock back to the warehouse isn't charged to a project.");
            if (UnitCostMinor <= 0) throw new ValidationFailedException("chargeToProject", "This item has no cost on record, so there is nothing to charge.");
            try { charge = LineAmounts.Calculate(qty, UnitCostMinor, VatTreatment.Exempt, 0).GrossMinor; }
            catch (OverflowException)
            {
                throw new ValidationFailedException("chargeToProject", "That amount is too large to charge. Check the quantity and the item's unit cost.");
            }
        }

        return Apply(MovementType.Transfer, from, to, qty, date, charge, note, null, recordedByUserId, clientRequestId, now);
    }

    /// <summary>Records a stock count: the location now holds <paramref name="countedQuantity"/>, whatever the records said.</summary>
    public StockMovement? Adjust(StockLocation location, decimal countedQuantity, string? reason, DateOnly date, Guid recordedByUserId,
        string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        EnsureActive();
        if (countedQuantity < 0m || decimal.Round(countedQuantity, 4) != countedQuantity)
            throw new ValidationFailedException("countedQuantity", "Enter the counted quantity (zero or more, up to 4 decimals).");
        if (IsUnitTracked && countedQuantity > 1m) throw new ValidationFailedException("countedQuantity", "A single piece is counted as 0 or 1.");
        ValidateDate(date, today);
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for the adjustment", 300);
        var current = BalanceAt(location);
        var delta = countedQuantity - current;
        if (delta == 0m) throw new DomainRuleException("The count matches the records. Nothing to adjust.");

        return delta > 0m
            ? Apply(MovementType.Adjustment, null, location, delta, date, 0, cleanReason, null, recordedByUserId, clientRequestId, now)
            : Apply(MovementType.Adjustment, location, null, -delta, date, 0, cleanReason, null, recordedByUserId, clientRequestId, now);
    }

    /// <summary>Removes stock that was lost, broken or used up. A single piece written off retires the item.</summary>
    public StockMovement WriteOff(StockLocation location, decimal quantity, string? reason, DateOnly date, Guid recordedByUserId,
        string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        EnsureActive();
        var qty = ValidateQuantity(quantity);
        ValidateDate(date, today);
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for the write-off", 300);
        EnsureAvailable(location, qty);
        var movement = Apply(MovementType.WriteOff, location, null, qty, date, 0, cleanReason, null, recordedByUserId, clientRequestId, now);
        if (IsUnitTracked)
        {
            Status = InventoryItemStatus.Retired;
            RetiredReason = cleanReason;
        }
        return movement;
    }

    /// <summary>
    /// Undoes an earlier movement with an equal and opposite one. It can't be done twice, a reversal can't itself be reversed, and the
    /// stock must still be where the original put it.
    /// </summary>
    public StockMovement Reverse(StockMovement original, string? reason, DateOnly date, Guid recordedByUserId, DateOnly today,
        DateTimeOffset now, string? clientRequestId = null)
    {
        if (original.ItemId != Id) throw new ArgumentException("That movement belongs to another item.", nameof(original));
        if (original.Type == MovementType.Reversal) throw new DomainRuleException("A reversal can't be reversed. Record the move again instead.");
        if (original.IsReversed) throw new DomainRuleException("This movement was already reversed.");
        // A retired item holds no stock. Only the write-off that retired it may come back, and it reactivates the item below.
        if (Status == InventoryItemStatus.Retired && original.Type != MovementType.WriteOff)
            throw new DomainRuleException("This item is retired, so only its write-off can be reversed.");
        ValidateDate(date, today);
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for reversing", 300);
        if (original.To is { } arrived) EnsureAvailable(arrived, original.Quantity);

        var reversal = Apply(MovementType.Reversal, original.To, original.From, original.Quantity, date, 0, cleanReason, original.Id,
            recordedByUserId, clientRequestId, now);
        if (Status == InventoryItemStatus.Retired)
        {
            Status = InventoryItemStatus.Active;
            RetiredReason = null;
        }

        original.MarkReversed(reversal.Id);
        return reversal;
    }

    /// <summary>Takes an item out of use. It must hold no stock anywhere.</summary>
    public void Retire(string? reason, DateTimeOffset now)
    {
        if (Status == InventoryItemStatus.Retired) throw new DomainRuleException("This item is already retired.");
        if (TotalQuantity > 0m) throw new DomainRuleException("This item still has stock. Move it out or write it off first.");
        RetiredReason = Guard.RequiredText(reason, "reason", "the reason for retiring this item", 300);
        Status = InventoryItemStatus.Retired;
        Touch(now);
    }

    private StockMovement Apply(MovementType type, StockLocation? from, StockLocation? to, decimal quantity, DateOnly date,
        long chargedMinor, string? note, Guid? reversalOf, Guid userId, string? clientRequestId, DateTimeOffset now)
    {
        // Everything that can refuse the movement is checked before any balance changes, so a refused request changes nothing.
        var cleanNote = Guard.OptionalText(note, "note", "the note", 300);
        var cleanRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80);
        if (IsUnitTracked && from is null && to is not null && TotalQuantity + quantity > 1m)
            throw new DomainRuleException("This single piece is already recorded in another place. Set the count there to 0 first.");

        if (from is { } source) Take(source, quantity);
        if (to is { } target) Put(target, quantity);

        var movement = new StockMovement(Guid.NewGuid(), TenantId, Id, type, from, to, quantity,
            date, chargedMinor, cleanNote, reversalOf, userId, cleanRequestId, now);
        _movements.Add(movement);
        Touch(now);
        return movement;
    }

    private void Put(StockLocation location, decimal quantity)
    {
        var existing = _balances.FirstOrDefault(balance => balance.LocationKey == location.Key);
        if (existing is null) _balances.Add(new StockBalance(Guid.NewGuid(), TenantId, Id, location, quantity));
        else existing.Add(quantity);
    }

    private void Take(StockLocation location, decimal quantity)
    {
        var existing = _balances.FirstOrDefault(balance => balance.LocationKey == location.Key);
        if (existing is null || existing.Quantity < quantity) throw new DomainRuleException("There isn't enough stock there.");
        existing.Remove(quantity);
        if (existing.Quantity == 0m) _balances.Remove(existing);
    }

    private void EnsureAvailable(StockLocation location, decimal quantity)
    {
        var available = BalanceAt(location);
        if (available < quantity)
            throw new DomainRuleException(available == 0m
                ? "There is none of this item there to move."
                : $"Only {available:0.####} {Unit} {(available == 1m ? "is" : "are")} there, but you asked for {quantity:0.####}.");
    }

    private void EnsureActive()
    {
        if (Status == InventoryItemStatus.Retired) throw new DomainRuleException("This item is retired. Register a new item instead.");
    }

    private decimal ValidateQuantity(decimal quantity)
    {
        var qty = Guard.PositiveQuantity(quantity, "quantity");
        if (IsUnitTracked && qty != 1m) throw new ValidationFailedException("quantity", "This is a single piece, so the quantity is 1.");
        return qty;
    }

    private void ValidateDate(DateOnly date, DateOnly today)
    {
        if (date > today) throw new ValidationFailedException("date", "The date can't be in the future.");
        if (PurchaseDate != default && date < PurchaseDate) throw new ValidationFailedException("date", $"The date can't be before the purchase date ({PurchaseDate:d MMM yyyy}).");
    }

    private void ApplyDetails(string? description, string? brand, string? serialNumber, string? unit, string? sourceDealer,
        string? invoiceNumber, DateOnly purchaseDate, long unitCostMinor, string? remarks, DateOnly today)
    {
        var errors = new ValidationErrors();
        string cleanDescription = "", cleanUnit = "";
        Capture(errors, () => cleanDescription = Guard.RequiredText(description, "description", "a description of the item", 200));
        Capture(errors, () => cleanUnit = UnitOfMeasure.Normalize(unit));
        var serial = Guard.OptionalText(serialNumber, "serialNumber", "the serial number", 80);
        if (!Enum.IsDefined(Category)) errors.Add("category", "Choose tool, equipment or machinery.");
        if (IsUnitTracked && serial is null) errors.Add("serialNumber", "Enter the serial number. Equipment and machinery are tracked one by one.");
        if (serial is not null && NormalizeSerial(serial) is null) errors.Add("serialNumber", "The serial number needs at least one letter or digit.");
        if (unitCostMinor < 0) errors.Add("unitCostMinor", "The unit cost can't be negative.");
        if (purchaseDate == default) errors.Add("purchaseDate", "Choose the purchase date.");
        else if (purchaseDate > today) errors.Add("purchaseDate", "The purchase date can't be in the future.");
        errors.ThrowIfAny();

        Description = cleanDescription;
        Unit = cleanUnit;
        Brand = Guard.OptionalText(brand, "brand", "the brand", 80);
        SerialNumber = serial;
        NormalizedSerialNumber = NormalizeSerial(serial);
        SourceDealer = Guard.OptionalText(sourceDealer, "sourceDealer", "the supplier", 120);
        InvoiceNumber = Guard.OptionalText(invoiceNumber, "invoiceNumber", "the invoice number", 60);
        PurchaseDate = purchaseDate;
        UnitCostMinor = unitCostMinor;
        Remarks = Guard.OptionalText(remarks, "remarks", "the remarks", 500);
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
