using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Application.Features.Collaboration;

// Tasks, the discussion on records, in-app notifications and the colleague lookup. Who may read or write a comment is the rule of the record
// it is on (see CollaborationAccess); tasks are seen by their creator, their assignee and Administrators; notifications only by their owner.
// Nothing here carries an amount. See docs/business-rules.md section 20 and docs/api/collaboration.md.

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface IWorkTaskRepository
{
    /// <summary>Loads a task, tracked for change. Null when it isn't in the organization.</summary>
    Task<WorkTask?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<TaskRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(WorkTask task);
}

public sealed record TaskRequestOwner(Guid TaskId, Guid CreatedByUserId);

/// <summary>Reads tasks straight into DTOs: a list is two statements (count and page), a task one, however many tasks exist.</summary>
public interface IWorkTaskReader
{
    Task<PagedResult<WorkTaskListItemDto>> ListAsync(TaskListFilter filter, DateOnly today, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The task with names filled in; the actions are all false (the service decides them for the caller).</summary>
    Task<WorkTaskDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken);
}

public enum TaskScope
{
    /// <summary>Tasks assigned to the caller.</summary>
    Assigned,

    /// <summary>Tasks the caller created.</summary>
    Created,

    /// <summary>Every task in the company (Administrators only).</summary>
    All
}

/// <param name="ProjectId">Only tasks on this project, or every task in the scope when null.</param>
public sealed record TaskListFilter(TaskScope Scope, Guid UserId, WorkTaskStatus? Status, Guid? ProjectId = null);

public interface ICommentRepository
{
    Task<Comment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CommentRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(Comment comment);
}

public sealed record CommentRequestOwner(Guid CommentId, RecordKind TargetKind, Guid TargetId, Guid AuthorUserId);

/// <summary>Reads a record's discussion: three statements (count, page, the page's mentions), however many comments exist.</summary>
public interface ICommentReader
{
    /// <summary>Newest first. Comments carry no actions; the service decides them for the caller.</summary>
    Task<PagedResult<CommentDto>> ListAsync(RecordKind kind, Guid targetId, PageRequest page, CancellationToken cancellationToken);

    Task<CommentDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>The caller's own notifications. Every read is filtered to <c>userId</c>, so another person's notification is never found.</summary>
public interface INotificationRepository
{
    Task<Notification?> GetOwnAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification of the person read, in one statement.</summary>
    Task MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface INotificationReader
{
    /// <summary>Newest first; two statements.</summary>
    Task<PagedResult<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken);

    /// <summary>One statement.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Adds a notification to the save of the action that causes it, so the two are written together or not at all. A notification for the person
/// who is acting is skipped: nobody is told about what they did themselves. The message must come from <see cref="NotificationMessages"/>
/// (names and document numbers only, never an amount).
/// </summary>
public interface INotificationWriter
{
    void Notify(Guid recipientUserId, NotificationKind kind, string message, RecordKind targetKind, Guid targetId, Guid? projectId);
}

/// <summary>Finds a record people can discuss or link a task to, with its project and its name in words. One statement; company-filtered.</summary>
public interface ICollaborationTargetReader
{
    Task<CollaborationTarget?> FindAsync(RecordKind kind, Guid id, CancellationToken cancellationToken);
}

/// <param name="Words">The record in words for a message or a link: "purchase order PO-0012", "project Riverside Residences".</param>
/// <param name="TaskCreatorId">For a task: who created it (with the assignee and Administrators, the only people who may read it).</param>
public sealed record CollaborationTarget(RecordKind Kind, Guid Id, Guid? ProjectId, string? ProjectName, bool ProjectArchived, string Words,
    Guid? TaskCreatorId = null, Guid? TaskAssigneeId = null);

/// <summary>Colleagues of the signed-in organization. The lookup never selects anything but the id and the name.</summary>
public interface IColleagueReader
{
    /// <summary>Active colleagues who hold any of <paramref name="anyRole"/>, or whose id is in <paramref name="alsoUserIds"/>; by name; at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<ColleagueDto>> SearchAsync(string? search, AccessRole anyRole, IReadOnlyCollection<Guid> alsoUserIds, int take,
        CancellationToken cancellationToken);

