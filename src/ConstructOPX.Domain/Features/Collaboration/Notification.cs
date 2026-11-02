namespace ConstructOPX.Domain.Features.Collaboration;

/// <summary>
/// Something that happened that one person should know about: a task given to them, their task done, a mention, their draft decided. It is
/// shown in the app only (no email or text message is sent) and only to that person. The message is built from names and document numbers
/// only (see <see cref="NotificationMessages"/>): it never carries an amount, a rate or a pay figure, and never words someone typed.
/// </summary>
public sealed class Notification
{
    public const int MaximumMessageLength = 200;

    private Notification() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The one person who sees it.</summary>
    public Guid UserId { get; private set; }

    public NotificationKind Kind { get; private set; }
    public string Message { get; private set; } = "";
    public RecordKind TargetKind { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public static Notification Create(Guid id, Guid tenantId, Guid userId, NotificationKind kind, string message, RecordKind targetKind, Guid targetId,
        Guid? projectId, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("A notification needs an id.", nameof(id));
        if (tenantId == Guid.Empty) throw new ArgumentException("A notification needs an organization.", nameof(tenantId));
        if (userId == Guid.Empty) throw new ArgumentException("A notification needs the person it is for.", nameof(userId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        if (!Enum.IsDefined(targetKind)) throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, null);
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A notification needs a message.", nameof(message));

        return new Notification
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Kind = kind,
            Message = Fit(message.Trim()),
            TargetKind = targetKind,
            TargetId = targetId,
            ProjectId = projectId,
            CreatedAtUtc = now
        };
    }

    /// <summary>Marks it read. Reading it again changes nothing (the first time it was read stays).</summary>
    public bool MarkRead(DateTimeOffset now)
    {
        if (ReadAtUtc is not null) return false;
        ReadAtUtc = now;
        return true;
    }

    /// <summary>A long name could push a message past the limit: it is cut at a word with an ellipsis rather than refused, because the action already happened.</summary>
    private static string Fit(string message)
    {
        if (message.Length <= MaximumMessageLength) return message;
        var cut = message[..(MaximumMessageLength - 1)];
        var space = cut.LastIndexOf(' ');
        return (space > MaximumMessageLength / 2 ? cut[..space] : cut).TrimEnd(' ', ',', '.') + "…";
    }
}

/// <summary>
/// The words of every notification, in one place so they can be checked: each is a person's name, what they did, and the record by its
/// kind and document number (or its project's name when it has no number yet). Nothing a person typed (a title, a comment, a note) and no
/// figure ever goes in.
/// </summary>
public static class NotificationMessages
{
    /// <summary>"Ana Reyes assigned you a task on Riverside Residences."</summary>
    public static string TaskAssigned(string actorName, string? projectName) => $"{actorName} assigned you a task{On(projectName)}.";

    /// <summary>"Ana Reyes marked your task on Riverside Residences as done."</summary>
    public static string TaskDone(string actorName, string? projectName) => $"{actorName} marked your task{On(projectName)} as done.";

    /// <summary>"Ana Reyes mentioned you on purchase order PO-0012."</summary>
    public static string Mention(string actorName, string recordWords) => $"{actorName} mentioned you on {recordWords}.";

    /// <summary>"Ana Reyes approved purchase order PO-0012."</summary>
    public static string Decided(string actorName, string verb, string recordWords) => $"{actorName} {verb} {recordWords}.";

    // ---- The record, in words ---------------------------------------------------------------------------------------------------

    public static string PurchaseOrder(string number) => $"purchase order {number}";

    public static string ChangeOrder(string? number, string projectName) =>
        string.IsNullOrWhiteSpace(number) ? $"a draft change order on {projectName}" : $"change order {number} on {projectName}";

    public static string Bill(string? number, string projectName) =>
        string.IsNullOrWhiteSpace(number) ? $"a draft bill on {projectName}" : $"bill {number} on {projectName}";

    public static string Subcontract(string? number, string projectName) =>
        string.IsNullOrWhiteSpace(number) ? $"a subcontract on {projectName}" : $"subcontract {number}";

    public static string Claim(int claimNumber, string subcontractNumber) => $"claim {claimNumber} on subcontract {subcontractNumber}";

    public static string Bond(string number) => $"bond {number}";

    public static string Project(string projectName) => $"project {projectName}";

    public static string Task(string? projectName) => $"a task{On(projectName)}";

    private static string On(string? projectName) => string.IsNullOrWhiteSpace(projectName) ? "" : $" on {projectName}";
}
