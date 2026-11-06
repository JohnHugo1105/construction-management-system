using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Domain.UnitTests.Application;

/// <summary>The comment permission map (a record's discussion follows the record's own read rule), task visibility, and the due-date words.</summary>
public sealed class CollaborationAccessTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();

    private static CollaborationTarget Target(RecordKind kind) =>
        new(kind, Guid.NewGuid(), Guid.NewGuid(), "Riverside", false, "record", kind == RecordKind.Task ? Creator : null, kind == RecordKind.Task ? Assignee : null);

    [Theory]
    [InlineData(RecordKind.Project, Permissions.ProjectsRead)]
    [InlineData(RecordKind.PurchaseOrder, Permissions.PurchasingRead)]
    [InlineData(RecordKind.ChangeOrder, Permissions.ChangeOrdersRead)]
    [InlineData(RecordKind.ClientBill, Permissions.BillingRead)]
    [InlineData(RecordKind.Subcontract, Permissions.SubcontractsRead)]
    [InlineData(RecordKind.Bond, Permissions.BondsRead)]
    public void Reading_a_record_discussion_needs_the_permission_that_reads_the_record(RecordKind kind, string permission)
    {
        Assert.Equal(permission, CollaborationAccess.ReadPermission(kind));
        foreach (var role in AccessRoles.Split(AccessRoles.All))
            Assert.Equal(Permissions.IsAllowed(role, permission), CollaborationAccess.CanRead(Guid.NewGuid(), role, Target(kind)));
    }

    [Fact]
    public void A_warehouse_keeper_and_an_hr_officer_cannot_read_the_discussion_on_money_records()
    {
        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        foreach (var kind in new[] { RecordKind.PurchaseOrder, RecordKind.ChangeOrder, RecordKind.ClientBill, RecordKind.Subcontract })
            Assert.False(CollaborationAccess.CanReadKind(role, kind), $"{role} {kind}");
        Assert.True(CollaborationAccess.CanReadKind(AccessRole.WarehouseKeeper, RecordKind.Project));
    }

    [Fact]
    public void A_task_discussion_is_for_its_creator_its_assignee_and_administrators()
    {
        var task = Target(RecordKind.Task);

        Assert.Null(CollaborationAccess.ReadPermission(RecordKind.Task));
        Assert.True(CollaborationAccess.CanRead(Creator, AccessRole.HrOfficer, task));
        Assert.True(CollaborationAccess.CanRead(Assignee, AccessRole.WarehouseKeeper, task));
        Assert.True(CollaborationAccess.CanRead(Guid.NewGuid(), AccessRole.Administrator, task));
        Assert.False(CollaborationAccess.CanRead(Guid.NewGuid(), AccessRole.ProjectManager, task));
        Assert.Equal(AccessRole.Administrator, CollaborationAccess.ReadingRoles(RecordKind.Task));
    }

    [Fact]
    public void Writing_needs_tasks_use_so_a_viewer_only_reads()
    {
        Assert.False(Permissions.IsAllowed(AccessRole.Viewer, Permissions.TasksUse));
        Assert.True(Permissions.IsAllowed(AccessRole.Viewer, Permissions.PurchasingRead));
        foreach (var role in new[] { AccessRole.Administrator, AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant,
                     AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
            Assert.True(Permissions.IsAllowed(role, Permissions.TasksUse), role.ToString());
        Assert.All(AccessRoles.Split(AccessRoles.All), role => Assert.True(Permissions.IsAllowed(role, Permissions.NotificationsRead)));
    }

    [Theory]
    [InlineData("PurchaseOrder", RecordKind.PurchaseOrder)]
    [InlineData(" clientbill ", RecordKind.ClientBill)]
    public void A_record_kind_is_read_from_its_word(string text, RecordKind expected) =>
        Assert.Equal(expected, CollaborationAccess.ParseKind(text, "kind", RecordKinds.CanCarryComments, "Choose."));

    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("Bond")]
    [InlineData("Invoice")]
    public void A_wrong_or_unsupported_kind_is_refused_on_its_field(string? text) =>
        Assert.Equal(["kind"], Assert.Throws<ValidationFailedException>(() =>
            CollaborationAccess.ParseKind(text, "kind", RecordKinds.CanCarryComments, "Choose.")).Errors.Keys);

    [Fact]
    public void The_due_date_reads_in_words_on_the_company_day()
    {
        var today = new DateOnly(2026, 11, 2);

        Assert.Equal("No due date", TaskWords.DueDetail(null, today));
        Assert.Equal("Due today", TaskWords.DueDetail(today, today));
        Assert.Equal("Due tomorrow", TaskWords.DueDetail(today.AddDays(1), today));
        Assert.Equal("Due 9 Nov 2026", TaskWords.DueDetail(today.AddDays(7), today));
        Assert.Equal("1 day overdue, due 1 Nov 2026", TaskWords.DueDetail(today.AddDays(-1), today));
        Assert.Equal("3 days overdue, due 30 Oct 2026", TaskWords.DueDetail(today.AddDays(-3), today));
        // A done or cancelled task has no due words: it is no longer waiting.
        Assert.Equal("Due today", TaskWords.DueDetailWhileOpen(WorkTaskStatus.Open, today, today));
        Assert.Null(TaskWords.DueDetailWhileOpen(WorkTaskStatus.Done, today.AddDays(-3), today));
        Assert.Null(TaskWords.DueDetailWhileOpen(WorkTaskStatus.Cancelled, today, today));
    }
}
