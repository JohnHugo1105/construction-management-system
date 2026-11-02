using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Collaboration;

/// <summary>
/// Tasks: give a piece of work to a colleague (or yourself), optionally on a project and about one record, then complete, cancel, reassign
/// or reopen it. A task is seen only by its creator, its assignee and Administrators: anyone else gets "not found", even with the id. Each step
/// checks the version the person opened and saves once, with its audit line and the notification it causes. No amount is ever part of a task.
/// </summary>
public sealed class WorkTaskService(IWorkTaskRepository tasks, IWorkTaskReader reader, ICollaborationTargetReader targets, IColleagueReader colleagues,
    IProjectRepository projects, IOrganizationRepository organizations, INotificationWriter notifications, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    /// <summary>The caller's tasks: assigned to them (the default), created by them, or (Administrators only) every task; optionally one project's.</summary>
    public async Task<PagedResult<WorkTaskListItemDto>> ListAsync(string? scope, string? status, Guid? projectId, PageRequest page,
        CancellationToken cancellationToken)
    {
        var parsedScope = string.IsNullOrWhiteSpace(scope) ? TaskScope.Assigned : ParseEnum<TaskScope>(scope, "scope", "Choose assigned, created or all.");
        if (parsedScope == TaskScope.All && !CollaborationAccess.IsAdministrator(currentUser.Roles))
            throw new UnauthorizedAccessException("Only an Administrator sees every task.");
        WorkTaskStatus? parsedStatus = string.IsNullOrWhiteSpace(status) ? null : ParseEnum<WorkTaskStatus>(status, "status", "Choose Open, Done or Cancelled.");
        return await reader.ListAsync(new TaskListFilter(parsedScope, currentUser.UserId, parsedStatus, projectId is { } id && id != Guid.Empty ? id : null), await TodayAsync(cancellationToken), page,
            cancellationToken);
    }

    public async Task<WorkTaskDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await reader.GetAsync(id, await TodayAsync(cancellationToken), cancellationToken);
        if (task is null || !Actor().IsAdministrator && currentUser.UserId != task.CreatedByUserId && currentUser.UserId != task.AssigneeUserId)
            throw new NotFoundException("that task");
        return task with { Actions = ActionsFor(task) };
    }

    // ---- Changes ----------------------------------------------------------------------------------------------------------------

    public async Task<WorkTaskDto> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await tasks.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, cancellationToken);

        var (link, linkTarget) = await ResolveLinkAsync(request.LinkKind, request.LinkId, cancellationToken);
        var projectId = request.ProjectId;
        if (linkTarget?.ProjectId is { } linkedProject)
        {
            if (projectId is { } chosen && chosen != linkedProject)
                throw new ValidationFailedException("projectId", "The linked record belongs to another project. Choose that project, or leave the project empty.");
            projectId = linkedProject;
        }
        var project = projectId is { } id ? await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project") : null;
        var assignee = await PersonAsync(request.AssigneeUserId, linkTarget, cancellationToken);

        var task = WorkTask.Create(Guid.NewGuid(), currentUser.TenantId, request.Title, request.Description, request.DueDate, assignee, currentUser.UserId,
            project, link, requestId, await TodayAsync(cancellationToken), clock.UtcNow);
        tasks.Add(task);
        notifications.Notify(task.AssigneeUserId, NotificationKind.TaskAssigned, NotificationMessages.TaskAssigned(currentUser.DisplayName, project?.Name),
            RecordKind.Task, task.Id, task.ProjectId);
        // The title is free text a person typed: it stays on the task, not in the audit line.
        audit.Record("Task.Created", nameof(WorkTask), task.Id, $"Created a task for {assignee!.DisplayName}{On(project)}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made.
            if (await tasks.FindByClientRequestAsync(requestId, cancellationToken) is { } first) return await SameCreateAsync(first, cancellationToken);
            throw;
        }
        return await GetAsync(task.Id, cancellationToken);
    }

    public async Task<WorkTaskDto> CompleteAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var (task, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        task.Complete(Actor(), project, clock.UtcNow);
        notifications.Notify(task.CreatedByUserId, NotificationKind.TaskDone, NotificationMessages.TaskDone(currentUser.DisplayName, project?.Name),
            RecordKind.Task, task.Id, task.ProjectId);
        audit.Record("Task.Completed", nameof(WorkTask), task.Id, $"Marked a task{On(project)} as done.");
        return await SaveAndReadAsync(task, cancellationToken);
    }

    public async Task<WorkTaskDto> CancelAsync(Guid id, TaskReasonRequest request, CancellationToken cancellationToken)
    {
        var (task, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        task.Cancel(Actor(), project, request.Reason, clock.UtcNow);
        audit.Record("Task.Cancelled", nameof(WorkTask), task.Id, $"Cancelled a task{On(project)}.");
        return await SaveAndReadAsync(task, cancellationToken);
    }

    public async Task<WorkTaskDto> ReopenAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var (task, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        task.Reopen(Actor(), project, clock.UtcNow);
        audit.Record("Task.Reopened", nameof(WorkTask), task.Id, $"Reopened a task{On(project)}.");
        return await SaveAndReadAsync(task, cancellationToken);
    }

    public async Task<WorkTaskDto> ReassignAsync(Guid id, ReassignTaskRequest request, CancellationToken cancellationToken)
    {
        var (task, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var linkTarget = task is { LinkKind: { } kind, LinkId: { } linkId } ? await targets.FindAsync(kind, linkId, cancellationToken) : null;
        var assignee = await PersonAsync(request.AssigneeUserId, linkTarget, cancellationToken);
        task.Reassign(Actor(), project, assignee, clock.UtcNow);
        notifications.Notify(task.AssigneeUserId, NotificationKind.TaskAssigned, NotificationMessages.TaskAssigned(currentUser.DisplayName, project?.Name),
            RecordKind.Task, task.Id, task.ProjectId);
        audit.Record("Task.Reassigned", nameof(WorkTask), task.Id, $"Reassigned a task{On(project)} to {assignee!.DisplayName}.");
        return await SaveAndReadAsync(task, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private TaskActor Actor() => new(currentUser.UserId, CollaborationAccess.IsAdministrator(currentUser.Roles));

    /// <summary>What the caller may do with the task now. On an archived project nothing changes.</summary>
    private TaskActionsDto ActionsFor(WorkTaskDto task)
    {
        if (task.ProjectArchived) return TaskActionsDto.None;
        var actor = Actor();
        var creatorOrAdmin = actor.IsAdministrator || actor.UserId == task.CreatedByUserId;
        var open = task.Status == nameof(WorkTaskStatus.Open);
        return new TaskActionsDto(open && (creatorOrAdmin || actor.UserId == task.AssigneeUserId), open && creatorOrAdmin, open && creatorOrAdmin,
            !open && creatorOrAdmin);
    }

    /// <summary>A repeated create answers with the original only when the same person made it.</summary>
    private async Task<WorkTaskDto> SameCreateAsync(TaskRequestOwner owner, CancellationToken cancellationToken) =>
        owner.CreatedByUserId == currentUser.UserId
            ? await GetAsync(owner.TaskId, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different task. Reload the page and try again.");

    /// <summary>The record a new task is about: the creator must be able to read it, and it must exist in the company.</summary>
    private async Task<(TaskLink? Link, CollaborationTarget? Target)> ResolveLinkAsync(string? linkKind, Guid? linkId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkKind) && linkId is null) return (null, null);
        var kind = CollaborationAccess.ParseKind(linkKind, "linkKind", RecordKinds.CanBeLinkedFromTask,
            "A task can link to a project, a purchase order, a change order, a bill, a subcontract or a bond.");
        if (linkId is not { } id || id == Guid.Empty) throw new ValidationFailedException("linkId", "Choose the record the task is about.");
        if (!CollaborationAccess.CanReadKind(currentUser.Roles, kind)) throw new UnauthorizedAccessException("You can't open that record.");
        var target = await targets.FindAsync(kind, id, cancellationToken) ?? throw new NotFoundException("that record");
        return (new TaskLink(kind, id), target);
    }

    /// <summary>The colleague a task is given to, with whether they may work on tasks and read the linked record; null when not in the company.</summary>
    private async Task<TaskPerson?> PersonAsync(Guid? userId, CollaborationTarget? linkTarget, CancellationToken cancellationToken)
    {
        if (userId is not { } id || id == Guid.Empty) return null;
        var found = (await colleagues.GetManyAsync([id], cancellationToken)).SingleOrDefault();
        if (found is null) return null;
        return new TaskPerson(found.Id, found.DisplayName, found.IsActive, Permissions.IsAllowed(found.Roles, Permissions.TasksUse),
            linkTarget is null || CollaborationAccess.CanRead(found.Id, found.Roles, linkTarget));
    }

    /// <summary>Loads a task the caller may see (anyone else gets "not found") and its project, checking the version they opened.</summary>
    private async Task<(WorkTask Task, Project? Project)> LoadForChangeAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var task = await tasks.GetAsync(id, cancellationToken);
        if (task is null || !task.CanBeSeenBy(Actor())) throw new NotFoundException("that task");
        ConcurrencyGuard.Ensure(task.Version, version);
        var project = task.ProjectId is { } projectId ? await projects.GetAsync(projectId, cancellationToken) : null;
        return (task, project);
    }

    private async Task<WorkTaskDto> SaveAndReadAsync(WorkTask task, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(task.Id, cancellationToken);
    }

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private static string On(Project? project) => project is null ? "" : $" on {project.Name}";

    private static T ParseEnum<T>(string text, string field, string message) where T : struct, Enum =>
        !int.TryParse(text, out _) && Enum.TryParse<T>(text.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new ValidationFailedException(field, message);
}
