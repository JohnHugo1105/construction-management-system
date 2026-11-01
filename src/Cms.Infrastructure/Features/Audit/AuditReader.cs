using Cms.Application.Common;
using Cms.Application.Features.Audit;
using Cms.Domain.Features.Audit;
using Cms.Infrastructure.Common;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Audit;

internal sealed class AuditReader(CmsDbContext db) : IAuditReader
{
    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var entries = db.Set<AuditEntry>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType)) entries = entries.Where(entry => entry.EntityType == query.EntityType);
        if (query.EntityId is { } entityId) entries = entries.Where(entry => entry.EntityId == entityId);
        if (query.FromUtc is { } from) entries = entries.Where(entry => entry.OccurredAtUtc >= from);
        if (query.ToUtc is { } to) entries = entries.Where(entry => entry.OccurredAtUtc < to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Sql.Contains(query.Search);
            entries = entries.Where(entry => EF.Functions.Like(entry.Summary, pattern, Sql.LikeEscape) ||
                EF.Functions.Like(entry.UserName, pattern, Sql.LikeEscape) || EF.Functions.Like(entry.Action, pattern, Sql.LikeEscape));
        }

        var total = await entries.CountAsync(cancellationToken);
        var items = await entries.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(entry => new AuditEntryDto(entry.Id, entry.OccurredAtUtc, entry.UserName, entry.Action, entry.EntityType, entry.EntityId, entry.Summary))
            .ToListAsync(cancellationToken);
        return PagedResult<AuditEntryDto>.Of(items, total, page);
    }
}
