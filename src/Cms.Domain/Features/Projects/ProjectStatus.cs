namespace Cms.Domain.Features.Projects;

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Archived = 4
}

/// <summary>The two reasons the retired desktop app offered when moving a project to archives.</summary>
public enum ArchiveReason
{
    Finished = 0,
    Other = 1
}

public enum SubProjectStatus
{
    Active = 0,
    Archived = 1
}
