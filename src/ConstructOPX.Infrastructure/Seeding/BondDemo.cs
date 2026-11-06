using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo bonds for Riverside Residences (fictional): a performance bond the company posted to the client, valid for most of the job, and a
/// performance bond the electrical subcontractor gave the company that expires in 18 days, so the register shows an "expiring soon" bond.
/// A bond is not a cost and changes no figure anywhere. Saves what it adds.
/// </summary>
public static class BondDemo
{
    public static async Task SeedAsync(ConstructOpxDbContext db, Organization organization, Project riverside, Guid userId, DateOnly today, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        db.Add(Bond.Create(Guid.NewGuid(), tenant, riverside, BondDirection.Posted,
            new BondDetails(BondKind.Performance, "Visayas Surety Co.", "PB-2026-0147", 4_850_000_00, today.AddMonths(-6), today.AddMonths(9), null,
                "Surety bond for 10% of the contract price."),
            organization.CurrencyCode, userId, null, now));
        db.Add(Bond.Create(Guid.NewGuid(), tenant, riverside, BondDirection.Received,
            new BondDetails(BondKind.Performance, "Cebu Guaranty Bank", "SG-88231", 310_000_00, today.AddMonths(-5), today.AddDays(18), SubcontractDemo.ElectricalFirm,
                "Covers the electrical subcontract. Ask for an extension before it ends."),
            organization.CurrencyCode, userId, null, now));
        await db.SaveChangesAsync(cancellationToken);
    }
}
