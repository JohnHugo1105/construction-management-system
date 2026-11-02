using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Collaboration;

/// <summary>
/// The discussion on a record. The record's own read rule decides who reads it (see <see cref="CollaborationAccess"/>): someone who may not read
/// purchase orders is refused before anything is looked up, so they cannot learn whether a comment exists; someone outside a task gets "not
/// found". Writing also needs tasks.use. A comment on an archived project is refused. Mentions are checked against the same read rule and
/// each mentioned person gets a notification in the same save. A comment is removed by voiding it, never edited or deleted.
/// </summary>
public sealed class CommentService(ICommentRepository comments, ICommentReader reader, ICollaborationTargetReader targets, IColleagueReader colleagues,
    IProjectRepository projects, INotificationWriter notifications, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const string ArchivedReason = "This project is archived, so its discussion is read-only.";

    private const string KindMessage = "Choose the record: Project, Task, PurchaseOrder, ChangeOrder, ClientBill or Subcontract.";

    public async Task<CommentPageDto> ListAsync(string? kind, Guid targetId, PageRequest page, CancellationToken cancellationToken)
    {
        var target = await ReadableTargetAsync(ParseKind(kind), targetId, cancellationToken);
        var result = await reader.ListAsync(target.Kind, target.Id, page, cancellationToken);
        var canWrite = CanWrite();
        return new CommentPageDto(result.Items.Select(item => WithActions(item, target, canWrite)).ToArray(), result.Total, result.Page, result.PageSize,
            canWrite && !target.ProjectArchived, target.ProjectArchived ? ArchivedReason : null);
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var kind = ParseKind(request.Kind);
        var target = await ReadableTargetAsync(kind, request.TargetId, cancellationToken);
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await comments.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, target, cancellationToken);

        var project = await ProjectOfAsync(target, cancellationToken);
        var mentioned = await MentionsAsync(request.MentionUserIds, target, cancellationToken);
        var comment = Comment.Create(Guid.NewGuid(), currentUser.TenantId, target.Kind, target.Id, project, request.Text, currentUser.UserId, mentioned,
            requestId, clock.UtcNow);
        comments.Add(comment);
        var message = NotificationMessages.Mention(currentUser.DisplayName, target.Words);
        foreach (var mention in comment.Mentions)
            notifications.Notify(mention.UserId, NotificationKind.Mention, message, target.Kind, target.Id, target.ProjectId);
        // The words of the comment stay on the comment, not in the audit line.
        audit.Record("Comment.Added", nameof(Comment), comment.Id, $"Commented on {target.Words}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            if (await comments.FindByClientRequestAsync(requestId, cancellationToken) is { } first) return await SameCreateAsync(first, target, cancellationToken);
            throw;
        }
        return await ReadAsync(comment.Id, target, cancellationToken);
    }

    /// <summary>Removes a comment (its author or an Administrator) with a reason. Someone who may not read the record gets "not found".</summary>
    public async Task<CommentDto> RemoveAsync(Guid id, RemoveCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await comments.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that comment");
        var target = await targets.FindAsync(comment.TargetKind, comment.TargetId, cancellationToken);
        if (target is null || !CollaborationAccess.CanRead(currentUser.UserId, currentUser.Roles, target)) throw new NotFoundException("that comment");
        ConcurrencyGuard.Ensure(comment.Version, request.Version);

        var project = await ProjectOfAsync(target, cancellationToken);
        comment.Void(currentUser.UserId, CollaborationAccess.IsAdministrator(currentUser.Roles), project, request.Reason, clock.UtcNow);
        // The reason is free text and stays on the comment.
        audit.Record("Comment.Removed", nameof(Comment), comment.Id, $"Removed a comment on {target.Words}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(comment.Id, target, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The record, when the caller may read it. The permission is checked before anything is looked up (403 whether or not the record exists);
    /// a record of another company, or a task the caller takes no part in, is "not found".
    /// </summary>
    private async Task<CollaborationTarget> ReadableTargetAsync(RecordKind kind, Guid targetId, CancellationToken cancellationToken)
    {
        if (!CollaborationAccess.CanReadKind(currentUser.Roles, kind)) throw new UnauthorizedAccessException("You can't open this record.");
        var target = await targets.FindAsync(kind, targetId, cancellationToken);
        if (target is null || !CollaborationAccess.CanRead(currentUser.UserId, currentUser.Roles, target))
            throw new NotFoundException(kind == RecordKind.Task ? "that task" : "that record");
        return target;
    }

    /// <summary>The people mentioned, with whether each could read the record. An id outside the company is refused like a wrong choice.</summary>
    private async Task<IReadOnlyList<MentionCandidate>> MentionsAsync(IReadOnlyList<Guid>? ids, CollaborationTarget target,
        CancellationToken cancellationToken)
    {
        var wanted = (ids ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (wanted.Length == 0) return [];
        if (wanted.Length > Comment.MaximumMentions)
            throw new ValidationFailedException("mentionUserIds", $"Mention at most {Comment.MaximumMentions} people in one comment.");
        var found = await colleagues.GetManyAsync(wanted, cancellationToken);
        if (found.Count != wanted.Length) throw new ValidationFailedException("mentionUserIds", "Choose the people to mention from the list.");
        return found.Select(person => new MentionCandidate(person.Id, person.DisplayName, person.IsActive,
            CollaborationAccess.CanRead(person.Id, person.Roles, target))).ToArray();
    }

    private async Task<Project?> ProjectOfAsync(CollaborationTarget target, CancellationToken cancellationToken) =>
        target.ProjectId is { } projectId ? await projects.GetAsync(projectId, cancellationToken) : null;

    private async Task<CommentDto> SameCreateAsync(CommentRequestOwner owner, CollaborationTarget target, CancellationToken cancellationToken) =>
        owner.AuthorUserId == currentUser.UserId && owner.TargetKind == target.Kind && owner.TargetId == target.Id
            ? await ReadAsync(owner.CommentId, target, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different comment. Reload the page and try again.");

    private async Task<CommentDto> ReadAsync(Guid id, CollaborationTarget target, CancellationToken cancellationToken) =>
        WithActions(await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that comment"), target, CanWrite());

    private CommentDto WithActions(CommentDto comment, CollaborationTarget target, bool canWrite) => comment with
    {
        CanRemove = canWrite && !comment.IsVoided && !target.ProjectArchived &&
            (comment.AuthorUserId == currentUser.UserId || CollaborationAccess.IsAdministrator(currentUser.Roles))
    };

    private bool CanWrite() => Permissions.IsAllowed(currentUser.Roles, Permissions.TasksUse);

    private static RecordKind ParseKind(string? kind) => CollaborationAccess.ParseKind(kind, "kind", RecordKinds.CanCarryComments, KindMessage);
}
