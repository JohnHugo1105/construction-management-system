using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Receiving;

/// <summary>
/// Reads orders for the receiving screen straight into quantity-only DTOs. It selects no price, rate, total or invoice column, so even a
/// mapping mistake could not put one on the wire. The list is two statements (a count and a page); one order is five (the order, its lines,
/// what has been received per line, the newest deliveries and their lines), however many orders, lines or deliveries exist.
/// </summary>
internal sealed class ReceivingReader(ConstructOpxDbContext db) : IReceivingReader
{
    private const int DeliveriesShown = 100;

    public async Task<ReceivingOrderPageDto> ListAsync(ReceivingOrderFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var orders = AwaitingDelivery(filter);
        var total = await orders.CountAsync(cancellationToken);
        // Soonest expected first; an order with no expected date waits at the end.
        var rows = await orders.OrderBy(order => order.ExpectedDate == null).ThenBy(order => order.ExpectedDate).ThenBy(order => order.Number).ThenBy(order => order.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(order => new
            {
                order.Id, order.Number,
                SupplierName = db.Set<Supplier>().Where(supplier => supplier.Id == order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                ProjectName = db.Set<Project>().Where(project => project.Id == order.ProjectId).Select(project => project.Name).FirstOrDefault(),
                SubProjectName = db.Set<SubProject>().Where(sub => sub.Id == order.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
                order.ExpectedDate,
                AnyReceived = db.Set<GoodsReceipt>().Any(receipt => receipt.PurchaseOrderId == order.Id && receipt.Status == GoodsReceiptStatus.Posted),
                order.Version
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new ReceivingOrderListItemDto(row.Id, row.Number, row.SupplierName ?? "", row.ProjectName ?? "", row.SubProjectName,
            row.ExpectedDate, (row.AnyReceived ? DeliveryState.PartlyReceived : DeliveryState.NotReceived).ToString(), row.Version)).ToArray();
        return new ReceivingOrderPageDto(items, total, page.SafePage, page.SafePageSize);
    }

    public Task<int> CountAsync(ReceivingOrderFilter filter, CancellationToken cancellationToken) => AwaitingDelivery(filter).CountAsync(cancellationToken);

    private IQueryable<PurchaseOrder> AwaitingDelivery(ReceivingOrderFilter filter)
    {
        // Awaiting delivery: approved, of a project that can still take a delivery, with at least one line still short of what was ordered.
        var orders = db.Set<PurchaseOrder>().AsNoTracking()
            .Where(order => order.Status == PurchaseOrderStatus.Approved &&
                !db.Set<Project>().Any(project => project.Id == order.ProjectId && project.Status == ProjectStatus.Archived) &&
                order.Lines.Any(line =>
                    (db.Set<GoodsReceiptLine>().Where(received => received.PurchaseOrderLineId == line.Id &&
                        db.Set<GoodsReceipt>().Any(receipt => receipt.Id == received.GoodsReceiptId && receipt.Status == GoodsReceiptStatus.Posted))
                        .Sum(received => (decimal?)received.Quantity) ?? 0m) < line.Quantity));
        if (filter.ProjectId is { } projectId) orders = orders.Where(order => order.ProjectId == projectId);
        if (filter.ExpectedOnOrBefore is { } dueBy) orders = orders.Where(order => order.ExpectedDate != null && order.ExpectedDate <= dueBy);
        if (filter.Search is { } search)
        {
            var pattern = Sql.Contains(search);
            orders = orders.Where(order => EF.Functions.Like(order.Number, pattern, Sql.LikeEscape) ||
                (order.SupplierReference != null && EF.Functions.Like(order.SupplierReference, pattern, Sql.LikeEscape)) ||
                db.Set<Supplier>().Any(supplier => supplier.Id == order.SupplierId && EF.Functions.Like(supplier.Name, pattern, Sql.LikeEscape)) ||
                db.Set<Project>().Any(project => project.Id == order.ProjectId && EF.Functions.Like(project.Name, pattern, Sql.LikeEscape)));
        }

        return orders;
    }

    public async Task<ReceivingOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var header = await db.Set<PurchaseOrder>().AsNoTracking().Where(order => order.Id == id)
            .Select(order => new
            {
                order.Id, order.Number, order.Status,
                SupplierName = db.Set<Supplier>().Where(supplier => supplier.Id == order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                ProjectName = db.Set<Project>().Where(project => project.Id == order.ProjectId).Select(project => project.Name).FirstOrDefault(),
                ProjectArchived = db.Set<Project>().Any(project => project.Id == order.ProjectId && project.Status == ProjectStatus.Archived),
                SubProjectName = db.Set<SubProject>().Where(sub => sub.Id == order.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
                order.ExpectedDate, order.SupplierReference,
                DeliveryCount = db.Set<GoodsReceipt>().Count(receipt => receipt.PurchaseOrderId == order.Id),
                order.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        // Only the columns a delivery needs: never the unit cost, VAT or amounts of the line.
        var lines = await db.Set<PurchaseOrderLine>().AsNoTracking().Where(line => line.PurchaseOrderId == id).OrderBy(line => line.LineNumber)
            .Select(line => new { line.Id, line.LineNumber, line.Description, line.Unit, line.Quantity })
            .ToListAsync(cancellationToken);
        var lineIds = db.Set<PurchaseOrderLine>().Where(line => line.PurchaseOrderId == id).Select(line => line.Id);
        var receivedByLine = await db.Set<GoodsReceiptLine>().AsNoTracking()
            .Where(received => lineIds.Contains(received.PurchaseOrderLineId) &&
                db.Set<GoodsReceipt>().Any(receipt => receipt.Id == received.GoodsReceiptId && receipt.Status == GoodsReceiptStatus.Posted))
            .GroupBy(received => received.PurchaseOrderLineId)
            .Select(group => new { LineId = group.Key, Quantity = group.Sum(received => received.Quantity) })
            .ToListAsync(cancellationToken);
        var receipts = await db.Set<GoodsReceipt>().AsNoTracking().Where(receipt => receipt.PurchaseOrderId == id)
            .OrderByDescending(receipt => receipt.ReceivedDate).ThenByDescending(receipt => receipt.CreatedAtUtc).ThenBy(receipt => receipt.Id)
            .Take(DeliveriesShown)
            .Select(receipt => new
            {
                receipt.Id, receipt.ReceivedDate, receipt.DeliveryReceiptNumber, receipt.Note, receipt.Status,
                RecordedByName = db.Set<User>().Where(user => user.Id == receipt.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                RecordedAtUtc = receipt.CreatedAtUtc, receipt.VoidedAtUtc,
                VoidedByName = db.Set<User>().Where(user => user.Id == receipt.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                receipt.Version
            })
            .ToListAsync(cancellationToken);
        var receiptIds = receipts.Select(receipt => receipt.Id).ToList();
        // Always asked, even when there are no deliveries, so the order read costs the same number of statements whatever it holds.
        var receiptLines = await db.Set<GoodsReceiptLine>().AsNoTracking().Where(received => receiptIds.Contains(received.GoodsReceiptId))
            .Select(received => new { received.Id, received.GoodsReceiptId, received.PurchaseOrderLineId, received.Quantity })
            .ToListAsync(cancellationToken);

        var received = receivedByLine.ToDictionary(item => item.LineId, item => item.Quantity);
        var lineDtos = lines.Select(line => new ReceivingLineDto(line.Id, line.LineNumber, line.Description, line.Unit, line.Quantity,
            received.GetValueOrDefault(line.Id), GoodsReceipt.LeftToReceive(line.Quantity, received.GetValueOrDefault(line.Id)))).ToArray();
        var canChange = !header.ProjectArchived;
        var deliveryDtos = receipts.Select(receipt => new ReceivingDeliveryDto(receipt.Id, receipt.ReceivedDate, receipt.DeliveryReceiptNumber, receipt.Note,
            receipt.Status.ToString(), receipt.RecordedByName ?? "", receipt.RecordedAtUtc, receipt.VoidedByName, receipt.VoidedAtUtc,
            CanVoid: canChange && receipt.Status == GoodsReceiptStatus.Posted,
            receiptLines.Where(item => item.GoodsReceiptId == receipt.Id)
                .Select(item => new { item, Line = lines.First(line => line.Id == item.PurchaseOrderLineId) })
                .OrderBy(pair => pair.Line.LineNumber)
                .Select(pair => new ReceivingDeliveryLineDto(pair.item.Id, pair.item.PurchaseOrderLineId, pair.Line.LineNumber, pair.Line.Description,
                    pair.Line.Unit, pair.item.Quantity)).ToArray(),
            receipt.Version)).ToArray();
        var state = DeliveryRules.StateOf(lines.Select(line => (line.Quantity, received.GetValueOrDefault(line.Id))));
        return new ReceivingOrderDto(header.Id, header.Number, header.Status.ToString(), header.SupplierName ?? "", header.ProjectName ?? "", header.SubProjectName,
            header.ExpectedDate, header.SupplierReference, state.ToString(),
            CanRecord: canChange && header.Status == PurchaseOrderStatus.Approved && lineDtos.Any(line => line.RemainingQuantity > 0m), lineDtos, deliveryDtos,
            header.DeliveryCount, header.Version);
    }
}
