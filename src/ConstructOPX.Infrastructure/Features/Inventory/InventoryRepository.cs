using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Inventory;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Inventory;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Inventory;

internal sealed class InventoryRepository(ConstructOpxDbContext db) : IInventoryRepository
{
    // The ledger is deliberately not included: it grows without bound, and a stock change only ever adds to it.
    public Task<InventoryItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<InventoryItem>().Include(item => item.Balances).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<StockMovement?> GetMovementAsync(Guid itemId, Guid movementId, CancellationToken cancellationToken) =>
        db.Set<StockMovement>().SingleOrDefaultAsync(movement => movement.Id == movementId && movement.ItemId == itemId, cancellationToken);

    public Task<Guid?> FindItemIdByRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<StockMovement>().Where(movement => movement.ClientRequestId == clientRequestId)
            .Select(movement => (Guid?)movement.ItemId).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> SerialExistsAsync(string normalizedSerialNumber, Guid? excludeItemId, CancellationToken cancellationToken) =>
        db.Set<InventoryItem>().AnyAsync(item => item.NormalizedSerialNumber == normalizedSerialNumber && item.Id != excludeItemId, cancellationToken);

    public void Add(InventoryItem item) => db.Set<InventoryItem>().Add(item);
}

internal sealed class InventoryReader(ConstructOpxDbContext db) : IInventoryReader
{
    private static readonly string WarehouseKey = StockLocation.Warehouse.Key;

    public async Task<PagedResult<InventoryListItemDto>> ListAsync(InventoryListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var items = db.Set<InventoryItem>().AsNoTracking();
        if (query.Category is { } category) items = items.Where(item => item.Category == category);
        if (query.Status is { } status) items = items.Where(item => item.Status == status);
        if (query.ProjectId is { } projectId) items = items.Where(item => item.Balances.Any(balance => balance.ProjectId == projectId));
        if (query.InWarehouse) items = items.Where(item => item.Balances.Any(balance => balance.LocationKey == WarehouseKey));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Sql.Contains(query.Search);
            items = items.Where(item => EF.Functions.Like(item.Description, pattern, Sql.LikeEscape) ||
                (item.SerialNumber != null && EF.Functions.Like(item.SerialNumber, pattern, Sql.LikeEscape)) ||
                (item.Brand != null && EF.Functions.Like(item.Brand, pattern, Sql.LikeEscape)));
        }

        // Two statements however many rows: the count, then the page with every item's balances and project names in one query.
        var total = await items.CountAsync(cancellationToken);
        var rows = await items
            .OrderBy(item => item.Description).ThenBy(item => item.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(item => new
            {
                item.Id,
                item.Description,
                item.Category,
                item.Brand,
                item.SerialNumber,
                item.Unit,
                item.UnitCostMinor,
                item.Status,
                Balances = item.Balances.Select(balance => new BalanceRow(balance.ProjectId, balance.Quantity,
                    db.Set<Project>().Where(project => project.Id == balance.ProjectId).Select(project => project.Name).FirstOrDefault())).ToList()
            })
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(row => new InventoryListItemDto(row.Id, row.Description, row.Category.ToString(), row.Brand, row.SerialNumber, row.Unit,
            row.UnitCostMinor, row.Status.ToString(), row.Balances.Sum(balance => balance.Quantity), ToBalances(row.Balances))).ToArray();
        return PagedResult<InventoryListItemDto>.Of(dtos, total, page);
    }

