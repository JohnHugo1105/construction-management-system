using Cms.Application.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Projects;

public interface IProjectRepository
{
    /// <summary>Loads a project with its sub-projects, tracked for change. Null when it doesn't exist in the organization.</summary>
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeProjectId, CancellationToken cancellationToken);

    void Add(Project project);
}

/// <summary>Reads that project straight into DTOs in SQL, without loading whole projects (see the performance standard).</summary>
public interface IProjectReader
{
    Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectListQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Id and name of every project that can still receive records, for pickers.</summary>
    Task<IReadOnlyList<ProjectLookupDto>> LookupAsync(bool includeArchived, CancellationToken cancellationToken);
}

public sealed record ProjectListQuery(string? Search, string? Status, bool IncludeArchived);

public sealed record ProjectListItemDto(Guid Id, string Name, string? Client, string? Location, string Status, DateOnly StartDate,
    DateOnly? TargetEndDate, long? ContractPriceMinor, int SubProjectCount, int Version);

public sealed record ProjectLookupDto(Guid Id, string Name, string Status, IReadOnlyList<SubProjectDto> SubProjects);

public sealed record SubProjectDto(Guid Id, string Name, DateOnly StartDate, string Status);

public sealed record ProjectDto(Guid Id, string Name, string? Client, string? Location, long? ContractPriceMinor, DateOnly StartDate,
    DateOnly? TargetEndDate, string Status, string? ArchiveReason, string? ArchiveNote, DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<SubProjectDto> SubProjects, int Version);

public sealed record CreateProjectRequest(string? Name, string? Client, string? Location, long? ContractPriceMinor, DateOnly StartDate,
    DateOnly? TargetEndDate);

public sealed record UpdateProjectRequest(string? Name, string? Client, string? Location, long? ContractPriceMinor, DateOnly StartDate,
    DateOnly? TargetEndDate, int Version);

public sealed record ArchiveProjectRequest(string? Reason, string? Note, int Version);

public sealed record VersionRequest(int Version);

public sealed record SaveSubProjectRequest(string? Name, DateOnly StartDate, int Version);

public static class ProjectMapper
{
    public static ProjectDto ToDto(Project project) => new(project.Id, project.Name, project.Client, project.Location, project.ContractPriceMinor,
        project.StartDate, project.TargetEndDate, project.Status.ToString(), project.ArchiveReason?.ToString(), project.ArchiveNote,
        project.ArchivedAtUtc, project.SubProjects.OrderBy(sub => sub.Name).Select(ToDto).ToArray(), project.Version);

    public static SubProjectDto ToDto(SubProject sub) => new(sub.Id, sub.Name, sub.StartDate, sub.Status.ToString());
}
