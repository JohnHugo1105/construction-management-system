using System.Text.RegularExpressions;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The collaboration rules: tasks and who may move them, the due date, comments, mentions and removal, and the notification words.</summary>
public sealed class CollaborationTests
{
    private static readonly DateOnly Today = TestData.Today; // 2 Nov 2026
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid AssigneeId = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private static TaskPerson Person(Guid? id = null, string name = "Pia Santos", bool active = true, bool canUseTasks = true, bool canReadLink = true) =>
        new(id ?? AssigneeId, name, active, canUseTasks, canReadLink);

    private static WorkTask NewTask(Project? project = null, DateOnly? due = null, TaskPerson? assignee = null, TaskLink? link = null,
        string? title = "Check the rebar quantities") =>
        WorkTask.Create(Guid.NewGuid(), TestData.Tenant, title, null, due, assignee ?? Person(), Creator, project, link, null, Today, Now);

    private static TaskActor As(Guid id, bool admin = false) => new(id, admin);

    private static Project Archived(Project project)
    {
        project.Archive(ArchiveReason.Finished, null, Now);
        return project;
    }

    private static IReadOnlyDictionary<string, string[]> Errors(Action action) => Assert.Throws<ValidationFailedException>(action).Errors;

    private static string Rule(Action action) => Assert.Throws<DomainRuleException>(action).Message;

    // ---- Creating a task --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_task_is_open_for_its_assignee_with_the_creator_and_the_trimmed_words()
    {
        var project = TestData.NewProject();
        var task = WorkTask.Create(Guid.NewGuid(), TestData.Tenant, "  Check the rebar  ", "  Before the pour ", Today, Person(), Creator, project,
            new TaskLink(RecordKind.PurchaseOrder, Guid.NewGuid()), " req-1 ", Today, Now);

        Assert.Equal((WorkTaskStatus.Open, "Check the rebar", "Before the pour", Today), (task.Status, task.Title, task.Description, task.DueDate));
        Assert.Equal((AssigneeId, Creator, project.Id, "req-1"), (task.AssigneeUserId, task.CreatedByUserId, task.ProjectId, task.ClientRequestId));
        Assert.Equal(RecordKind.PurchaseOrder, task.LinkKind);
        Assert.Equal(0, task.Version);
    }

    [Fact]
    public void Assigning_a_task_to_yourself_is_allowed()
    {
        var task = NewTask(assignee: Person(Creator, "Ana Reyes"));

        Assert.Equal(task.CreatedByUserId, task.AssigneeUserId);
    }

