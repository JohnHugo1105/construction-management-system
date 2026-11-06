using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Audit;

/// <summary>
/// One line of the append-only audit trail: who changed what, and when. Rows are inserted and never updated or deleted.
/// The retired desktop app kept no trail at all.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserName { get; private set; } = "";
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>A stable dotted name such as <c>Project.Archived</c> or <c>CostEntry.Voided</c>.</summary>
    public string Action { get; private set; } = "";

    public string EntityType { get; private set; } = "";
    public Guid EntityId { get; private set; }
    public string Summary { get; private set; } = "";
    public string? CorrelationId { get; private set; }

    public static AuditEntry Record(Guid tenantId, Guid? userId, string userName, string action, string entityType, Guid entityId,
        string summary, string? correlationId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("An audit entry needs an organization.", nameof(tenantId));
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserName = Limit(userName, 120),
            OccurredAtUtc = now,
            Action = Limit(action, 80),
            EntityType = Limit(entityType, 80),
            EntityId = entityId,
            Summary = Limit(summary, 500),
            CorrelationId = correlationId is null ? null : Limit(correlationId, 80)
        };
    }

    private static string Limit(string value, int maximum)
    {
        var text = (value ?? "").Trim();
        return text.Length > maximum ? text[..maximum] : text;
    }
}
