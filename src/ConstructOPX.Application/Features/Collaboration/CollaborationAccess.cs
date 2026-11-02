using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Application.Features.Collaboration;

/// <summary>
/// Who may read a record's discussion, be mentioned in it, or be given a task about it: exactly the people who may read the record itself.
/// A project needs projects.read, a purchase order purchasing.read, a change order changeorders.read, a bill billing.read, a subcontract
/// subcontracts.read, a bond bonds.read; a task is read by its creator, its assignee and Administrators. Writing a comment also needs
/// tasks.use, so a Viewer reads the discussion and does not add to it.
/// </summary>
public static class CollaborationAccess
{
    /// <summary>The permission that reads the record, or null for a task (decided by who takes part in it).</summary>
    public static string? ReadPermission(RecordKind kind) => kind switch
    {
        RecordKind.Project => Permissions.ProjectsRead,
        RecordKind.PurchaseOrder => Permissions.PurchasingRead,
        RecordKind.ChangeOrder => Permissions.ChangeOrdersRead,
        RecordKind.ClientBill => Permissions.BillingRead,
        RecordKind.Subcontract => Permissions.SubcontractsRead,
        RecordKind.Bond => Permissions.BondsRead,
        RecordKind.Task => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>The roles that read records of this kind (for a task, Administrators; its creator and assignee are added by id).</summary>
    public static AccessRole ReadingRoles(RecordKind kind) =>
        ReadPermission(kind) is { } permission ? Permissions.RolesFor(permission) : AccessRole.Administrator;

    /// <summary>True when the roles may read records of this kind at all. A task is decided per task (see <see cref="CanRead"/>).</summary>
    public static bool CanReadKind(AccessRole roles, RecordKind kind) => kind == RecordKind.Task || roles.HasAny(ReadingRoles(kind));

    /// <summary>True when the person may read this record (and so its discussion).</summary>
    public static bool CanRead(Guid userId, AccessRole roles, CollaborationTarget target) =>
        target.Kind == RecordKind.Task
            ? roles.HasAny(AccessRole.Administrator) || userId == target.TaskCreatorId || userId == target.TaskAssigneeId
            : roles.HasAny(ReadingRoles(target.Kind));

    public static bool IsAdministrator(AccessRole roles) => roles.HasAny(AccessRole.Administrator);

    /// <summary>A record kind from a request word ("PurchaseOrder"); numbers are not words. Refused with a plain message on <paramref name="field"/>.</summary>
    public static RecordKind ParseKind(string? text, string field, Func<RecordKind, bool> allowed, string message)
    {
        if (!string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) &&
            Enum.TryParse<RecordKind>(text.Trim(), ignoreCase: true, out var kind) && Enum.IsDefined(kind) && allowed(kind))
            return kind;
        throw new ValidationFailedException(field, message);
    }
}

/// <summary>How a task's due date reads in a list or the "Needs your action" queue, judged on the company's calendar day.</summary>
public static class TaskWords
{
    /// <summary>"3 days overdue, due 30 Oct 2026", "Due today", "Due tomorrow", "Due 5 Nov 2026" or "No due date".</summary>
    public static string DueDetail(DateOnly? dueDate, DateOnly today)
    {
        if (dueDate is not { } due) return "No due date";
        var days = due.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => $"{(-days == 1 ? "1 day" : $"{-days} days")} overdue, due {Day(due)}",
            0 => "Due today",
            1 => "Due tomorrow",
            _ => $"Due {Day(due)}"
        };
    }

    /// <summary>The words for a task in a list: the due detail while Open, nothing once it is done or cancelled.</summary>
    public static string? DueDetailWhileOpen(WorkTaskStatus status, DateOnly? dueDate, DateOnly today) =>
        status == WorkTaskStatus.Open ? DueDetail(dueDate, today) : null;

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
