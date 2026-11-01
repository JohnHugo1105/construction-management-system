namespace Cms.Domain.Common;

/// <summary>
/// Base for records that belong to one organization (the tenant) and are edited concurrently by several people.
/// <see cref="Version"/> is an optimistic-concurrency token that the persistence layer increments on every update, so two
/// people saving the same record cannot silently overwrite each other.
/// </summary>
public abstract class AggregateRoot
{
    public Guid Id { get; protected set; }
    public Guid TenantId { get; protected set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; protected set; }
    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    protected void Initialize(Guid id, Guid tenantId, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("A record needs an id.", nameof(id));
        if (tenantId == Guid.Empty) throw new ArgumentException("A record needs an organization.", nameof(tenantId));
        Id = id;
        TenantId = tenantId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    protected void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