    /// <summary>The colleagues with these ids (active or not), for checking an assignee or the people a comment mentions. One statement.</summary>
    Task<IReadOnlyList<Colleague>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public sealed record Colleague(Guid Id, string DisplayName, bool IsActive, AccessRole Roles);

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

public sealed record ColleagueDto(Guid Id, string DisplayName);

/// <param name="IsOverdue">Open with a due date before the company's today.</param>
/// <param name="DueDetail">The due date in words for an Open task, as the "Needs your action" page says it ("2 days overdue, due 30 Oct 2026",
/// "Due today", "No due date"); null once the task is done or cancelled.</param>
public sealed record WorkTaskListItemDto(Guid Id, string Title, string Status, DateOnly? DueDate, bool IsOverdue, string? DueDetail, Guid AssigneeUserId,
    string? AssigneeName, Guid CreatedByUserId, string? CreatedByName, Guid? ProjectId, string? ProjectName, DateTimeOffset CreatedAtUtc, int Version);

/// <param name="LinkKind">The kind of record the task is about (Project, PurchaseOrder, ChangeOrder, ClientBill, Subcontract, Bond), or null.</param>
/// <param name="LinkLabel">That record in words ("purchase order PO-0012"), or null when it no longer exists.</param>
/// <param name="DueDetail">The due date in words while Open (see <see cref="WorkTaskListItemDto"/>); null once done or cancelled.</param>
/// <param name="Actions">What the caller may do now.</param>
public sealed record WorkTaskDto(Guid Id, string Title, string? Description, string Status, DateOnly? DueDate, bool IsOverdue, string? DueDetail, Guid AssigneeUserId,
    string? AssigneeName, Guid CreatedByUserId, string? CreatedByName, Guid? ProjectId, string? ProjectName, bool ProjectArchived, string? LinkKind,
    Guid? LinkId, string? LinkLabel, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc, string? CompletedByName,
    DateTimeOffset? CancelledAtUtc, string? CancelledByName, string? CancelReason, int Version, TaskActionsDto Actions);

public sealed record TaskActionsDto(bool Complete, bool Cancel, bool Reassign, bool Reopen)
{
    public static readonly TaskActionsDto None = new(false, false, false, false);
}

/// <param name="Text">The words; null once the comment was removed.</param>
/// <param name="Mentions">The people it mentions; empty once removed.</param>
/// <param name="VoidedByName">Who removed it ("Removed by ..."), or null.</param>
/// <param name="CanRemove">The caller wrote it or is an Administrator, may write comments, and the discussion is not read-only.</param>
public sealed record CommentDto(Guid Id, Guid AuthorUserId, string? AuthorName, string? Text, IReadOnlyList<ColleagueDto> Mentions,
    DateTimeOffset CreatedAtUtc, bool IsVoided, string? VoidedByName, DateTimeOffset? VoidedAtUtc, bool CanRemove, int Version);

/// <summary>A page of a record's discussion (newest first) and whether the caller may add to it.</summary>
/// <param name="ReadOnlyReason">Why nobody may write now (an archived project), or null.</param>
public sealed record CommentPageDto(IReadOnlyList<CommentDto> Items, int Total, int Page, int PageSize, bool CanWrite, string? ReadOnlyReason);

/// <param name="Kind">TaskAssigned, TaskDone, Mention or DraftDecided.</param>
/// <param name="TargetKind">The record it opens: Task, Project, PurchaseOrder, ChangeOrder, ClientBill or Subcontract.</param>
public sealed record NotificationDto(Guid Id, string Kind, string Message, string TargetKind, Guid TargetId, Guid? ProjectId, DateTimeOffset CreatedAtUtc,
    bool IsRead);

public sealed record NotificationCountDto(int Unread);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <param name="ProjectId">Optional; with a link, it may be left out (the linked record's project is used) or must be that project.</param>
/// <param name="LinkKind">Project, PurchaseOrder, ChangeOrder, ClientBill, Subcontract or Bond; with <paramref name="LinkId"/>.</param>
public sealed record CreateTaskRequest(string? Title, string? Description, DateOnly? DueDate, Guid? AssigneeUserId, Guid? ProjectId, string? LinkKind,
    Guid? LinkId, string? ClientRequestId);

public sealed record ReassignTaskRequest(Guid? AssigneeUserId, int Version);

public sealed record TaskReasonRequest(string? Reason, int Version);

/// <param name="Kind">Project, Task, PurchaseOrder, ChangeOrder, ClientBill or Subcontract.</param>
public sealed record CreateCommentRequest(string? Kind, Guid TargetId, string? Text, IReadOnlyList<Guid>? MentionUserIds, string? ClientRequestId);

public sealed record RemoveCommentRequest(string? Reason, int Version);
