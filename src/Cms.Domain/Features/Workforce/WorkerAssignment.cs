namespace Cms.Domain.Features.Workforce;

/// <summary>
/// Where a worker was posted. An assignment covers <see cref="StartDate"/> up to, but not including, <see cref="EndDate"/>; an
/// assignment without an end date is the current one. A worker has at most one current assignment.
/// </summary>
public sealed class WorkerAssignment
{
    private WorkerAssignment() { }

    internal WorkerAssignment(Guid id, Guid tenantId, Guid workerId, Guid projectId, Guid? subProjectId, DateOnly startDate,
        Guid assignedByUserId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkerId = workerId;
        ProjectId = projectId;
        SubProjectId = subProjectId;
        StartDate = startDate;
        AssignedByUserId = assignedByUserId;
        AssignedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }

    public bool IsCurrent => EndDate is null;

    internal void End(DateOnly endDate) => EndDate = endDate;
}
