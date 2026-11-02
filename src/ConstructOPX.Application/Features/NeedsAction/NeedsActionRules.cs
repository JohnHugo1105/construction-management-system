using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Application.Features.NeedsAction;

/// <summary>Which groups exist, their keys, and who may act on each. The permission decides the group; the caller never chooses.</summary>
public static class NeedsActionGroups
{
    public static readonly IReadOnlyList<NeedsActionGroup> InOrder = Enum.GetValues<NeedsActionGroup>();

    public static string Key(NeedsActionGroup group) => group switch
    {
        NeedsActionGroup.PurchaseOrdersToApprove => "purchase-orders-to-approve",
        NeedsActionGroup.ChangeOrdersToApprove => "change-orders-to-approve",
        NeedsActionGroup.BillsToIssue => "bills-to-issue",
        NeedsActionGroup.ClaimsToCertify => "claims-to-certify",
        NeedsActionGroup.PayrollToApprove => "payroll-to-approve",
        NeedsActionGroup.BillsToChase => "bills-to-chase",
        NeedsActionGroup.DeliveriesExpected => "deliveries-expected",
        NeedsActionGroup.BondsToAttend => "bonds-to-attend",
        NeedsActionGroup.ProgressNotUpdated => "progress-not-updated",
        NeedsActionGroup.AttendanceNotRecorded => "attendance-not-recorded",
        NeedsActionGroup.StatutoryNeedsSetup => "statutory-needs-setup",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
    };

    /// <summary>
    /// Every permission that can put work on the page, in the order the page's navigation entry checks them: the permission each built-in group
    /// acts on (<see cref="CanActOn"/>), then those of the groups other features contribute through <see cref="INeedsActionSource"/> (tasks, the
    /// site diary, documents, the schedule). The web client keeps the same list (<c>NEEDS_ACTION_PERMISSIONS</c>) to decide whether to show the
    /// entry; a test on each side pins the same array so the two cannot drift apart.
    /// </summary>
    public static readonly IReadOnlyList<string> ActingPermissions =
    [
        Permissions.PurchasingApprove, Permissions.ChangeOrdersApprove, Permissions.BillingApprove, Permissions.BillingManage,
        Permissions.SubcontractsApprove, Permissions.PayrollApprove, Permissions.PurchasingReceive, Permissions.ReceivingRecord, Permissions.BondsManage,
        Permissions.ProgressWrite, Permissions.AttendanceWrite, Permissions.StatutoryManage,
        Permissions.TasksUse, Permissions.DiaryWrite, Permissions.DocumentsManage, Permissions.ScheduleWrite
    ];

    /// <summary>True when the roles hold the permission that acts on the group (the approval, issue, certify or record step).</summary>
    public static bool CanActOn(AccessRole roles, NeedsActionGroup group) => group switch
    {
        NeedsActionGroup.PurchaseOrdersToApprove => Permissions.IsAllowed(roles, Permissions.PurchasingApprove),
        NeedsActionGroup.ChangeOrdersToApprove => Permissions.IsAllowed(roles, Permissions.ChangeOrdersApprove),
        NeedsActionGroup.BillsToIssue => Permissions.IsAllowed(roles, Permissions.BillingApprove),
        NeedsActionGroup.ClaimsToCertify => Permissions.IsAllowed(roles, Permissions.SubcontractsApprove),
        NeedsActionGroup.PayrollToApprove => Permissions.IsAllowed(roles, Permissions.PayrollApprove),
        NeedsActionGroup.BillsToChase => Permissions.IsAllowed(roles, Permissions.BillingManage),
        NeedsActionGroup.DeliveriesExpected => Permissions.IsAllowed(roles, Permissions.PurchasingReceive) ||
            Permissions.IsAllowed(roles, Permissions.ReceivingRecord),
        NeedsActionGroup.BondsToAttend => Permissions.IsAllowed(roles, Permissions.BondsManage),
        NeedsActionGroup.ProgressNotUpdated => Permissions.IsAllowed(roles, Permissions.ProgressWrite),
        NeedsActionGroup.AttendanceNotRecorded => Permissions.IsAllowed(roles, Permissions.AttendanceWrite),
        NeedsActionGroup.StatutoryNeedsSetup => Permissions.IsAllowed(roles, Permissions.StatutoryManage),
        _ => false
    };

