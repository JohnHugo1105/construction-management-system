using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Domain.Features.Billing;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Domain.Features.Subcontracts;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Collaboration;

internal sealed class WorkTaskRepository(ConstructOpxDbContext db) : IWorkTaskRepository
{
    public Task<WorkTask?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<WorkTask>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<TaskRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<WorkTask>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new TaskRequestOwner(item.Id, item.CreatedByUserId)).SingleOrDefaultAsync(cancellationToken);

    public void Add(WorkTask task) => db.Set<WorkTask>().Add(task);
}

internal sealed class CommentRepository(ConstructOpxDbContext db) : ICommentRepository
{
    public Task<Comment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Comment>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<CommentRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<Comment>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new CommentRequestOwner(item.Id, item.TargetKind, item.TargetId, item.AuthorUserId)).SingleOrDefaultAsync(cancellationToken);

    public void Add(Comment comment) => db.Set<Comment>().Add(comment);
}

/// <summary>
/// The caller's notifications, and the writer every feature uses to add one to its own save. Nothing is sent anywhere: a notification is a row
/// the person reads in the app.
/// </summary>
internal sealed class NotificationRepository(ConstructOpxDbContext db, ICurrentUser currentUser, IClock clock) : INotificationRepository, INotificationWriter
{
    public Task<Notification?> GetOwnAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.Set<Notification>().SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Set<Notification>().Where(item => item.UserId == userId && item.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAtUtc, (DateTimeOffset?)now), cancellationToken);

    public void Notify(Guid recipientUserId, NotificationKind kind, string message, RecordKind targetKind, Guid targetId, Guid? projectId)
    {
        // Nobody is told about what they did themselves.
        if (recipientUserId == Guid.Empty || recipientUserId == currentUser.UserId) return;
        db.Set<Notification>().Add(Notification.Create(Guid.NewGuid(), currentUser.TenantId, recipientUserId, kind, message, targetKind, targetId,
            projectId, clock.UtcNow));
    }
}

/// <summary>Finds a record by kind and id with its project and its name in words: one statement, company-filtered, nothing else selected.</summary>
internal sealed class CollaborationTargetReader(ConstructOpxDbContext db) : ICollaborationTargetReader
{
    public async Task<CollaborationTarget?> FindAsync(RecordKind kind, Guid id, CancellationToken cancellationToken)
    {
        var projects = db.Set<Project>().AsNoTracking();
        switch (kind)
        {
            case RecordKind.Project:
            {
                var row = await projects.Where(project => project.Id == id)
                    .Select(project => new { project.Name, project.Status }).SingleOrDefaultAsync(cancellationToken);
                return row is null ? null : new CollaborationTarget(kind, id, id, row.Name, row.Status == ProjectStatus.Archived, NotificationMessages.Project(row.Name));
            }
            case RecordKind.Task:
            {
                var row = await db.Set<WorkTask>().AsNoTracking().Where(task => task.Id == id)
                    .Select(task => new
                    {
                        task.ProjectId,
                        task.CreatedByUserId,
                        task.AssigneeUserId,
                        Project = projects.Where(project => project.Id == task.ProjectId).Select(project => new { project.Name, project.Status }).FirstOrDefault()
                    }).SingleOrDefaultAsync(cancellationToken);
                return row is null
                    ? null
                    : new CollaborationTarget(kind, id, row.ProjectId, row.Project?.Name, row.Project?.Status == ProjectStatus.Archived,
                        NotificationMessages.Task(row.Project?.Name), row.CreatedByUserId, row.AssigneeUserId);
            }
            case RecordKind.PurchaseOrder:
                return Make(kind, id, await (from order in db.Set<PurchaseOrder>().AsNoTracking()
                    where order.Id == id
                    join project in projects on order.ProjectId equals project.Id
                    select new Row(order.Number, project.Id, project.Name, project.Status)).SingleOrDefaultAsync(cancellationToken),
                    row => NotificationMessages.PurchaseOrder(row.Number!));
            case RecordKind.ChangeOrder:
                return Make(kind, id, await (from order in db.Set<ChangeOrder>().AsNoTracking()
                    where order.Id == id
                    join project in projects on order.ProjectId equals project.Id
                    select new Row(order.Number, project.Id, project.Name, project.Status)).SingleOrDefaultAsync(cancellationToken),
                    row => NotificationMessages.ChangeOrder(row.Number, row.ProjectName));
            case RecordKind.ClientBill:
                return Make(kind, id, await (from bill in db.Set<ClientInvoice>().AsNoTracking()
                    where bill.Id == id
                    join project in projects on bill.ProjectId equals project.Id
                    select new Row(bill.Number, project.Id, project.Name, project.Status)).SingleOrDefaultAsync(cancellationToken),
                    row => NotificationMessages.Bill(row.Number, row.ProjectName));
            case RecordKind.Subcontract:
                return Make(kind, id, await (from subcontract in db.Set<Subcontract>().AsNoTracking()
                    where subcontract.Id == id
                    join project in projects on subcontract.ProjectId equals project.Id
                    select new Row(subcontract.Number, project.Id, project.Name, project.Status)).SingleOrDefaultAsync(cancellationToken),
                    row => NotificationMessages.Subcontract(row.Number, row.ProjectName));
            case RecordKind.Bond:
                return Make(kind, id, await (from bond in db.Set<Bond>().AsNoTracking()
                    where bond.Id == id
                    join project in projects on bond.ProjectId equals project.Id
                    select new Row(bond.BondNumber, project.Id, project.Name, project.Status)).SingleOrDefaultAsync(cancellationToken),
                    row => NotificationMessages.Bond(row.Number!));
            default:
                return null;
        }
    }

