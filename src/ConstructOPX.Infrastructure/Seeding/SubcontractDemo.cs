using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Subcontracts;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo subcontracts for Riverside Residences: an awarded electrical subcontract with a variation, two certified claims (their cost posted
/// to the project, exactly as certifying does) and the first claim paid; and a waterproofing subcontract still in draft. Built through the
/// domain so the figures are the real calculation. Obviously fictional firm names. Call it after the project, the users and the organization
/// are saved (it saves its own records); it does nothing if its firms already exist.
/// </summary>
public static class SubcontractDemo
{
    internal const string ElectricalFirm = "Lumen Electrical Contractors (demo)";
    private const string WaterproofingFirm = "Seal-Tight Waterproofing (demo)";

    /// <param name="drafterUserId">Who drafts subcontracts and claims (a project manager or cost engineer).</param>
    /// <param name="approverUserId">Who awards, certifies and records payments (an accountant or administrator).</param>
    public static async Task SeedAsync(ConstructOpxDbContext db, Organization organization, Project riverside, Guid drafterUserId, Guid approverUserId,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        if (await db.Set<Supplier>().IgnoreQueryFilters().AnyAsync(firm => firm.TenantId == tenant &&
                (firm.NormalizedName == ElectricalFirm.ToUpperInvariant() || firm.NormalizedName == WaterproofingFirm.ToUpperInvariant()), cancellationToken))
            return;

        var electrical = Supplier.Create(Guid.NewGuid(), tenant, ElectricalFirm, SupplierKind.Subcontractor, "000-111-222-000", "Ramon Villanueva",
            "0917 555 0101", "projects@lumen.example", "Mandaue City", "Demo firm", now);
        var waterproofing = Supplier.Create(Guid.NewGuid(), tenant, WaterproofingFirm, SupplierKind.Subcontractor | SupplierKind.Supplier, null,
            "Liza Ocampo", "0918 555 0202", null, "Cebu City", "Demo firm", now);
        db.AddRange(electrical, waterproofing);

        var towerA = riverside.SubProjects.FirstOrDefault(sub => sub.Name == "Tower A")?.Id;

        // SC-0001: electrical works, awarded. Before VAT 2,750,000.00 plus a 150,000.00 variation; 10% retention, 2% withholding.
        var sc1 = Subcontract.Create(Guid.NewGuid(), tenant, electrical, riverside, towerA, "Electrical rough-in and fit-out, Tower A",
            "Includes panel boards and lighting fixtures supplied by the subcontractor.", 2_750_000_00, VatTreatment.Exclusive, organization.VatRateBps,
            1_000, 200, drafterUserId, null, now);
        sc1.AssignNumber(Subcontract.FormatNumber(1));
        sc1.Award(electrical, riverside, approverUserId, now);
        sc1.AddVariation(Guid.NewGuid(), "Additional lighting circuits at the lobby", 150_000_00, today.AddDays(-50), approverUserId, today, now);

        var claim1 = sc1.DraftClaim(Guid.NewGuid(), today.AddDays(-40), today.AddDays(-45), 900_000_00, null, drafterUserId, null, today, now);
        Certify(db, sc1, claim1.Id, 1, riverside, electrical.Name, approverUserId, now);
        var claim2 = sc1.DraftClaim(Guid.NewGuid(), today.AddDays(-10), today.AddDays(-14), 1_650_000_00, null, drafterUserId, null, today, now);
        Certify(db, sc1, claim2.Id, 2, riverside, electrical.Name, approverUserId, now);
        sc1.RecordClaimPayment(Guid.NewGuid(), claim1.Id, sc1.ClaimOutstandingMinor(claim1), today.AddDays(-30), SubcontractPaymentMethod.BankTransfer,
            "BDO-778812", null, approverUserId, null, today, now);

        // SC-0002: waterproofing, still a draft (VAT included in the sum), so it commits nothing yet.
        var sc2 = Subcontract.Create(Guid.NewGuid(), tenant, waterproofing, riverside, null, "Roof deck waterproofing membrane", null, 480_000_00,
            VatTreatment.Inclusive, organization.VatRateBps, 1_000, 0, drafterUserId, null, now);
        sc2.AssignNumber(Subcontract.FormatNumber(2));
        db.AddRange(sc1, sc2);

        // The sequences continue after the demo numbers.
        db.Add(new NumberSequenceRow { TenantId = tenant, Key = "subcontract", LastValue = 2 });
        db.Add(new NumberSequenceRow { TenantId = tenant, Key = $"subcontract-claim:{sc1.Id}", LastValue = 2 });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Certifies and posts the cost the way the service does, so the demo project's cost equals the certified claims.</summary>
    private static void Certify(ConstructOpxDbContext db, Subcontract subcontract, Guid claimId, int number, Project project, string firmName, Guid userId,
        DateTimeOffset now)
    {
        var costId = Guid.NewGuid();
        var claim = subcontract.CertifyClaim(claimId, number, costId, userId, now);
        db.Add(CostEntry.RecordSystem(costId, subcontract.TenantId, project, subcontract.SubProjectId, CostOrigin.Subcontract, claim.Id,
            $"{subcontract.Number} claim {number}: {subcontract.Title}", ItemType.Subcontract, 1m, "l.s", claim.ThisClaimBaseMinor,
            subcontract.VatTreatment, subcontract.VatRateBps, claim.ClaimDate, userId, now, firmName));
    }
}
