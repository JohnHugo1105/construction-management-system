using Cms.Application.Features.Costing;
using Cms.Domain.Features.Costing;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Costing;

/// <summary>Owned by the Costing feature, which may add members to <see cref="ICostEntryRepository"/> and here. Other features use only what the port declares.</summary>
internal sealed class CostEntryRepository(CmsDbContext db) : ICostEntryRepository
{
    public Task<CostEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<CostEntry>().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public void Add(CostEntry entry) => db.Set<CostEntry>().Add(entry);

    public async Task<IReadOnlyList<CostEntry>> ListByOriginAsync(CostOrigin origin, Guid originReferenceId, CancellationToken cancellationToken) =>
        await db.Set<CostEntry>().Where(entry => entry.Origin == origin && entry.OriginReferenceId == originReferenceId).ToListAsync(cancellationToken);
}
