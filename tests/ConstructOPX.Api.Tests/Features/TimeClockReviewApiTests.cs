using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using Xunit.Abstractions;
using static ConstructOPX.Api.Tests.Features.TimeClockTestKit;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Reviewing a day of scans and confirming it into attendance, badges, the company settings and the Needs your action group.</summary>
public sealed class TimeClockReviewApiTests(ConstructOpxApiFactory factory, ITestOutputHelper output) : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory = factory;

    [SqlServerFact]
    public async Task The_day_lists_each_worker_with_hours_overtime_waiting_for_approval_and_exceptions_and_defaults_to_the_earliest_day_waiting()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 3);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        await PushAsync(_factory, key,
            Scan("a-1", crew[0].Code, At(2, 7, 2)), Scan("a-2", crew[0].Code, At(2, 17, 3)),  // 10 h 01 less the break: 9 h; 1 h overtime proposed
            Scan("b-1", crew[1].Code, At(2, 7, 0)),                                              // never scanned out
            Scan("c-1", crew[2].Code, At(2, 7, 0)), Scan("c-2", crew[2].Code, At(2, 16, 0)),   // a clean 8 hours
            Scan("d-1", crew[2].Code, At(1, 7, 0)), Scan("d-2", crew[2].Code, At(1, 16, 0)));

        var day = await admin.GetAsync<TimeClockDayListDto>($"/api/timeclock/days?projectId={project.Id}");
        Assert.Equal(Today.AddDays(-2), day.WorkDate);
        Assert.Equal((3, 2, 4, 1, Today.AddDays(-2)), (day.Total, day.Summary.DaysWaiting, day.Summary.RowsWaiting, day.Summary.RowsWithExceptions,
            day.Summary.EarliestDate));

        var rows = day.Items.ToDictionary(row => row.WorkerId);
        var overtime = rows[crew[0].Worker.Id];
        Assert.Equal(("07:02", "17:03", 60, 8m, 1m, true, false), (overtime.FirstInLocal, overtime.LastOutLocal, overtime.BreakMinutes, overtime.RegularHours,
            overtime.ProposedOvertimeHours, overtime.CanConfirm, overtime.IsClean));
        var missing = rows[crew[1].Worker.Id];
        Assert.Equal(["MissingTimeOut"], missing.Exceptions);
        Assert.False(missing.CanConfirm);
        var clean = rows[crew[2].Worker.Id];
        Assert.Equal((8m, 0m, true, 2), (clean.RegularHours, clean.ProposedOvertimeHours, clean.IsClean, clean.Punches.Count));

        // Hours only: no pay figure, employee number, code, photo or place in the review.
        AssertNothingPrivate(await RawAsync(admin, $"/api/timeclock/days?projectId={project.Id}"));

        var summary = await admin.GetAsync<TimeClockSummaryDto>($"/api/timeclock/summary?projectId={project.Id}");
        Assert.Equal((2, 4), (summary.DaysWaiting, summary.RowsWaiting));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync($"/api/timeclock/days?projectId={project.Id}&date={Iso(Today.AddDays(1))}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Confirming_a_row_records_attendance_through_the_attendance_rules_with_overtime_only_when_approved()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 2);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var date = Today.AddDays(-1);
        // Both 07:00 to 18:00: 11 h less the break, 10 h: 8 regular and 2 overtime proposed.
        await PushAsync(_factory, key, Scan("o-1", crew[0].Code, At(1, 7, 0)), Scan("o-2", crew[0].Code, At(1, 18, 0)),
            Scan("p-1", crew[1].Code, At(1, 7, 0)), Scan("p-2", crew[1].Code, At(1, 18, 0)));

        var result = await ConfirmAsync(admin, project.Id, date, false, (crew[0].Worker.Id, true), (crew[1].Worker.Id, false));
        Assert.Equal(new[] { crew[0].Worker.Id, crew[1].Worker.Id }.Order(), result.Confirmed.Order());
        Assert.Empty(result.Skipped);

        var sheet = await admin.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date={Iso(date)}");
        var approved = sheet.Entries.Single(entry => entry.WorkerId == crew[0].Worker.Id);
        var notApproved = sheet.Entries.Single(entry => entry.WorkerId == crew[1].Worker.Id);
        Assert.Equal((8m, 2m, "TimeClock", 60), (approved.RegularHours, approved.OvertimeHours, approved.Source, approved.ClockBreakMinutes));
        Assert.Equal(At(1, 7, 0), approved.ClockFirstIn);
        Assert.Equal(At(1, 18, 0), approved.ClockLastOut);
        Assert.Equal((8m, 0m), (notApproved.RegularHours, notApproved.OvertimeHours));

        // Confirming again records nothing new.
        var again = await ConfirmAsync(admin, project.Id, date, false, (crew[0].Worker.Id, true));
        Assert.Empty(again.Confirmed);
        Assert.Equal("Already recorded.", Assert.Single(again.Skipped).Reason);
        Assert.Equal(2, (await admin.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date={Iso(date)}")).Entries.Count);

        var row = (await DayAsync(admin, project.Id, date)).Items.First();
        Assert.Equal((true, false), (row.AlreadyRecorded, row.CanConfirm));
    }

    [SqlServerFact]
    public async Task Confirm_all_clean_rows_skips_rows_with_an_exception_or_overtime_not_approved_and_never_overwrites_typed_attendance()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 5);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var date = Today.AddDays(-1);
        await PushAsync(_factory, key,
            Scan("1a", crew[0].Code, At(1, 7, 0)), Scan("1b", crew[0].Code, At(1, 16, 0)),   // clean
            Scan("2a", crew[1].Code, At(1, 7, 0)),                                            // missing time out
            Scan("3a", crew[2].Code, At(1, 6, 0)), Scan("3b", crew[2].Code, At(1, 18, 0)),   // 3 h overtime, not approved
            Scan("4a", crew[3].Code, At(1, 6, 0)), Scan("4b", crew[3].Code, At(1, 18, 0)),   // 3 h overtime, approved
            Scan("5a", crew[4].Code, At(1, 7, 0)), Scan("5b", crew[4].Code, At(1, 16, 0)));  // clean, but typed by hand already
        var typed = await RecordAsync(admin, crew[4].Worker.Id, project.Id, Iso(date), 6m);

        var result = await ConfirmAsync(admin, project.Id, date, true, (crew[3].Worker.Id, true));
        Assert.Equal(new[] { crew[0].Worker.Id, crew[3].Worker.Id }.Order(), result.Confirmed.Order());
        var skipped = result.Skipped.ToDictionary(item => item.WorkerId, item => item.Reason);
        Assert.Equal("A scan is missing. Add it, or remove the extra one, then confirm.", skipped[crew[1].Worker.Id]);
        Assert.Equal("The overtime waits for your approval.", skipped[crew[2].Worker.Id]);
        Assert.Equal("Already recorded.", skipped[crew[4].Worker.Id]);

        var sheet = await admin.GetAsync<AttendanceSheetDto>($"/api/projects/{project.Id}/attendance?date={Iso(date)}");
        var kept = sheet.Entries.Single(entry => entry.WorkerId == crew[4].Worker.Id);
        Assert.Equal((typed.Id, 6m, "Manual", typed.Version), (kept.Id, kept.RegularHours, kept.Source, kept.Version));
        var four = sheet.Entries.Single(entry => entry.WorkerId == crew[3].Worker.Id);
        Assert.Equal((8m, 3m), (four.RegularHours, four.OvertimeHours));

        // A row with a missing scan is refused even when chosen by itself.
        var single = await ConfirmAsync(admin, project.Id, date, false, (crew[1].Worker.Id, false));
        Assert.Empty(single.Confirmed);

        // Nothing chosen and not "all clean": refused.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PostAsync("/api/timeclock/days/confirm", new { projectId = project.Id, workDate = Iso(date), allClean = false, rows = Array.Empty<object>() })).StatusCode);
    }

    [SqlServerFact]
    public async Task Pay_is_the_same_whether_the_hours_were_typed_or_came_from_the_time_clock()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 2);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        var date = Today.AddDays(-2);
        // A late shift from 13:00 to midnight (before the 04:00 day-start, so one day): 11 h less the break, 10 h; 8 regular, 2 overtime (approved),
        // and the 2 hours after 22:00 are night hours.
        await PushAsync(_factory, key, Scan("n-1", crew[0].Code, At(2, 13, 0)), Scan("n-2", crew[0].Code, At(1, 0, 0)));
        var confirmed = await ConfirmAsync(admin, project.Id, date, false, (crew[0].Worker.Id, true));
        Assert.Single(confirmed.Confirmed);

        // The same hours typed by hand for a worker on the same rate.
        await admin.PostAsync<AttendanceEntryDto>("/api/attendance", new
        {
            workerId = crew[1].Worker.Id, projectId = project.Id, workDate = Iso(date), regularHours = 8m, overtimeHours = 2m, nightHours = 2m
        });

        var run = await CreateRunAsync(admin, Iso(date), Iso(date));
        var clock = run.Lines.Single(line => line.WorkerId == crew[0].Worker.Id);
        var typed = run.Lines.Single(line => line.WorkerId == crew[1].Worker.Id);
        Assert.Equal((8m, 2m, 2m), (clock.RegularHours, clock.OvertimeHours, clock.NightHours));
        Assert.Equal((typed.RegularPayMinor, typed.OvertimePayMinor, typed.PremiumPayMinor, typed.NightPayMinor, typed.GrossPayMinor),
            (clock.RegularPayMinor, clock.OvertimePayMinor, clock.PremiumPayMinor, clock.NightPayMinor, clock.GrossPayMinor));
        Assert.True(clock.GrossPayMinor > 0);
    }

    [SqlServerFact]
    public async Task The_review_list_costs_the_same_number_of_statements_for_one_worker_and_for_ten()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (small, smallCrew) = await CrewAsync(admin, 1, "Small site");
        var (large, largeCrew) = await CrewAsync(admin, 10, "Large site", numberPrefix: "L");
        var smallKey = (await RegisterAsync(admin, small.Id)).Key;
        var largeKey = (await RegisterAsync(admin, large.Id)).Key;
        await PushAsync(_factory, smallKey, Scan("s-1", smallCrew[0].Code, At(1, 7, 0)), Scan("s-2", smallCrew[0].Code, At(1, 16, 0)));
        await PushAsync(_factory, largeKey, largeCrew.SelectMany((member, index) => new[]
        {
            Scan($"l-{index}-in", member.Code, At(1, 7, index)), Scan($"l-{index}-out", member.Code, At(1, 16, index))
        }).ToArray());
        var date = Iso(Today.AddDays(-1));

        var one = await _factory.Sql.MeasureAsync(() => admin.GetAsync<TimeClockDayListDto>($"/api/timeclock/days?projectId={small.Id}&date={date}"));
        var ten = await _factory.Sql.MeasureAsync(() => admin.GetAsync<TimeClockDayListDto>($"/api/timeclock/days?projectId={large.Id}&date={date}"));

        output.WriteLine($"Statements: {one} for one, {ten} for ten.");
        Assert.Equal(one, ten);
        Assert.Equal(10, (await DayAsync(admin, large.Id, Today.AddDays(-1))).Items.Count);
        Assert.Equal(6, one);
    }

    [SqlServerFact]
    public async Task Reviewing_needs_timeclock_review_and_another_companys_project_is_not_found()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var project = await NewProjectAsync(admin);
        var date = Iso(Today.AddDays(-1));

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.HrOfficer })
        {
            var (allowed, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = allowed;
            Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync($"/api/timeclock/days?projectId={project.Id}&date={date}")).StatusCode);
        }
        foreach (var role in new[] { AccessRole.Accountant, AccessRole.CostEngineer, AccessRole.WarehouseKeeper, AccessRole.Viewer })
        {
            var (refused, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = refused;
            Assert.Equal(HttpStatusCode.Forbidden, (await refused.GetAsync($"/api/timeclock/summary?projectId={project.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await refused.PostAsync("/api/timeclock/days/confirm",
                new { projectId = project.Id, workDate = date, allClean = true })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/timeclock/summary?projectId={project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync("/api/timeclock/days/confirm",
            new { projectId = project.Id, workDate = date, allClean = true })).StatusCode);
    }

    [SqlServerFact]
    public async Task Badges_carry_name_employee_number_job_title_and_code_and_never_pay()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var (_, crew) = await CrewAsync(admin, 2);
        var withoutCode = await HireAsync(admin, "W-900", "Lito", "Abad", "Carpenter");
        var otherCompany = await HireAsync(stranger, "W-001", "Other", "Person");

        var sheet = await admin.GetAsync<BadgeSheetDto>(
            $"/api/timeclock/badges?workerIds={crew[0].Worker.Id}&workerIds={withoutCode.Id}&workerIds={otherCompany.Id}");
        Assert.Equal("Test Builders", sheet.CompanyName);
        Assert.Equal(1, sheet.ActiveWithoutCode);
        Assert.Equal([("Lito Abad", "W-900", "Carpenter", (string?)null), ("Worker01 Crew01", "W-001", "Mason", crew[0].Code)],
            sheet.Items.Select(item => (item.FullName, item.EmployeeNumber, item.JobTitle, item.Code)));
        WorkforceTestKit.AssertNoPayFields(await RawAsync(admin, $"/api/timeclock/badges?workerIds={crew[0].Worker.Id}"));
        Assert.DoesNotContain("rate", (await RawAsync(admin, $"/api/timeclock/badges?workerIds={crew[0].Worker.Id}")).GetRawText(), StringComparison.OrdinalIgnoreCase);

        var issued = await admin.PostAsync<IssueMissingCodesResultDto>("/api/timeclock/badges/issue-missing");
        Assert.Equal((1, 0), (issued.Issued, issued.StillWithoutCode));
        Assert.Equal([withoutCode.Id], issued.WorkerIds);
        Assert.True((await admin.GetAsync<WorkerDetailDto>($"/api/workers/{withoutCode.Id}")).HasPunchCode);

        var tooMany = string.Join('&', Enumerable.Range(0, 101).Select(_ => $"workerIds={Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync($"/api/timeclock/badges?{tooMany}")).StatusCode);

        // Codes are HR's and administrators'; the audit says a code was issued, never which.
        var (pm, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, owner.TenantId);
        using var _p = pm;
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.GetAsync($"/api/timeclock/badges?workerIds={crew[0].Worker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.PostAsync($"/api/workers/{crew[0].Worker.Id}/punch-code", new { version = crew[0].Worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/workers/{crew[0].Worker.Id}/punch-code", new { version = crew[0].Worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/workers/{crew[0].Worker.Id}/punch-code", new { version = 0 })).StatusCode);
        var audit = await AuditSummariesAsync(_factory, owner.TenantId);
        Assert.Contains(audit, line => line == "Issued a time clock badge code to Lito Abad.");
        Assert.All(audit, line => Assert.DoesNotContain(crew[0].Code, line, StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task The_company_keeps_its_day_start_break_and_rounding_and_the_day_is_worked_out_with_them()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var settings = await admin.GetAsync<OrganizationDto>("/api/organization");
        Assert.Equal((240, 60, 15), (settings.TimeClockDayStartMinutes, settings.UnpaidBreakMinutes, settings.PunchRoundingMinutes));

        var invalid = await admin.PutAsync("/api/organization", new
        {
            settings.Name, settings.CurrencyCode, settings.VatRateBps, settings.TimeZoneId, settings.HoursPerDay, settings.OvertimeMultiplierBps,
            settings.VarianceBasis, settings.Version, timeClockDayStartMinutes = 900, unpaidBreakMinutes = 200, punchRoundingMinutes = 90
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        Assert.Equal(["punchRoundingMinutes", "timeClockDayStartMinutes", "unpaidBreakMinutes"],
            errors.EnumerateObject().Select(property => property.Name).Order());

        var saved = await admin.PutAsync<OrganizationDto>("/api/organization", new
        {
            settings.Name, settings.CurrencyCode, settings.VatRateBps, settings.TimeZoneId, settings.HoursPerDay, settings.OvertimeMultiplierBps,
            settings.VarianceBasis, settings.Version, timeClockDayStartMinutes = 300, unpaidBreakMinutes = 30, punchRoundingMinutes = 0
        });
        Assert.Equal((300, 30, 0), (saved.TimeClockDayStartMinutes, saved.UnpaidBreakMinutes, saved.PunchRoundingMinutes));

        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        // 07:00 to 16:07 is 9 h 07; less the 30-minute break 8 h 37 (no rounding): 8 regular, 0.62 overtime.
        await PushAsync(_factory, key, Scan("r-1", crew[0].Code, At(1, 7, 0)), Scan("r-2", crew[0].Code, At(1, 16, 7)));
        var row = Assert.Single((await DayAsync(admin, project.Id, Today.AddDays(-1))).Items);
        Assert.Equal((30, 8m, 0.62m), (row.BreakMinutes, row.RegularHours, row.ProposedOvertimeHours));

        // Left out, the time clock settings stay as they are.
        var kept = await admin.PutAsync<OrganizationDto>("/api/organization", new
        {
            saved.Name, saved.CurrencyCode, saved.VatRateBps, saved.TimeZoneId, saved.HoursPerDay, saved.OvertimeMultiplierBps, saved.VarianceBasis,
            saved.Version
        });
        Assert.Equal((300, 30, 0), (kept.TimeClockDayStartMinutes, kept.UnpaidBreakMinutes, kept.PunchRoundingMinutes));
    }

    [SqlServerFact]
    public async Task Needs_your_action_lists_projects_with_days_waiting_for_the_people_who_confirm_them_with_no_amounts()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 2);
        var key = (await RegisterAsync(admin, project.Id)).Key;
        await PushAsync(_factory, key, Scan("w-1", crew[0].Code, At(1, 7, 0)), Scan("w-2", crew[0].Code, At(1, 16, 0)),
            Scan("w-3", crew[1].Code, At(1, 7, 0)), Scan("today", crew[0].Code, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var group = await GroupAsync(admin);
        Assert.NotNull(group);
        Assert.Equal(1, group!.Value.GetProperty("count").GetInt32());
        var row = group.Value.GetProperty("rows")[0];
        Assert.Equal(project.Id, row.GetProperty("id").GetGuid());
        Assert.Equal($"2 workers to review for {Today.AddDays(-1):ddd d MMM}; 1 needs a closer look", row.GetProperty("detail").GetString());
        Assert.True(row.GetProperty("isUrgent").GetBoolean());
        Assert.False(row.TryGetProperty("amountMinor", out _));

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.HrOfficer })
        {
            var (allowed, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = allowed;
            Assert.NotNull(await GroupAsync(allowed));
        }
        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer, AccessRole.WarehouseKeeper })
        {
            var (refused, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = refused;
            Assert.Null(await GroupAsync(refused));
        }

        // Once confirmed (the incomplete one with its scan removed), the project leaves the list.
        await ConfirmAsync(admin, project.Id, Today.AddDays(-1), true);
        var stray = (await DayAsync(admin, project.Id, Today.AddDays(-1))).Items.Single(item => item.WorkerId == crew[1].Worker.Id).Punches[0];
        await admin.PostAsync<TimeClockPunchDto>($"/api/timeclock/punches/{stray.Id}/void", new { reason = "Went home sick, no time out", version = stray.Version });
        Assert.Equal(0, (await GroupAsync(admin))!.Value.GetProperty("count").GetInt32());
    }

    private static async Task<JsonElement?> GroupAsync(ApiClient client)
    {
        var page = await RawAsync(client, "/api/needs-action");
        foreach (var group in page.GetProperty("groups").EnumerateArray())
            if (group.GetProperty("key").GetString() == "time-clock-to-review") return group;
        return null;
    }
}
