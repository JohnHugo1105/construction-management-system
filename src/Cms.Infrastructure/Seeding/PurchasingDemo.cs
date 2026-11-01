using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Seeding;

/// <summary>
/// A small, obviously fictional purchasing demo for the Riverside Residences project the development seeder creates: the firms the demo's
/// invoices already name, and four purchase orders in the four states a person meets (approved and partly invoiced, approved and not yet
/// invoiced and part delivered, still a draft, closed early). The approved orders are what the dashboard and the Budget tab show as committed cost.
/// The seeder calls this with its own organization, project, users and clock; it saves what it adds.
/// </summary>
public static class PurchasingDemo
{
    public static async Task SeedAsync(CmsDbContext db, Organization organization, Project riverside, Guid costEngineerUserId, Guid accountantUserId,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        var vat = organization.VatRateBps;

        Supplier Firm(string name, string contact, string phone)
        {
            var normalized = Domain.Common.Guard.Normalize(name);
            if (db.Set<Supplier>().Local.FirstOrDefault(item => item.TenantId == tenant && item.NormalizedName == normalized) is { } existing) return existing;
            var firm = Supplier.Create(Guid.NewGuid(), tenant, name, SupplierKind.Supplier, null, contact, phone, null, "Cebu", null, now);
            db.Add(firm);
            return firm;
        }

        var tiles = Firm("Tilemart Cebu", "Rina Uy", "0917 555 0101");
        var plumbing = Firm("Cebu Plumbing Supply", "Ben Lim", "0917 555 0102");
        var blocks = Firm("Cebu Block Makers", "Joel Tan", "0917 555 0103");
        var steel = Firm("Visayas Steel Trading", "Mara Co", "0917 555 0104");
        Firm("Metro Concrete Batching", "Dan Sy", "0917 555 0105");

        var number = 0;
        PurchaseOrder Order(Supplier firm, DateOnly orderDate, DateOnly? expected, string? reference, string? notes, params PurchaseOrderLineInput[] lines)
        {
            var order = PurchaseOrder.CreateDraft(Guid.NewGuid(), tenant, firm, riverside, null, orderDate, expected, reference, notes, lines, vat,
                costEngineerUserId, null, today, now);
            order.AssignNumber(++number);
            db.Add(order);
            return order;
        }

        PurchaseOrderLineInput Line(int division, string wbs, string description, ItemType type, decimal quantity, string unit, long unitCostMinor) =>
            new(division, null, null, wbs, description, type, quantity, unit, unitCostMinor, VatTreatment.Inclusive);

        void Invoice(PurchaseOrder order, int lineIndex, string description, decimal quantity, string unit, long unitCostMinor, string dealer, string invoice, int daysAgo)
        {
            var line = order.Lines[lineIndex];
            var entry = CostEntry.RecordManual(Guid.NewGuid(), tenant, riverside, null, line.Division, line.MainScope, line.SubScope, line.WbsCode, description,
                line.ItemType, quantity, unit, unitCostMinor, VatTreatment.Inclusive, vat, today.AddDays(-daysAgo), null, dealer, invoice, costEngineerUserId, null, today, now);
            entry.LinkToPurchaseOrderLine(line.Id);
            db.Add(entry);
        }

        // PO-0001: floor tiles, approved, one of two deliveries invoiced: 2,000 m² at 790.00 (1,580,000.00), 800 m² invoiced (632,000.00).
        var tilesOrder = Order(tiles, today.AddDays(-30), today.AddDays(10), "TM-Q-2210", "Second delivery for the lobby and corridors.",
            Line(9, "9300", "Floor tiles 60x60", ItemType.Material, 2_000m, "m²", 790_00));
        tilesOrder.Approve(tiles, riverside, accountantUserId, now);
        Invoice(tilesOrder, 0, "Floor tiles 60x60", 800m, "m²", 790_00, tiles.Name, "TM-31044", 7);
        // The first truckload arrived 8 days ago: 800 m² of the 2,000 m² ordered. The rest is still to come.
        db.Add(GoodsReceipt.Record(Guid.NewGuid(), tenant, tilesOrder, riverside, today.AddDays(-8), "DR-5521", "First truckload, delivered to the lobby.",
            [new GoodsReceiptLineInput(tilesOrder.Lines[0].Id, 800m)], new Dictionary<Guid, decimal>(), false, costEngineerUserId, null, today, now));

        // PO-0002: plumbing fixtures, approved, nothing invoiced yet: 1,850,000.00 committed.
        var plumbingOrder = Order(plumbing, today.AddDays(-12), today.AddDays(45), "CPS-PO-118", null,
            Line(15, "15400", "Plumbing fixtures, complete set", ItemType.Material, 1m, "lot", 1_850_000_00));
        plumbingOrder.Approve(plumbing, riverside, accountantUserId, now);

        // PO-0003: hollow blocks, a draft waiting for someone to approve it.
        Order(blocks, today.AddDays(-2), today.AddDays(14), null, "Needs approval before the supplier is told.",
            Line(4, "4200", "Hollow blocks 6 inch", ItemType.Material, 30_000m, "pcs", 14_50));

        // PO-0004: tie wire, approved, then closed because the supplier delivered less: 1,200 kg ordered, 700 kg invoiced and the rest released.
        var wireOrder = Order(steel, today.AddDays(-60), today.AddDays(-40), null, null,
            Line(3, "3200", "Tie wire, 16 gauge", ItemType.Material, 1_200m, "kg", 68_00));
        wireOrder.Approve(steel, riverside, accountantUserId, now);
        Invoice(wireOrder, 0, "Tie wire, 16 gauge", 700m, "kg", 68_00, steel.Name, "VST-5610", 45);
        wireOrder.Close(riverside, "The supplier could deliver only 700 kg.", accountantUserId, now);

        db.Add(new NumberSequenceRow { TenantId = tenant, Key = Application.Features.Purchasing.PurchaseOrderService.NumberSequenceKey, LastValue = number });
        await db.SaveChangesAsync(cancellationToken);
    }
}
