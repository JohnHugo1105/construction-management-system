using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Change order demo for Riverside Residences: the budget is locked as the baseline, CO-001 (an approved roof upgrade that adds a line,
/// revises the floor tiles and raises the price) has moved the budget and the price, CO-002 (a basement membrane) waits for approval, and a
/// draft re-measures the rebar. Everything is built through the domain, so the baseline reconciles exactly. Call it after the main demo
/// data is saved (it reads the saved budget lines). Fictional figures.
/// </summary>
public static class ChangeOrderDemo
{
    public static async Task SeedAsync(ConstructOpxDbContext db, Guid tenantId, Guid projectId, Guid managerUserId, Guid costEngineerUserId, int vatRateBps,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // Nobody is signed in while seeding, so the tenant filter is replaced by an explicit tenant check.
        var project = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects)
            .SingleAsync(item => item.Id == projectId && item.TenantId == tenantId, cancellationToken);
        var lines = await db.Set<BudgetLine>().IgnoreQueryFilters()
            .Where(line => line.ProjectId == projectId && line.TenantId == tenantId && line.Status == BudgetLineStatus.Active)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0 || project.IsBudgetLocked) return;

        var lockedAt = now.AddDays(-60);
        project.LockBudgetBaseline(lines.Count, lines.Sum(line => line.NetMinor), lines.Sum(line => line.GrossMinor), managerUserId, lockedAt);

        var tiles = lines.FirstOrDefault(line => line.Description.StartsWith("Floor tiles", StringComparison.Ordinal));
        var rebar = lines.FirstOrDefault(line => line.Description.StartsWith("Deformed bars", StringComparison.Ordinal));
        var clubhouse = project.SubProjects.FirstOrDefault(sub => sub.Name == "Clubhouse" && sub.Status == SubProjectStatus.Active);

        // CO-001, approved: a roof upgrade on the clubhouse.
        var roof = ChangeOrder.Draft(Guid.NewGuid(), tenantId, project, "Clubhouse roof upgrade", "The client chose insulated roof panels and larger floor tiles.",
            ChangeOrderReason.ClientRequest, 350_000_00, costEngineerUserId, null, now.AddDays(-40));
        roof.AddLine(project, Add("Insulated roof panels", 120m, "m²", 1_850_00, clubhouse?.Id, today.AddDays(-40)), null, vatRateBps, now.AddDays(-40));
        if (tiles is not null)
            roof.AddLine(project, new ChangeOrderLineInput(ChangeOrderLineKind.Revise, tiles.Id, null, null, null, null, null, null, tiles.ItemType,
                tiles.Quantity + 400m, null, tiles.UnitCostMinor, tiles.VatTreatment, tiles.EntryDate, null), tiles, vatRateBps, now.AddDays(-40));
        roof.Submit(project, Targets(roof, lines), 1, costEngineerUserId, now.AddDays(-39));
        lines.AddRange(roof.Approve(project, Targets(roof, lines), managerUserId, "Agreed with the client in the site meeting.", now.AddDays(-38)));

        // CO-002, waiting for approval: a basement membrane.
        var membrane = ChangeOrder.Draft(Guid.NewGuid(), tenantId, project, "Basement waterproofing", "Groundwater found during excavation.",
            ChangeOrderReason.SiteCondition, 120_000_00, costEngineerUserId, null, now.AddDays(-5));
        membrane.AddLine(project, Add("Waterproofing membrane", 450m, "m²", 420_00, null, today.AddDays(-5)), null, vatRateBps, now.AddDays(-5));
        membrane.Submit(project, Targets(membrane, lines), 2, costEngineerUserId, now.AddDays(-4));

        // A draft: the rebar re-measured after the structural revision.
        var remeasure = ChangeOrder.Draft(Guid.NewGuid(), tenantId, project, "Rebar re-measure", null, ChangeOrderReason.DesignChange, 0, costEngineerUserId,
            null, now.AddDays(-1));
        if (rebar is not null)
            remeasure.AddLine(project, new ChangeOrderLineInput(ChangeOrderLineKind.Revise, rebar.Id, null, null, null, null, null, null, rebar.ItemType,
                rebar.Quantity - 1_500m, null, rebar.UnitCostMinor, rebar.VatTreatment, rebar.EntryDate, null), rebar, vatRateBps, now.AddDays(-1));

        db.AddRange(roof, membrane, remeasure);
        foreach (var line in lines.Where(line => db.Entry(line).State == EntityState.Detached)) db.Add(line);
        // The project's sequence continues after CO-002.
        db.Add(new NumberSequenceRow { TenantId = tenantId, Key = $"change-order:{projectId}", LastValue = 2 });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ChangeOrderLineInput Add(string description, decimal quantity, string unit, long unitCostMinor, Guid? subProjectId, DateOnly date) =>
        new(ChangeOrderLineKind.Add, null, subProjectId, null, null, null, null, description, ItemType.Material, quantity, unit, unitCostMinor,
            VatTreatment.Inclusive, date, null);

    private static IReadOnlyCollection<BudgetLine> Targets(ChangeOrder order, IEnumerable<BudgetLine> lines) =>
        lines.Where(line => order.TargetIds().Contains(line.Id)).ToArray();
}
