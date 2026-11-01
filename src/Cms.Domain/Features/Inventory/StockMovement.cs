namespace Cms.Domain.Features.Inventory;

/// <summary>
/// One line of the stock ledger: <c>Quantity</c> leaves <c>From</c> and arrives at <c>To</c> (a missing side is outside the system,
/// such as a supplier or a write-off). Movements are only added; a mistake is undone by a reversal, so the ledger and the balances
/// can never drift apart. The desktop app let history rows be edited in place without recomputing stock.
/// </summary>
public sealed class StockMovement
{
    private StockMovement() { }

    internal StockMovement(Guid id, Guid tenantId, Guid itemId, MovementType type, StockLocation? from, StockLocation? to,
        decimal quantity, DateOnly movementDate, long chargedAmountMinor, string? note, Guid? reversalOfMovementId,
        Guid recordedByUserId, string? clientRequestId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ItemId = itemId;
        Type = type;
        FromKey = from?.Key;
        FromProjectId = from?.ProjectId;
        ToKey = to?.Key;
        ToProjectId = to?.ProjectId;
        Quantity = quantity;
        MovementDate = movementDate;
        ChargedAmountMinor = chargedAmountMinor;
        Note = note;
        ReversalOfMovementId = reversalOfMovementId;
        RecordedByUserId = recordedByUserId;
        ClientRequestId = clientRequestId;
        RecordedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public MovementType Type { get; private set; }
    public string? FromKey { get; private set; }
    public Guid? FromProjectId { get; private set; }
    public string? ToKey { get; private set; }
    public Guid? ToProjectId { get; private set; }
    public decimal Quantity { get; private set; }
    public DateOnly MovementDate { get; private set; }

    /// <summary>When above zero, the transfer was charged to the destination project at this amount (gross, minor units).</summary>
    public long ChargedAmountMinor { get; private set; }

    public string? Note { get; private set; }
    public Guid? ReversalOfMovementId { get; private set; }
    public Guid? ReversedByMovementId { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    public StockLocation? From => FromKey is null ? null : StockLocation.FromKey(FromKey);
    public StockLocation? To => ToKey is null ? null : StockLocation.FromKey(ToKey);
    public bool IsReversed => ReversedByMovementId is not null;

    internal void MarkReversed(Guid reversalId) => ReversedByMovementId = reversalId;
}