    [Fact]
    public void A_task_reports_every_problem_together()
    {
        var errors = Errors(() => NewTask(title: " ", due: Today.AddDays(-1), assignee: Person(canUseTasks: false)));

        Assert.Equal(["assigneeUserId", "dueDate", "title"], errors.Keys.Order());
        Assert.Equal("Enter a title for the task.", errors["title"][0]);
        Assert.Equal("The due date can't be in the past. Choose today or a later day.", errors["dueDate"][0]);
        Assert.Equal("Pia Santos can't work on tasks. Choose someone else.", errors["assigneeUserId"][0]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void The_due_date_may_be_today_or_later_but_not_before_today(int days, bool allowed)
    {
        var create = () => NewTask(due: Today.AddDays(days));

        if (allowed) Assert.Equal(Today.AddDays(days), create().DueDate);
        else Assert.Equal(["dueDate"], Errors(() => create()).Keys);
    }

    [Fact]
    public void A_task_without_a_due_date_is_never_overdue()
    {
        var task = NewTask();

        Assert.Null(task.DueDate);
        Assert.False(task.IsOverdue(Today.AddYears(5)));
    }

    [Fact]
    public void Overdue_is_derived_from_the_due_date_while_open_only()
    {
        Assert.False(WorkTask.IsOverdueOn(WorkTaskStatus.Open, Today, Today));
        Assert.True(WorkTask.IsOverdueOn(WorkTaskStatus.Open, Today.AddDays(-1), Today));
        Assert.False(WorkTask.IsOverdueOn(WorkTaskStatus.Done, Today.AddDays(-1), Today));
        Assert.False(WorkTask.IsOverdueOn(WorkTaskStatus.Cancelled, Today.AddDays(-1), Today));
    }

    [Theory]
    [InlineData(121, "title")]
    [InlineData(1_001, "description")]
    public void Long_words_are_refused(int length, string field)
    {
        var text = new string('a', length);
        var errors = Errors(() => WorkTask.Create(Guid.NewGuid(), TestData.Tenant, field == "title" ? text : "Title", field == "description" ? text : null,
            null, Person(), Creator, null, null, null, Today, Now));

        Assert.Equal([field], errors.Keys);
    }

    [Fact]
    public void The_assignee_must_be_able_to_sign_in_use_tasks_and_see_the_linked_record()
    {
        var link = new TaskLink(RecordKind.ClientBill, Guid.NewGuid());

        Assert.Equal("Choose who the task is for.", Errors(() => WorkTask.Create(Guid.NewGuid(), TestData.Tenant, "T", null, null, null, Creator, null,
            null, null, Today, Now))["assigneeUserId"][0]);
        Assert.Equal("Pia Santos can no longer sign in. Choose someone else.", Errors(() => NewTask(assignee: Person(active: false)))["assigneeUserId"][0]);
        Assert.Equal("Pia Santos can't see the linked record. Choose someone who can, or remove the link.",
            Errors(() => NewTask(assignee: Person(canReadLink: false), link: link))["assigneeUserId"][0]);
        // Without a link the record rule does not apply.
        Assert.Equal(AssigneeId, NewTask(assignee: Person(canReadLink: false)).AssigneeUserId);
    }

    [Fact]
    public void A_task_cannot_link_to_another_task_or_to_nothing()
    {
        Assert.Equal(["linkKind"], Errors(() => NewTask(link: new TaskLink(RecordKind.Task, Guid.NewGuid()))).Keys);
        Assert.Equal(["linkId"], Errors(() => NewTask(link: new TaskLink(RecordKind.Bond, Guid.Empty))).Keys);
    }

    [Fact]
    public void A_task_cannot_be_created_on_an_archived_project()
    {
        var project = Archived(TestData.NewProject());

        Assert.Equal("This project is archived. Restore it before making changes.", Rule(() => NewTask(project)));
    }

    // ---- Moving a task ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_assignee_the_creator_or_an_administrator_completes_a_task_and_nobody_else()
    {
        foreach (var actor in new[] { As(AssigneeId), As(Creator), As(Stranger, admin: true) })
        {
            var task = NewTask();
            task.Complete(actor, null, Now);
            Assert.Equal((WorkTaskStatus.Done, actor.UserId, Now), (task.Status, task.CompletedByUserId, task.CompletedAtUtc));
        }

        Assert.Equal("Only the person it is assigned to, the person who created it or an Administrator can mark this task as done.",
            Rule(() => NewTask().Complete(As(Stranger), null, Now)));
    }

    [Fact]
    public void Only_the_creator_or_an_administrator_cancels_reassigns_or_reopens()
    {
        var task = NewTask();
        Assert.Equal("Only the person who created this task or an Administrator can cancel it.", Rule(() => task.Cancel(As(AssigneeId), null, null, Now)));
        Assert.Equal("Only the person who created this task or an Administrator can reassign it.",
            Rule(() => task.Reassign(As(AssigneeId), null, Person(Guid.NewGuid(), "Leo Cruz"), Now)));

        task.Cancel(As(Creator), null, " Not needed ", Now);
        Assert.Equal((WorkTaskStatus.Cancelled, Creator, "Not needed"), (task.Status, task.CancelledByUserId, task.CancelReason));
        Assert.Equal("Only the person who created this task or an Administrator can reopen it.", Rule(() => task.Reopen(As(AssigneeId), null, Now)));

        task.Reopen(As(Stranger, admin: true), null, Now);
        Assert.Equal(WorkTaskStatus.Open, task.Status);
        Assert.Null(task.CancelReason);
        Assert.Null(task.CancelledByUserId);
    }

    [Fact]
    public void A_done_or_cancelled_task_must_be_reopened_before_anything_else_and_an_open_one_cannot_be_reopened()
    {
        var done = NewTask();
        done.Complete(As(AssigneeId), null, Now);
        Assert.Equal("This task is already done, so it can't be marked as done. Reopen it first.", Rule(() => done.Complete(As(Creator), null, Now)));
        Assert.Equal("This task is already done, so it can't be cancelled. Reopen it first.", Rule(() => done.Cancel(As(Creator), null, null, Now)));

        var cancelled = NewTask();
        cancelled.Cancel(As(Creator), null, null, Now);
        Assert.Equal("This task was cancelled, so it can't be reassigned. Reopen it first.",
            Rule(() => cancelled.Reassign(As(Creator), null, Person(Guid.NewGuid()), Now)));

        Assert.Equal("This task is already open.", Rule(() => NewTask().Reopen(As(Creator), null, Now)));

        done.Reopen(As(Creator), null, Now);
        Assert.Equal((WorkTaskStatus.Open, (Guid?)null, (DateTimeOffset?)null), (done.Status, done.CompletedByUserId, done.CompletedAtUtc));
    }

    [Fact]
    public void Reassigning_checks_the_new_assignee_and_refuses_the_same_person()
    {
        var task = NewTask(link: new TaskLink(RecordKind.Subcontract, Guid.NewGuid()));
        var leo = Guid.NewGuid();

        Assert.Equal("This task is already assigned to Pia Santos.", Errors(() => task.Reassign(As(Creator), null, Person(), Now))["assigneeUserId"][0]);
        Assert.Equal("Leo Cruz can't see the linked record. Choose someone who can, or remove the link.",
            Errors(() => task.Reassign(As(Creator), null, Person(leo, "Leo Cruz", canReadLink: false), Now))["assigneeUserId"][0]);

        task.Reassign(As(Creator), null, Person(leo, "Leo Cruz"), Now);
        Assert.Equal(leo, task.AssigneeUserId);
    }

    [Fact]
    public void Nothing_changes_on_an_archived_project()
    {
        var project = TestData.NewProject();
        var task = NewTask(project);
        Archived(project);

        Assert.Equal("This project is archived. Restore it before making changes.", Rule(() => task.Complete(As(AssigneeId), project, Now)));
        Assert.Equal("This project is archived. Restore it before making changes.", Rule(() => task.Cancel(As(Creator), project, null, Now)));
        Assert.Equal(WorkTaskStatus.Open, task.Status);
    }

    [Fact]
    public void Only_participants_and_administrators_see_a_task()
    {
        var task = NewTask();

        Assert.True(task.CanBeSeenBy(As(Creator)));
        Assert.True(task.CanBeSeenBy(As(AssigneeId)));
        Assert.True(task.CanBeSeenBy(As(Stranger, admin: true)));
        Assert.False(task.CanBeSeenBy(As(Stranger)));
    }

    // ---- Comments ---------------------------------------------------------------------------------------------------------------

    private static Comment NewComment(Project? project, params MentionCandidate[] mentions) =>
        Comment.Create(Guid.NewGuid(), TestData.Tenant, RecordKind.Project, project?.Id ?? Guid.NewGuid(), project, " Can the pour move? ", Creator,
            mentions, null, Now);

    [Fact]
    public void A_comment_keeps_its_record_project_author_and_each_mention_once()
    {
        var project = TestData.NewProject();
        var pia = new MentionCandidate(AssigneeId, "Pia Santos", true, true);
        var comment = NewComment(project, pia, pia);

        Assert.Equal((RecordKind.Project, project.Id, project.Id, "Can the pour move?", Creator),
            (comment.TargetKind, comment.TargetId, comment.ProjectId, comment.Text, comment.AuthorUserId));
        Assert.Equal([AssigneeId], comment.Mentions.Select(mention => mention.UserId));
        Assert.All(comment.Mentions, mention => Assert.Equal((TestData.Tenant, comment.Id), (mention.TenantId, mention.CommentId)));
        Assert.False(comment.IsVoided);
    }

    [Fact]
    public void A_mention_of_someone_who_cannot_see_the_record_is_refused_with_their_name()
    {
        var errors = Errors(() => NewComment(TestData.NewProject(), new MentionCandidate(AssigneeId, "Pia Santos", true, false),
            new MentionCandidate(Stranger, "Leo Cruz", false, true)));

        Assert.Equal(["Pia Santos can't see this record, so they can't be mentioned here.", "Leo Cruz can no longer sign in, so they can't be mentioned."],
            errors["mentionUserIds"]);
    }

    [Fact]
    public void A_comment_needs_words_up_to_1000_characters_and_at_most_ten_mentions()
    {
        Assert.Equal("Enter your comment.", Errors(() => Comment.Create(Guid.NewGuid(), TestData.Tenant, RecordKind.Task, Guid.NewGuid(), null, "  ",
            Creator, [], null, Now))["text"][0]);
        Assert.Equal(["text"], Errors(() => Comment.Create(Guid.NewGuid(), TestData.Tenant, RecordKind.Task, Guid.NewGuid(), null, new string('a', 1_001),
            Creator, [], null, Now)).Keys);
        Assert.Equal(1_000, Comment.Create(Guid.NewGuid(), TestData.Tenant, RecordKind.Task, Guid.NewGuid(), null, new string('a', 1_000), Creator, [], null,
            Now).Text.Length);

        var eleven = Enumerable.Range(0, 11).Select(index => new MentionCandidate(Guid.NewGuid(), $"Person {index}", true, true)).ToArray();
        Assert.Equal("Mention at most 10 people in one comment.", Errors(() => NewComment(null, eleven))["mentionUserIds"][0]);
    }

    [Fact]
    public void A_bond_has_no_discussion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Comment.Create(Guid.NewGuid(), TestData.Tenant, RecordKind.Bond, Guid.NewGuid(), null, "Hi", Creator,
            [], null, Now));
        Assert.False(RecordKinds.CanCarryComments(RecordKind.Bond));
        Assert.True(RecordKinds.CanCarryComments(RecordKind.Task));
    }

