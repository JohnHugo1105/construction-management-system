using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Collaboration;

/// <summary>The person a task is given to, as the application found them in the organization.</summary>
/// <param name="CanUseTasks">They hold the permission that lets them work on tasks.</param>
/// <param name="CanReadLink">They could open the record the task links to (true when the task links to nothing).</param>
public sealed record TaskPerson(Guid UserId, string DisplayName, bool IsActive, bool CanUseTasks, bool CanReadLink = true);

/// <summary>Who is acting on a task. An Administrator may do every step on any task.</summary>
public sealed record TaskActor(Guid UserId, bool IsAdministrator);

/// <summary>The one record a task is about, if any.</summary>
public sealed record TaskLink(RecordKind Kind, Guid Id);

/// <summary>
/// A piece of work one person gives another (or themselves): a title, an optional description and due date, optionally on a project and
/// about one record. It is seen only by the person who created it, the person it is assigned to, and Administrators. A task never carries an
/// amount: it points at the record that does.
/// </summary>
/// <remarks>
/// Open tasks are completed by the assignee, the creator or an Administrator, and cancelled or reassigned by the creator or an Administrator.
/// A done or cancelled task can be reopened by the creator or an Administrator. On an archived project a task is read-only.
/// </remarks>
public sealed class WorkTask : AggregateRoot
{
    public const int MaximumTitleLength = 120;
    public const int MaximumDescriptionLength = 1_000;
    public const int MaximumReasonLength = 300;

    private static readonly DateOnly LatestDate = new(2100, 12, 31);

    private WorkTask() { }

    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public WorkTaskStatus Status { get; private set; } = WorkTaskStatus.Open;

    /// <summary>The company calendar day it should be done by; none when it has no deadline.</summary>
    public DateOnly? DueDate { get; private set; }

    public Guid AssigneeUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public RecordKind? LinkKind { get; private set; }
    public Guid? LinkId { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancelReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    /// <summary>Overdue is worked out, never stored: an Open task whose due date is before the company's today.</summary>
    public static bool IsOverdueOn(WorkTaskStatus status, DateOnly? dueDate, DateOnly today) =>
        status == WorkTaskStatus.Open && dueDate is { } due && due < today;

    public bool IsOverdue(DateOnly today) => IsOverdueOn(Status, DueDate, today);

    /// <summary>The creator and the assignee; with Administrators, the only people who see the task and its discussion.</summary>
    public bool IsParticipant(Guid userId) => userId == CreatedByUserId || userId == AssigneeUserId;

    public bool CanBeSeenBy(TaskActor actor) => actor.IsAdministrator || IsParticipant(actor.UserId);

    // ---- Lifecycle --------------------------------------------------------------------------------------------------------------

    public static WorkTask Create(Guid id, Guid tenantId, string? title, string? description, DateOnly? dueDate, TaskPerson? assignee,
        Guid createdByUserId, Project? project, TaskLink? link, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        project?.EnsureNotArchived();
        var errors = new ValidationErrors();
        var task = new WorkTask { CreatedByUserId = createdByUserId, ProjectId = project?.Id };
        task.Title = Field(errors, () => Guard.RequiredText(title, "title", "a title for the task", MaximumTitleLength)) ?? "";
        task.Description = Field(errors, () => Guard.OptionalText(description, "description", "the description", MaximumDescriptionLength));
        task.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));

        if (dueDate is { } due)
        {
            if (due < today) errors.Add("dueDate", "The due date can't be in the past. Choose today or a later day.");
            else if (due > LatestDate) errors.Add("dueDate", "Choose a valid date.");
        }
        task.DueDate = dueDate;

        if (link is not null)
        {
            if (!RecordKinds.CanBeLinkedFromTask(link.Kind)) errors.Add("linkKind", "A task can link to a project, a purchase order, a change order, a bill, a subcontract or a bond.");
            else if (link.Id == Guid.Empty) errors.Add("linkId", "Choose the record the task is about.");
            task.LinkKind = link.Kind;
            task.LinkId = link.Id;
        }

        CheckAssignee(errors, assignee, link is not null);
        errors.ThrowIfAny();

