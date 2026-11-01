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

/// <summary>
/// Premium pay, the holiday calendar, the pay rules, unworked regular holidays and the 13th-month pay, through the real API on SQL Server.
/// Days used: Sunday 4 Oct 2026, Monday 5, Tuesday 6, Wednesday 7 (a regular holiday in these tests), Thursday 8. Juan earns 800.00 a day
/// (100.00 an hour), Ana 600.00 a day (75.00 an hour); a day is 8 paid hours and ordinary overtime is 125%.
/// </summary>
public sealed class PayrollPremiumApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public PayrollPremiumApiTests(CmsApiFactory factory) => _factory = factory;

    private static async Task<WorkerDetailDto> SetRestDayAsync(ApiClient client, WorkerDetailDto worker, string restDay)
    {
        var current = await client.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}");
        return await client.PutAsync<WorkerDetailDto>($"/api/workers/{worker.Id}", new
        {
            firstName = current.FirstName, middleName = current.MiddleName, lastName = current.LastName, dateOfBirth = current.DateOfBirth,
            maritalStatus = current.MaritalStatus, spouseName = current.SpouseName, religion = current.Religion,
            educationalAttainment = current.EducationalAttainment, presentAddress = current.PresentAddress, provincialAddress = current.ProvincialAddress,
            contactNumber = current.ContactNumber, reference = current.Reference, version = current.Version, restDay
        });
    }

    private static Task<CompanyHolidayDto> AddHolidayAsync(ApiClient client, string date, string type = "Regular", string name = "Founders day (test)") =>
        client.PostAsync<CompanyHolidayDto>("/api/payroll-holidays", new { date, name, type });

    private static Task<AttendanceEntryDto> RecordNightAsync(ApiClient client, Guid workerId, Guid projectId, string date, decimal regular, decimal overtime,
        decimal night) =>
        client.PostAsync<AttendanceEntryDto>("/api/attendance",
            new { workerId, projectId, subProjectId = (Guid?)null, workDate = date, regularHours = regular, overtimeHours = overtime, nightHours = night });

    private static async Task<PayrollRunDto> ApproveAsync(ApiClient client, Guid runId) =>
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{runId}/approve",
            new { version = (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{runId}")).Version });

    private static async Task<string?> RefusalAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        // A validation problem names its fields under "errors"; a refused rule says why in "detail".
        var problem = await ApiClient.ProblemAsync(response);
        return expected == HttpStatusCode.UnprocessableEntity ? problem.GetProperty("errors").ToString() : problem.GetProperty("detail").GetString();
    }

    [SqlServerFact]
    public async Task Rest_day_holiday_and_night_premiums_and_an_unworked_holiday_are_paid_and_posted_to_the_projects()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var juan = await SetRestDayAsync(client, await AssignAsync(client, await HireAsync(client, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), tower), "Sunday");
        var ana = await AssignAsync(client, await HireAsync(client, "W-002", "Ana", "Abad", "Helper", 60_000), tower);
        await AssignAsync(client, await HireAsync(client, "W-003", "Pedro", "Reyes", "Electrician", 10_000, "Hourly"), tower);
        Assert.Equal("Sunday", juan.RestDay);
        Assert.Null(ana.RestDay);

        var holiday = await AddHolidayAsync(client, "2026-10-07");
        Assert.Equal(("Regular", false), (holiday.Type, holiday.CoveredByApprovedPayroll));

        // Juan, Sunday (his rest day): 8 h = 800.00 + 240.00 premium; 2 h overtime = 100.00 x 2 x 1.30 x 1.30 = 338.00; 2 night hours =
        // 100.00 x 2 x 1.30 x 10% = 26.00. Wednesday (regular holiday) on Warehouse B: 8 h at 200% = 1,600.00. Juan: 3,004.00.
        var sunday = await RecordNightAsync(client, juan.Id, tower.Id, "2026-10-04", 8m, 2m, 2m);
        var wednesday = await RecordAsync(client, juan.Id, warehouse.Id, "2026-10-07", 8m);
        Assert.Equal(("RestDay", 2m), (sunday.DayType, sunday.NightHours));
        Assert.Equal("RegularHoliday", wednesday.DayType);
        // Ana, Monday 600.00, and the regular holiday not worked, posted to Tower A: one day at 600.00. Ana: 1,200.00.
        // Pedro is hourly-rated and has no attendance: no holiday pay, no line.
        Assert.Equal("Ordinary", (await RecordAsync(client, ana.Id, tower.Id, "2026-10-05", 8m)).DayType);

        var sheet = await client.GetAsync<AttendanceSheetDto>($"/api/projects/{warehouse.Id}/attendance?date=2026-10-07");
        Assert.Equal(("Founders day (test)", "Regular"), (sheet.HolidayName, sheet.HolidayType));

        var run = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        Assert.Null(run.HolidaysMissingForYear);
        Assert.Equal(2, run.WorkerCount);
        Assert.Equal(420_400, run.TotalGrossMinor);
        var juanLine = run.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal((160_000L, 104_000L, 33_800L, 2_600L, 0L, 300_400L),
            (juanLine.RegularPayMinor, juanLine.PremiumPayMinor, juanLine.OvertimePayMinor, juanLine.NightPayMinor, juanLine.HolidayPayMinor, juanLine.GrossPayMinor));
        Assert.Equal(2m, juanLine.NightHours);
        Assert.Equal([140_400L, 160_000L], juanLine.Allocations.OrderBy(share => share.ProjectName).Select(share => share.GrossMinor));
        var anaLine = run.Lines.Single(line => line.WorkerId == ana.Id);
        Assert.Equal((60_000L, 60_000L, 120_000L), (anaLine.RegularPayMinor, anaLine.HolidayPayMinor, anaLine.GrossPayMinor));
        Assert.Equal([new DateOnly(2026, 10, 7)], anaLine.HolidayDates);
        Assert.Equal(120_000, Assert.Single(anaLine.Allocations).GrossMinor);

        // Ana was paid for the holiday as a day not worked: recording work on it now would pay it twice.
        var twice = await client.PostAsync("/api/attendance", new { workerId = ana.Id, projectId = tower.Id, workDate = "2026-10-07", regularHours = 8m, overtimeHours = 0m });
        Assert.Contains("was paid for 7 Oct 2026 as a regular holiday not worked", await RefusalAsync(twice, HttpStatusCode.UnprocessableEntity));

        var approved = await ApproveAsync(client, run.Id);
        Assert.Equal("Approved", approved.Status);
        var posted = await PostedCostsAsync(_factory, user.TenantId, run.Id);
        Assert.Equal(approved.TotalGrossMinor, posted.Sum(entry => entry.GrossMinor));
        Assert.Equal(260_400, posted.Single(entry => entry.ProjectId == tower.Id).GrossMinor);
        Assert.Equal(160_000, posted.Single(entry => entry.ProjectId == warehouse.Id).GrossMinor);

        // The approved payroll covers the holiday: its type can't change and it can't be removed, and no holiday can be added in the period.
        var change = await client.PutAsync($"/api/payroll-holidays/{holiday.Id}", new { name = holiday.Name, type = "Special", version = holiday.Version });
        Assert.Equal("A payroll that covers this date was approved. Void it before changing this holiday.", await RefusalAsync(change, HttpStatusCode.Conflict));
        var remove = await client.PostAsync($"/api/payroll-holidays/{holiday.Id}/remove", new { version = holiday.Version });
        Assert.Equal("A payroll that covers this date was approved. Void it before changing this holiday.", await RefusalAsync(remove, HttpStatusCode.Conflict));
        var add = await client.PostAsync("/api/payroll-holidays", new { date = "2026-10-08", name = "Extra day", type = "Special" });
        Assert.Equal("A payroll that covers this date was approved. Void it before adding a holiday on this date.", await RefusalAsync(add, HttpStatusCode.Conflict));
        var renamed = await client.PutAsync<CompanyHolidayDto>($"/api/payroll-holidays/{holiday.Id}", new { name = "Founding anniversary", type = "Regular", version = holiday.Version });
        Assert.Equal(("Founding anniversary", true), (renamed.Name, renamed.CoveredByApprovedPayroll));

        // Voiding the payroll frees the calendar again (and the holiday for another payroll).
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{run.Id}/void", new { reason = "Wrong period", version = approved.Version });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/payroll-holidays/{holiday.Id}/remove", new { version = renamed.Version })).StatusCode);
        Assert.Empty((await client.GetAsync<HolidayCalendarDto>("/api/payroll-holidays?year=2026")).Holidays);
        AssertNoPayFigures(await AuditSummariesAsync(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task A_draft_made_before_the_rules_or_a_rest_day_changed_is_recalculated_before_it_can_be_approved()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client);
        var juan = await AssignAsync(client, await HireAsync(client, "W-001", rateMinor: 80_000), tower);
        await RecordAsync(client, juan.Id, tower.Id, "2026-10-04", 8m);

        // No rest day: Sunday is an ordinary day, 800.00. No holiday entered for 2026: the run page says so.
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        Assert.Equal(80_000, draft.TotalGrossMinor);
        Assert.Equal(2026, draft.HolidaysMissingForYear);
        draft = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "Cash advance", amountMinor = 10_000L } } });

        await SetRestDayAsync(client, juan, "Sunday");
        var stale = await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });
        Assert.Equal("The pay rules, holidays, or a worker's rest day or posting changed after this payroll was worked out. Recalculate the payroll, then approve.",
            await RefusalAsync(stale, HttpStatusCode.Conflict));

        // Recalculating takes the rest day (130%: 1,040.00) and keeps the cash advance.
        var recalculated = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = draft.Version });
        Assert.Equal((104_000L, 10_000L), (recalculated.TotalGrossMinor, recalculated.TotalDeductionsMinor));

        // The rules change (rest day 150%): the draft is stale again until it is recalculated (1,200.00).
        var rules = await client.GetAsync<PayRulesDto>("/api/payroll-rules");
        Assert.Equal((true, 0, 13_000, 12_500), (rules.IsDefault, rules.Version, rules.RestDayBps, rules.OvertimeMultiplierBps));
        var saved = await client.PutAsync<PayRulesDto>("/api/payroll-rules", new
        {
            restDayBps = 15_000, specialDayBps = 13_000, specialRestDayBps = 15_000, regularHolidayBps = 20_000, regularHolidayRestDayBps = 26_000,
            premiumDayOvertimeExtraBps = 3_000, nightDifferentialBps = 1_000, version = 0
        });
        Assert.Equal((false, 15_000), (saved.IsDefault, saved.RestDayBps));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = recalculated.Version })).StatusCode);
        recalculated = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = recalculated.Version });
        Assert.Equal(120_000, recalculated.TotalGrossMinor);

        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = recalculated.Version });
        Assert.Equal(120_000, (await PostedCostsAsync(_factory, user.TenantId, approved.Id)).Single().GrossMinor);

        // An approved payroll keeps its amounts whatever the rules do afterwards.
        await client.PutAsync<PayRulesDto>("/api/payroll-rules", new
        {
            restDayBps = 20_000, specialDayBps = 13_000, specialRestDayBps = 15_000, regularHolidayBps = 20_000, regularHolidayRestDayBps = 26_000,
            premiumDayOvertimeExtraBps = 3_000, nightDifferentialBps = 1_000, version = saved.Version
        });
        Assert.Equal(120_000, (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}")).TotalGrossMinor);
    }

    [SqlServerFact]
    public async Task Pay_rules_and_holidays_are_validated_versioned_and_kept_to_the_people_who_run_payroll()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        using var _m = manager;
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, adminUser.TenantId);
        using var _h = hr;
        var (other, _) = await SignedInAsync(_factory);
        using var _o = other;

        var invalid = await hr.PutAsync("/api/payroll-rules", new
        {
            restDayBps = 9_000, specialDayBps = 13_000, specialRestDayBps = 15_000, regularHolidayBps = 60_000, regularHolidayRestDayBps = 26_000,
            premiumDayOvertimeExtraBps = 3_000, nightDifferentialBps = -5, version = 0
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("restDayBps", out _) && errors.TryGetProperty("regularHolidayBps", out _) && errors.TryGetProperty("nightDifferentialBps", out _));

        var body = new
        {
            restDayBps = 13_000, specialDayBps = 13_000, specialRestDayBps = 15_000, regularHolidayBps = 20_000, regularHolidayRestDayBps = 26_000,
            premiumDayOvertimeExtraBps = 3_000, nightDifferentialBps = 1_500, version = 0
        };
        Assert.Equal(1_500, (await hr.PutAsync<PayRulesDto>("/api/payroll-rules", body)).NightDifferentialBps);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsync("/api/payroll-rules", body)).StatusCode); // version 0 is stale now

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/payroll-rules")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsync("/api/payroll-rules", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/payroll-holidays")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/payroll-holidays", new { date = "2026-12-25", name = "Christmas", type = "Regular" })).StatusCode);

        var christmas = await AddHolidayAsync(hr, "2026-12-25", "Regular", "Christmas Day (test)");
        var duplicate = await hr.PostAsync("/api/payroll-holidays", new { date = "2026-12-25", name = "Again", type = "Special" });
        Assert.Contains("already has a holiday", await RefusalAsync(duplicate, HttpStatusCode.UnprocessableEntity));
        var noType = await hr.PostAsync("/api/payroll-holidays", new { date = "2026-12-30", name = "", type = "Holiday" });
        var noTypeErrors = (await ApiClient.ProblemAsync(noType)).GetProperty("errors");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noType.StatusCode);
        Assert.True(noTypeErrors.TryGetProperty("name", out _) && noTypeErrors.TryGetProperty("type", out _));

        var special = await hr.PutAsync<CompanyHolidayDto>($"/api/payroll-holidays/{christmas.Id}", new { name = "Christmas Day (test)", type = "Special", version = christmas.Version });
        Assert.Equal("Special", special.Type);
        Assert.Equal(HttpStatusCode.Conflict, (await hr.PutAsync($"/api/payroll-holidays/{christmas.Id}", new { name = "x", type = "Regular", version = christmas.Version })).StatusCode);

        // Another company can neither see nor change it.
        Assert.Empty((await other.GetAsync<HolidayCalendarDto>("/api/payroll-holidays?year=2026")).Holidays);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/payroll-holidays/{christmas.Id}", new { name = "x", type = "Regular", version = special.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/payroll-holidays/{christmas.Id}/remove", new { version = special.Version })).StatusCode);
        Assert.True((await other.GetAsync<PayRulesDto>("/api/payroll-rules")).IsDefault);

        var calendar = await admin.GetAsync<HolidayCalendarDto>("/api/payroll-holidays?year=2026");
        Assert.Equal((2026, "Christmas Day (test)"), (calendar.Year, Assert.Single(calendar.Holidays).Name));
        Assert.Empty((await admin.GetAsync<HolidayCalendarDto>("/api/payroll-holidays?year=2027")).Holidays);
    }

    [SqlServerFact]
    public async Task The_13th_month_pay_is_a_twelfth_of_the_years_basic_pay_split_by_project_once_per_worker_and_voiding_reverses_it()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, user.TenantId);
        using var _h = hr;
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        using var _m = manager;
        var (other, _) = await SignedInAsync(_factory);
        using var _o = other;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var juan = await AssignAsync(client, await HireAsync(client, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), tower);
        var ana = await AssignAsync(client, await HireAsync(client, "W-002", "Ana", "Abad", "Helper", 60_000), tower);

        // October, first half: Juan 8 h on Tower A, 8 h + 2 h overtime on Warehouse B (overtime is not basic pay); Ana 4 h on Tower A.
        await RecordAsync(client, juan.Id, tower.Id, "2026-10-05", 8m);
        await RecordAsync(client, juan.Id, warehouse.Id, "2026-10-06", 8m, 2m);
        await RecordAsync(client, ana.Id, tower.Id, "2026-10-05", 4m);
        await ApproveAsync(client, (await CreateRunAsync(client, "2026-10-01", "2026-10-15")).Id);
        // Second half: Juan 8 h on Tower A, still a draft when the 13th-month pay is first worked out.
        await RecordAsync(client, juan.Id, tower.Id, "2026-10-20", 8m);
        var secondHalf = await CreateRunAsync(client, "2026-10-16", "2026-10-31");

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/payroll-runs/thirteenth-month", new { year = 2026 })).StatusCode);
        var future = await hr.PostAsync("/api/payroll-runs/thirteenth-month", new { year = 2027 });
        Assert.Contains("can't be for a future year", await RefusalAsync(future, HttpStatusCode.UnprocessableEntity));

        // Basic pay so far: Juan 1,600.00 (133.33), Ana 300.00 (25.00).
        var requestId = Guid.NewGuid().ToString("N");
        var thirteenth = await hr.PostAsync<PayrollRunDto>("/api/payroll-runs/thirteenth-month", new { year = 2026, clientRequestId = requestId });
        Assert.Equal(thirteenth.Id, (await hr.PostAsync<PayrollRunDto>("/api/payroll-runs/thirteenth-month", new { year = 2026, clientRequestId = requestId })).Id);
        Assert.Equal(("ThirteenthMonth", 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            (thirteenth.Kind, thirteenth.Year, thirteenth.PeriodStart, thirteenth.PeriodEnd));
        Assert.Equal([13_333L, 2_500L], thirteenth.Lines.OrderBy(line => line.EmployeeNumber).Select(line => line.ThirteenthMonthPayMinor));
        Assert.All(thirteenth.Lines, line => Assert.Empty(line.Deductions));
        Assert.Null(thirteenth.HolidaysMissingForYear);

        // The second half is approved: the 13th-month draft no longer matches the year's pay and must be recalculated first.
        await ApproveAsync(client, secondHalf.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync($"/api/payroll-runs/{thirteenth.Id}/approve", new { version = thirteenth.Version })).StatusCode);
        var stale = await client.PostAsync($"/api/payroll-runs/{thirteenth.Id}/approve", new { version = thirteenth.Version });
        Assert.Equal("The approved payrolls of 2026 changed after this 13th-month pay was worked out. Recalculate it, then approve.",
            await RefusalAsync(stale, HttpStatusCode.Conflict));
        thirteenth = await hr.PostAsync<PayrollRunDto>($"/api/payroll-runs/{thirteenth.Id}/recalculate-statutory", new { version = thirteenth.Version });

        // Juan: 2,400.00 of basic pay (Tower A 1,600.00, Warehouse B 800.00) = 200.00, split 133.33 + 66.67 by the largest remainder.
        var juanLine = thirteenth.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal((20_000L, 240_000L, 20_000L), (juanLine.ThirteenthMonthPayMinor, juanLine.ThirteenthMonthBasisMinor, juanLine.GrossPayMinor));
        Assert.Equal([13_333L, 6_667L], juanLine.Allocations.OrderBy(share => share.ProjectName).Select(share => share.GrossMinor));
        Assert.Equal(22_500, thirteenth.TotalGrossMinor);

        // A second 13th-month payroll for the year finds nobody left to pay.
        var again = await hr.PostAsync("/api/payroll-runs/thirteenth-month", new { year = 2026 });
        Assert.Equal("No worker has approved pay in 2026 that isn't already in a 13th-month payroll for that year.", await RefusalAsync(again, HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/payroll-runs/{thirteenth.Id}")).StatusCode);

        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{thirteenth.Id}/approve", new { version = thirteenth.Version });
        var posted = await PostedCostsAsync(_factory, user.TenantId, approved.Id);
        Assert.Equal(22_500, posted.Sum(entry => entry.GrossMinor));
        Assert.Equal(15_833, posted.Single(entry => entry.ProjectId == tower.Id).GrossMinor);
        Assert.Equal(6_667, posted.Single(entry => entry.ProjectId == warehouse.Id).GrossMinor);
        Assert.All(posted, entry => Assert.Equal("13th-month pay 2026", entry.Description));
        var list = await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs");
        Assert.Equal(("ThirteenthMonth", 2026, 22_500L), list.Items.Where(item => item.Id == approved.Id).Select(item => (item.Kind, item.Year, item.TotalGrossMinor)).Single());

        // Voiding reverses the cost and frees the workers' year: a new 13th-month payroll pays them again.
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{approved.Id}/void", new { reason = "Prepared early", version = approved.Version });
        Assert.All(await PostedCostsAsync(_factory, user.TenantId, approved.Id), entry => Assert.True(entry.IsVoided));
        var redo = await hr.PostAsync<PayrollRunDto>("/api/payroll-runs/thirteenth-month", new { year = 2026 });
        Assert.Equal(22_500, redo.TotalGrossMinor);
        AssertNoPayFigures(await AuditSummariesAsync(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task The_json_carries_every_name_the_web_client_reads()
    {
        // The web models (payroll.models.ts, workforce.models.ts) are written by hand: this pins the names they read to the real responses.
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client);
        var juan = await SetRestDayAsync(client, await AssignAsync(client, await HireAsync(client, "W-001"), tower), "Sunday");
        var holiday = await AddHolidayAsync(client, "2026-10-07");
        await RecordNightAsync(client, juan.Id, tower.Id, "2026-10-04", 8m, 0m, 1m);
        var run = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        static void Has(System.Text.Json.JsonElement element, params string[] names) =>
            Assert.All(names, name => Assert.True(element.TryGetProperty(name, out _), $"Missing {name} in {element}"));

        var runJson = await RawAsync(client, $"/api/payroll-runs/{run.Id}");
        Has(runJson, "kind", "year", "holidaysMissingForYear", "lines");
        Has(runJson.GetProperty("lines")[0], "nightHours", "premiumPayMinor", "nightPayMinor", "holidayPayMinor", "thirteenthMonthPayMinor",
            "thirteenthMonthBasisMinor", "holidayDates");
        Has((await RawAsync(client, "/api/payroll-runs")).GetProperty("items")[0], "kind", "year");
        Has(await RawAsync(client, "/api/payroll-rules"), "restDayBps", "specialDayBps", "specialRestDayBps", "regularHolidayBps",
            "regularHolidayRestDayBps", "premiumDayOvertimeExtraBps", "nightDifferentialBps", "overtimeMultiplierBps", "isDefault", "version");
        var calendar = await RawAsync(client, "/api/payroll-holidays?year=2026");
        Has(calendar, "year", "holidays");
        Has(calendar.GetProperty("holidays")[0], "id", "date", "name", "type", "coveredByApprovedPayroll", "version");
        Has((await RawAsync(client, $"/api/attendance?workerId={juan.Id}")).GetProperty("items")[0], "nightHours", "dayType");
        Has(await RawAsync(client, $"/api/projects/{tower.Id}/attendance?date=2026-10-07"), "holidayName", "holidayType");
        Assert.Equal("Sunday", (await RawAsync(client, $"/api/workers/{juan.Id}")).GetProperty("restDay").GetString());
        Assert.Equal(holiday.Id, calendar.GetProperty("holidays")[0].GetProperty("id").GetGuid());
    }

    [SqlServerFact]
    public async Task Working_out_and_approving_a_payroll_with_a_holiday_costs_the_same_statements_for_five_workers_as_for_forty()
    {
        async Task<(int Read, int Approve, int ThirteenthApprove)> MeasureAsync(int crew)
        {
            var (client, _) = await SignedInAsync(_factory);
            using var _c = client;
            var tower = await NewProjectAsync(client, "Tower A");
            var warehouse = await NewProjectAsync(client, "Warehouse B");
            var workers = new List<WorkerDetailDto>();
            for (var index = 1; index <= crew; index++)
            {
                var hired = await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}");
                workers.Add(await AssignAsync(client, hired, index % 2 == 0 ? tower : warehouse));
            }

            await AddHolidayAsync(client, "2026-10-07");
            foreach (var (date, crewOnDay) in new[] { ("2026-10-05", workers), ("2026-10-06", workers), ("2026-10-07", workers.Take(crew / 2).ToList()) })
                foreach (var group in crewOnDay.GroupBy(worker => worker.CurrentAssignment!.ProjectId))
                    await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", new
                    {
                        projectId = group.Key, workDate = date,
                        entries = group.Select(worker => new { workerId = worker.Id, regularHours = 8m, overtimeHours = 1m, nightHours = 1m }).ToArray()
                    });

            var run = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
            Assert.Equal(crew, run.Lines.Count);
            Assert.Equal(crew - crew / 2, run.Lines.Count(line => line.HolidayPayMinor > 0));
            var read = await _factory.Sql.MeasureAsync(async () => await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{run.Id}"));
            var approve = await _factory.Sql.MeasureAsync(async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/payroll-runs/{run.Id}/approve", new { version = run.Version })).StatusCode));
            var thirteenth = await client.PostAsync<PayrollRunDto>("/api/payroll-runs/thirteenth-month", new { year = 2026 });
            Assert.Equal(crew, thirteenth.Lines.Count);
            var thirteenthApprove = await _factory.Sql.MeasureAsync(async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/payroll-runs/{thirteenth.Id}/approve", new { version = thirteenth.Version })).StatusCode));
            return (read, approve, thirteenthApprove);
        }

        var five = await MeasureAsync(5);
        var forty = await MeasureAsync(40);

        Assert.Equal(five, forty);
        // Measured 1 Nov 2026: the read is 4 statements, approving (which works the pay out again to check it) 23, approving the 13th-month pay 16.
        Assert.Equal((4, 23, 16), forty);
    }
}