    [Fact]
    public void A_comment_on_an_archived_project_is_refused()
    {
        Assert.Equal("This project is archived, so its discussion is read-only. Restore the project to comment.",
            Rule(() => NewComment(Archived(TestData.NewProject()))));
    }

    [Fact]
    public void The_author_or_an_administrator_removes_a_comment_with_a_reason_once()
    {
        var project = TestData.NewProject();
        var comment = NewComment(project);

        Assert.Equal("Only the person who wrote this comment or an Administrator can remove it.",
            Rule(() => comment.Void(Stranger, false, project, "Wrong thread", Now)));
        Assert.Equal("Enter the reason for removing it.", Errors(() => comment.Void(Creator, false, project, " ", Now))["reason"][0]);

        comment.Void(Stranger, true, project, " Posted on the wrong project ", Now);
        Assert.Equal((true, "Posted on the wrong project", Stranger, Now), (comment.IsVoided, comment.VoidReason, comment.VoidedByUserId, comment.VoidedAtUtc));
        // The words stay on the record; only the thread stops showing them.
        Assert.Equal("Can the pour move?", comment.Text);
        Assert.Equal("This comment was already removed.", Rule(() => comment.Void(Creator, false, project, "Again", Now)));
    }

    [Fact]
    public void A_comment_on_an_archived_project_cannot_be_removed_either()
    {
        var project = TestData.NewProject();
        var comment = NewComment(project);
        Archived(project);

        Assert.Equal("This project is archived, so its discussion is read-only. Restore the project to comment.",
            Rule(() => comment.Void(Creator, false, project, "Mistake", Now)));
    }

