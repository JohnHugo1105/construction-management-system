using System.Globalization;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.SiteDiaryTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>The "Site diary not written yesterday" group of "Needs your action": which projects it lists, who receives it, and that it costs a fixed number of statements.</summary>
public sealed class DiaryNeedsActionApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const string Key = "diary-not-written";

    private readonly ConstructOpxApiFactory _factory;

    public DiaryNeedsActionApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private static string Yesterday() => Today().AddDays(-1).ToString("ddd d MMM", CultureInfo.InvariantCulture);

    private static async Task<ProjectDto> ActiveProjectAsync(ApiClient client, string name)
    {
        var project = await CreateProjectAsync(client, name);
        return await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });
    }

    [SqlServerFact]
    public async Task Only_active_projects_that_report_regularly_and_missed_yesterday_are_listed_with_no_amount()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;

        // Reported two and three days ago, nothing yesterday: listed.
        var missed = await ActiveProjectAsync(admin, "Missed yesterday");
        await CreateAsync(admin, missed.Id, DaysFromToday(-3));
        await CreateAsync(admin, missed.Id, DaysFromToday(-2));
        // The oldest report that still counts (seven days before yesterday): listed. One day older: not.
        var edge = await ActiveProjectAsync(admin, "Edge of the week");
        await CreateAsync(admin, edge.Id, DaysFromToday(-8));
        var tooOld = await ActiveProjectAsync(admin, "Too long ago");
        await CreateAsync(admin, tooOld.Id, DaysFromToday(-9));
        // Reported yesterday: not listed. Never reported: not listed.
        var reported = await ActiveProjectAsync(admin, "Reported yesterday");
        await CreateAsync(admin, reported.Id, DaysFromToday(-2));
        await CreateAsync(admin, reported.Id, DaysFromToday(-1));
        await ActiveProjectAsync(admin, "Never kept a diary");
        // A voided report does not count as written, nor as a sign the project keeps a diary.
        var onlyVoided = await ActiveProjectAsync(admin, "Only a voided report");
        var voided = await CreateAsync(admin, onlyVoided.Id, DaysFromToday(-3));
        await VoidAsync(admin, voided);
        var voidedYesterday = await ActiveProjectAsync(admin, "Yesterday voided");
        await CreateAsync(admin, voidedYesterday.Id, DaysFromToday(-3));
        await VoidAsync(admin, await CreateAsync(admin, voidedYesterday.Id, DaysFromToday(-1)));
        // A project that is on hold, one that is only planned, and an archived one are not listed.
        var held = await ActiveProjectAsync(admin, "On hold");
        await CreateAsync(admin, held.Id, DaysFromToday(-3));
        await admin.PostAsync<ProjectDto>($"/api/projects/{held.Id}/hold", new { version = (await admin.GetAsync<ProjectDto>($"/api/projects/{held.Id}")).Version });
        var planned = await CreateProjectAsync(admin, "Still planned");
        await CreateAsync(admin, planned.Id, DaysFromToday(-3));
        var archived = await ActiveProjectAsync(admin, "Archived");
        await CreateAsync(admin, archived.Id, DaysFromToday(-3));
        await ArchiveAsync(admin, await admin.GetAsync<ProjectDto>($"/api/projects/{archived.Id}"));
        // Another company's project in the same state is never listed here.
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var theirs = await ActiveProjectAsync(stranger, "Their project");
        await CreateAsync(stranger, theirs.Id, DaysFromToday(-3));

        var group = (await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key);

        // A voided report for yesterday is not a report: that project is listed too.
        Assert.Equal(3, group.Count);
        Assert.Equal(new[] { "Edge of the week", "Missed yesterday", "Yesterday voided" }, group.Rows.Select(row => row.Label).ToArray());
        Assert.Equal(new[] { edge.Id, missed.Id, voidedYesterday.Id }, group.Rows.Select(row => row.Id).ToArray());
        Assert.All(group.Rows, row =>
        {
            Assert.Equal($"No report for {Yesterday()}", row.Detail);
            Assert.Equal(row.Id, row.ProjectId);
            Assert.Equal(row.Label, row.ProjectName);
            Assert.Null(row.AmountMinor);
            Assert.False(row.IsUrgent);
        });

        // The raw group carries no amount property at all, and the count route counts it.
        using var raw = JsonDocument.Parse(await (await admin.GetAsync("/api/needs-action")).Content.ReadAsStringAsync());
        var rawGroup = raw.RootElement.GetProperty("groups").EnumerateArray().Single(item => item.GetProperty("key").GetString() == Key);
        Assert.All(rawGroup.GetProperty("rows").EnumerateArray(), row => Assert.False(row.TryGetProperty("amountMinor", out _)));
        var total = (await admin.GetAsync<NeedsActionCountDto>("/api/needs-action/count")).Total;
        Assert.Equal((await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Sum(item => item.Count), total);
        _ = user;
    }

    [SqlServerFact]
    public async Task Writing_yesterdays_report_takes_the_project_off_the_list_and_a_company_holiday_empties_it()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await ActiveProjectAsync(admin, "Riverside");
        await CreateAsync(admin, project.Id, DaysFromToday(-2));
        Assert.Equal(1, (await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key).Count);

        // Yesterday is a company holiday: nobody is expected to report it.
        await admin.PostAsync("/api/payroll-holidays", new { date = DaysFromToday(-1), name = "Founders day", type = "Special" });
        var holiday = (await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key);
        Assert.Equal((0, 0), (holiday.Count, holiday.Rows.Count));
    }

    [SqlServerFact]
    public async Task Writing_the_missing_report_removes_the_row()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await ActiveProjectAsync(admin, "Riverside");
        await CreateAsync(admin, project.Id, DaysFromToday(-2));
        var before = (await admin.GetAsync<NeedsActionCountDto>("/api/needs-action/count")).Total;
        Assert.Equal(1, (await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key).Count);

        await CreateAsync(admin, project.Id, DaysFromToday(-1));

        Assert.Equal(before - 1, (await admin.GetAsync<NeedsActionCountDto>("/api/needs-action/count")).Total);
        var group = (await admin.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key);
        Assert.Equal((0, 0), (group.Count, group.Rows.Count));
    }

    [SqlServerFact]
    public async Task Only_people_who_write_reports_receive_the_group()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await ActiveProjectAsync(admin, "Riverside");
        await CreateAsync(admin, project.Id, DaysFromToday(-2));

        foreach (var role in Enum.GetValues<AccessRole>().Where(role => role != AccessRole.None))
        {
            var (client, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _c = client;
            var groups = (await client.GetAsync<NeedsActionDto>("/api/needs-action")).Groups;
            var receives = role is AccessRole.Administrator or AccessRole.ProjectManager or AccessRole.CostEngineer;

            Assert.Equal((role, receives), (role, groups.Any(group => group.Key == Key)));
            if (receives) Assert.Equal((role, 1), (role, groups.Single(group => group.Key == Key).Count));
        }
    }

    [SqlServerFact]
    public async Task The_group_costs_a_fixed_number_of_statements_however_many_projects_are_listed()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (engineer, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, user.TenantId);
        using var _e = engineer;
        var first = await ActiveProjectAsync(admin, "Project 0");
        await CreateAsync(admin, first.Id, DaysFromToday(-2));

        async Task<(int Full, int Count)> MeasureAsync() =>
        (
            await StatementsAsync(_factory, async () => await engineer.GetAsync<NeedsActionDto>("/api/needs-action")),
            await StatementsAsync(_factory, async () => await engineer.GetAsync<NeedsActionCountDto>("/api/needs-action/count"))
        );

        var one = await MeasureAsync();
        for (var index = 1; index <= 9; index++)
        {
            var project = await ActiveProjectAsync(admin, $"Project {index}");
            await CreateAsync(admin, project.Id, DaysFromToday(-2));
        }
        var ten = await MeasureAsync();

        Assert.Equal(one, ten);
        var group = (await engineer.GetAsync<NeedsActionDto>("/api/needs-action")).Groups.Single(item => item.Key == Key);
        Assert.Equal((10, 8), (group.Count, group.Rows.Count)); // the true count, and the eight rows the page shows
    }
}