    private static CollaborationTarget? Make(RecordKind kind, Guid id, Row? row, Func<Row, string> words) =>
        row is null ? null : new CollaborationTarget(kind, id, row.ProjectId, row.ProjectName, row.Status == ProjectStatus.Archived, words(row));

    private sealed record Row(string? Number, Guid ProjectId, string ProjectName, ProjectStatus Status);
}

/// <summary>Tasks straight into DTOs: a list is two statements (count and page) and a task two (the task, and its linked record's name).</summary>
internal sealed class WorkTaskReader(ConstructOpxDbContext db) : IWorkTaskReader
{
    public async Task<PagedResult<WorkTaskListItemDto>> ListAsync(TaskListFilter filter, DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = db.Set<WorkTask>().AsNoTracking();
        rows = filter.Scope switch
        {
            TaskScope.Assigned => rows.Where(task => task.AssigneeUserId == filter.UserId),
            TaskScope.Created => rows.Where(task => task.CreatedByUserId == filter.UserId),
            _ => rows
        };
        if (filter.Status is { } status) rows = rows.Where(task => task.Status == status);
        if (filter.ProjectId is { } projectId) rows = rows.Where(task => task.ProjectId == projectId);

        var total = await rows.CountAsync(cancellationToken);
        var users = db.Set<User>();
        var projects = db.Set<Project>();
        // Open first; among them the overdue and soonest due first and those without a date last; then the most recently changed.
        var items = await rows
            .OrderBy(task => task.Status == WorkTaskStatus.Open ? 0 : 1)
            .ThenBy(task => task.Status == WorkTaskStatus.Open && task.DueDate != null ? 0 : 1)
            .ThenBy(task => task.Status == WorkTaskStatus.Open ? task.DueDate : null)
            .ThenByDescending(task => task.UpdatedAtUtc)
            .ThenBy(task => task.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.Status,
                task.DueDate,
                task.AssigneeUserId,
                AssigneeName = users.Where(user => user.Id == task.AssigneeUserId).Select(user => user.DisplayName).FirstOrDefault(),
                task.CreatedByUserId,
                CreatedByName = users.Where(user => user.Id == task.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                task.ProjectId,
                ProjectName = projects.Where(project => project.Id == task.ProjectId).Select(project => project.Name).FirstOrDefault(),
                task.CreatedAtUtc,
                task.Version
            }).ToListAsync(cancellationToken);

        return PagedResult<WorkTaskListItemDto>.Of(items.Select(row => new WorkTaskListItemDto(row.Id, row.Title, row.Status.ToString(), row.DueDate,
            WorkTask.IsOverdueOn(row.Status, row.DueDate, today), TaskWords.DueDetailWhileOpen(row.Status, row.DueDate, today), row.AssigneeUserId,
            row.AssigneeName, row.CreatedByUserId, row.CreatedByName, row.ProjectId, row.ProjectName, row.CreatedAtUtc, row.Version)).ToArray(), total, page);
    }