    // ---- Notifications ----------------------------------------------------------------------------------------------------------

    /// <summary>A currency sign, or a figure with thousands separators or decimals: what an amount, rate or pay figure looks like.</summary>
    private static readonly Regex MoneyLike = new(@"[₱$€£]|PHP|\d{1,3}(,\d{3})+|\d+\.\d{2}\b", RegexOptions.CultureInvariant);

    [Fact]
    public void Every_message_names_people_and_documents_only()
    {
        var messages = new[]
        {
            NotificationMessages.TaskAssigned("Ana Reyes", "Riverside Residences"),
            NotificationMessages.TaskAssigned("Ana Reyes", null),
            NotificationMessages.TaskDone("Pia Santos", "Riverside Residences"),
            NotificationMessages.Mention("Ana Reyes", NotificationMessages.PurchaseOrder("PO-0012")),
            NotificationMessages.Decided("Alma Accountant", "approved", NotificationMessages.PurchaseOrder("PO-0012")),
            NotificationMessages.Decided("Alma Accountant", "rejected", NotificationMessages.ChangeOrder("CO-003", "Riverside Residences")),
            NotificationMessages.Decided("Alma Accountant", "issued", NotificationMessages.Bill("BILL-0004", "Riverside Residences")),
            NotificationMessages.Decided("Alma Accountant", "certified", NotificationMessages.Claim(2, "SC-0001")),
            NotificationMessages.Mention("Ana Reyes", NotificationMessages.Bill(null, "Riverside Residences"))
        };

        Assert.Equal([
            "Ana Reyes assigned you a task on Riverside Residences.",
            "Ana Reyes assigned you a task.",
            "Pia Santos marked your task on Riverside Residences as done.",
            "Ana Reyes mentioned you on purchase order PO-0012.",
            "Alma Accountant approved purchase order PO-0012.",
            "Alma Accountant rejected change order CO-003 on Riverside Residences.",
            "Alma Accountant issued bill BILL-0004 on Riverside Residences.",
            "Alma Accountant certified claim 2 on subcontract SC-0001.",
            "Ana Reyes mentioned you on a draft bill on Riverside Residences."
        ], messages);
        Assert.All(messages, message => Assert.DoesNotMatch(MoneyLike, message));
    }

    [Fact]
    public void A_notification_is_unread_until_read_and_reading_again_changes_nothing()
    {
        var notification = Notification.Create(Guid.NewGuid(), TestData.Tenant, AssigneeId, NotificationKind.Mention, " Ana Reyes mentioned you. ",
            RecordKind.Project, Guid.NewGuid(), null, Now);

        Assert.Equal(("Ana Reyes mentioned you.", false), (notification.Message, notification.IsRead));
        Assert.True(notification.MarkRead(Now));
        Assert.False(notification.MarkRead(Now.AddHours(1)));
        Assert.Equal(Now, notification.ReadAtUtc);
    }

    [Fact]
    public void A_message_longer_than_200_characters_is_cut_at_a_word_with_an_ellipsis()
    {
        var longName = string.Join(' ', Enumerable.Repeat("Riverside", 30));
        var notification = Notification.Create(Guid.NewGuid(), TestData.Tenant, AssigneeId, NotificationKind.TaskAssigned,
            NotificationMessages.TaskAssigned("Ana Reyes", longName), RecordKind.Task, Guid.NewGuid(), null, Now);

        Assert.True(notification.Message.Length <= Notification.MaximumMessageLength);
        Assert.EndsWith("Riverside…", notification.Message);
    }
}
