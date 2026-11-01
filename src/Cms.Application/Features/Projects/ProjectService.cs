using Cms.Application.Common;
using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Projects;

/// <summary>
/// Project use cases. A project is archived with a reason and can be restored; it is never deleted (the desktop app's remove
/// left budget lines and costs pointing at nothing).
/// </summary>
public sealed class ProjectService(IProjectRepository projects, IProjectReader reader, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    // The contract price is what the company will be paid, so it is shown only to the people who see money: a warehouse keeper or an
    // HR officer can open a project to post stock or attendance without learning the price.
    private bool CanSeeContractPrice => Permissions.IsAllowed(currentUser.Roles, Permissions.CostsRead);

    public async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var result = await reader.ListAsync(query, page, cancellationToken);
        return CanSeeContractPrice
            ? result
            : result with { Items = result.Items.Select(item => item with { ContractPriceMinor = null }).ToArray() };
    }

    public Task<IReadOnlyList<ProjectLookupDto>> LookupAsync(bool includeArchived, CancellationToken cancellationToken) =>
        reader.LookupAsync(includeArchived, cancellationToken);

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await Load(id, cancellationToken));

    private ProjectDto ToDto(Project project)
    {
        var dto = ProjectMapper.ToDto(project);
        return CanSeeContractPrice ? dto : dto with { ContractPriceMinor = null };
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = Project.Create(Guid.NewGuid(), currentUser.TenantId, request.Name ?? "", request.Client, request.Location,
            request.ContractPriceMinor, request.StartDate, request.TargetEndDate, clock.UtcNow);
        await EnsureNameIsFreeAsync(project, null, cancellationToken);

        projects.Add(project);
        audit.Record("Project.Created", nameof(Project), project.Id, $"Created project {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        var previousName = project.Name;
        project.UpdateDetails(request.Name ?? "", request.Client, request.Location, request.ContractPriceMinor, request.StartDate,
            request.TargetEndDate, clock.UtcNow);
        await EnsureNameIsFreeAsync(project, project.Id, cancellationToken);

        audit.Record("Project.Updated", nameof(Project), project.Id,
            previousName == project.Name ? $"Updated project {project.Name}." : $"Updated project {previousName}, now named {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public Task<ProjectDto> StartAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.Started", "Started", (project, now) => project.Start(now), cancellationToken);

    public Task<ProjectDto> PutOnHoldAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.PutOnHold", "Put on hold", (project, now) => project.PutOnHold(now), cancellationToken);

    public Task<ProjectDto> ResumeAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.Resumed", "Resumed", (project, now) => project.Resume(now), cancellationToken);

    public Task<ProjectDto> CompleteAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.Completed", "Marked completed", (project, now) => project.Complete(now), cancellationToken);

    public Task<ProjectDto> ReopenAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.Reopened", "Reopened", (project, now) => project.Reopen(now), cancellationToken);

    public Task<ProjectDto> RestoreAsync(Guid id, VersionRequest request, CancellationToken cancellationToken) =>
        Transition(id, request, "Project.Restored", "Restored from the archive", (project, now) => project.Restore(now), cancellationToken);

    public async Task<ProjectDto> ArchiveAsync(Guid id, ArchiveProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        if (!Enum.TryParse<ArchiveReason>(request.Reason, ignoreCase: true, out var reason) || !Enum.IsDefined(reason))
            throw new ValidationFailedException("reason", "Choose why the project is being archived.");

        project.Archive(reason, request.Note, clock.UtcNow);
        audit.Record("Project.Archived", nameof(Project), project.Id,
            $"Archived project {project.Name} ({(reason == ArchiveReason.Finished ? "finished" : request.Note!.Trim())}).");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> AddSubProjectAsync(Guid projectId, SaveSubProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        var sub = project.AddSubProject(Guid.NewGuid(), request.Name ?? "", request.StartDate, clock.UtcNow);
        audit.Record("SubProject.Created", nameof(SubProject), sub.Id, $"Added sub-project {sub.Name} to {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateSubProjectAsync(Guid projectId, Guid subProjectId, SaveSubProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        project.UpdateSubProject(subProjectId, request.Name ?? "", request.StartDate, clock.UtcNow);
        audit.Record("SubProject.Updated", nameof(SubProject), subProjectId, $"Updated a sub-project of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> ArchiveSubProjectAsync(Guid projectId, Guid subProjectId, VersionRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        project.ArchiveSubProject(subProjectId, clock.UtcNow);
        audit.Record("SubProject.Archived", nameof(SubProject), subProjectId, $"Archived a sub-project of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> RestoreSubProjectAsync(Guid projectId, Guid subProjectId, VersionRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        project.RestoreSubProject(subProjectId, clock.UtcNow);
        audit.Record("SubProject.Restored", nameof(SubProject), subProjectId, $"Restored a sub-project of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    private async Task<ProjectDto> Transition(Guid id, VersionRequest request, string action, string verb, Action<Project, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        change(project, clock.UtcNow);
        audit.Record(action, nameof(Project), project.Id, $"{verb}: {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    private async Task<Project> Load(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task EnsureNameIsFreeAsync(Project project, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await projects.NameExistsAsync(project.NormalizedName, excludeId, cancellationToken))
            throw new ValidationFailedException("name", $"You already have a project named \"{project.Name}\". Choose a different name.");
    }
}
