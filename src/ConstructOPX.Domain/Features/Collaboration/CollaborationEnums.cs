namespace ConstructOPX.Domain.Features.Collaboration;

/// <summary>
/// The records people coordinate on. A task may link to any of them except another task; a comment can sit on any of them except a bond;
/// a notification points at the record it is about.
/// </summary>
public enum RecordKind
{
    Project = 0,
    Task = 1,
    PurchaseOrder = 2,
    ChangeOrder = 3,
    ClientBill = 4,
    Subcontract = 5,
    Bond = 6
}

public enum WorkTaskStatus
{
    /// <summary>Waiting on the assignee.</summary>
    Open = 0,

    /// <summary>The assignee, the creator or an Administrator marked it done.</summary>
    Done = 1,

    /// <summary>The creator or an Administrator called it off. It can be reopened.</summary>
    Cancelled = 2
}

/// <summary>What a notification says happened.</summary>
public enum NotificationKind
{
    /// <summary>Someone gave you a task (a new task, or one reassigned to you).</summary>
    TaskAssigned = 0,

    /// <summary>A task you created was marked done by someone else.</summary>
    TaskDone = 1,

    /// <summary>Someone mentioned you in a comment.</summary>
    Mention = 2,

    /// <summary>A draft you prepared was decided: a purchase order approved or cancelled, a change order approved or rejected, a bill issued, a claim certified.</summary>
    DraftDecided = 3
}

/// <summary>Where a comment may be written, and where a task may link to.</summary>
public static class RecordKinds
{
    public static bool CanCarryComments(RecordKind kind) => Enum.IsDefined(kind) && kind != RecordKind.Bond;

    public static bool CanBeLinkedFromTask(RecordKind kind) => Enum.IsDefined(kind) && kind != RecordKind.Task;
}
