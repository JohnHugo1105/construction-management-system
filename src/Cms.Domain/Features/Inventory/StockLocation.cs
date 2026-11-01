namespace Cms.Domain.Features.Inventory;

/// <summary>
/// Where stock sits: the warehouse, or a project. The desktop app identified the warehouse by a typed project name
/// ("Warehouse", and in older builds a company-named store room), which any typo could create anew.
/// </summary>
public readonly record struct StockLocation(StockLocationKind Kind, Guid? ProjectId)
{
    public static StockLocation Warehouse => new(StockLocationKind.Warehouse, null);

    public static StockLocation Project(Guid projectId)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.", nameof(projectId));
        return new(StockLocationKind.Project, projectId);
    }

    /// <summary>A stable text key used for storage and comparison: <c>W</c> or <c>P:{projectId}</c>.</summary>
    public string Key => Kind == StockLocationKind.Warehouse ? "W" : $"P:{ProjectId:N}";

    public bool IsWarehouse => Kind == StockLocationKind.Warehouse;

    public static StockLocation FromKey(string key)
    {
        if (key == "W") return Warehouse;
        if (key.StartsWith("P:", StringComparison.Ordinal) && Guid.TryParseExact(key[2..], "N", out var id)) return Project(id);
        throw new ArgumentException("That isn't a stock location key.", nameof(key));
    }
}
