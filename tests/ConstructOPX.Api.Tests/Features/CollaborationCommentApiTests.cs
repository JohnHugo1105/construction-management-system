using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CollaborationTestKit;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The comment permission map: for every kind of record, reading and writing its discussion follows the record's own read rule, writing also
/// needs tasks.use, mentions are checked against the same rule, and another company's records are not found. Plus removal, archived projects,
/// idempotency, the colleague lookup's fields and the fixed statement counts.
/// </summary>
public sealed class CollaborationCommentApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public CollaborationCommentApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerTheory]
    [InlineData("PurchaseOrder", AccessRole.CostEngineer, AccessRole.WarehouseKeeper)]
    [InlineData("ChangeOrder", AccessRole.Accountant, AccessRole.HrOfficer)]
    [InlineData("ClientBill", AccessRole.ProjectManager, AccessRole.WarehouseKeeper)]
    [InlineData("Subcontract", AccessRole.CostEngineer, AccessRole.HrOfficer)]
    public async Task A_records_discussion_follows_the_records_own_read_rule(string kind, AccessRole readerRole, AccessRole outsiderRole)
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (reader, readerUser) = await AsRoleAsync(_factory, world, readerRole);
        var (outsider, outsiderUser) = await AsRoleAsync(_factory, world, outsiderRole);
        var (viewer, _) = await AsRoleAsync(_factory, world, AccessRole.Viewer);
        using var _r = reader;
        using var _o = outsider;
        using var _v = viewer;
        var targetId = world.IdOf(kind);

        // Someone who reads the record and works on tasks reads and writes its discussion, and may mention someone who can read it.
        var mine = await reader.ReadAsync<CommentDto>(await CommentAsync(reader, kind, targetId, [world.User.UserId]));
        Assert.Equal(("Can the pour move to Monday?", readerUser.UserId, true), (mine.Text, mine.AuthorUserId, mine.CanRemove));
        Assert.Equal([world.User.UserId], mine.Mentions.Select(person => person.Id));
        var page = await reader.GetAsync<CommentPageDto>(Thread(kind, targetId));
        Assert.Equal((1, true, (string?)null), (page.Total, page.CanWrite, page.ReadOnlyReason));
        var mention = Assert.Single(await NotificationsAsync(world.Admin));
        Assert.Equal(("Mention", kind, targetId), (mention.Kind, mention.TargetKind, mention.TargetId));
        Assert.DoesNotMatch(MoneyLike, mention.Message);

        // A mention of someone who could not read the record is refused with their name, and nothing is saved.
        var refused = await CommentAsync(reader, kind, targetId, [outsiderUser.UserId]);
        Assert.Equal("Test User can't see this record, so they can't be mentioned here.", await ErrorMessageAsync(refused, "mentionUserIds"));
        Assert.Equal(1, (await reader.GetAsync<CommentPageDto>(Thread(kind, targetId))).Total);

        // Someone who may not read the record gets the same refusal whether or not the record, or a comment on it, exists.
        foreach (var id in new[] { targetId, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(Thread(kind, id))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await CommentAsync(outsider, kind, id)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/users/lookup?targetKind={kind}&targetId={id}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveAsync(outsider, mine)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveAsync(outsider, mine with { Id = Guid.NewGuid() })).StatusCode);
        Assert.Empty(await NotificationsAsync(outsider));

        // A Viewer reads the discussion but neither writes nor removes.
        var seen = await viewer.GetAsync<CommentPageDto>(Thread(kind, targetId));
        Assert.Equal((1, false), (seen.Total, seen.CanWrite));
        Assert.False(Assert.Single(seen.Items).CanRemove);
        Assert.Equal(HttpStatusCode.Forbidden, (await CommentAsync(viewer, kind, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(viewer, mine)).StatusCode);

        // Another company: the record and the comment are not found.
        var (theirs, _) = await SignedInAsync(_factory);
        using var _t = theirs;
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(Thread(kind, targetId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CommentAsync(theirs, kind, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveAsync(theirs, mine)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/users/lookup?targetKind={kind}&targetId={targetId}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Everyone_reads_a_projects_discussion_and_people_who_work_on_tasks_write_in_it()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (store, _) = await AsRoleAsync(_factory, world, AccessRole.WarehouseKeeper);
        var (viewer, viewerUser) = await AsRoleAsync(_factory, world, AccessRole.Viewer);
        using var _s = store;
        using var _v = viewer;

        // A viewer may be mentioned (they can read the project) though they may not write.
        var comment = await store.ReadAsync<CommentDto>(await CommentAsync(store, "Project", world.Project.Id, [viewerUser.UserId]));
        Assert.Equal("Mention", Assert.Single(await NotificationsAsync(viewer)).Kind);
        Assert.Equal(comment.Id, Assert.Single((await viewer.GetAsync<CommentPageDto>(Thread("Project", world.Project.Id))).Items).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await CommentAsync(viewer, "Project", world.Project.Id)).StatusCode);

        // A bond has no discussion and a kind must be a word the API knows.
        Assert.Equal(["kind"], await ErrorFieldsAsync(await world.Admin.GetAsync(Thread("Bond", Guid.NewGuid()))));
        Assert.Equal(["kind"], await ErrorFieldsAsync(await CommentAsync(world.Admin, "Invoice", world.Project.Id)));
    }

    [SqlServerFact]
    public async Task A_tasks_discussion_is_for_the_people_in_it_and_administrators()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (creator, _) = await AsRoleAsync(_factory, world, AccessRole.Accountant);
        var (assignee, assigneeUser) = await AsRoleAsync(_factory, world, AccessRole.HrOfficer);
        var (other, otherUser) = await AsRoleAsync(_factory, world, AccessRole.ProjectManager);
        var (viewer, _) = await AsRoleAsync(_factory, world, AccessRole.Viewer);
        using var _c = creator;
        using var _s = assignee;
        using var _o = other;
        using var _v = viewer;
        var task = await CreateTaskAsync(creator, assigneeUser.UserId);

        // The assignee writes, mentioning an Administrator (who may read every task); the creator and the Administrator read it.
        var comment = await assignee.ReadAsync<CommentDto>(await CommentAsync(assignee, "Task", task.Id, [world.User.UserId]));
        Assert.Equal(comment.Id, Assert.Single((await creator.GetAsync<CommentPageDto>(Thread("Task", task.Id))).Items).Id);
        Assert.Equal(1, (await world.Admin.GetAsync<CommentPageDto>(Thread("Task", task.Id))).Total);

        // Someone outside the task cannot learn it or its discussion exists, and cannot be mentioned in it.
        foreach (var outsider in new[] { other, viewer })
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(Thread("Task", task.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CommentAsync(other, "Task", task.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveAsync(other, comment)).StatusCode);
        Assert.Equal(["mentionUserIds"], await ErrorFieldsAsync(await CommentAsync(creator, "Task", task.Id, [otherUser.UserId])));

        // The mention picker for the task offers only its people and Administrators.
        var offered = await creator.GetAsync<List<ColleagueDto>>($"/api/users/lookup?targetKind=Task&targetId={task.Id}");
        Assert.DoesNotContain(offered, person => person.Id == otherUser.UserId);
        Assert.Contains(offered, person => person.Id == assigneeUser.UserId);
        Assert.Contains(offered, person => person.Id == world.User.UserId);
    }

    [SqlServerFact]
    public async Task A_comment_is_removed_by_its_author_or_an_administrator_with_a_reason_and_never_edited()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (author, _) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        var (colleague, _) = await AsRoleAsync(_factory, world, AccessRole.ProjectManager);
        using var _au = author;
        using var _co = colleague;
        var kind = "PurchaseOrder";
        var comment = await author.ReadAsync<CommentDto>(await CommentAsync(author, kind, world.Order.Id, [world.User.UserId]));

        // Someone else who reads it may not remove it; a reason is required; a stale version is a conflict.
        Assert.False(Assert.Single((await colleague.GetAsync<CommentPageDto>(Thread(kind, world.Order.Id))).Items).CanRemove);
        var notAuthor = await RemoveAsync(colleague, comment);
        Assert.Equal("Only the person who wrote this comment or an Administrator can remove it.", await DetailAsync(notAuthor));
        Assert.Equal(["reason"], await ErrorFieldsAsync(await RemoveAsync(author, comment, " ")));
        var stale = await RemoveAsync(author, comment, version: comment.Version + 1);
        Assert.Equal((HttpStatusCode.Conflict, "concurrency_conflict"), (stale.StatusCode, await RuleCodeAsync(stale)));

        var removed = await world.Admin.ReadAsync<CommentDto>(await RemoveAsync(world.Admin, comment));
        Assert.Equal((true, (string?)null, "Test Admin", false), (removed.IsVoided, removed.Text, removed.VoidedByName, removed.CanRemove));
        Assert.Empty(removed.Mentions);
        var thread = await author.GetAsync<CommentPageDto>(Thread(kind, world.Order.Id));
        var shown = Assert.Single(thread.Items);
        Assert.Equal((true, (string?)null), (shown.IsVoided, shown.Text));
        Assert.Equal(HttpStatusCode.Conflict, (await RemoveAsync(author, removed)).StatusCode);

        // There is no way to edit or delete a comment.
        HttpStatusCode[] noSuchRoute = [HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed];
        Assert.Contains((await author.PutAsync($"/api/comments/{comment.Id}", new { text = "edited" })).StatusCode, noSuchRoute);
        Assert.Contains((await author.SendAsync(HttpMethod.Delete, $"/api/comments/{comment.Id}")).StatusCode, noSuchRoute);
    }

    [SqlServerFact]
    public async Task A_repeated_request_returns_the_first_comment_and_an_archived_project_is_read_only()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (engineer, engineerUser) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        using var _e = engineer;

        var body = NewComment("Project", world.Project.Id, mentions: [engineerUser.UserId], clientRequestId: "comment-1");
        var first = await world.Admin.ReadAsync<CommentDto>(await world.Admin.PostAsync("/api/comments", body));
        var again = await world.Admin.ReadAsync<CommentDto>(await world.Admin.PostAsync("/api/comments", body));
        Assert.Equal(first.Id, again.Id);
        Assert.Single(await NotificationsAsync(engineer));
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsync("/api/comments", body)).StatusCode);

        await ArchiveAsync(world.Admin, await world.Admin.GetAsync<ProjectDto>($"/api/projects/{world.Project.Id}"));
        foreach (var (kind, id) in new[] { ("Project", world.Project.Id), ("PurchaseOrder", world.Order.Id), ("Subcontract", world.Subcontract.Id) })
        {
            var refused = await CommentAsync(world.Admin, kind, id);
            Assert.Equal((HttpStatusCode.Conflict, "This project is archived, so its discussion is read-only. Restore the project to comment."),
                (refused.StatusCode, await DetailAsync(refused)));
            var page = await world.Admin.GetAsync<CommentPageDto>(Thread(kind, id));
            Assert.Equal((false, CommentService.ArchivedReason), (page.CanWrite, page.ReadOnlyReason));
        }
        Assert.False(Assert.Single((await world.Admin.GetAsync<CommentPageDto>(Thread("Project", world.Project.Id))).Items).CanRemove);
        Assert.Equal(HttpStatusCode.Conflict, (await RemoveAsync(world.Admin, first)).StatusCode);
    }

    [SqlServerFact]
    public async Task The_lookup_returns_only_ids_and_names_of_active_colleagues_who_could_take_part()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (store, storeUser) = await AsRoleAsync(_factory, world, AccessRole.WarehouseKeeper);
        var (_, viewerUser) = await AsRoleAsync(_factory, world, AccessRole.Viewer);
        var (_, engineerUser) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        var (_, leftUser) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        using var _s = store;
        Assert.Equal(HttpStatusCode.OK, (await world.Admin.PostAsync($"/api/users/{leftUser.UserId}/deactivate")).StatusCode);

        var response = await store.GetAsync("/api/users/lookup?search=Test");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(["displayName", "id"], PropertyNames(json).Distinct().Order());

        // With no record: the people who can be given a task (a Viewer can't; someone who left is not offered).
        var everyone = json.Deserialize<List<ColleagueDto>>(ApiClient.Json)!.Select(person => person.Id).ToHashSet();
        Assert.Superset(new HashSet<Guid> { world.User.UserId, storeUser.UserId, engineerUser.UserId }, everyone);
        Assert.DoesNotContain(viewerUser.UserId, everyone);
        Assert.DoesNotContain(leftUser.UserId, everyone);

        // With a purchase order: only those who could read it, a Viewer included, a warehouse keeper not.
        var forOrder = (await world.Admin.GetAsync<List<ColleagueDto>>($"/api/users/lookup?targetKind=PurchaseOrder&targetId={world.Order.Id}"))
            .Select(person => person.Id).ToHashSet();
        Assert.Contains(viewerUser.UserId, forOrder);
        Assert.Contains(engineerUser.UserId, forOrder);
        Assert.DoesNotContain(storeUser.UserId, forOrder);

        // A Viewer can't look anyone up.
        var (viewer, _) = await AsRoleAsync(_factory, world, AccessRole.Viewer);
        using var _v = viewer;
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/users/lookup")).StatusCode);
    }

    [SqlServerFact]
    public async Task The_discussion_costs_a_fixed_number_of_statements_however_many_comments_exist()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (engineer, engineerUser) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        using var _e = engineer;
        var url = Thread("PurchaseOrder", world.Order.Id, 100);
        await world.Admin.ReadAsync<CommentDto>(await CommentAsync(world.Admin, "PurchaseOrder", world.Order.Id, [engineerUser.UserId]));

        var withOne = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync(url)).StatusCode));
        for (var index = 0; index < 9; index++)
            await world.Admin.ReadAsync<CommentDto>(await CommentAsync(world.Admin, "PurchaseOrder", world.Order.Id, [engineerUser.UserId]));
        var withTen = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync(url)).StatusCode));

        var page = await world.Admin.GetAsync<CommentPageDto>(url);
        Assert.Equal(10, page.Total);
        Assert.All(page.Items, item => Assert.Equal(engineerUser.UserId, Assert.Single(item.Mentions).Id));
        // The record, the count, the page and the page's mentions.
        Assert.Equal((4, 4), (withOne, withTen));
    }
}
