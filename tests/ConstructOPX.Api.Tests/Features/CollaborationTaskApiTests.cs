using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CollaborationTestKit;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Tasks end to end: who sees and moves them, the notifications they cause, idempotency, versions, companies and statement counts.</summary>
public sealed class CollaborationTaskApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public CollaborationTaskApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_task_round_trip_assign_complete_and_reopen_notifies_the_right_people()
    {
        var (manager, managerUser) = await SignedInAsync(_factory, AccessRole.ProjectManager);
        using var _m = manager;
        var (engineer, engineerUser) = await SignedInAsync(_factory, AccessRole.CostEngineer, managerUser.TenantId);
        using var _e = engineer;
        var project = await CreateAdminProjectAsync(managerUser.TenantId);

        var created = await manager.ReadAsync<WorkTaskDto>(await manager.PostAsync("/api/tasks",
            NewTask(engineerUser.UserId, projectId: project, dueDate: BondTestKit.DaysFromToday(2), description: "Before the pour")));
        Assert.Equal(("Open", "Check the rebar quantities", "Before the pour", false), (created.Status, created.Title, created.Description, created.IsOverdue));
        Assert.Equal((managerUser.UserId, engineerUser.UserId, project), (created.CreatedByUserId, created.AssigneeUserId, created.ProjectId));
        Assert.Equal(new TaskActionsDto(true, true, true, false), created.Actions);
        Assert.Equal($"Due {DateWords(BondTestKit.Today().AddDays(2))}", created.DueDetail);

        // The assignee is told; the creator is not told about what they did.
        var assigned = Assert.Single(await NotificationsAsync(engineer));
        Assert.Equal(("TaskAssigned", "Task", created.Id, false), (assigned.Kind, assigned.TargetKind, assigned.TargetId, assigned.IsRead));
        Assert.StartsWith("Test Admin assigned you a task on ", assigned.Message); // the first person of a new company is named "Test Admin"
        Assert.Empty(await NotificationsAsync(manager));

        // The assignee sees it in "Assigned to me" and may complete it, but not cancel or reassign it.
        var mine = await engineer.GetAsync<PagedResult<WorkTaskListItemDto>>("/api/tasks?scope=assigned");
        Assert.Equal(created.Id, Assert.Single(mine.Items).Id);
        var seen = await engineer.GetAsync<WorkTaskDto>($"/api/tasks/{created.Id}");
        Assert.Equal(new TaskActionsDto(true, false, false, false), seen.Actions);
        Assert.Equal(HttpStatusCode.Conflict, (await StepAsync(engineer, seen, "cancel", new { reason = (string?)null, version = seen.Version })).StatusCode);

        var done = await engineer.ReadAsync<WorkTaskDto>(await StepAsync(engineer, seen, "complete"));
        Assert.Equal(("Done", "Test User", (string?)null), (done.Status, done.CompletedByName, done.DueDetail));
        var told = Assert.Single(await NotificationsAsync(manager));
        Assert.Equal(("TaskDone", created.Id), (told.Kind, told.TargetId));
        Assert.Matches("^Test User marked your task on .+ as done\\.$", told.Message);

        // Only the creator (or an Administrator) reopens it.
        Assert.Equal(new TaskActionsDto(false, false, false, false), (await engineer.GetAsync<WorkTaskDto>($"/api/tasks/{created.Id}")).Actions);
        var reopened = await manager.ReadAsync<WorkTaskDto>(await StepAsync(manager, done, "reopen"));
        Assert.Equal(("Open", (string?)null), (reopened.Status, reopened.CompletedByName));
    }

    [SqlServerFact]
    public async Task Reassigning_tells_the_new_assignee_and_a_task_assigned_to_yourself_tells_nobody()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (first, firstUser) = await SignedInAsync(_factory, AccessRole.Accountant, adminUser.TenantId);
        var (second, secondUser) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, adminUser.TenantId);
        using var _f = first;
        using var _s = second;

        var own = await CreateTaskAsync(admin, adminUser.UserId);
        Assert.Equal(adminUser.UserId, own.AssigneeUserId);
        Assert.Empty(await NotificationsAsync(admin));

        var task = await CreateTaskAsync(admin, firstUser.UserId);
        var moved = await admin.ReadAsync<WorkTaskDto>(await StepAsync(admin, task, "reassign", new { assigneeUserId = secondUser.UserId, version = task.Version }));
        Assert.Equal(secondUser.UserId, moved.AssigneeUserId);
        Assert.Equal("TaskAssigned", Assert.Single(await NotificationsAsync(second)).Kind);

        // The first assignee no longer sees it at all.
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync($"/api/tasks/{task.Id}")).StatusCode);

        // The same person again, someone who can't use tasks, and someone of another company are refused on the field.
        var (viewer, viewerUser) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        using var _v = viewer;
        var (_, stranger) = await SignedInAsync(_factory);
        foreach (var (assignee, message) in new[]
                 {
                     (secondUser.UserId, "This task is already assigned to Test User."),
                     (viewerUser.UserId, "Test User can't work on tasks. Choose someone else."),
                     (stranger.UserId, "Choose who the task is for.")
                 })
            Assert.Equal(message, await ErrorMessageAsync(
                await StepAsync(admin, moved, "reassign", new { assigneeUserId = assignee, version = moved.Version }), "assigneeUserId"));
    }

    [SqlServerFact]
    public async Task A_task_is_seen_only_by_its_creator_its_assignee_and_administrators_and_all_is_for_administrators()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (creator, creatorUser) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        var (assignee, assigneeUser) = await SignedInAsync(_factory, AccessRole.HrOfficer, adminUser.TenantId);
        var (other, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        using var _c = creator;
        using var _s = assignee;
        using var _o = other;
        using var _v = viewer;

        var task = await CreateTaskAsync(creator, assigneeUser.UserId);

        Assert.Equal(HttpStatusCode.OK, (await assignee.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        foreach (var step in new[] { "complete", "reopen" })
            Assert.Equal(HttpStatusCode.NotFound, (await StepAsync(other, task, step)).StatusCode);
        Assert.Empty((await other.GetAsync<PagedResult<WorkTaskListItemDto>>("/api/tasks?scope=created")).Items);

        // "Created by me" and, for an Administrator, every task.
        Assert.Equal(task.Id, Assert.Single((await creator.GetAsync<PagedResult<WorkTaskListItemDto>>("/api/tasks?scope=created")).Items).Id);
        Assert.Contains((await admin.GetAsync<PagedResult<WorkTaskListItemDto>>("/api/tasks?scope=all")).Items, item => item.Id == task.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync("/api/tasks?scope=all")).StatusCode);
        Assert.Equal(["scope"], await ErrorFieldsAsync(await creator.GetAsync("/api/tasks?scope=everyone")));

        // A Viewer has no tasks at all.
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/tasks", NewTask(adminUser.UserId))).StatusCode);
    }

    [SqlServerFact]
    public async Task Refusals_a_stale_version_a_repeated_request_and_another_company()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (assignee, assigneeUser) = await SignedInAsync(_factory, AccessRole.CostEngineer, adminUser.TenantId);
        using var _s = assignee;

        // Every problem at once, in plain words.
        var refused = await admin.PostAsync("/api/tasks", NewTask(Guid.Empty, title: " ", dueDate: BondTestKit.DaysFromToday(-1)));
        Assert.Equal(["assigneeUserId", "dueDate", "title"], (await ErrorFieldsAsync(refused)).Order());

        // A repeat of the same request answers with the first task and tells the assignee once.
        var body = NewTask(assigneeUser.UserId, clientRequestId: "task-request-1");
        var first = await admin.ReadAsync<WorkTaskDto>(await admin.PostAsync("/api/tasks", body));
        var again = await admin.ReadAsync<WorkTaskDto>(await admin.PostAsync("/api/tasks", body));
        Assert.Equal(first.Id, again.Id);
        Assert.Single(await NotificationsAsync(assignee));

        // A stale version is a conflict.
        await assignee.ReadAsync<WorkTaskDto>(await StepAsync(assignee, first, "complete"));
        var stale = await StepAsync(admin, first, "reopen");
        Assert.Equal((HttpStatusCode.Conflict, "concurrency_conflict"), (stale.StatusCode, await RuleCodeAsync(stale)));

        // Another company: the task, its steps, and its project or people are not found.
        var (theirs, _) = await SignedInAsync(_factory);
        using var _t = theirs;
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/tasks/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await StepAsync(theirs, first, "complete")).StatusCode);
        var project = await CreateProjectAsync(admin);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/tasks", NewTask(Guid.NewGuid(), projectId: project.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PostAsync("/api/tasks", NewTask(Guid.NewGuid(), linkKind: "Project", linkId: project.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task A_linked_task_takes_the_records_project_and_needs_an_assignee_who_can_read_it()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (store, storeUser) = await AsRoleAsync(_factory, world, AccessRole.WarehouseKeeper);
        var (engineer, engineerUser) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        using var _s = store;
        using var _e = engineer;

        var linked = await world.Admin.PostAsync<WorkTaskDto>("/api/tasks",
            NewTask(engineerUser.UserId, linkKind: "PurchaseOrder", linkId: world.Order.Id));
        Assert.Equal((world.Project.Id, "PurchaseOrder", world.Order.Id), (linked.ProjectId, linked.LinkKind, linked.LinkId));
        Assert.Equal($"purchase order {world.Order.Number}", linked.LinkLabel);

        // A warehouse keeper can't read purchase orders, so can't be given a task about one, nor link one themselves.
        Assert.Equal("Test User can't see the linked record. Choose someone who can, or remove the link.", await ErrorMessageAsync(
            await world.Admin.PostAsync("/api/tasks", NewTask(storeUser.UserId, linkKind: "PurchaseOrder", linkId: world.Order.Id)), "assigneeUserId"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await store.PostAsync("/api/tasks", NewTask(storeUser.UserId, linkKind: "PurchaseOrder", linkId: world.Order.Id))).StatusCode);

        // The project chosen must be the linked record's.
        var other = await CreateProjectAsync(world.Admin);
        Assert.Equal(["projectId"], await ErrorFieldsAsync(await world.Admin.PostAsync("/api/tasks",
            NewTask(engineerUser.UserId, projectId: other.Id, linkKind: "PurchaseOrder", linkId: world.Order.Id))));
        Assert.Equal(["linkKind"], await ErrorFieldsAsync(await world.Admin.PostAsync("/api/tasks",
            NewTask(engineerUser.UserId, linkKind: "Task", linkId: linked.Id))));
    }

    [SqlServerFact]
    public async Task An_archived_projects_tasks_are_read_only()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var task = await CreateTaskAsync(admin, adminUser.UserId, project.Id);
        await ArchiveAsync(admin, await admin.GetAsync<ProjectDto>($"/api/projects/{project.Id}"));

        var read = await admin.GetAsync<WorkTaskDto>($"/api/tasks/{task.Id}");
        Assert.True(read.ProjectArchived);
        Assert.Equal(TaskActionsDto.None, read.Actions);
        var refused = await StepAsync(admin, read, "complete");
        Assert.Equal((HttpStatusCode.Conflict, "This project is archived. Restore it before making changes."), (refused.StatusCode, await DetailAsync(refused)));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/tasks", NewTask(adminUser.UserId, projectId: project.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task The_needs_action_group_lists_the_callers_open_tasks_overdue_first_with_no_amount()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (store, storeUser) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, adminUser.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        using var _s = store;
        using var _v = viewer;

        var later = await CreateTaskAsync(admin, storeUser.UserId, dueDate: BondTestKit.DaysFromToday(5));
        var noDate = await CreateTaskAsync(admin, storeUser.UserId);
        var soon = await CreateTaskAsync(admin, storeUser.UserId, dueDate: BondTestKit.DaysFromToday(0));
        var finished = await CreateTaskAsync(admin, storeUser.UserId);
        await store.ReadAsync<WorkTaskDto>(await StepAsync(store, finished, "complete"));
        await CreateTaskAsync(admin, adminUser.UserId); // someone else's

        var group = (await store.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == "tasks-assigned");
        Assert.Equal(3, group.Count);
        Assert.Equal([soon.Id, later.Id, noDate.Id], group.Rows.Select(row => row.Id));
        Assert.Equal(["Due today", $"Due {DateWords(BondTestKit.Today().AddDays(5))}", "No due date"], group.Rows.Select(row => row.Detail));
        Assert.All(group.Rows, row => Assert.Null(row.AmountMinor));
        Assert.Equal("Check the rebar quantities", group.Rows[0].Label);
        Assert.Equal(3, (await store.GetAsync<NeedsActionCountDto>("/api/needs-action/count")).Total);

        // A Viewer cannot act on tasks, so the group is not theirs.
        Assert.DoesNotContain((await viewer.GetAsync<NeedsActionDto>("/api/needs-action")).Groups, item => item.Key == "tasks-assigned");
    }

    [SqlServerFact]
    public async Task The_task_list_and_a_task_cost_a_fixed_number_of_statements()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var first = await CreateTaskAsync(admin, adminUser.UserId, project.Id);

        var otherProject = await CreateProjectAsync(admin);
        await CreateTaskAsync(admin, adminUser.UserId, otherProject.Id);

        async Task<(int List, int Created, int OneProject, int One)> MeasureAsync() => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/tasks?pageSize=100")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/tasks?scope=created&status=Open")).StatusCode)),
            await StatementsAsync(_factory, async () =>
                Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tasks?scope=all&projectId={project.Id}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tasks/{first.Id}")).StatusCode)));

        var withOne = await MeasureAsync();
        for (var index = 0; index < 9; index++) await CreateTaskAsync(admin, adminUser.UserId, project.Id);
        var withTen = await MeasureAsync();

        Assert.Equal(11, (await admin.GetAsync<PagedResult<WorkTaskListItemDto>>("/api/tasks?pageSize=100")).Total);
        // The project filter keeps only that project's tasks.
        var onProject = await admin.GetAsync<PagedResult<WorkTaskListItemDto>>($"/api/tasks?projectId={project.Id}&pageSize=100");
        Assert.Equal(10, onProject.Total);
        Assert.All(onProject.Items, item => Assert.Equal(project.Id, item.ProjectId));
        Assert.Equal(withOne, withTen);
        // The organization (today's date) plus count and page; a task is the organization plus the task.
        Assert.Equal((3, 3, 3, 2), withTen);
    }

    private async Task<Guid> CreateAdminProjectAsync(Guid tenantId)
    {
        var (admin, _) = await SignedInAsync(_factory, AccessRole.Administrator, tenantId);
        using var _a = admin;
        return (await CreateProjectAsync(admin)).Id;
    }

    private static string DateWords(DateOnly date) => date.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
