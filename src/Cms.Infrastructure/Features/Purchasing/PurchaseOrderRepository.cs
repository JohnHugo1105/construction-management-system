using Cms.Application.Features.Purchasing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Purchasing;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Purchasing;

internal sealed class PurchaseOrderRepository(CmsDbContext db) : IPurchaseOrderRepository
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

/// <summary>Finds one order line with its order, for matching a supplier invoice to it. Read-only, so it never tracks the order.</summary>
internal sealed class PurchaseOrderLineReader(CmsDbContext db) : IPurchaseOrderLineLookup
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
