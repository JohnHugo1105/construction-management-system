using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.ScheduleApiTests;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The "Schedule activities past their planned finish" group of "Needs your action": one row per Active project with an activity past its planned
/// finish and below 100%, only for the people who plan the schedule, never with an amount, and a fixed number of statements however many rows.
/// </summary>
public sealed class ScheduleNeedsActionApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const string Key = "activities-behind";

    private readonly ConstructOpxApiFactory _factory;

    public ScheduleNeedsActionApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private static async Task<ProjectDto> ActiveProjectAsync(ApiClient client)
    {
        var project = await NewProjectAsync(client);
        return await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });
    }

    /// <summary>An Active project with one activity that finished 5 days ago while its line is at 40%.</summary>
    private static async Task<ProjectDto> BehindProjectAsync(ApiClient client, int activities = 1)
    {
        var project = await ActiveProjectAsync(client);
        for (var index = 0; index < activities; index++)
        {
            var line = await AddLine(client, project.Id, $"Walls {index}", 100_000);
            await RecordProgressAsync(client, project.Id, -6, (line.Id, 4_000));
            await CreateOkAsync(client, project.Id, Activity($"Walls {index}", -20, -5 - index, [line.Id]));
        }

        return project;
    }

    private static async Task<NeedsActionGroupDto?> GroupAsync(ApiClient client) =>
        (await client.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.SingleOrDefault(group => group.Key == Key);

    [SqlServerFact]
    public async Task Only_active_projects_with_an_open_activity_past_its_finish_are_listed_in_plain_words_without_an_amount()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var behind = await BehindProjectAsync(admin, activities: 2);

        // Not listed: the work is done; no line is linked; it finishes today; the project is still being planned; the project is archived.
        var done = await ActiveProjectAsync(admin);
        var doneLine = await AddLine(admin, done.Id, "Footings", 50_000);
        await RecordProgressAsync(admin, done.Id, -3, (doneLine.Id, 10_000));
        await CreateOkAsync(admin, done.Id, Activity("Footings", -20, -5, [doneLine.Id]));
        var unlinked = await ActiveProjectAsync(admin);
        await CreateOkAsync(admin, unlinked.Id, Activity("Mobilisation", -20, -5, []));
        var today = await ActiveProjectAsync(admin);
        var todayLine = await AddLine(admin, today.Id, "Slab", 50_000);
        await CreateOkAsync(admin, today.Id, Activity("Slab", -20, 0, [todayLine.Id]));
        var planning = await NewProjectAsync(admin);
        var planningLine = await AddLine(admin, planning.Id, "Slab", 50_000);
        await CreateOkAsync(admin, planning.Id, Activity("Slab", -20, -5, [planningLine.Id]));
        var archived = await BehindProjectAsync(admin);
        await ArchiveAsync(admin, await admin.GetAsync<ProjectDto>($"/api/projects/{archived.Id}"));

        var group = await GroupAsync(admin);

        Assert.NotNull(group);
        Assert.Equal(1, group.Count);
        var row = Assert.Single(group.Rows);
        Assert.Equal((behind.Id, behind.Name, behind.Id, behind.Name, "2 activities are past their planned finish", true, Today.AddDays(-6)),
            (row.Id, row.Label, row.ProjectId!.Value, row.ProjectName, row.Detail, row.IsUrgent, row.DueDate!.Value));
        Assert.Null(row.AmountMinor);

        // The JSON row has no amount property at all.
        var json = await admin.GetAsync<JsonElement>("/api/needs-action");
        var raw = json.GetProperty("groups").EnumerateArray().Single(item => item.GetProperty("key").GetString() == Key).GetProperty("rows")[0];
        Assert.False(raw.TryGetProperty("amountMinor", out _));

        // The single-activity wording, and the count route adds the group in.
        var (other, _) = await SignedInAsync(_factory);
        using var _o = other;
        await BehindProjectAsync(other);
        Assert.Equal("1 activity is past its planned finish", Assert.Single((await GroupAsync(other))!.Rows).Detail);
        Assert.Equal((await other.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Sum(item => item.Count),
            (await other.GetAsync<NeedsActionCountDto>("/api/needs-action/count")).Total);
    }

    [SqlServerFact]
    public async Task Only_the_people_who_plan_the_schedule_receive_the_group()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var behind = await BehindProjectAsync(admin);

        foreach (var role in new[] { AccessRole.Administrator, AccessRole.ProjectManager, AccessRole.CostEngineer })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _c = client;
            var group = await GroupAsync(client);
            Assert.NotNull(group);
            Assert.Equal(behind.Id, Assert.Single(group.Rows).Id);
        }

        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer, AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _c = client;
            Assert.Null(await GroupAsync(client));
        }

        // Another company sees none of it.
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        Assert.Equal(0, (await GroupAsync(stranger))!.Count);
    }

    [SqlServerFact]
    public async Task The_group_costs_the_same_statements_whatever_the_number_of_projects_and_activities_behind()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (cost, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, adminUser.TenantId);
        using var _c = cost;
        await BehindProjectAsync(admin);
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await cost.GetAsync("/api/needs-action")).StatusCode));

        for (var index = 0; index < 5; index++) await BehindProjectAsync(admin, activities: 2);
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await cost.GetAsync("/api/needs-action")).StatusCode));

        Assert.Equal(6, (await GroupAsync(cost))!.Count);
        Assert.Equal(small, large);
    }
}
