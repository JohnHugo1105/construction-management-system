using Cms.Application.Common;
using Cms.Domain.Features.Inventory;

namespace Cms.Application.Features.Inventory;

public interface IInventoryRepository
{
    /// <summary>Loads an item with its balances, tracked for change. The ledger is not loaded. Null when it isn't in the organization.</summary>
    Task<InventoryItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads one ledger line of an item, tracked, so a reversal can mark it. Null when the item has no such movement.</summary>
    Task<StockMovement?> GetMovementAsync(Guid itemId, Guid movementId, CancellationToken cancellationToken);

    /// <summary>The item a request id was already used for, so a repeated request returns the original result instead of repeating.</summary>
    Task<Guid?> FindItemIdByRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    Task<bool> SerialExistsAsync(string normalizedSerialNumber, Guid? excludeItemId, CancellationToken cancellationToken);

    void Add(InventoryItem item);
}

/// <summary>Reads inventory straight into DTOs in SQL, with project names resolved in the same statement.</summary>
public interface IInventoryReader
{
    Task<PagedResult<InventoryListItemDto>> ListAsync(InventoryListQuery query, PageRequest page, CancellationToken cancellationToken);

    Task<InventoryItemDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The ledger of one item, newest first. Null when the item doesn't exist in the organization.</summary>
    Task<PagedResult<StockMovementDto>?> ListMovementsAsync(Guid itemId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>What is on a project now. Null when the project doesn't exist in the organization.</summary>
    Task<PagedResult<ProjectStockDto>?> ListForProjectAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>The list filters as the web app sends them (text). The service checks them and passes <see cref="InventoryListQuery"/> on.</summary>
public sealed record InventoryListRequest(string? Search, string? Category, string? Status, Guid? ProjectId, bool? InWarehouse);

public sealed record InventoryListQuery(string? Search, InventoryCategory? Category, InventoryItemStatus? Status, Guid? ProjectId, bool InWarehouse);

/// <summary>How much of an item is in one place. <c>Location</c> is <c>Warehouse</c> or <c>Project</c>; the project is named, never just an id.</summary>
public sealed record StockBalanceDto(string Location, Guid? ProjectId, string? ProjectName, decimal Quantity);

public sealed record InventoryListItemDto(Guid Id, string Description, string Category, string? Brand, string? SerialNumber, string Unit,
    long UnitCostMinor, string Status, decimal TotalQuantity, IReadOnlyList<StockBalanceDto> Balances);

public sealed record InventoryItemDto(Guid Id, string Description, string Category, string? Brand, string? SerialNumber, string Unit,
    string? SourceDealer, string? InvoiceNumber, DateOnly PurchaseDate, long UnitCostMinor, string? Remarks, string Status,
    string? RetiredReason, decimal TotalQuantity, IReadOnlyList<StockBalanceDto> Balances, int Version);

/// <summary>
/// One ledger line. A missing <c>FromName</c> or <c>ToName</c> means that side is outside the system (a supplier, a write-off);
/// the movement <c>Type</c> says which.
/// </summary>
public sealed record StockMovementDto(Guid Id, string Type, string? FromName, Guid? FromProjectId, string? ToName, Guid? ToProjectId,
    decimal Quantity, DateOnly Date, long ChargedAmountMinor, string? Note, string RecordedBy, DateTimeOffset RecordedAtUtc, bool IsReversed,
    Guid? ReversalOfMovementId);

public sealed record ProjectStockDto(Guid ItemId, string Description, string Category, string? Brand, string? SerialNumber, string Unit,
    decimal Quantity);

/// <summary>Where stock is: <c>{ kind: "Warehouse" }</c> or <c>{ kind: "Project", projectId }</c>.</summary>
public sealed record StockLocationRequest(string? Kind, Guid? ProjectId);

public sealed record RegisterItemRequest(string? Description, string? Category, string? Brand, string? SerialNumber, string? Unit,
    string? SourceDealer, string? InvoiceNumber, DateOnly PurchaseDate, long UnitCostMinor, decimal Quantity, string? Remarks,
    string? ClientRequestId);

public sealed record UpdateItemRequest(string? Description, string? Brand, string? SerialNumber, string? Unit, string? SourceDealer,
    string? InvoiceNumber, DateOnly PurchaseDate, long UnitCostMinor, string? Remarks, int Version);

/// <summary>A movement date left out means today.</summary>
public sealed record ReceiveStockRequest(decimal Quantity, DateOnly? Date, string? Note, string? ClientRequestId, int Version);

public sealed record TransferStockRequest(StockLocationRequest? FromLocation, StockLocationRequest? ToLocation, decimal Quantity,
    DateOnly? Date, bool ChargeToProject, string? Note, string? ClientRequestId, int Version);

public sealed record AdjustStockRequest(StockLocationRequest? Location, decimal CountedQuantity, string? Reason, DateOnly? Date,
    string? ClientRequestId, int Version);

public sealed record WriteOffStockRequest(StockLocationRequest? Location, decimal Quantity, string? Reason, DateOnly? Date,
    string? ClientRequestId, int Version);

public sealed record ReverseMovementRequest(string? Reason, string? ClientRequestId, int Version);

public sealed record RetireItemRequest(string? Reason, int Version);
