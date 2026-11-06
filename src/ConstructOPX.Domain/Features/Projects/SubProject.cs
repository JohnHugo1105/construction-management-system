using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Projects;

/// <summary>A part of a project (a building, a phase). Costs and budget lines may point at it or just at the project.</summary>
public sealed class SubProject
{
    private SubProject() { }

    internal SubProject(Guid id, Guid tenantId, Guid projectId, string name, DateOnly startDate)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Name = name;
        NormalizedName = Guard.Normalize(name);
        StartDate = startDate;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public DateOnly StartDate { get; private set; }
    public SubProjectStatus Status { get; private set; } = SubProjectStatus.Active;

    internal void Rename(string name, DateOnly startDate)
    {
        Name = name;
        NormalizedName = Guard.Normalize(name);
        StartDate = startDate;
    }

    /// <summary>True when this sub-project was archived only because its whole project was, so restoring the project brings it back.</summary>
    public bool ArchivedWithProject { get; private set; }

    internal void Archive()
    {
        Status = SubProjectStatus.Archived;
        ArchivedWithProject = false;
    }

    internal void Restore()
    {
        Status = SubProjectStatus.Active;
        ArchivedWithProject = false;
    }

    /// <summary>Archives the sub-project along with its project. One that was already archived on its own stays that way.</summary>
    internal void ArchiveWithProject()
    {
        if (Status != SubProjectStatus.Active) return;
        Status = SubProjectStatus.Archived;
        ArchivedWithProject = true;
    }

    internal void RestoreWithProject()
    {
        if (!ArchivedWithProject) return;
        Restore();
    }
}
