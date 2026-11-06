using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using Xunit.Abstractions;
using static ConstructOPX.Api.Tests.Features.TimeClockTestKit;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Scans coming in: idempotency, offline replay, the clock bounds, no enumeration, duplicates, directions, imports and a supervisor's scans.</summary>
public sealed class TimeClockPunchApiTests(ConstructOpxApiFactory factory, ITestOutputHelper output) : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory = factory;

    [SqlServerFact]
    public async Task The_same_scan_id_from_the_same_device_is_already_recorded_and_stores_nothing()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);

        var first = await PushAsync(_factory, key, Scan("scan-1", crew[0].Code, at));
        var again = await PushAsync(_factory, key, Scan("scan-1", crew[0].Code, at));
        // The same id twice in one batch: the second is the first one again.
        var twice = await PushAsync(_factory, key, Scan("scan-2", crew[0].Code, at.AddMinutes(-20)), Scan("scan-2", crew[0].Code, at.AddMinutes(-20)));

        Assert.Equal(PunchOutcome.Accepted, first.Results[0].Outcome);
        Assert.Equal((PunchOutcome.AlreadyRecorded, first.Results[0].Direction, first.Results[0].DisplayName),
            (again.Results[0].Outcome, again.Results[0].Direction, again.Results[0].DisplayName));
        Assert.Equal([PunchOutcome.Accepted, PunchOutcome.AlreadyRecorded], twice.Results.Select(item => item.Outcome));
        Assert.Equal(2, StoredPunches(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task A_queue_sent_hours_later_keeps_its_original_times_and_directions_and_sending_it_again_changes_nothing()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 2);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        // Yesterday's scans, queued on a kiosk that was offline all day and sent now, deliberately out of order.
        var queue = new[]
        {
            Scan("q-4", crew[0].Code, At(1, 17, 1)),
            Scan("q-1", crew[0].Code, At(1, 7, 0)),
            Scan("q-2", crew[0].Code, At(1, 12, 0)),
            Scan("q-3", crew[0].Code, At(1, 12, 58)),
            Scan("q-5", crew[1].Code, At(1, 7, 5)),
            Scan("q-6", crew[1].Code, At(1, 16, 5))
        };

        var sent = await PushAsync(_factory, key, queue);
        Assert.All(sent.Results, item => Assert.Equal(PunchOutcome.Accepted, item.Outcome));
        Assert.Equal(["Out", "In", "Out", "In", "In", "Out"], sent.Results.Select(item => item.Direction));
        Assert.Equal(["17:01", "07:00", "12:00", "12:58", "07:05", "16:05"], sent.Results.Select(item => item.LocalTime));
        Assert.Equal(At(1, 7, 0), sent.Results[1].PunchedAt);

        var resent = await PushAsync(_factory, key, queue);
        Assert.All(resent.Results, item => Assert.Equal(PunchOutcome.AlreadyRecorded, item.Outcome));
        Assert.Equal(6, StoredPunches(_factory, user.TenantId));

        var day = await DayAsync(admin, project.Id, Today.AddDays(-1));
        var first = day.Items.Single(row => row.WorkerId == crew[0].Worker.Id);
        // 07:00-12:00 and 12:58-17:01: 9 h 03 worked, the 58-minute gap was the break; to the quarter hour 9 h.
        Assert.Equal(("07:00", "17:01", 58, 8m, 1m, false), (first.FirstInLocal, first.LastOutLocal, first.BreakMinutes, first.RegularHours,
            first.ProposedOvertimeHours, first.IsClean));
        Assert.Empty(first.Exceptions);
    }

    [SqlServerFact]
    public async Task A_machine_clock_more_than_five_minutes_ahead_or_more_than_seven_days_behind_is_refused_in_plain_words()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var now = DateTimeOffset.UtcNow;

        var result = await PushAsync(_factory, key,
            Scan("ahead", crew[0].Code, now.AddMinutes(6)),
            Scan("old", crew[0].Code, now.AddDays(-7).AddMinutes(-5)),
            Scan("ok-ahead", crew[0].Code, now.AddMinutes(4)),
            Scan("ok-old", crew[0].Code, now.AddDays(-6).AddHours(-12)));

        Assert.Equal(["ClockWrong", "ClockWrong", null, null], result.Results.Select(item => item.ReasonCode));
        Assert.Equal("The time on this machine is wrong, so the scan wasn't kept. Ask your supervisor to set the clock.", result.Results[0].Reason);
        Assert.Equal(2, StoredPunches(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task Unknown_replaced_and_inactive_badges_answer_identically_and_the_answer_carries_only_name_direction_and_time()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 3);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var (replaced, resigned) = (crew[1], crew[2]);

        // A lost badge: reissuing gives a new code and the old one stops working at once.
        var reissued = await admin.PostAsync<PunchCodeIssuedDto>($"/api/workers/{replaced.Worker.Id}/punch-code", new { version = replaced.Worker.Version });
        Assert.True(reissued.Reissued);
        Assert.NotEqual(replaced.Code, reissued.Code);
        await admin.PostAsync<Application.Features.Workforce.WorkerDetailDto>($"/api/workers/{resigned.Worker.Id}/status",
            new { status = "Resigned", effectiveDate = Iso(Today), reason = (string?)null, version = resigned.Worker.Version });

        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        var response = await PushRawAsync(_factory, key, new
        {
            punches = new[]
            {
                Scan("u-1", "ZZZZ-ZZZZ-ZZZZ", at),
                Scan("u-2", replaced.Code, at),
                Scan("u-3", resigned.Code, at),
                Scan("u-4", "not a code at all", at),
                Scan("u-5", reissued.Code, at),
                Scan("u-6", crew[0].Code, at)
            }
        });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var results = JsonSerializer.Deserialize<PushPunchesResultDto>(json.GetRawText(), ApiClient.Json)!.Results;

        var refusals = results.Take(4).Select(item => (item.Outcome, item.ReasonCode, item.Reason, item.DisplayName, item.Direction, item.PunchedAt)).Distinct();
        Assert.Equal([(PunchOutcome.Rejected, "BadgeNotActive", "This badge isn't active. See your supervisor.", (string?)null, (string?)null, (DateTimeOffset?)null)],
            refusals);
        Assert.Equal((PunchOutcome.Accepted, $"{replaced.Worker.FirstName} {replaced.Worker.LastName}"), (results[4].Outcome, results[4].DisplayName));
        Assert.Equal((PunchOutcome.Accepted, "Worker01 Crew01", "In"), (results[5].Outcome, results[5].DisplayName, results[5].Direction));
        Assert.Matches(new Regex(@"^\d{2}:\d{2}$"), results[5].LocalTime!);

        // No pay, employee number, code, photo, biometric or place anywhere in the answer, and none of the codes sent is echoed back.
        AssertNothingPrivate(json, "reasonCode");
        foreach (var code in new[] { replaced.Code, reissued.Code, crew[0].Code, resigned.Code })
            Assert.DoesNotContain(code, json.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("W-00", json.GetRawText(), StringComparison.Ordinal);
    }

    [SqlServerFact]
    public async Task A_second_scan_within_sixty_seconds_on_the_same_machine_is_already_recorded_and_the_next_one_goes_the_other_way()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var gate2 = (await RegisterAsync(admin, project.Id, "Gate 2")).Key;
        var at = At(1, 7, 0);

        var result = await PushAsync(_factory, key, Scan("d-1", crew[0].Code, at), Scan("d-2", crew[0].Code, at.AddSeconds(59)),
            Scan("d-3", crew[0].Code, at.AddSeconds(61)));
        Assert.Equal([PunchOutcome.Accepted, PunchOutcome.AlreadyRecorded, PunchOutcome.Accepted], result.Results.Select(item => item.Outcome));
        Assert.Equal(["In", "In", "Out"], result.Results.Select(item => item.Direction));

        // Another machine is another scan.
        var other = await PushAsync(_factory, gate2, Scan("d-1", crew[0].Code, at.AddSeconds(90)));
        Assert.Equal((PunchOutcome.Accepted, "In"), (other.Results[0].Outcome, other.Results[0].Direction));
        Assert.Equal(3, StoredPunches(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task A_worker_not_posted_to_the_project_is_kept_and_flagged_and_explicit_directions_are_kept()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1, post: false);
        var key = (await RegisterAsync(admin, project.Id)).Key;

        var result = await PushAsync(_factory, key, Scan("n-1", crew[0].Code, At(1, 7, 0), "In"), Scan("n-2", crew[0].Code, At(1, 11, 0), "out"));
        Assert.All(result.Results, item => Assert.True(item.NotPostedHere));
        Assert.Equal(["In", "Out"], result.Results.Select(item => item.Direction));

        var row = Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);
        Assert.Equal(["NotPostedHere"], row.Exceptions);
        Assert.True(row.CanConfirm);
        Assert.False(row.IsClean);
    }

    [SqlServerFact]
    public async Task A_night_shift_across_midnight_is_one_work_day_on_the_company_calendar()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;

        // In at 22:00 two days ago, out at 02:00 yesterday (before the 04:00 day-start): one day, dated two days ago.
        await PushAsync(_factory, key, Scan("night-1", crew[0].Code, At(2, 22, 0)), Scan("night-2", crew[0].Code, At(1, 2, 0)));

        var row = Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-2))).Items);
        Assert.Equal((4m, 0m, 4m), (row.RegularHours, row.ProposedOvertimeHours, row.NightHours));
        Assert.Empty((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);

        // 07:30 in Manila is 23:30 UTC the evening before: the work date is the Manila date.
        await PushAsync(_factory, key, Scan("m-1", crew[0].Code, At(1, 7, 30)));
        Assert.NotEqual(Today.AddDays(-1), DateOnly.FromDateTime(At(1, 7, 30).UtcDateTime));
        Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);
    }

    [SqlServerFact]
    public async Task A_night_shift_that_ends_after_the_day_start_is_one_day_and_its_second_scan_is_a_time_out()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var code = crew[0].Code;

        // Two nights of 22:00 to 06:00. 06:00 is after the 04:00 day-start, so by the clock alone it would open a new day as a time in.
        var result = await PushAsync(_factory, key, Scan("a-1", code, At(3, 22, 0)), Scan("a-2", code, At(2, 6, 0)),
            Scan("a-3", code, At(2, 22, 0)), Scan("a-4", code, At(1, 6, 0)));
        Assert.Equal(["In", "Out", "In", "Out"], result.Results.Select(item => item.Direction));

        // Each night is one day, dated when the shift began: 8 hours between the scans, the break once, seven paid hours all worked at night.
        foreach (var night in new[] { Today.AddDays(-3), Today.AddDays(-2) })
        {
            var row = Assert.Single((await DayAsync(admin, project.Id, night)).Items);
            Assert.Equal((7m, 0m, 7m), (row.RegularHours, row.ProposedOvertimeHours, row.NightHours));
            Assert.Empty(row.Exceptions);
            Assert.True(row.IsClean);
        }
        Assert.Empty((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);
    }

    [SqlServerFact]
    public async Task A_supervisors_time_out_that_closes_a_night_shift_is_dated_when_the_shift_began()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        await PushAsync(_factory, key, Scan("m-1", crew[0].Code, At(2, 22, 0)));
        Assert.Equal(["MissingTimeOut"], Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-2))).Items).Exceptions);

        var added = await admin.PostAsync<TimeClockPunchDto>("/api/timeclock/punches/manual", new
        {
            projectId = project.Id,
            workerId = crew[0].Worker.Id,
            at = $"{Iso(Today.AddDays(-1))}T06:00",
            direction = "Out",
            reason = "Badge left in the site office"
        });
        Assert.Equal(("06:00", "Out"), (added.LocalTime, added.Direction));

        var row = Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-2))).Items);
        Assert.Empty(row.Exceptions);
        Assert.Equal((7m, 7m), (row.RegularHours, row.NightHours));
        Assert.Empty((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);
    }

    [SqlServerFact]
    public async Task A_batch_is_bounded_unreadable_scans_are_refused_one_by_one_and_an_archived_project_takes_none()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);

        var tooMany = await PushRawAsync(_factory, key, new { punches = Enumerable.Range(0, 201).Select(index => Scan($"b-{index}", crew[0].Code, at)).ToArray() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PushRawAsync(_factory, key, new { punches = Array.Empty<object>() })).StatusCode);

        var mixed = await PushAsync(_factory, key, Scan("", crew[0].Code, at), Scan(new string('x', 81), crew[0].Code, at),
            new { clientPunchId = "no-time", workerCode = crew[0].Code }, Scan("sideways", crew[0].Code, at, "Sideways"));
        Assert.All(mixed.Results, item => Assert.Equal(("Rejected", "Unreadable"), (item.Outcome, item.ReasonCode)));
        Assert.Equal(0, StoredPunches(_factory, user.TenantId));

        var current = await admin.GetAsync<Application.Features.Projects.ProjectDto>($"/api/projects/{project.Id}");
        await admin.PostAsync<Application.Features.Projects.ProjectDto>($"/api/projects/{project.Id}/archive",
            new { reason = "Finished", note = (string?)null, version = current.Version });
        var closed = await PushAsync(_factory, key, Scan("late", crew[0].Code, at));
        Assert.Equal(("Rejected", "ProjectClosed"), (closed.Results[0].Outcome, closed.Results[0].ReasonCode));
        Assert.Equal(0, StoredPunches(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task A_push_costs_the_same_number_of_statements_for_one_scan_and_for_ten()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 10);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        await PushAsync(_factory, key, Scan("warm", crew[0].Code, At(1, 6, 0)));

        var one = await _factory.Sql.MeasureAsync(() => PushAsync(_factory, key, Scan("one", crew[0].Code, At(1, 7, 0))));
        var ten = await _factory.Sql.MeasureAsync(() => PushAsync(_factory, key,
            crew.Select((member, index) => Scan($"ten-{index}", member.Code, At(1, 8, index))).ToArray()));

        output.WriteLine($"Statements: {one} for one, {ten} for ten.");
        Assert.Equal(one, ten);
        // Device, last seen, company, project, scan ids, workers, the day's scans, postings, and one save.
        Assert.Equal(10, one);
    }

    [SqlServerFact]
    public async Task An_import_answers_every_row_stores_the_good_ones_once_and_never_stores_the_file()
    {
        var (hr, owner) = await SignedInAsync(_factory, AccessRole.Administrator);
        using var _h = hr;
        var (project, crew) = await CrewAsync(hr, 2);
        var day = Iso(Today.AddDays(-1));
        var csv = $"""
            code,timestamp,direction
            {crew[0].Code},{day} 07:00,In
            {crew[0].Code},{day} 07:00:30
            {crew[0].Code},{day} 16:00,
            {crew[1].Code.ToLowerInvariant()},{day}T07:10:00+08:00,auto
            ZZZZZZZZZZZZ,{day} 07:00
            {crew[1].Code},yesterday at seven
            {crew[1].Code},{Iso(Today.AddDays(-9))} 07:00
            """;

        var result = await ImportAsync(hr, project.Id, csv);
        Assert.Equal((7, 3, 1, 3), (result.Rows, result.Accepted, result.AlreadyRecorded, result.Rejected));
        Assert.Equal([6, 7, 8], result.RejectedRows.Select(row => row.Row));
        Assert.Equal("This badge code isn't active. Check the code, or issue the worker a badge.", result.RejectedRows[0].Reason);
        Assert.All(result.RejectedRows, row => Assert.DoesNotContain(crew[1].Code, row.Reason, StringComparison.OrdinalIgnoreCase));

        var again = await ImportAsync(hr, project.Id, csv);
        Assert.Equal((0, 4), (again.Accepted, again.AlreadyRecorded));
        Assert.Equal(3, StoredPunches(_factory, owner.TenantId));

        var row0 = (await DayAsync(hr, project.Id, Today.AddDays(-1))).Items.Single(row => row.WorkerId == crew[0].Worker.Id);
        Assert.Equal((8m, 0m), (row0.RegularHours, row0.ProposedOvertimeHours));
    }

    [SqlServerFact]
    public async Task An_import_is_for_hr_and_administrators_on_their_own_open_projects_and_is_bounded()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var (project, crew) = await CrewAsync(admin, 1);
        var csv = $"{crew[0].Code},{Iso(Today.AddDays(-1))} 07:00";

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.Accountant })
        {
            var (other, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = other;
            Assert.Equal(HttpStatusCode.Forbidden, (await PostCsvAsync(other, project.Id, csv)).StatusCode);
        }
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, owner.TenantId);
        using var _h = hr;
        Assert.Equal(1, (await ImportAsync(hr, project.Id, csv)).Accepted);

        Assert.Equal(HttpStatusCode.NotFound, (await PostCsvAsync(stranger, project.Id, csv)).StatusCode);
        var tooLarge = await PostCsvAsync(admin, project.Id, new string('A', 1_000_001));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLarge.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostCsvAsync(admin, project.Id, "code,timestamp\n")).StatusCode);
    }

    [SqlServerFact]
    public async Task A_supervisor_adds_a_missing_scan_with_a_reason_and_removes_a_wrong_one_until_the_day_is_recorded()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var yesterday = Today.AddDays(-1);
        await PushAsync(_factory, key, Scan("s-1", crew[0].Code, At(1, 7, 0)), Scan("s-2", crew[0].Code, At(1, 9, 0)));
        // 07:00 In, 09:00 Out: the second scan was a mistake (the worker came back in); the real time out was never scanned.
        var (pm, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, owner.TenantId);
        using var _p = pm;

        var refused = await pm.PostAsync("/api/timeclock/punches/manual",
            new { projectId = project.Id, workerId = crew[0].Worker.Id, at = $"{Iso(yesterday)}T16:00", direction = (string?)null, reason = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var fields = (await ApiClient.ProblemAsync(refused)).GetProperty("errors");
        Assert.True(fields.TryGetProperty("direction", out _) && fields.TryGetProperty("reason", out _));

        var request = new { projectId = project.Id, workerId = crew[0].Worker.Id, at = $"{Iso(yesterday)}T16:00", direction = "Out",
            reason = "Badge left in the site office", clientRequestId = "manual-1" };
        var added = await pm.PostAsync<TimeClockPunchDto>("/api/timeclock/punches/manual", request);
        Assert.Equal(("16:00", "Out", "Manual", "Badge left in the site office"), (added.LocalTime, added.Direction, added.Source, added.Reason));
        Assert.Equal(added.Id, (await pm.PostAsync<TimeClockPunchDto>("/api/timeclock/punches/manual", request)).Id);

        // Within a minute of an existing scan: refused.
        var clash = await pm.PostAsync("/api/timeclock/punches/manual", request with { clientRequestId = "manual-2", at = $"{Iso(yesterday)}T16:00:30" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, clash.StatusCode);

        var row = Assert.Single((await DayAsync(pm, project.Id, yesterday)).Items);
        Assert.Equal(["MissingTimeIn"], row.Exceptions);
        var wrong = row.Punches.Single(punch => punch.LocalTime == "09:00");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pm.PostAsync($"/api/timeclock/punches/{wrong.Id}/void", new { reason = "", version = wrong.Version })).StatusCode);
        await pm.PostAsync<TimeClockPunchDto>($"/api/timeclock/punches/{wrong.Id}/void", new { reason = "Scanned out by mistake", version = wrong.Version });
        Assert.Equal(HttpStatusCode.Conflict,
            (await pm.PostAsync($"/api/timeclock/punches/{wrong.Id}/void", new { reason = "Again", version = wrong.Version })).StatusCode);

        row = Assert.Single((await DayAsync(pm, project.Id, yesterday)).Items);
        Assert.Equal((true, 8m), (row.IsClean, row.RegularHours));

        // Once the day is recorded, scans can't be added or removed: the attendance entry is what changes.
        await ConfirmAsync(pm, project.Id, yesterday, false, (crew[0].Worker.Id, false));
        var late = await pm.PostAsync("/api/timeclock/punches/manual", request with { clientRequestId = "manual-3", at = $"{Iso(yesterday)}T18:00" });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        var lastPunch = row.Punches[^1];
        Assert.Equal(HttpStatusCode.Conflict,
            (await pm.PostAsync($"/api/timeclock/punches/{lastPunch.Id}/void", new { reason = "Wrong", version = lastPunch.Version })).StatusCode);

        // Audit lines never carry the reason typed.
        Assert.All(await AuditSummariesAsync(_factory, owner.TenantId), line => Assert.DoesNotContain("Badge left", line, StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task Manual_scans_and_voids_need_timeclock_review_and_stay_inside_the_company()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var (project, crew) = await CrewAsync(admin, 1);
        var added = await admin.PostAsync<TimeClockPunchDto>("/api/timeclock/punches/manual",
            new { projectId = project.Id, workerId = crew[0].Worker.Id, at = $"{Iso(Today.AddDays(-1))}T07:00", direction = "In", reason = "Kiosk down" });

        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer, AccessRole.CostEngineer })
        {
            var (other, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = other;
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync("/api/timeclock/punches/manual",
                new { projectId = project.Id, workerId = crew[0].Worker.Id, at = $"{Iso(Today.AddDays(-1))}T08:00", direction = "In", reason = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/timeclock/days?projectId={project.Id}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/timeclock/punches/{added.Id}/void", new { reason = "x", version = added.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync("/api/timeclock/punches/manual",
            new { projectId = project.Id, workerId = crew[0].Worker.Id, at = $"{Iso(Today.AddDays(-1))}T08:00", direction = "In", reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/timeclock/days?projectId={project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/timeclock/punches/{added.Id}/void", new { reason = "x", version = added.Version + 5 })).StatusCode);
    }

    private static Task<HttpResponseMessage> PostCsvAsync(ApiClient client, Guid projectId, string csv) =>
        client.PostAsync($"/api/timeclock/import?projectId={projectId}", new { csv });

    private static async Task<ImportPunchesResultDto> ImportAsync(ApiClient client, Guid projectId, string csv) =>
        await client.ReadAsync<ImportPunchesResultDto>(await PostCsvAsync(client, projectId, csv));
}
