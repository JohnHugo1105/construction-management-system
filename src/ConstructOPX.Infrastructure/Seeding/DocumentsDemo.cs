using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo documents for Riverside Residences (fictional): the building permit, which expires in 14 days so "Needs your action" and the
/// Documents tab show an expiring permit; the architectural drawings with a revised second version; the contractor's all-risk insurance; and the
/// signed contract. Every version is a link to an example address, so no stored file is needed (and none is missing when someone downloads).
/// Saves what it adds.
/// </summary>
public static class DocumentsDemo
{
    /// <summary>
    /// Adds the four demo documents to <paramref name="riverside"/> and saves them. Call it once from the development seeder, after the
    /// organization, the project and the user are saved (it does not check whether it ran before), for example
    /// <c>await DocumentsDemo.SeedAsync(db, organization, riverside, users["manager"].Id, today, now, cancellationToken);</c>
    /// </summary>
    /// <param name="db">The seeder's context.</param>
    /// <param name="organization">The demo company the documents belong to.</param>
    /// <param name="riverside">The project that gets the documents; it must not be archived.</param>
    /// <param name="userId">Who added them (shown as "Added by").</param>
    /// <param name="today">The company's calendar day; the permit's valid-until date is 14 days after it.</param>
    /// <param name="now">The current time; the documents are dated weeks before it.</param>
    public static async Task SeedAsync(ConstructOpxDbContext db, Organization organization, Project riverside, Guid userId, DateOnly today,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;

        db.Add(ProjectDocument.Create(Guid.NewGuid(), tenant, riverside,
            new DocumentDetails("Building permit", "Issued by the city building official for the full structure.", DocumentCategory.Permit, today.AddDays(14)),
            null, "https://files.example.com/riverside/building-permit.pdf", "Renewal application is with the city office.", userId, null, now.AddDays(-60)));

        var drawings = ProjectDocument.Create(Guid.NewGuid(), tenant, riverside,
            new DocumentDetails("Architectural drawings", "Floor plans, elevations and sections.", DocumentCategory.Drawing, null),
            null, "https://files.example.com/riverside/architectural-set-rev0.pdf", "Issued for construction.", userId, null, now.AddDays(-90));
        drawings.AddVersion(riverside, null, "https://files.example.com/riverside/architectural-set-rev1.pdf", "Revision 1: stair core moved 600 mm.",
            userId, now.AddDays(-20));
        db.Add(drawings);

        db.Add(ProjectDocument.Create(Guid.NewGuid(), tenant, riverside,
            new DocumentDetails("Contractor's all-risk insurance", null, DocumentCategory.Insurance, today.AddMonths(8)),
            null, "https://files.example.com/riverside/car-policy.pdf", null, userId, null, now.AddDays(-75)));

        db.Add(ProjectDocument.Create(Guid.NewGuid(), tenant, riverside,
            new DocumentDetails("Signed construction contract", "Lump-sum contract with Riverside Land Corp.", DocumentCategory.Contract, null),
            null, "https://files.example.com/riverside/contract-signed.pdf", null, userId, null, now.AddDays(-100)));

        await db.SaveChangesAsync(cancellationToken);
    }
}
