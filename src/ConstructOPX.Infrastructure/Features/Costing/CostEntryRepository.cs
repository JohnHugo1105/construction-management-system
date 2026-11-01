using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Costing;

/// <summary>Owned by the Costing feature, which may add members to <see cref="ICostEntryRepository"/> and here. Other features use only what the port declares.</summary>
internal sealed class CostEntryRepository(ConstructOpxDbContext db) : ICostEntryRepository
{
    public Task<CostEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<CostEntry>().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public void Add(CostEntry entry) => db.Set<CostEntry>().Add(entry);

    public async Task<IReadOnlyList<CostEntry>> ListByOriginAsync(CostOrigin origin, Guid originReferenceId, CancellationToken cancellationToken) =>
        await db.Set<CostEntry>().Where(entry => entry.Origin == origin && entry.OriginReferenceId == originReferenceId).ToListAsync(cancellationToken);

    public Task<CostEntry?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<CostEntry>().SingleOrDefaultAsync(entry => entry.ClientRequestId == clientRequestId, cancellationToken);

    // Matches the filtered unique index (tenant, project, supplier, invoice where posted and manual), so this is an index seek.
    public Task<DuplicateInvoice?> FindDuplicateInvoiceAsync(Guid projectId, string normalizedSourceDealer, string normalizedInvoiceNumber,
        Guid? excludeEntryId, CancellationToken cancellationToken) =>
        db.Set<CostEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.Status == CostEntryStatus.Posted && entry.Origin == CostOrigin.Manual &&
                entry.NormalizedSourceDealer == normalizedSourceDealer && entry.NormalizedInvoiceNumber == normalizedInvoiceNumber &&
                entry.Id != excludeEntryId)
            .OrderBy(entry => entry.EntryDate)
            .Select(entry => new DuplicateInvoice(entry.Id, entry.EntryDate))
            .FirstOrDefaultAsync(cancellationToken);
}
