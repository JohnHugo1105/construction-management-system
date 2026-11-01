using Cms.Application.Common;
using Cms.Application.Features.Purchasing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;
using Cms.Infrastructure.Common;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Purchasing;

/// <summary>
/// Reads orders straight into DTOs. The list is two statements (a count and a page); one order is four (the order, its lines, what is
/// invoiced per line, and the newest invoices), however many orders, lines or invoices exist.
/// </summary>
internal sealed class PurchaseOrderReader(CmsDbContext db) : IPurchaseOrderReader
{
    private const int InvoicesShown = 100;
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
                order.OrderDate, order.ExpectedDate, order.Status, LineCount = order.Lines.Count, order.NetMinor, order.VatMinor, order.GrossMinor
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new PurchaseOrderListItemDto(row.Id, row.Number, row.SupplierId, row.SupplierName ?? "", row.ProjectId,
            row.ProjectName ?? "", row.OrderDate, row.ExpectedDate, row.Status.ToString(), row.LineCount, row.NetMinor, row.VatMinor, row.GrossMinor)).ToArray();
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

        var matchedByLine = matched.ToDictionary(item => item.LineId);
        var lineNumbers = lines.ToDictionary(line => line.Id, line => line.LineNumber);
        var lineDtos = lines.Select(line =>
        {
            matchedByLine.TryGetValue(line.Id, out var match);
            var invoicedNet = match?.Net ?? 0;
            var invoicedGross = match?.Gross ?? 0;
            return new PurchaseOrderLineDto(line.Id, line.LineNumber, line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description,
                line.ItemType.ToString(), line.Quantity, line.Unit, line.UnitCostMinor, line.VatTreatment.ToString(), line.VatRateBps, line.NetMinor,
                line.VatMinor, line.GrossMinor, invoicedNet, invoicedGross, Math.Max(0, line.NetMinor - invoicedNet), Math.Max(0, line.GrossMinor - invoicedGross),
                Math.Max(0, invoicedGross - line.GrossMinor));
        }).ToArray();

        var approved = header.Status == PurchaseOrderStatus.Approved;
        var invoiceCount = matched.Sum(item => item.Count);
        var canChange = !header.ProjectArchived;
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
            CanCancel: canChange && (header.Status == PurchaseOrderStatus.Draft || (approved && invoiceCount == 0)),
            CanClose: canChange && approved, header.Version);
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
