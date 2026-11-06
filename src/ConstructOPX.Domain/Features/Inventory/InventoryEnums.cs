namespace ConstructOPX.Domain.Features.Inventory;

public enum InventoryCategory
{
    /// <summary>Counted by quantity (hammers, shovels). Many can be in one place.</summary>
    Tool = 0,

    /// <summary>One identifiable piece with a serial number (a generator).</summary>
    Equipment = 1,

    /// <summary>One identifiable piece with a serial number (an excavator).</summary>
    Machinery = 2
}

public enum InventoryItemStatus
{
    Active = 0,
    Retired = 1
}

public enum StockLocationKind
{
    Warehouse = 0,
    Project = 1
}

public enum MovementType
{
    /// <summary>Stock arrived in the warehouse from a supplier.</summary>
    Purchase = 0,

    /// <summary>Stock moved between the warehouse and a project, or between projects.</summary>
    Transfer = 1,

    /// <summary>A stock count found a different quantity than the records.</summary>
    Adjustment = 2,

    /// <summary>Stock was lost, broken or used up.</summary>
    WriteOff = 3,

    /// <summary>An earlier movement was undone.</summary>
    Reversal = 4
}
