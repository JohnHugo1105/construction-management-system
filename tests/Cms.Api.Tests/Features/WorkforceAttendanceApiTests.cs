using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.WorkforceTestKit;

namespace Cms.Api.Tests.Features;

public sealed class WorkforceAttendanceApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public WorkforceAttendanceApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Hours_are_recorded_and_the_projects_sheet_shows_them_and_the_crew_still_to_record()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var juan = await AssignAsync(client, await HireAsync(client, "W-001", "Juan", "Dela Cruz"), project);
        var ana = await AssignAsync(client, await HireAsync(client, "W-002", "Ana", "Abad"), project);

        var empty = await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05");
        Assert.Empty(empty.Entries);
        Assert.Equal(["Ana Abad", "Juan Dela Cruz"], empty.CrewWithoutEntry.Select(worker => worker.FullName));

        var entry = await RecordAsync(client, juan.Id, project.Id, "2026-10-05", 8m, 2m);
        Assert.Equal(("Juan Dela Cruz", "Tower A", 8m, 2m, false), (entry.WorkerName, entry.ProjectName, entry.RegularHours, entry.OvertimeHours, entry.IsLocked));

        var sheet = await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05");
        Assert.Equal([entry.Id], sheet.Entries.Select(item => item.Id));
        Assert.Equal((8m, 2m), (sheet.TotalRegularHours, sheet.TotalOvertimeHours));
        Assert.Equal([ana.Id], sheet.CrewWithoutEntry.Select(worker => worker.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/attendance")).StatusCode);
    }

    [SqlServerFact]
    public async Task Invalid_hours_and_dates_are_reported_per_field()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");

        async Task<System.Text.Json.JsonElement> ErrorsFor(string date, decimal regular, decimal overtime)
        {
            var response = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, workDate = date, regularHours = regular, overtimeHours = overtime });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            return (await ApiClient.ProblemAsync(response)).GetProperty("errors");
        }

        Assert.True((await ErrorsFor("2026-10-05", 9m, 0m)).TryGetProperty("regularHours", out _));
        Assert.True((await ErrorsFor("2026-10-05", 0m, 0m)).TryGetProperty("regularHours", out _));
        Assert.True((await ErrorsFor("2026-10-05", 8m, -1m)).TryGetProperty("overtimeHours", out _));
        Assert.True((await ErrorsFor("2026-10-05", 7.555m, 0m)).TryGetProperty("regularHours", out _));
        Assert.True((await ErrorsFor("2026-10-05", 8m, 17m)).TryGetProperty("overtimeHours", out _));
        Assert.True((await ErrorsFor("2999-01-01", 8m, 0m)).TryGetProperty("workDate", out _));
        Assert.True((await ErrorsFor("2025-12-31", 8m, 0m)).TryGetProperty("workDate", out _));
        Assert.True((await ErrorsFor("0001-01-01", 8m, 0m)).TryGetProperty("workDate", out _));

        var unknownWorker = await client.PostAsync("/api/attendance", new { workerId = Guid.NewGuid(), projectId = project.Id, workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.NotFound, unknownWorker.StatusCode);
        var unknownProject = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = Guid.NewGuid(), workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.NotFound, unknownProject.StatusCode);
    }

    [SqlServerFact]
    public async Task A_posting_has_one_entry_per_day_until_it_is_removed_and_sub_projects_are_separate_postings()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects", new { name = "Building 1", startDate = "2026-02-01", version = project.Version });
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects", new { name = "Building 2", startDate = "2026-02-01", version = project.Version });
        var (building1, building2) = (project.SubProjects.Single(sub => sub.Name == "Building 1").Id, project.SubProjects.Single(sub => sub.Name == "Building 2").Id);
        var worker = await HireAsync(client, "W-001");

        var first = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 4m, subProjectId: building1);
        var duplicate = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, subProjectId = building1, workDate = "2026-10-05", regularHours = 4m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        var message = (await ApiClient.ProblemAsync(duplicate)).GetProperty("errors").GetProperty("workerId")[0].GetString()!;
        Assert.Contains("Juan Dela Cruz", message);
        Assert.Contains("5 Oct 2026", message);

        var otherBuilding = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 4m, subProjectId: building2);
        Assert.NotEqual(first.Id, otherBuilding.Id);
        var wholeProject = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, workDate = "2026-10-05", regularHours = 0.5m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.Created, wholeProject.StatusCode);

        await client.PostAsync<AttendanceEntryDto>($"/api/attendance/{first.Id}/void", new { reason = "Entered twice", version = first.Version });
        var again = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 4m, subProjectId: building1);
        Assert.NotEqual(first.Id, again.Id);

        var foreign = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, subProjectId = Guid.NewGuid(), workDate = "2026-10-06", regularHours = 4m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreign.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(foreign)).GetProperty("errors").TryGetProperty("subProjectId", out _));
    }

    [SqlServerFact]
    public async Task A_workers_hours_on_one_day_can_not_pass_24_across_all_projects()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var a = await NewProjectAsync(client, "Tower A");
        var b = await NewProjectAsync(client, "Warehouse B");
        var c = await NewProjectAsync(client, "Clinic C");
        var worker = await HireAsync(client, "W-001");

        // 8 + 8 on Tower A (16 h), 8 on Warehouse B reaches exactly 24 h, and nothing more fits on Clinic C.
        var onA = await RecordAsync(client, worker.Id, a.Id, "2026-10-05", 8m, 8m);
        await RecordAsync(client, worker.Id, b.Id, "2026-10-05", 8m);
        var tooMuch = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = c.Id, workDate = "2026-10-05", regularHours = 0.5m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        var message = (await ApiClient.ProblemAsync(tooMuch)).GetProperty("errors").GetProperty("overtimeHours")[0].GetString()!;
        Assert.Contains("Juan Dela Cruz", message);
        Assert.Contains("5 Oct 2026", message);
        Assert.Contains("24.5", message);

        // The next day is a fresh day.
        await RecordAsync(client, worker.Id, c.Id, "2026-10-06", 8m);

        // A revision doesn't count its own hours twice (other entries hold 8 h, so up to 16 h fits), but can't go beyond.
        var revised = await client.PutAsync<AttendanceEntryDto>($"/api/attendance/{onA.Id}", new { subProjectId = (Guid?)null, regularHours = 8m, overtimeHours = 8m, remarks = "Pour day", version = onA.Version });
        Assert.Equal(8m, revised.OvertimeHours);
        var beyond = await client.PutAsync($"/api/attendance/{onA.Id}", new { subProjectId = (Guid?)null, regularHours = 8m, overtimeHours = 8.5m, remarks = (string?)null, version = revised.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, beyond.StatusCode);
        Assert.Equal(8m, (await client.GetAsync<PagedResult<AttendanceEntryDto>>($"/api/attendance?projectId={a.Id}")).Items.Single().OvertimeHours);
    }

    [SqlServerFact]
    public async Task An_archived_project_takes_no_attendance()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/archive", new { reason = "Finished", note = (string?)null, version = project.Version });

        var single = await client.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m });
        Assert.Equal(HttpStatusCode.Conflict, single.StatusCode);
        var batch = await client.PostAsync("/api/attendance/batch", new
        {
            projectId = project.Id, workDate = "2026-10-05", entries = new[] { new { workerId = worker.Id, regularHours = 8m, overtimeHours = 0m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, batch.StatusCode);
    }

    [SqlServerFact]
    public async Task A_crews_day_is_recorded_all_together_and_a_repeated_submission_changes_nothing()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var ana = await HireAsync(client, "W-002", "Ana", "Abad");
        var requestId = Guid.NewGuid().ToString("N");
        var submission = new
        {
            projectId = project.Id, workDate = "2026-10-05", clientRequestId = requestId,
            entries = new[] { new { workerId = juan.Id, regularHours = 8m, overtimeHours = 1m }, new { workerId = ana.Id, regularHours = 6m, overtimeHours = 0m } }
        };

        var response = await client.PostAsync("/api/attendance/batch", submission);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/projects/{project.Id}/attendance?date=2026-10-05", response.Headers.Location!.ToString());
        var recorded = await client.ReadAsync<List<AttendanceEntryDto>>(response);
        Assert.Equal(["Ana Abad", "Juan Dela Cruz"], recorded.Select(entry => entry.WorkerName));

        var repeated = await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", submission);
        Assert.Equal(recorded.Select(entry => entry.Id).Order(), repeated.Select(entry => entry.Id).Order());
        Assert.Equal(2, (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Total);
    }

    [SqlServerFact]
    public async Task One_bad_line_in_a_crew_submission_records_nobody_and_points_at_the_line()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var other = await NewProjectAsync(client, "Warehouse B");
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var ana = await HireAsync(client, "W-002", "Ana", "Abad");
        var pedro = await HireAsync(client, "W-003", "Pedro", "Reyes");
        await RecordAsync(client, pedro.Id, other.Id, "2026-10-05", 8m, 8m);

        // Line 1 has 9 regular hours; line 2 would put Pedro past 24 hours in the day (16 recorded elsewhere + 9).
        var refused = await client.PostAsync("/api/attendance/batch", new
        {
            projectId = project.Id, workDate = "2026-10-05",
            entries = new[]
            {
                new { workerId = juan.Id, regularHours = 8m, overtimeHours = 0m },
                new { workerId = ana.Id, regularHours = 9m, overtimeHours = 0m },
                new { workerId = pedro.Id, regularHours = 8m, overtimeHours = 1m }
            }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var errors = (await ApiClient.ProblemAsync(refused)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("entries[1].regularHours", out _));
        Assert.True(errors.TryGetProperty("entries[2].overtimeHours", out _));
        Assert.False(errors.TryGetProperty("entries[0].regularHours", out _));
        Assert.Empty((await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05")).Entries);

        var twice = await client.PostAsync("/api/attendance/batch", new
        {
            projectId = project.Id, workDate = "2026-10-05",
            entries = new[] { new { workerId = juan.Id, regularHours = 4m, overtimeHours = 0m }, new { workerId = juan.Id, regularHours = 4m, overtimeHours = 0m } }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, twice.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(twice)).GetProperty("errors").TryGetProperty("entries[1].workerId", out _));

        var empty = await client.PostAsync("/api/attendance/batch", new { projectId = project.Id, workDate = "2026-10-05", entries = Array.Empty<object>() });
        Assert.True((await ApiClient.ProblemAsync(empty)).GetProperty("errors").TryGetProperty("entries", out _));
        var strange = await client.PostAsync("/api/attendance/batch", new
        {
            projectId = project.Id, workDate = "2026-10-05", clientRequestId = new string('r', 41),
            entries = new[] { new { workerId = juan.Id, regularHours = 4m, overtimeHours = 0m } }
        });
        Assert.True((await ApiClient.ProblemAsync(strange)).GetProperty("errors").TryGetProperty("clientRequestId", out _));
    }

    [SqlServerFact]
    public async Task An_entry_is_revised_and_removed_with_a_reason_and_a_stale_version_is_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        var entry = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);

        var revised = await client.PutAsync<AttendanceEntryDto>($"/api/attendance/{entry.Id}", new { subProjectId = (Guid?)null, regularHours = 6m, overtimeHours = 2m, remarks = "Rain delay", version = entry.Version });
        Assert.Equal((6m, 2m, "Rain delay"), (revised.RegularHours, revised.OvertimeHours, revised.Remarks));

        var stale = await client.PutAsync($"/api/attendance/{entry.Id}", new { subProjectId = (Guid?)null, regularHours = 5m, overtimeHours = 0m, remarks = (string?)null, version = entry.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var invalid = await client.PutAsync($"/api/attendance/{entry.Id}", new { subProjectId = (Guid?)null, regularHours = 9m, overtimeHours = 0m, remarks = (string?)null, version = revised.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var noReason = await client.PostAsync($"/api/attendance/{entry.Id}/void", new { reason = " ", version = revised.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(noReason)).GetProperty("errors").TryGetProperty("reason", out _));
        var voided = await client.PostAsync<AttendanceEntryDto>($"/api/attendance/{entry.Id}/void", new { reason = "Worker was absent", version = revised.Version });
        Assert.Equal(("Voided", "Worker was absent"), (voided.Status, voided.VoidReason));

        var edit = await client.PutAsync($"/api/attendance/{entry.Id}", new { subProjectId = (Guid?)null, regularHours = 5m, overtimeHours = 0m, remarks = (string?)null, version = voided.Version });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Empty((await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05")).Entries);
        Assert.Empty((await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Items);
        Assert.Single((await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance?includeVoided=true")).Items);
    }

    [SqlServerFact]
    public async Task The_list_filters_by_worker_project_and_dates_and_pages_newest_first()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var a = await NewProjectAsync(client, "Tower A");
        var b = await NewProjectAsync(client, "Warehouse B");
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var ana = await HireAsync(client, "W-002", "Ana", "Abad");
        foreach (var day in new[] { "2026-10-05", "2026-10-06", "2026-10-07" }) await RecordAsync(client, juan.Id, a.Id, day, 8m);
        await RecordAsync(client, ana.Id, b.Id, "2026-10-06", 8m);

        var all = await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance");
        Assert.Equal(4, all.Total);
        Assert.Equal(new DateOnly(2026, 10, 7), all.Items[0].WorkDate);
        Assert.Equal(3, (await client.GetAsync<PagedResult<AttendanceEntryDto>>($"/api/attendance?workerId={juan.Id}")).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<AttendanceEntryDto>>($"/api/attendance?projectId={b.Id}")).Total);
        Assert.Equal(2, (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance?from=2026-10-06&to=2026-10-06")).Total);
        var page2 = await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance?pageSize=3&page=2");
        Assert.Single(page2.Items);
    }

    [SqlServerFact]
    public async Task Project_managers_and_HR_record_attendance_viewers_and_accountants_only_read_it()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var worker = await HireAsync(admin, "W-001");
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, adminUser.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, adminUser.TenantId);
        var (warehouse, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, adminUser.TenantId);
        using var _m = manager;
        using var _h = hr;
        using var _v = viewer;
        using var _c = accountant;
        using var _w = warehouse;
        object Body(string date) => new { workerId = worker.Id, projectId = project.Id, workDate = date, regularHours = 8m, overtimeHours = 0m };

        var recorded = await manager.PostAsync<AttendanceEntryDto>("/api/attendance", Body("2026-10-05"));
        Assert.Equal(HttpStatusCode.Created, (await hr.PostAsync("/api/attendance", Body("2026-10-06"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/attendance", Body("2026-10-07"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync("/api/attendance", Body("2026-10-07"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/attendance/batch", new { projectId = project.Id, workDate = "2026-10-07", entries = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/attendance/{recorded.Id}/void", new { reason = "x", version = recorded.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsync($"/api/attendance/{recorded.Id}", new { regularHours = 1m, overtimeHours = 0m, version = recorded.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/projects/{project.Id}/attendance?date=2026-10-05")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/api/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync($"/api/projects/{project.Id}/attendance")).StatusCode);
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_attendance()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _a = mine;
        using var _b = theirs;
        var project = await NewProjectAsync(mine);
        var worker = await HireAsync(mine, "W-001");
        var entry = await RecordAsync(mine, worker.Id, project.Id, "2026-10-05", 8m);
        var theirProject = await NewProjectAsync(theirs, "Their Tower");

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/attendance?date=2026-10-05")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync($"/api/attendance/{entry.Id}", new { regularHours = 1m, overtimeHours = 0m, version = entry.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/attendance/{entry.Id}/void", new { reason = "Hijacked", version = entry.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = theirProject.Id, workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/attendance", new { workerId = worker.Id, projectId = project.Id, workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/attendance/batch", new
        {
            projectId = theirProject.Id, workDate = "2026-10-05", entries = new[] { new { workerId = worker.Id, regularHours = 8m, overtimeHours = 0m } }
        })).StatusCode);
        Assert.Empty((await theirs.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Items);
        Assert.Equal(8m, (await mine.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05")).TotalRegularHours);
    }

    [SqlServerFact]
    public async Task A_double_clicked_entry_is_recorded_once()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        var requestId = Guid.NewGuid().ToString("N");

        var first = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m, clientRequestId: requestId);
        var second = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m, clientRequestId: requestId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Total);
    }

    [SqlServerFact]
    public async Task Two_simultaneous_identical_entries_record_the_hours_once_and_one_form_submitted_twice_answers_both_alike()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/workers");
        await second.GetAsync("/api/workers");
        var project = await NewProjectAsync(first);
        var worker = await HireAsync(first, "W-001");

        // No request id: the posting's unique index decides. One entry is recorded; the other is refused.
        object Entry(string date, string? requestId) => new { workerId = worker.Id, projectId = project.Id, workDate = date, regularHours = 8m, overtimeHours = 0m, clientRequestId = requestId };
        var racing = await Task.WhenAll(first.PostAsync("/api/attendance", Entry("2026-10-05", null)), second.PostAsync("/api/attendance", Entry("2026-10-05", null)));
        Assert.Single(racing, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(racing, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, (await first.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Total);

        // The same form submitted twice at once: both get the one entry.
        var requestId = Guid.NewGuid().ToString("N");
        var same = await Task.WhenAll(first.PostAsync("/api/attendance", Entry("2026-10-06", requestId)), second.PostAsync("/api/attendance", Entry("2026-10-06", requestId)));
        Assert.All(same, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single((await Task.WhenAll(same.Select(async response => (await first.ReadAsync<AttendanceEntryDto>(response)).Id))).Distinct());
        Assert.Equal(2, (await first.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Total);
    }

    [SqlServerFact]
    public async Task The_day_sheet_and_the_list_cost_the_same_number_of_statements_for_five_entries_as_for_forty()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var workers = new List<WorkerDetailDto>();
        for (var index = 1; index <= 40; index++) workers.Add(await AssignAsync(client, await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}"), project));

        async Task RecordCrewAsync(string date, IEnumerable<WorkerDetailDto> crew) =>
            await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", new
            {
                projectId = project.Id, workDate = date, entries = crew.Select(worker => new { workerId = worker.Id, regularHours = 8m, overtimeHours = 0m }).ToArray()
            });

        await RecordCrewAsync("2026-10-05", workers.Take(5));
        await RecordCrewAsync("2026-10-06", workers);
        var smallSheet = await _factory.Sql.MeasureAsync(async () => Assert.Equal(5, (await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-05")).Entries.Count));
        var largeSheet = await _factory.Sql.MeasureAsync(async () => Assert.Equal(40, (await client.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date=2026-10-06")).Entries.Count));
        var smallList = await _factory.Sql.MeasureAsync(async () => Assert.Equal(5, (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance?from=2026-10-05&to=2026-10-05&pageSize=100")).Items.Count));
        var largeList = await _factory.Sql.MeasureAsync(async () => Assert.Equal(40, (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance?from=2026-10-06&to=2026-10-06&pageSize=100")).Items.Count));

        Assert.Equal(smallSheet, largeSheet);
        Assert.Equal(smallList, largeList);
    }
}
