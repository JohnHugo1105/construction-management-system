using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Audit;

namespace ConstructOPX.Infrastructure.Persistence;

public sealed class AuditTrail(ConstructOpxDbContext db, ICurrentUser currentUser, IClock clock) : IAuditTrail
{
    public void Record(string action, string entityType, Guid entityId, string summary) =>
        db.Set<AuditEntry>().Add(AuditEntry.Record(currentUser.TenantId, currentUser.UserId, currentUser.UserName, action, entityType, entityId,
            summary, currentUser.CorrelationId, clock.UtcNow));

    public void RecordAs(Guid tenantId, Guid? userId, string userName, string action, string entityType, Guid entityId, string summary) =>
        db.Set<AuditEntry>().Add(AuditEntry.Record(tenantId, userId, userName, action, entityType, entityId, summary, currentUser.CorrelationId,
            clock.UtcNow));
}