    public async Task<WorkTaskDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var row = await db.Set<WorkTask>().AsNoTracking().Where(task => task.Id == id)
            .Select(task => new
            {
                Task = task,
                AssigneeName = users.Where(user => user.Id == task.AssigneeUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CreatedByName = users.Where(user => user.Id == task.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CompletedByName = users.Where(user => user.Id == task.CompletedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CancelledByName = users.Where(user => user.Id == task.CancelledByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                Project = db.Set<Project>().Where(project => project.Id == task.ProjectId).Select(project => new { project.Name, project.Status }).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var t = row.Task;
        var link = t is { LinkKind: { } kind, LinkId: { } linkId } ? await new CollaborationTargetReader(db).FindAsync(kind, linkId, cancellationToken) : null;
        return new WorkTaskDto(t.Id, t.Title, t.Description, t.Status.ToString(), t.DueDate, t.IsOverdue(today),
            TaskWords.DueDetailWhileOpen(t.Status, t.DueDate, today), t.AssigneeUserId, row.AssigneeName,
            t.CreatedByUserId, row.CreatedByName, t.ProjectId, row.Project?.Name, row.Project?.Status == ProjectStatus.Archived, t.LinkKind?.ToString(),
            t.LinkId, link?.Words, t.CreatedAtUtc, t.CompletedAtUtc, row.CompletedByName, t.CancelledAtUtc, row.CancelledByName, t.CancelReason, t.Version,
            TaskActionsDto.None);
    }
}

/// <summary>A record's discussion: three statements (count, the page, the page's mentions) however many comments exist.</summary>
internal sealed class CommentReader(ConstructOpxDbContext db) : ICommentReader
{
    public async Task<PagedResult<CommentDto>> ListAsync(RecordKind kind, Guid targetId, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = db.Set<Comment>().AsNoTracking().Where(comment => comment.TargetKind == kind && comment.TargetId == targetId);
        var total = await rows.CountAsync(cancellationToken);
        var items = await Shape(rows.OrderByDescending(comment => comment.CreatedAtUtc).ThenByDescending(comment => comment.Id)
            .Skip(page.Skip).Take(page.SafePageSize)).ToListAsync(cancellationToken);
        return PagedResult<CommentDto>.Of(await WithMentionsAsync(items, cancellationToken), total, page);
    }

    public async Task<CommentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Shape(db.Set<Comment>().AsNoTracking().Where(comment => comment.Id == id)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (await WithMentionsAsync([row], cancellationToken))[0];
    }

    private IQueryable<Row> Shape(IQueryable<Comment> comments)
    {
        var users = db.Set<User>();
        return comments.Select(comment => new Row(comment.Id, comment.AuthorUserId,
            users.Where(user => user.Id == comment.AuthorUserId).Select(user => user.DisplayName).FirstOrDefault(),
            comment.IsVoided ? null : comment.Text, comment.CreatedAtUtc, comment.IsVoided,
            users.Where(user => user.Id == comment.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault(), comment.VoidedAtUtc,
            comment.Version));
    }

    /// <summary>The page's mentions in one statement (none is read for a removed comment: its mentions are no longer shown).</summary>
    private async Task<IReadOnlyList<CommentDto>> WithMentionsAsync(IReadOnlyList<Row> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Where(row => !row.IsVoided).Select(row => row.Id).ToArray();
        var mentions = ids.Length == 0
            ? []
            : await (from mention in db.Set<CommentMention>().AsNoTracking()
                where ids.Contains(mention.CommentId)
                join user in db.Set<User>() on mention.UserId equals user.Id
                orderby user.DisplayName
                select new { mention.CommentId, user.Id, user.DisplayName }).ToListAsync(cancellationToken);
        var byComment = mentions.ToLookup(mention => mention.CommentId, mention => new ColleagueDto(mention.Id, mention.DisplayName));
        return rows.Select(row => new CommentDto(row.Id, row.AuthorUserId, row.AuthorName, row.Text, byComment[row.Id].ToArray(), row.CreatedAtUtc,
            row.IsVoided, row.VoidedByName, row.VoidedAtUtc, false, row.Version)).ToArray();
    }

    private sealed record Row(Guid Id, Guid AuthorUserId, string? AuthorName, string? Text, DateTimeOffset CreatedAtUtc, bool IsVoided,
        string? VoidedByName, DateTimeOffset? VoidedAtUtc, int Version);
}

internal sealed class NotificationReader(ConstructOpxDbContext db) : INotificationReader
{
    public async Task<PagedResult<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = db.Set<Notification>().AsNoTracking().Where(item => item.UserId == userId);
        if (unreadOnly) rows = rows.Where(item => item.ReadAtUtc == null);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id).Skip(page.Skip).Take(page.SafePageSize)
            .Select(item => new { item.Id, item.Kind, item.Message, item.TargetKind, item.TargetId, item.ProjectId, item.CreatedAtUtc, item.ReadAtUtc })
            .ToListAsync(cancellationToken);
        return PagedResult<NotificationDto>.Of(items.Select(item => new NotificationDto(item.Id, item.Kind.ToString(), item.Message,
            item.TargetKind.ToString(), item.TargetId, item.ProjectId, item.CreatedAtUtc, item.ReadAtUtc is not null)).ToArray(), total, page);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Set<Notification>().AsNoTracking().CountAsync(item => item.UserId == userId && item.ReadAtUtc == null, cancellationToken);
}

/// <summary>Colleagues by name: only the id and the display name are selected, never a role, email or username.</summary>
internal sealed class ColleagueReader(ConstructOpxDbContext db) : IColleagueReader
{
    public async Task<IReadOnlyList<ColleagueDto>> SearchAsync(string? search, AccessRole anyRole, IReadOnlyCollection<Guid> alsoUserIds, int take,
        CancellationToken cancellationToken)
    {
        var also = alsoUserIds.ToArray();
        var users = db.Set<User>().AsNoTracking().Where(user => user.IsActive && ((user.Roles & anyRole) != 0 || also.Contains(user.Id)));
        if (search is not null) users = users.Where(user => user.DisplayName.Contains(search));
        return await users.OrderBy(user => user.DisplayName).ThenBy(user => user.Id).Take(take)
            .Select(user => new ColleagueDto(user.Id, user.DisplayName)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Colleague>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await db.Set<User>().AsNoTracking().Where(user => wanted.Contains(user.Id))
            .Select(user => new Colleague(user.Id, user.DisplayName, user.IsActive, user.Roles)).ToListAsync(cancellationToken);
    }
}
