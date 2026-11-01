using Cms.Application.Common;

namespace Cms.Application.Features.Audit;

public sealed record AuditEntryDto(Guid Id, DateTimeOffset OccurredAtUtc, string UserName, string Action, string EntityType, Guid EntityId, string Summary);

public sealed record AuditQuery(string? Search, string? EntityType, Guid? EntityId, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc);

public interface IAuditReader
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken);
}

public sealed class AuditQueryService(IAuditReader reader)
{
    public Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);
}
