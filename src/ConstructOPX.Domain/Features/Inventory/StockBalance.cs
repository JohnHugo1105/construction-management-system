namespace ConstructOPX.Domain.Features.Inventory;

/// <summary>How much of an item sits in one location. Rows with nothing left are removed.</summary>
public sealed class StockBalance
{
    private StockBalance() { }

    internal StockBalance(Guid id, Guid tenantId, Guid itemId, StockLocation location, decimal quantity)
    {
        Id = id;
        TenantId = tenantId;
        ItemId = itemId;
        LocationKey = location.Key;
        ProjectId = location.ProjectId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public string LocationKey { get; private set; } = "";
    public Guid? ProjectId { get; private set; }
    public decimal Quantity { get; private set; }

    public StockLocation Location => StockLocation.FromKey(LocationKey);

    internal void Add(decimal quantity) => Quantity += quantity;

    internal void Remove(decimal quantity) => Quantity -= quantity;
}