        task.AssigneeUserId = assignee!.UserId;
        task.Initialize(id, tenantId, now);
        return task;
    }

    /// <summary>The assignee, the creator or an Administrator marks an Open task done.</summary>
    public void Complete(TaskActor actor, Project? project, DateTimeOffset now)
    {
        EnsureProject(project);
        project?.EnsureNotArchived();
        EnsureOpen("marked as done");
        if (!actor.IsAdministrator && !IsParticipant(actor.UserId))
            throw new DomainRuleException("Only the person it is assigned to, the person who created it or an Administrator can mark this task as done.");

        Status = WorkTaskStatus.Done;
        CompletedAtUtc = now;
        CompletedByUserId = actor.UserId;
        Touch(now);
    }

    /// <summary>The creator or an Administrator calls off an Open task. The reason is optional and stays on the task.</summary>
    public void Cancel(TaskActor actor, Project? project, string? reason, DateTimeOffset now)
    {
        EnsureProject(project);
        project?.EnsureNotArchived();
        EnsureOpen("cancelled");
        EnsureCreatorOrAdministrator(actor, "cancel");
        var cleanReason = Guard.OptionalText(reason, "reason", "the reason", MaximumReasonLength);

        Status = WorkTaskStatus.Cancelled;
        CancelledAtUtc = now;
        CancelledByUserId = actor.UserId;
        CancelReason = cleanReason;
        Touch(now);
    }

    /// <summary>The creator or an Administrator brings back a done or cancelled task. Its due date stays; a past one makes it overdue.</summary>
    public void Reopen(TaskActor actor, Project? project, DateTimeOffset now)
    {
        EnsureProject(project);
        project?.EnsureNotArchived();
        if (Status == WorkTaskStatus.Open) throw new DomainRuleException("This task is already open.");
        EnsureCreatorOrAdministrator(actor, "reopen");

        Status = WorkTaskStatus.Open;
        CompletedAtUtc = null;
        CompletedByUserId = null;
        CancelledAtUtc = null;
        CancelledByUserId = null;
        CancelReason = null;
        Touch(now);
    }

    /// <summary>The creator or an Administrator gives an Open task to someone else (or to themselves).</summary>
    public void Reassign(TaskActor actor, Project? project, TaskPerson? assignee, DateTimeOffset now)
    {
        EnsureProject(project);
        project?.EnsureNotArchived();
        EnsureOpen("reassigned");
        EnsureCreatorOrAdministrator(actor, "reassign");

        var errors = new ValidationErrors();
        CheckAssignee(errors, assignee, LinkKind is not null);
        if (!errors.Any && assignee!.UserId == AssigneeUserId)
            errors.Add("assigneeUserId", $"This task is already assigned to {assignee.DisplayName}.");
        errors.ThrowIfAny();

        AssigneeUserId = assignee!.UserId;
        Touch(now);
    }

    // ---- Checks -----------------------------------------------------------------------------------------------------------------

    private static void CheckAssignee(ValidationErrors errors, TaskPerson? assignee, bool linked)
    {
        if (assignee is null) errors.Add("assigneeUserId", "Choose who the task is for.");
        else if (!assignee.IsActive) errors.Add("assigneeUserId", $"{assignee.DisplayName} can no longer sign in. Choose someone else.");
        else if (!assignee.CanUseTasks) errors.Add("assigneeUserId", $"{assignee.DisplayName} can't work on tasks. Choose someone else.");
        else if (linked && !assignee.CanReadLink)
            errors.Add("assigneeUserId", $"{assignee.DisplayName} can't see the linked record. Choose someone who can, or remove the link.");
    }

    private void EnsureOpen(string action)
    {
        if (Status == WorkTaskStatus.Done) throw new DomainRuleException($"This task is already done, so it can't be {action}. Reopen it first.");
        if (Status == WorkTaskStatus.Cancelled) throw new DomainRuleException($"This task was cancelled, so it can't be {action}. Reopen it first.");
    }

    private void EnsureCreatorOrAdministrator(TaskActor actor, string action)
    {
        if (!actor.IsAdministrator && actor.UserId != CreatedByUserId)
            throw new DomainRuleException($"Only the person who created this task or an Administrator can {action} it.");
    }

    private void EnsureProject(Project? project)
    {
        if (project?.Id != ProjectId) throw new InvalidOperationException("The project given is not the task's project.");
    }

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}
