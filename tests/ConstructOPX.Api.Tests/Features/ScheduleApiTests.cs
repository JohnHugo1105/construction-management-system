using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Schedule;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The project schedule on the real API and a real SQL Server: activities, links, the baseline, who may do what, another company, an archived
/// project, the S-curve, and where the schedule's planned value lands on the Progress tab. Dates are relative to the company's today. Lines have no
/// VAT, so net equals gross. The worked project runs from 60 days ago to 40 days ahead (100 days).
/// </summary>
public sealed class ScheduleApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public ScheduleApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    internal static DateOnly Today => BondTestKit.Today();

    internal static string Day(int fromToday) => BondTestKit.DaysFromToday(fromToday);

    internal static Task<ProjectDto> NewProjectAsync(ApiClient client, int startDays = -60, int endDays = 40) =>
        client.PostAsync<ProjectDto>("/api/projects", new
        {
            name = TenantSeeder.Unique("Schedule"), client = "ACME Realty", location = "Cebu City", contractPriceMinor = 3_000_000L,
            startDate = Day(startDays), targetEndDate = Day(endDays)
        });

    internal static Task<BudgetLineDto> AddLine(ApiClient client, Guid projectId, string description, long amount, string? wbsCode = "3100") =>
        client.PostAsync<BudgetLineDto>($"/api/projects/{projectId}/budget-lines", Line(description, unitCostMinor: amount, wbsCode: wbsCode));

    internal static async Task RecordProgressAsync(ApiClient client, Guid projectId, int daysFromToday, params (Guid Line, int Percent)[] lines)
    {
        var response = await client.PostAsync($"/api/projects/{projectId}/progress", new
        {
            asOfDate = Day(daysFromToday),
            lines = lines.Select(line => new { budgetLineId = line.Line, percentBps = line.Percent, note = (string?)null }).ToArray(),
            clientRequestId = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    internal static object Activity(string name, int startDays, int finishDays, Guid[] lines, string? requestId = null) =>
        new { name, plannedStart = Day(startDays), plannedFinish = Day(finishDays), budgetLineIds = lines, clientRequestId = requestId };

    internal static Task<HttpResponseMessage> CreateAsync(ApiClient client, Guid projectId, object body) =>
        client.PostAsync($"/api/projects/{projectId}/schedule/activities", body);

    internal static async Task<ScheduleDto> CreateOkAsync(ApiClient client, Guid projectId, object body)
    {
        var response = await CreateAsync(client, projectId, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadAsync<ScheduleDto>(response);
    }

    internal static Task<ScheduleDto> ReadAsync(ApiClient client, Guid projectId) => client.GetAsync<ScheduleDto>($"/api/projects/{projectId}/schedule");

    private static Task<HttpResponseMessage> UpdateAsync(ApiClient client, Guid projectId, ScheduleActivityDto activity, int startDays, int finishDays,
        Guid[]? lines = null, string? name = null, int? version = null) =>
        client.PutAsync($"/api/projects/{projectId}/schedule/activities/{activity.Id}", new
        {
            name = name ?? activity.Name, plannedStart = Day(startDays), plannedFinish = Day(finishDays), budgetLineIds = lines ?? activity.BudgetLineIds,
            sortOrder = (int?)null, version = version ?? activity.Version
        });

    private static Task<HttpResponseMessage> BaselineAsync(ApiClient client, Guid projectId, ScheduleDto seen) =>
        client.PostAsync($"/api/projects/{projectId}/schedule/baseline",
            new { activities = seen.Activities.Select(activity => new { id = activity.Id, version = activity.Version }).ToArray() });

    /// <summary>
    /// Footings 1,000,003 and Columns 250,001 (division 3), Site office 200,000 (unclassified). "Substructure" runs from 10 days ago to 9 days
    /// ahead (20 days) on Footings; "Superstructure" from 10 to 29 days ahead on Columns. Footings is 50% done. 400,000 spent.
    /// </summary>
    private async Task<(ProjectDto Project, BudgetLineDto Footings, BudgetLineDto Columns, BudgetLineDto Office)> WorkedProjectAsync(ApiClient client)
    {
        var project = await NewProjectAsync(client);
        var footings = await AddLine(client, project.Id, "Footings", 1_000_003);
        var columns = await AddLine(client, project.Id, "Columns", 250_001);
        var office = await AddLine(client, project.Id, "Site office", 200_000, wbsCode: null);
        await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("Spent", unitCostMinor: 400_000));
        await RecordProgressAsync(client, project.Id, -1, (footings.Id, 5_000));
        return (project, footings, columns, office);
    }

    private static Task<ScheduleDto> AddWorkedScheduleAsync(ApiClient client, Guid projectId, BudgetLineDto footings, BudgetLineDto columns) =>
        CreateOkAsync(client, projectId, Activity("Substructure", -10, 9, [footings.Id]))
            .ContinueWith(_ => CreateOkAsync(client, projectId, Activity("Superstructure", 10, 29, [columns.Id]))).Unwrap();

    // ---- Round trips ------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task An_activity_is_weighted_by_its_lines_and_plans_its_weight_over_its_days()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, office) = await WorkedProjectAsync(client);

        var schedule = await AddWorkedScheduleAsync(client, project.Id, footings, columns);

        Assert.Equal(Today, schedule.Today);
        Assert.True(schedule.UsesSchedule);
        Assert.Equal(["Substructure", "Superstructure"], schedule.Activities.Select(activity => activity.Name));
        var sub = schedule.Activities[0];
        Assert.Equal((1_000_003L, 20, 5_000, false, false, 0), (sub.WeightMinor, sub.PlannedDays, sub.PercentCompleteBps!.Value, sub.IsBehind,
            sub.NeedsLines, sub.SortOrder));
        Assert.Equal([footings.Id], sub.BudgetLineIds);
        Assert.Equal((250_001L, 0, 1), (schedule.Activities[1].WeightMinor, schedule.Activities[1].PercentCompleteBps!.Value, schedule.Activities[1].SortOrder));
        // 11 of Substructure's 20 days are gone by the end of today: 1,000,003 x 11 / 20 = 550,001.65.
        Assert.Equal(550_002, schedule.PlannedMinor);
        Assert.Null(schedule.BaselinePlannedMinor);
        Assert.Equal((1_250_004L, 1_450_004L, 200_000L, 1), (schedule.PlanWeightMinor, schedule.BudgetMinor, schedule.UnplannedBudgetMinor,
            schedule.UnplannedLineCount));
        Assert.Equal([sub.Id, schedule.Activities[1].Id, (Guid?)null],
            new[] { footings.Id, columns.Id, office.Id }.Select(id => schedule.BudgetLines.Single(line => line.Id == id).ActivityId));
        Assert.Equal("Concrete", schedule.BudgetLines.Single(line => line.Id == footings.Id).DivisionName);

        // An activity with no lines is listed and flagged, and weighs nothing.
        var flagged = await CreateOkAsync(client, project.Id, Activity("Mobilisation", -60, -55, []));
        var mobilisation = flagged.Activities.Single(activity => activity.Name == "Mobilisation");
        Assert.Equal((true, 0L, (int?)null, false), (mobilisation.NeedsLines, mobilisation.WeightMinor, mobilisation.PercentCompleteBps, mobilisation.IsBehind));
        Assert.Equal(550_002, flagged.PlannedMinor);
    }

    [SqlServerFact]
    public async Task A_budget_line_already_in_an_activity_is_refused_naming_it_and_a_repeated_request_adds_nothing()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        await CreateOkAsync(client, project.Id, Activity("Substructure", -10, 9, [footings.Id], requestId: "sched-1"));

        var twice = await CreateAsync(client, project.Id, Activity("Footing pours", 0, 5, [columns.Id, footings.Id]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, twice.StatusCode);
        Assert.Equal(
            "\"Footings\" is already in the activity \"Substructure\". A budget line can be in one activity only: take it out of that activity first.",
            await ErrorMessageAsync(twice, "budgetLineIds[1]"));

        var again = await CreateOkAsync(client, project.Id, Activity("Substructure", -10, 9, [footings.Id], requestId: "sched-1"));
        Assert.Single(again.Activities);

        var invalid = await CreateAsync(client, project.Id, new { name = "", plannedStart = Day(5), plannedFinish = Day(1), budgetLineIds = new[] { Guid.NewGuid() } });
        Assert.Equal(["budgetLineIds[0]", "name", "plannedFinish"], (await ErrorFieldsAsync(invalid)).Order());
        Assert.Equal("The planned finish can't be before the planned start.", await ErrorMessageAsync(invalid, "plannedFinish"));
    }

    [SqlServerFact]
    public async Task Changing_and_removing_an_activity_round_trip_and_a_stale_version_is_a_409()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var created = await CreateOkAsync(client, project.Id, Activity("Substructure", -10, 9, [footings.Id]));
        var activity = created.Activities.Single();

        var changed = await client.ReadAsync<ScheduleDto>(await UpdateAsync(client, project.Id, activity, -10, 19, [footings.Id, columns.Id], "Structure"));
        var after = changed.Activities.Single();
        Assert.Equal(("Structure", Today.AddDays(19), 1_250_004L, activity.Version + 1), (after.Name, after.PlannedFinish, after.WeightMinor, after.Version));

        var stale = await UpdateAsync(client, project.Id, activity, -10, 29);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));

        var noReason = await client.PostAsync($"/api/projects/{project.Id}/schedule/activities/{after.Id}/remove", new { reason = " ", version = after.Version });
        Assert.Equal("Enter the reason for removing it.", await ErrorMessageAsync(noReason, "reason"));
        var removed = await client.PostAsync<ScheduleDto>($"/api/projects/{project.Id}/schedule/activities/{after.Id}/remove",
            new { reason = "Re-planned with the contractor", version = after.Version });
        Assert.Empty(removed.Activities);
        Assert.False(removed.UsesSchedule);
        Assert.All(removed.BudgetLines, line => Assert.Null(line.ActivityId));

        // Its lines are free again, and the removal is in the audit trail without the reason.
        await CreateOkAsync(client, project.Id, Activity("Footings again", 0, 4, [footings.Id]));
        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=ScheduleActivity&entityId={after.Id}");
        Assert.Equal(["Schedule.ActivityRemoved", "Schedule.ActivityChanged", "Schedule.ActivityAdded"], trail.Items.Select(entry => entry.Action));
        Assert.DoesNotContain("contractor", trail.Items[0].Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(user.Username, trail.Items[0].UserName);
    }

    [SqlServerFact]
    public async Task Setting_the_baseline_shows_slip_is_repeatable_and_refuses_a_schedule_that_changed_since_it_was_seen()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var seen = await AddWorkedScheduleAsync(client, project.Id, footings, columns);

        var noActivities = await NewProjectAsync(client);
        Assert.Equal("Add an activity to the schedule before setting the baseline.",
            await DetailAsync(await BaselineAsync(client, noActivities.Id, await ReadAsync(client, noActivities.Id))));

        var based = await client.ReadAsync<ScheduleDto>(await BaselineAsync(client, project.Id, seen));
        Assert.NotNull(based.BaselineSetAtUtc);
        Assert.Equal(based.PlannedMinor, based.BaselinePlannedMinor);
        Assert.All(based.Activities, activity => Assert.Equal((0, activity.PlannedFinish), (activity.SlipDays!.Value, activity.BaselineFinish!.Value)));

        // Substructure slips five days later: 11 of its 25 days are gone (440,001.32), against 550,001.65 in the baseline.
        var sub = based.Activities[0];
        var slipped = await client.ReadAsync<ScheduleDto>(await UpdateAsync(client, project.Id, sub, -10, 14));
        Assert.Equal((5, Today.AddDays(9)), (slipped.Activities[0].SlipDays!.Value, slipped.Activities[0].BaselineFinish!.Value));
        Assert.Equal((440_001L, 550_002L), (slipped.PlannedMinor, slipped.BaselinePlannedMinor!.Value));

        // The baseline as first seen is stale now (Substructure has a newer version): refused, nothing changes.
        var stale = await BaselineAsync(client, project.Id, based);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(5, (await ReadAsync(client, project.Id)).Activities[0].SlipDays);

        // Setting it again from what is on screen makes today's plan the baseline.
        var again = await client.ReadAsync<ScheduleDto>(await BaselineAsync(client, project.Id, slipped));
        Assert.Equal(0, again.Activities[0].SlipDays);
        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=Project&entityId={project.Id}&pageSize=50");
        Assert.Equal(2, trail.Items.Count(entry => entry.Action == "Schedule.BaselineSet"));
    }

    [SqlServerFact]
    public async Task An_archived_projects_schedule_can_be_read_but_not_changed()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var seen = await AddWorkedScheduleAsync(client, project.Id, footings, columns);
        await ArchiveAsync(client, await client.GetAsync<ProjectDto>($"/api/projects/{project.Id}"));

        var read = await ReadAsync(client, project.Id);
        Assert.True(read.ProjectArchived);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/schedule/s-curve")).StatusCode);
        foreach (var refused in new[]
                 {
                     await CreateAsync(client, project.Id, Activity("More", 0, 3, [])),
                     await UpdateAsync(client, project.Id, read.Activities[0], -10, 12),
                     await client.PostAsync($"/api/projects/{project.Id}/schedule/activities/{read.Activities[0].Id}/remove",
                         new { reason = "No longer needed", version = read.Activities[0].Version }),
                     await BaselineAsync(client, project.Id, seen)
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("This project is archived. Restore it before making changes.", await DetailAsync(refused));
        }
    }

    // ---- Who may ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Roles_decide_who_reads_the_schedule_and_who_plans_it()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, footings, columns, _) = await WorkedProjectAsync(admin);
        var seen = await AddWorkedScheduleAsync(admin, project.Id, footings, columns);
        var urls = new[] { $"/api/projects/{project.Id}/schedule", $"/api/projects/{project.Id}/schedule/s-curve" };

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            foreach (var url in urls) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(client, project.Id, Activity("X", 0, 1, []))).StatusCode);
        }

        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _v = client;
            foreach (var url in urls) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(client, project.Id, Activity("X", 0, 1, []))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await UpdateAsync(client, project.Id, seen.Activities[0], -10, 12)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/projects/{project.Id}/schedule/activities/{seen.Activities[0].Id}/remove",
                new { reason = "x", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await BaselineAsync(client, project.Id, seen)).StatusCode);
        }

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.CostEngineer })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _w = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(urls[1])).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(client, project.Id, Activity($"By {role}", 0, 1, []))).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Another_company_never_reads_or_plans_on_my_project_and_cannot_link_my_lines()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _a = mine;
        using var _b = theirs;
        var (project, footings, columns, _) = await WorkedProjectAsync(mine);
        var seen = await AddWorkedScheduleAsync(mine, project.Id, footings, columns);
        var activity = seen.Activities[0];

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/schedule/s-curve")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(theirs, project.Id, Activity("Mine now", 0, 1, []))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(theirs, project.Id, activity, 0, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/projects/{project.Id}/schedule/activities/{activity.Id}/remove",
            new { reason = "x", version = activity.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await BaselineAsync(theirs, project.Id, seen)).StatusCode);

        // In their own project my activity id is unknown and my budget line isn't theirs to link.
        var theirProject = await NewProjectAsync(theirs);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(theirs, theirProject.Id, activity, 0, 1, [])).StatusCode);
        var planted = await CreateAsync(theirs, theirProject.Id, Activity("Planted", 0, 1, [footings.Id]));
        Assert.Equal("That budget line isn't part of this project any more. Reload the page and try again.",
            await ErrorMessageAsync(planted, "budgetLineIds[0]"));

        // An activity of another of my projects is not found under this one either.
        var other = await NewProjectAsync(mine);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(mine, other.Id, activity, 0, 1)).StatusCode);
        Assert.Equal(2, (await ReadAsync(mine, project.Id)).Activities.Count);
    }

    // ---- Where the numbers land ---------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Without_a_schedule_the_progress_tab_keeps_the_straight_line_and_with_one_it_uses_the_schedules_planned_value()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var url = $"/api/projects/{project.Id}/progress";

        // No schedule: the project's 100 days, 60 gone. 1,450,004 x 60 / 100 = 870,002.4.
        var before = await client.GetAsync<ProgressDto>(url);
        Assert.Equal("ProjectDates", before.PlannedFrom);
        Assert.Equal((870_002L, 500_002L, -370_000L, 5_747, 6_000), (before.EarnedValue.PlannedMinor!.Value, before.EarnedValue.EarnedMinor,
            before.EarnedValue.ScheduleVarianceMinor!.Value, before.EarnedValue.SpiBps!.Value, before.EarnedValue.PlannedPercentBps!.Value));

        // An activity with no lines is not a schedule yet: nothing moves.
        await CreateOkAsync(client, project.Id, Activity("Mobilisation", -60, -55, []));
        Assert.Equal(before.EarnedValue, (await client.GetAsync<ProgressDto>(url)).EarnedValue);

        var schedule = await AddWorkedScheduleAsync(client, project.Id, footings, columns);
        var after = await client.GetAsync<ProgressDto>(url);
        var ev = after.EarnedValue;
        Assert.Equal("Schedule", after.PlannedFrom);
        Assert.Equal(schedule.PlannedMinor, ev.PlannedMinor);
        Assert.Equal((550_002L, -50_000L, 9_091, 3_793), (ev.PlannedMinor!.Value, ev.ScheduleVarianceMinor!.Value, ev.SpiBps!.Value,
            ev.PlannedPercentBps!.Value));
        // The forecast uses the cost index, never the planned value: nothing about cost or the forecast changed.
        Assert.Equal(before.EarnedValue with
        {
            PlannedMinor = ev.PlannedMinor, ScheduleVarianceMinor = ev.ScheduleVarianceMinor, SpiBps = ev.SpiBps, PlannedPercentBps = ev.PlannedPercentBps
        }, ev);
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(ev.ForecastMinor, dashboard.Projects.Single(row => row.Id == project.Id).ProgressForecast!.EstimateAtCompletionMinor);
    }

    [SqlServerFact]
    public async Task The_s_curves_point_for_today_equals_the_progress_tab()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        await RecordProgressAsync(client, project.Id, -12, (columns.Id, 1_000));
        await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("Spent later", unitCostMinor: 33_333, entryDate: Day(-3)));
        var seen = await AddWorkedScheduleAsync(client, project.Id, footings, columns);
        await BaselineAsync(client, project.Id, seen);

        var curve = await client.GetAsync<ScheduleCurveDto>($"/api/projects/{project.Id}/schedule/s-curve");
        var progress = await client.GetAsync<ProgressDto>($"/api/projects/{project.Id}/progress");

        Assert.Equal((7, true, "IncludingVat"), (curve.StepDays, curve.HasBaseline, curve.Basis));
        Assert.Equal(Today.AddDays(-10), curve.Points[0].Date);
        Assert.True(curve.Points[^1].Date >= Today.AddDays(29));
        var today = curve.Points.Single(point => point.IsToday);
        Assert.Equal(Today, today.Date);
        Assert.Equal((progress.EarnedValue.PlannedMinor!.Value, (long?)progress.EarnedValue.EarnedMinor, (long?)progress.EarnedValue.SpentMinor),
            (today.PlannedMinor, today.EarnedMinor, today.ActualMinor));
        Assert.Equal(today.PlannedMinor, today.BaselinePlannedMinor);
        Assert.Equal(1_250_004, curve.Points[^1].PlannedMinor);
        Assert.All(curve.Points.Where(point => point.Date > Today), point => Assert.Null(point.EarnedMinor));
        Assert.All(curve.Points.Where(point => point.Date <= Today), point => Assert.NotNull(point.ActualMinor));
        // The first point already counts the cost and progress dated before the plan began.
        Assert.Equal((400_000L, 25_000L), (curve.Points[0].ActualMinor!.Value, curve.Points[0].EarnedMinor!.Value));
    }

    // ---- Performance --------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_schedule_the_s_curve_and_the_progress_tab_cost_the_same_statements_with_1_or_10_activities()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var lines = new List<Guid>();
        for (var index = 0; index < 12; index++) lines.Add((await AddLine(client, project.Id, $"Line {index}", 10_000 + index)).Id);
        await RecordProgressAsync(client, project.Id, -20, (lines[0], 1_000));
        await CreateOkAsync(client, project.Id, Activity("One", -30, 30, [lines[0]]));
        var urls = new[]
        {
            $"/api/projects/{project.Id}/schedule", $"/api/projects/{project.Id}/schedule/s-curve", $"/api/projects/{project.Id}/progress"
        };
        var small = new List<int>();
        foreach (var url in urls) small.Add(await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode)));

        for (var index = 1; index < 10; index++) await CreateOkAsync(client, project.Id, Activity($"Activity {index}", -40 + index * 5, index * 7, [lines[index]]));
        for (var day = 19; day >= 1; day--) await RecordProgressAsync(client, project.Id, -day, lines.Take(10).Select(id => (id, 1_000 + (20 - day) * 400)).ToArray());
        for (var index = 0; index < 15; index++)
            await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("More", unitCostMinor: 100 + index, entryDate: Day(-index)));
        var large = new List<int>();
        foreach (var url in urls) large.Add(await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode)));

        Assert.Equal(10, (await ReadAsync(client, project.Id)).Activities.Count);
        Assert.Equal(small, large);
        // The schedule: the project, the company, the activities, the lines with today's percent. The S-curve adds the progress history and the
        // posted cost, each grouped into steps in SQL. The Progress tab: the project, the company, the lines, the cost and the activities.
        Assert.Equal([4, 6, 5], large);
    }
}
