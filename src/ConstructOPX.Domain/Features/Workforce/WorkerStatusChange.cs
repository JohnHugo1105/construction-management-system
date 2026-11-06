namespace ConstructOPX.Domain.Features.Workforce;

/// <summary>
/// One line of a worker's status history: from which day the worker was on leave, suspended, back at work, or gone. Lines are only
/// added. The history is what answers "could this worker be recorded as working on that day?" long after the status changed again;
/// the status held on the worker alone would forget a suspension once the worker came back.
/// </summary>
public sealed class WorkerStatusChange
{
    private WorkerStatusChange() { }

    internal WorkerStatusChange(Guid id, Guid tenantId, Guid workerId, WorkerStatus status, DateOnly effectiveOn, string? reason, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkerId = workerId;
        Status = status;
        EffectiveOn = effectiveOn;
        Reason = reason;
        RecordedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkerId { get; private set; }
    public WorkerStatus Status { get; private set; }

    /// <summary>The day the status began. For a worker who left it is the last day they worked.</summary>
    public DateOnly EffectiveOn { get; private set; }

    public string? Reason { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    /// <summary>The first day this status applies: the day after the last day worked for a worker who left, otherwise the effective day.</summary>
    public DateOnly AppliesFrom => WorkerStatusRules.IsTerminal(Status) ? EffectiveOn.AddDays(1) : EffectiveOn;
}
