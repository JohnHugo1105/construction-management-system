using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Purchasing;

internal sealed class PurchaseOrderRepository(ConstructOpxDbContext db) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<PurchaseOrder>().Include(order => order.Lines).SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<PurchaseOrder?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<PurchaseOrder>().SingleOrDefaultAsync(order => order.ClientRequestId == clientRequestId, cancellationToken);

    public Task<bool> HasMatchedInvoicesAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Set<CostEntry>().AsNoTracking().AnyAsync(entry => entry.Status == CostEntryStatus.Posted && entry.PurchaseOrderLineId != null &&
            db.Set<PurchaseOrderLine>().Any(line => line.Id == entry.PurchaseOrderLineId && line.PurchaseOrderId == orderId), cancellationToken);

    public void Add(PurchaseOrder order) => db.Set<PurchaseOrder>().Add(order);
}

internal sealed class GoodsReceiptRepository(ConstructOpxDbContext db) : IGoodsReceiptRepository
{
    public Task<GoodsReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<GoodsReceipt>().Include(receipt => receipt.Lines).SingleOrDefaultAsync(receipt => receipt.Id == id, cancellationToken);

    public Task<GoodsReceipt?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<GoodsReceipt>().SingleOrDefaultAsync(receipt => receipt.ClientRequestId == clientRequestId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> ReceivedQuantitiesAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await db.Set<GoodsReceiptLine>().AsNoTracking()
            .Where(line => db.Set<GoodsReceipt>().Any(receipt => receipt.Id == line.GoodsReceiptId && receipt.PurchaseOrderId == orderId &&
                receipt.Status == GoodsReceiptStatus.Posted))
            .GroupBy(line => line.PurchaseOrderLineId)
            .Select(group => new { LineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken)).ToDictionary(item => item.LineId, item => item.Quantity);

    public Task<bool> NumberIsRecordedAsync(Guid supplierId, string normalizedNumber, CancellationToken cancellationToken) =>
        db.Set<GoodsReceipt>().AsNoTracking().AnyAsync(receipt => receipt.SupplierId == supplierId && receipt.Status == GoodsReceiptStatus.Posted &&
            receipt.NormalizedDeliveryReceiptNumber == normalizedNumber, cancellationToken);

    public Task<bool> HasPostedAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Set<GoodsReceipt>().AsNoTracking().AnyAsync(receipt => receipt.PurchaseOrderId == orderId && receipt.Status == GoodsReceiptStatus.Posted, cancellationToken);

    public void Add(GoodsReceipt receipt) => db.Set<GoodsReceipt>().Add(receipt);
}

/// <summary>Finds one order line with its order, for matching a supplier invoice to it. Read-only, so it never tracks the order.</summary>
internal sealed class PurchaseOrderLineReader(ConstructOpxDbContext db) : IPurchaseOrderLineLookup
{
    public async Task<PurchaseOrderLineRef?> FindAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var row = await db.Set<PurchaseOrderLine>().AsNoTracking().Where(line => line.Id == lineId)
            .Join(db.Set<PurchaseOrder>().AsNoTracking(), line => line.PurchaseOrderId, order => order.Id,
                (line, order) => new { line.Id, OrderId = order.Id, order.Number, order.ProjectId, order.Status, line.Description })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new PurchaseOrderLineRef(row.Id, row.OrderId, row.Number, row.ProjectId, row.Status, row.Description);
    }
}
