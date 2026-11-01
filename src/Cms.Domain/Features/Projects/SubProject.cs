using Cms.Domain.Common;

namespace Cms.Domain.Features.Projects;

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

    internal void Archive() => Status = SubProjectStatus.Archived;

    internal void Restore() => Status = SubProjectStatus.Active;
}
