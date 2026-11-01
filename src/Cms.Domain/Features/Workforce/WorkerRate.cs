namespace Cms.Domain.Features.Workforce;

/// <summary>
/// One line of a worker's pay history. Lines are only ever added: a rate is never edited in place, so what a worker was paid on any
/// day can always be answered. The desktop app let the rate be typed over in a grid with no record of the change.
/// </summary>
public sealed class WorkerRate
{
    private WorkerRate() { }

    internal WorkerRate(Guid id, Guid tenantId, Guid workerId, string designation, long rateMinor, RateUnit unit,
        RateChangeCategory category, DateOnly effectiveFrom, string? reason, Guid recordedByUserId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkerId = workerId;
        Designation = designation;
        RateMinor = rateMinor;
        Unit = unit;
        Category = category;
        EffectiveFrom = effectiveFrom;
        Reason = reason;
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkerId { get; private set; }
    public string Designation { get; private set; } = "";
    public long RateMinor { get; private set; }
    public RateUnit Unit { get; private set; }
    public RateChangeCategory Category { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public string? Reason { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