    public async Task<InventoryItemDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Set<InventoryItem>().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Description,
                item.Category,
                item.Brand,
                item.SerialNumber,
                item.Unit,
                item.SourceDealer,
                item.InvoiceNumber,
                item.PurchaseDate,
                item.UnitCostMinor,
                item.Remarks,
                item.Status,
                item.RetiredReason,
                item.Version,
                Balances = item.Balances.Select(balance => new BalanceRow(balance.ProjectId, balance.Quantity,
                    db.Set<Project>().Where(project => project.Id == balance.ProjectId).Select(project => project.Name).FirstOrDefault())).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        return new InventoryItemDto(row.Id, row.Description, row.Category.ToString(), row.Brand, row.SerialNumber, row.Unit, row.SourceDealer,
            row.InvoiceNumber, row.PurchaseDate, row.UnitCostMinor, row.Remarks, row.Status.ToString(), row.RetiredReason,
            row.Balances.Sum(balance => balance.Quantity), ToBalances(row.Balances), row.Version);
    }

    public async Task<PagedResult<StockMovementDto>?> ListMovementsAsync(Guid itemId, PageRequest page, CancellationToken cancellationToken)
    {
        if (!await db.Set<InventoryItem>().AnyAsync(item => item.Id == itemId, cancellationToken)) return null;

        var movements = db.Set<StockMovement>().AsNoTracking().Where(movement => movement.ItemId == itemId);
        var total = await movements.CountAsync(cancellationToken);
        var rows = await movements
            .OrderByDescending(movement => movement.RecordedAtUtc).ThenByDescending(movement => movement.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(movement => new
            {
                movement.Id,
                movement.Type,
                movement.FromKey,
                movement.FromProjectId,
                movement.ToKey,
                movement.ToProjectId,
                movement.Quantity,
                movement.MovementDate,
                movement.ChargedAmountMinor,
                movement.Note,
                movement.RecordedAtUtc,
                IsReversed = movement.ReversedByMovementId != null,
                movement.ReversalOfMovementId,
                FromProject = db.Set<Project>().Where(project => project.Id == movement.FromProjectId).Select(project => project.Name).FirstOrDefault(),
                ToProject = db.Set<Project>().Where(project => project.Id == movement.ToProjectId).Select(project => project.Name).FirstOrDefault(),
                RecordedBy = db.Set<User>().Where(user => user.Id == movement.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(row => new StockMovementDto(row.Id, row.Type.ToString(), PlaceName(row.FromKey, row.FromProject), row.FromProjectId,
            PlaceName(row.ToKey, row.ToProject), row.ToProjectId, row.Quantity, row.MovementDate, row.ChargedAmountMinor, row.Note,
            row.RecordedBy ?? "", row.RecordedAtUtc, row.IsReversed, row.ReversalOfMovementId)).ToArray();
        return PagedResult<StockMovementDto>.Of(dtos, total, page);
    }

    public async Task<PagedResult<ProjectStockDto>?> ListForProjectAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        if (!await db.Set<Project>().AnyAsync(project => project.Id == projectId, cancellationToken)) return null;

        var stock = from balance in db.Set<StockBalance>().AsNoTracking()
                    join item in db.Set<InventoryItem>().AsNoTracking() on balance.ItemId equals item.Id
                    where balance.ProjectId == projectId && balance.Quantity > 0m
                    select new
                    {
                        ItemId = item.Id,
                        item.Description,
                        item.Category,
                        item.Brand,
                        item.SerialNumber,
                        item.Unit,
                        balance.Quantity
                    };
        var total = await stock.CountAsync(cancellationToken);
        var rows = await stock.OrderBy(row => row.Description).ThenBy(row => row.ItemId).Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(row => new ProjectStockDto(row.ItemId, row.Description, row.Category.ToString(), row.Brand, row.SerialNumber, row.Unit,
            row.Quantity)).ToArray();
        return PagedResult<ProjectStockDto>.Of(dtos, total, page);
    }

    private sealed record BalanceRow(Guid? ProjectId, decimal Quantity, string? ProjectName);

    /// <summary>The warehouse first, then projects by name.</summary>
    private static IReadOnlyList<StockBalanceDto> ToBalances(IEnumerable<BalanceRow> rows) => rows
        .Select(row => row.ProjectId is null
            ? new StockBalanceDto(nameof(StockLocationKind.Warehouse), null, null, row.Quantity)
            : new StockBalanceDto(nameof(StockLocationKind.Project), row.ProjectId, row.ProjectName, row.Quantity))
        .OrderBy(balance => balance.ProjectId is null ? 0 : 1)
        .ThenBy(balance => balance.ProjectName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? PlaceName(string? key, string? projectName) => key switch
    {
        null => null,
        _ when key == WarehouseKey => nameof(StockLocationKind.Warehouse),
        _ => projectName
    };
}
