using Cms.Application.Common;
using Cms.Domain.Features.Audit;

namespace Cms.Infrastructure.Persistence;

public sealed class AuditTrail(CmsDbContext db, ICurrentUser currentUser, IClock clock) : IAuditTrail
{
    public void Record(string action, string entityType, Guid entityId, string summary) =>
        db.Set<AuditEntry>().Add(AuditEntry.Record(currentUser.TenantId, currentUser.UserId, currentUser.UserName, action, entityType, entityId,
            summary, currentUser.CorrelationId, clock.UtcNow));

    public void RecordAs(Guid tenantId, Guid? userId, string userName, string action, string entityType, Guid entityId, string summary) =>
        db.Set<AuditEntry>().Add(AuditEntry.Record(tenantId, userId, userName, action, entityType, entityId, summary, currentUser.CorrelationId,
            clock.UtcNow));
}