    /// <summary>The groups the roles may act on, in page order.</summary>
    public static IReadOnlyList<NeedsActionGroup> For(AccessRole roles) => InOrder.Where(group => CanActOn(roles, group)).ToArray();

    /// <summary>Groups with an urgent row first (keeping their order), then the rest in page order.</summary>
    public static IReadOnlyList<NeedsActionGroupDto> Ordered(IEnumerable<NeedsActionGroupDto> groups) =>
        groups.Select((group, index) => (group, index))
            .OrderBy(item => item.group.Rows.Any(row => row.IsUrgent) ? 0 : 1).ThenBy(item => item.index)
            .Select(item => item.group).ToArray();
}

/// <summary>
/// The day-based thresholds of the queue, judged on the company's calendar day. Readers filter with the cut-off dates; rows are worded with
/// the predicates; both come from here so the list and its wording never disagree.
/// </summary>
public static class NeedsActionRules
{
    /// <summary>Most rows a group shows; the group's count says how many wait in all.</summary>
    public const int RowsShown = 8;

    /// <summary>A project whose latest progress is older than this many days (or that started longer ago and has none) is listed.</summary>
    public const int ProgressStaleDays = 14;

    /// <summary>A project is expected to record attendance yesterday if it recorded some on the same weekday one week earlier.</summary>
    public const int AttendanceCompareDays = 7;

    /// <summary>Progress dated before this day is stale: more than 14 days old.</summary>
    public static DateOnly ProgressStaleBefore(DateOnly today) => today.AddDays(-ProgressStaleDays);

    /// <summary>The latest progress (or, with none, the start date) is more than 14 days before today.</summary>
    public static bool IsProgressStale(DateOnly? latestProgress, DateOnly startDate, DateOnly today) =>
        (latestProgress ?? startDate) < ProgressStaleBefore(today);

    /// <summary>The last valid-until date that needs attention: the bond expiry rule's "expiring soon" window (30 days, today counts).</summary>
    public static DateOnly BondsNeedAttentionUntil(DateOnly today) => today.AddDays(Bond.ExpiringSoonDays);

    /// <summary>An Active bond that has expired or expires soon, by the bond expiry rule.</summary>
    public static bool BondNeedsAttention(DateOnly validUntil, DateOnly today) => Bond.ExpiryFor(validUntil, today) != BondExpiry.Valid;

    /// <summary>A bill with something outstanding is overdue from the day after its due date.</summary>
    public static bool IsBillOverdue(DateOnly dueDate, DateOnly today) => today > dueDate;

    /// <summary>
    /// A delivery is listed once it is due (expected today or earlier; one with no expected date, or expected later, is not yet due) and is late
    /// once its expected date has passed; one expected today is due, not late yet.
    /// </summary>
    public static bool IsDeliveryLate(DateOnly? expectedDate, DateOnly today) => expectedDate is { } expected && expected < today;

    /// <summary>The day attendance should have been recorded for: yesterday on the company's calendar.</summary>
    public static DateOnly AttendanceDay(DateOnly today) => today.AddDays(-1);

    /// <summary>The same weekday a week before the attendance day: the day a project that records attendance would also have recorded some.</summary>
    public static DateOnly AttendanceCompareDay(DateOnly today) => AttendanceDay(today).AddDays(-AttendanceCompareDays);

    /// <summary>Whole days from <paramref name="from"/> to <paramref name="today"/>.</summary>
    public static int DaysBetween(DateOnly from, DateOnly today) => today.DayNumber - from.DayNumber;
}

/// <summary>The cut-off dates every group is read with, worked out once per request from the company's today.</summary>
public sealed record NeedsActionDates(DateOnly Today)
{
    public DateOnly ProgressStaleBefore => NeedsActionRules.ProgressStaleBefore(Today);
    public DateOnly BondsNeedAttentionUntil => NeedsActionRules.BondsNeedAttentionUntil(Today);
    public DateOnly AttendanceDay => NeedsActionRules.AttendanceDay(Today);

    /// <summary>The attendance day's weekday a week earlier.</summary>
    public DateOnly AttendanceCompareDay => NeedsActionRules.AttendanceCompareDay(Today);
}
