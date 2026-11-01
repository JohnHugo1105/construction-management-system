using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Purchasing;

/// <summary>
/// Reads orders straight into DTOs. The list is two statements (a count and a page, the page carrying each order's delivery state as
/// correlated subqueries); one order is seven (the order, its lines, what is invoiced per line, the newest invoices, what has been received per
/// line, the newest deliveries and their lines), however many orders, lines, invoices or deliveries exist.
/// </summary>
internal sealed class PurchaseOrderReader(ConstructOpxDbContext db) : IPurchaseOrderReader
{
    private const int InvoicesShown = 100;
    private const int ReceiptsShown = 100;
    private const int OpenLinesShown = 500;

    public async Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var orders = db.Set<PurchaseOrder>().AsNoTracking();
        if (filter.ProjectId is { } projectId) orders = orders.Where(order => order.ProjectId == projectId);
        if (filter.SupplierId is { } supplierId) orders = orders.Where(order => order.SupplierId == supplierId);
        if (filter.Status is { } status) orders = orders.Where(order => order.Status == status);
        if (filter.Search is { } search)
        {
            var pattern = Sql.Contains(search);
            orders = orders.Where(order => EF.Functions.Like(order.Number, pattern, Sql.LikeEscape) ||
                (order.SupplierReference != null && EF.Functions.Like(order.SupplierReference, pattern, Sql.LikeEscape)) ||
                db.Set<Supplier>().Any(supplier => supplier.Id == order.SupplierId && EF.Functions.Like(supplier.Name, pattern, Sql.LikeEscape)));
        }

        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders.OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.Number).ThenBy(order => order.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(order => new
            {
                order.Id, order.Number, order.SupplierId,
                SupplierName = db.Set<Supplier>().Where(supplier => supplier.Id == order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                order.ProjectId,
                ProjectName = db.Set<Project>().Where(project => project.Id == order.ProjectId).Select(project => project.Name).FirstOrDefault(),
                order.OrderDate, order.ExpectedDate, order.Status, LineCount = order.Lines.Count, order.NetMinor, order.VatMinor, order.GrossMinor,
                // Only an approved order has a delivery state to show: has anything arrived, and is any line still short.
                AnyReceived = db.Set<GoodsReceipt>().Any(receipt => receipt.PurchaseOrderId == order.Id && receipt.Status == GoodsReceiptStatus.Posted),
                AnyShort = order.Lines.Any(line =>
                    (db.Set<GoodsReceiptLine>().Where(received => received.PurchaseOrderLineId == line.Id &&
                        db.Set<GoodsReceipt>().Any(receipt => receipt.Id == received.GoodsReceiptId && receipt.Status == GoodsReceiptStatus.Posted))
                        .Sum(received => (decimal?)received.Quantity) ?? 0m) < line.Quantity)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new PurchaseOrderListItemDto(row.Id, row.Number, row.SupplierId, row.SupplierName ?? "", row.ProjectId,
            row.ProjectName ?? "", row.OrderDate, row.ExpectedDate, row.Status.ToString(), row.LineCount, row.NetMinor, row.VatMinor, row.GrossMinor,
            row.Status != PurchaseOrderStatus.Approved ? null : (!row.AnyReceived ? DeliveryState.NotReceived : row.AnyShort ? DeliveryState.PartlyReceived : DeliveryState.FullyReceived).ToString())).ToArray();
        return PagedResult<PurchaseOrderListItemDto>.Of(items, total, page);
    }

    public async Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var header = await db.Set<PurchaseOrder>().AsNoTracking().Where(order => order.Id == id)
            .Select(order => new
            {
                order.Id, order.Number, order.Status, order.SupplierId,
                SupplierName = db.Set<Supplier>().Where(supplier => supplier.Id == order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                order.ProjectId,
                ProjectName = db.Set<Project>().Where(project => project.Id == order.ProjectId).Select(project => project.Name).FirstOrDefault(),
                ProjectArchived = db.Set<Project>().Any(project => project.Id == order.ProjectId && project.Status == ProjectStatus.Archived),
                order.SubProjectId,
                SubProjectName = db.Set<SubProject>().Where(sub => sub.Id == order.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
                order.OrderDate, order.ExpectedDate, order.SupplierReference, order.Notes, order.NetMinor, order.VatMinor, order.GrossMinor,
                CreatedByName = db.Set<User>().Where(user => user.Id == order.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                order.CreatedAtUtc, order.ApprovedAtUtc,
                ApprovedByName = db.Set<User>().Where(user => user.Id == order.ApprovedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                order.ClosedAtUtc, order.CloseReason,
                ClosedByName = db.Set<User>().Where(user => user.Id == order.ClosedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                order.CancelledAtUtc, order.CancelReason,
                CancelledByName = db.Set<User>().Where(user => user.Id == order.CancelledByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                ReceiptCount = db.Set<GoodsReceipt>().Count(receipt => receipt.PurchaseOrderId == order.Id),
                order.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var lines = await db.Set<PurchaseOrderLine>().AsNoTracking().Where(line => line.PurchaseOrderId == id).OrderBy(line => line.LineNumber)
            .ToListAsync(cancellationToken);
        var lineIds = db.Set<PurchaseOrderLine>().Where(line => line.PurchaseOrderId == id).Select(line => line.Id);
        var matched = await MatchedEntries().Where(entry => lineIds.Contains(entry.PurchaseOrderLineId!.Value))
            .GroupBy(entry => entry.PurchaseOrderLineId!.Value)
            .Select(group => new { LineId = group.Key, Count = group.Count(), Net = group.Sum(entry => entry.NetMinor), Gross = group.Sum(entry => entry.GrossMinor) })
            .ToListAsync(cancellationToken);
        var invoices = await MatchedEntries().Where(entry => lineIds.Contains(entry.PurchaseOrderLineId!.Value))
            .OrderByDescending(entry => entry.EntryDate).ThenByDescending(entry => entry.CreatedAtUtc).ThenBy(entry => entry.Id)
            .Take(InvoicesShown)
            .Select(entry => new
            {
                entry.Id, LineId = entry.PurchaseOrderLineId!.Value, entry.EntryDate, entry.SourceDealer, entry.InvoiceNumber, entry.Description, entry.NetMinor,
                entry.GrossMinor
            })
            .ToListAsync(cancellationToken);

        // What posted deliveries say arrived, per line (a voided delivery counts for nothing).
        var receivedByLine = await db.Set<GoodsReceiptLine>().AsNoTracking()
            .Where(received => lineIds.Contains(received.PurchaseOrderLineId) &&
                db.Set<GoodsReceipt>().Any(receipt => receipt.Id == received.GoodsReceiptId && receipt.Status == GoodsReceiptStatus.Posted))
            .GroupBy(received => received.PurchaseOrderLineId)
            .Select(group => new { LineId = group.Key, Quantity = group.Sum(received => received.Quantity) })
            .ToListAsync(cancellationToken);
        var receipts = await db.Set<GoodsReceipt>().AsNoTracking().Where(receipt => receipt.PurchaseOrderId == id)
            .OrderByDescending(receipt => receipt.ReceivedDate).ThenByDescending(receipt => receipt.CreatedAtUtc).ThenBy(receipt => receipt.Id)
            .Take(ReceiptsShown)
            .Select(receipt => new
            {
                receipt.Id, receipt.ReceivedDate, receipt.DeliveryReceiptNumber, receipt.Note, receipt.Status,
                RecordedByName = db.Set<User>().Where(user => user.Id == receipt.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                RecordedAtUtc = receipt.CreatedAtUtc, receipt.VoidedAtUtc, receipt.VoidReason,
                VoidedByName = db.Set<User>().Where(user => user.Id == receipt.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                receipt.Version
            })
            .ToListAsync(cancellationToken);
        var receiptIds = receipts.Select(receipt => receipt.Id).ToList();
        // Always asked, even when there are no deliveries, so the order read costs the same number of statements whatever it holds.
        var receiptLines = await db.Set<GoodsReceiptLine>().AsNoTracking().Where(received => receiptIds.Contains(received.GoodsReceiptId))
            .Select(received => new { received.Id, received.GoodsReceiptId, received.PurchaseOrderLineId, received.Quantity })
            .ToListAsync(cancellationToken);

        var matchedByLine = matched.ToDictionary(item => item.LineId);
        var receivedQuantities = receivedByLine.ToDictionary(item => item.LineId, item => item.Quantity);
        var lineNumbers = lines.ToDictionary(line => line.Id, line => line.LineNumber);
        var lineDtos = lines.Select(line =>
        {
            matchedByLine.TryGetValue(line.Id, out var match);
            var invoicedNet = match?.Net ?? 0;
            var invoicedGross = match?.Gross ?? 0;
            var received = receivedQuantities.GetValueOrDefault(line.Id);
            var receivedValue = DeliveryRules.ReceivedValue(received, line.UnitCostMinor, line.VatTreatment, line.VatRateBps);
            return new PurchaseOrderLineDto(line.Id, line.LineNumber, line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description,
                line.ItemType.ToString(), line.Quantity, line.Unit, line.UnitCostMinor, line.VatTreatment.ToString(), line.VatRateBps, line.NetMinor,
                line.VatMinor, line.GrossMinor, invoicedNet, invoicedGross, Math.Max(0, line.NetMinor - invoicedNet), Math.Max(0, line.GrossMinor - invoicedGross),
                Math.Max(0, invoicedGross - line.GrossMinor), received, GoodsReceipt.LeftToReceive(line.Quantity, received), receivedValue.NetMinor,
                receivedValue.GrossMinor, DeliveryRules.InvoicedAheadOfDelivery(invoicedGross, receivedValue.GrossMinor));
        }).ToArray();

        var approved = header.Status == PurchaseOrderStatus.Approved;
        var invoiceCount = matched.Sum(item => item.Count);
        var canChange = !header.ProjectArchived;
        var deliveryState = DeliveryRules.StateOf(lines.Select(line => (line.Quantity, receivedQuantities.GetValueOrDefault(line.Id))));
        var receiptDtos = receipts.Select(receipt => new GoodsReceiptDto(receipt.Id, receipt.ReceivedDate, receipt.DeliveryReceiptNumber, receipt.Note,
            receipt.Status.ToString(), receipt.RecordedByName ?? "", receipt.RecordedAtUtc, receipt.VoidedByName, receipt.VoidedAtUtc, receipt.VoidReason,
            CanVoid: canChange && receipt.Status == GoodsReceiptStatus.Posted,
            receiptLines.Where(received => received.GoodsReceiptId == receipt.Id)
                .Select(received => new { received, Line = lines.First(line => line.Id == received.PurchaseOrderLineId) })
                .OrderBy(pair => pair.Line.LineNumber)
                .Select(pair => new GoodsReceiptLineDto(pair.received.Id, pair.received.PurchaseOrderLineId, pair.Line.LineNumber, pair.Line.Description,
                    pair.Line.Unit, pair.received.Quantity)).ToArray(),
            receipt.Version)).ToArray();
        return new PurchaseOrderDto(header.Id, header.Number, header.Status.ToString(), header.SupplierId, header.SupplierName ?? "", header.ProjectId,
            header.ProjectName ?? "", header.ProjectArchived, header.SubProjectId, header.SubProjectName, header.OrderDate, header.ExpectedDate,
            header.SupplierReference, header.Notes, header.NetMinor, header.VatMinor, header.GrossMinor, lineDtos.Sum(line => line.InvoicedNetMinor),
            lineDtos.Sum(line => line.InvoicedGrossMinor), approved ? lineDtos.Sum(line => line.RemainingNetMinor) : 0,
            approved ? lineDtos.Sum(line => line.RemainingGrossMinor) : 0, lineDtos,
            invoices.Select(item => new MatchedInvoiceDto(item.Id, item.LineId, lineNumbers.GetValueOrDefault(item.LineId), item.EntryDate, item.SourceDealer,
                item.InvoiceNumber, item.Description, item.NetMinor, item.GrossMinor)).ToArray(),
            invoiceCount, header.CreatedByName ?? "", header.CreatedAtUtc, header.ApprovedByName, header.ApprovedAtUtc, header.ClosedByName,
            header.ClosedAtUtc, header.CloseReason, header.CancelledByName, header.CancelledAtUtc, header.CancelReason,
            CanEdit: canChange && header.Status == PurchaseOrderStatus.Draft, CanApprove: canChange && header.Status == PurchaseOrderStatus.Draft,
            CanCancel: canChange && (header.Status == PurchaseOrderStatus.Draft || (approved && invoiceCount == 0 && receivedByLine.Count == 0)),
            CanClose: canChange && approved, deliveryState.ToString(), receiptDtos, header.ReceiptCount,
            CanReceive: canChange && approved && lineDtos.Any(line => line.ToReceiveQuantity > 0m), header.Version);
    }

    public async Task<IReadOnlyList<OpenPurchaseOrderLineDto>> ListOpenLinesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var approved = db.Set<PurchaseOrder>().AsNoTracking().Where(order => order.ProjectId == projectId && order.Status == PurchaseOrderStatus.Approved);
        var lines = await (
                from line in db.Set<PurchaseOrderLine>().AsNoTracking()
                join order in approved on line.PurchaseOrderId equals order.Id
                orderby order.Number, line.LineNumber
                select new
                {
                    line.Id, OrderId = order.Id, order.Number, line.LineNumber,
                    SupplierName = db.Set<Supplier>().Where(supplier => supplier.Id == order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                    line.Description, line.NetMinor, line.GrossMinor
                })
            .Take(OpenLinesShown)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0) return [];

        var approvedLineIds = db.Set<PurchaseOrderLine>().Where(line => approved.Any(order => order.Id == line.PurchaseOrderId)).Select(line => line.Id);
        var matched = (await MatchedEntries().Where(entry => approvedLineIds.Contains(entry.PurchaseOrderLineId!.Value))
            .GroupBy(entry => entry.PurchaseOrderLineId!.Value)
            .Select(group => new { LineId = group.Key, Net = group.Sum(entry => entry.NetMinor), Gross = group.Sum(entry => entry.GrossMinor) })
            .ToListAsync(cancellationToken)).ToDictionary(item => item.LineId);

        return lines.Select(line =>
        {
            matched.TryGetValue(line.Id, out var match);
            return new OpenPurchaseOrderLineDto(line.Id, line.OrderId, line.Number, line.LineNumber, line.SupplierName ?? "", line.Description, line.GrossMinor,
                Math.Max(0, line.NetMinor - (match?.Net ?? 0)), Math.Max(0, line.GrossMinor - (match?.Gross ?? 0)));
        }).ToArray();
    }

    /// <summary>The posted supplier invoices that are matched to an order line (a voided one never counts).</summary>
    private IQueryable<CostEntry> MatchedEntries() => db.Set<CostEntry>().AsNoTracking()
        .Where(entry => entry.PurchaseOrderLineId != null && entry.Status == CostEntryStatus.Posted);
}
