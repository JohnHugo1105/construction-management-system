namespace Cms.Domain.Features.Workforce;

/// <summary>An incident or disciplinary note about a worker. Cases are only added; they are never edited or deleted.</summary>
public sealed class WorkerCase
{
    private WorkerCase() { }

    internal WorkerCase(Guid id, Guid tenantId, Guid workerId, Guid? projectId, DateOnly caseDate, string title, string? remarks,
        Guid recordedByUserId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkerId = workerId;
        ProjectId = projectId;
        CaseDate = caseDate;
        Title = title;
        Remarks = remarks;
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public DateOnly CaseDate { get; private set; }
    public string Title { get; private set; } = "";
    public string? Remarks { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
