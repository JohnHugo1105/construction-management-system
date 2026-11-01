using Cms.Application.Common;

namespace Cms.Application.Features.Audit;

public sealed record AuditEntryDto(Guid Id, DateTimeOffset OccurredAtUtc, string UserName, string Action, string EntityType, Guid EntityId, string Summary);

/// <param name="IncludeSignIns">
/// Successful sign-ins are the most frequent entries and drown out everything else, so they are left out unless asked for. Failed
/// sign-ins are always shown: they matter for security.
/// </param>
public sealed record AuditQuery(string? Search, string? EntityType, Guid? EntityId, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc,
    bool IncludeSignIns = false)
{
    public const string SignedInAction = "User.SignedIn";
}

public interface IAuditReader
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken);
}

public sealed class AuditQueryService(IAuditReader reader)
{
    public Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);
}
