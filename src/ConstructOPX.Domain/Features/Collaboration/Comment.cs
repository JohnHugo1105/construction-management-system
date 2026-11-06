using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Collaboration;

/// <summary>Someone a comment mentions, as the application found them, with whether they could open the record the comment is on.</summary>
public sealed record MentionCandidate(Guid UserId, string DisplayName, bool IsActive, bool CanReadTarget);

/// <summary>
/// One message in the discussion on a record (a project, a task, a purchase order, a change order, a bill or a subcontract). Comments are
/// append-only: a comment is never edited or deleted. One posted by mistake is removed by voiding it with a reason (by its author or an
/// Administrator), and the thread then shows "Removed by" in its place. Who may read or write a comment follows the record's own rule.
/// </summary>
public sealed class Comment : AggregateRoot
{
    public const int MaximumTextLength = 1_000;
    public const int MaximumReasonLength = 300;
    public const int MaximumMentions = 10;

    private readonly List<CommentMention> _mentions = [];

    private Comment() { }

    public RecordKind TargetKind { get; private set; }
    public Guid TargetId { get; private set; }

    /// <summary>The project of the record (copied, so the archived-project check and the project's discussion need no join). None for a task without a project.</summary>
    public Guid? ProjectId { get; private set; }

    public string Text { get; private set; } = "";
    public Guid AuthorUserId { get; private set; }
    public bool IsVoided { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<CommentMention> Mentions => _mentions;

    public static Comment Create(Guid id, Guid tenantId, RecordKind targetKind, Guid targetId, Project? project, string? text, Guid authorUserId,
        IReadOnlyList<MentionCandidate> mentions, string? clientRequestId, DateTimeOffset now)
    {
        if (!RecordKinds.CanCarryComments(targetKind)) throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "This record has no discussion.");
        if (targetId == Guid.Empty) throw new ArgumentException("A comment needs the record it is on.", nameof(targetId));
        EnsureOpenForDiscussion(project);

        var errors = new ValidationErrors();
        var comment = new Comment { TargetKind = targetKind, TargetId = targetId, ProjectId = project?.Id, AuthorUserId = authorUserId };
        comment.Text = Field(errors, () => Guard.RequiredText(text, "text", "your comment", MaximumTextLength)) ?? "";
        comment.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));

        var distinct = mentions.DistinctBy(mention => mention.UserId).ToArray();
        if (distinct.Length > MaximumMentions) errors.Add("mentionUserIds", $"Mention at most {MaximumMentions} people in one comment.");
        foreach (var mention in distinct)
        {
            if (!mention.IsActive) errors.Add("mentionUserIds", $"{mention.DisplayName} can no longer sign in, so they can't be mentioned.");
            else if (!mention.CanReadTarget) errors.Add("mentionUserIds", $"{mention.DisplayName} can't see this record, so they can't be mentioned here.");
        }
        errors.ThrowIfAny();

        comment.Initialize(id, tenantId, now);
        foreach (var mention in distinct) comment._mentions.Add(new CommentMention(Guid.NewGuid(), tenantId, id, mention.UserId));
        return comment;
    }

    /// <summary>
    /// Removes the comment from the discussion: its author or an Administrator, with a reason that stays on the record. The words are kept
    /// in the database but no longer shown.
    /// </summary>
    public void Void(Guid actorUserId, bool actorIsAdministrator, Project? project, string? reason, DateTimeOffset now)
    {
        if (project?.Id != ProjectId) throw new InvalidOperationException("The project given is not the comment's project.");
        EnsureOpenForDiscussion(project);
        if (IsVoided) throw new DomainRuleException("This comment was already removed.");
        if (!actorIsAdministrator && actorUserId != AuthorUserId)
            throw new DomainRuleException("Only the person who wrote this comment or an Administrator can remove it.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for removing it", MaximumReasonLength);

        IsVoided = true;
        VoidReason = cleanReason;
        VoidedByUserId = actorUserId;
        VoidedAtUtc = now;
        Touch(now);
    }

    /// <summary>An archived project's discussion is read-only, like the rest of the project.</summary>
    public static void EnsureOpenForDiscussion(Project? project)
    {
        if (project?.IsArchived == true)
            throw new DomainRuleException("This project is archived, so its discussion is read-only. Restore the project to comment.");
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

/// <summary>One person a comment mentions. Append-only, written with the comment.</summary>
public sealed class CommentMention
{
    private CommentMention() { }

    internal CommentMention(Guid id, Guid tenantId, Guid commentId, Guid userId)
    {
        Id = id;
        TenantId = tenantId;
        CommentId = commentId;
        UserId = userId;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CommentId { get; private set; }
    public Guid UserId { get; private set; }
}
