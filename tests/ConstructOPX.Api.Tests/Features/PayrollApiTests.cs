using System.Net;
using System.Text;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

public sealed class PayrollApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public PayrollApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Hire_to_approval_to_void_runs_the_whole_journey_and_the_costs_follow_the_payroll()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");

        // Juan: 800.00 a day = 100.00 an hour. Ana: 600.00 a day = 75.00 an hour. A day is 8 paid hours; overtime is 125%.
        var juan = await AssignAsync(client, await HireAsync(client, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), tower);
        var ana = await AssignAsync(client, await HireAsync(client, "W-002", "Ana", "Abad", "Helper", 60_000), tower);

        // Mon 5 Oct: Juan 8h on Tower A (800.00) and Ana 8h on Tower A (600.00).
        // Tue 6 Oct: Juan 4h regular + 2h overtime on Warehouse B (4 x 100.00 = 400.00, 2 x 100.00 x 1.25 = 250.00).
        var batch = await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", new
        {
            projectId = tower.Id,
            workDate = "2026-10-05",
            clientRequestId = Guid.NewGuid().ToString("N"),
            entries = new[] { new { workerId = juan.Id, regularHours = 8m, overtimeHours = 0m }, new { workerId = ana.Id, regularHours = 8m, overtimeHours = 0m } }
        });
        Assert.Equal(2, batch.Count);
        await RecordAsync(client, juan.Id, warehouse.Id, "2026-10-06", 4m, 2m);

        // Juan 800.00 + 400.00 + 250.00 = 1,450.00 (145,000). Ana 600.00 (60,000). Run gross 2,050.00 (205,000).
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        Assert.Equal("Draft", draft.Status);
        Assert.Equal(2, draft.WorkerCount);
        Assert.Equal(205_000, draft.TotalGrossMinor);
        var juanLine = draft.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal(120_000, juanLine.RegularPayMinor);
        Assert.Equal(25_000, juanLine.OvertimePayMinor);
        Assert.Equal(145_000, juanLine.GrossPayMinor);
        Assert.Equal(2, juanLine.Allocations.Count);
        Assert.Equal(juanLine.GrossPayMinor, juanLine.Allocations.Sum(share => share.GrossMinor));

        // The attendance the draft took is locked and can't be edited or removed.
        var locked = await client.GetAsync<PagedResult<AttendanceEntryDto>>($"/api/attendance?workerId={juan.Id}");
        Assert.All(locked.Items, entry => Assert.Equal((draft.Id, true), (entry.PayrollRunId, entry.IsLocked)));
        var edit = await client.PutAsync($"/api/attendance/{locked.Items[0].Id}",
            new { subProjectId = (Guid?)null, regularHours = 5m, overtimeHours = 0m, remarks = (string?)null, version = locked.Items[0].Version });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("rule_violation", (await ApiClient.ProblemAsync(edit)).GetProperty("code").GetString());
        var remove = await client.PostAsync($"/api/attendance/{locked.Items[0].Id}/void", new { reason = "Mistake", version = locked.Items[0].Version });
        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);

        // Deductions: SSS 120.00 and a cash advance 200.00 take Juan's net from 1,450.00 to 1,130.00.
        var withDeductions = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_000L }, new { description = "Cash advance", amountMinor = 20_000L } } });
        Assert.Equal(32_000, withDeductions.TotalDeductionsMinor);
        Assert.Equal(113_000, withDeductions.Lines.Single(line => line.WorkerId == juan.Id).NetPayMinor);
        Assert.Equal(205_000 - 32_000, withDeductions.TotalNetMinor);

        // Approval posts one labor cost per project: Tower A 800.00 + 600.00 = 1,400.00; Warehouse B 650.00. Together 2,050.00.
        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = withDeductions.Version });
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedAtUtc);
        Assert.Equal("Test Admin", approved.ApprovedByName);
        var posted = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
        Assert.Equal(2, posted.Count);
        Assert.Equal(140_000, posted.Single(entry => entry.ProjectId == tower.Id).GrossMinor);
        Assert.Equal(65_000, posted.Single(entry => entry.ProjectId == warehouse.Id).GrossMinor);
        Assert.Equal(approved.TotalGrossMinor, posted.Sum(entry => entry.GrossMinor));
        Assert.All(posted, entry =>
        {
            Assert.Equal("Payroll 1 Oct - 15 Oct 2026", entry.Description);
            Assert.Equal(ItemType.Labor, entry.ItemType);
            Assert.Equal(1m, entry.Quantity);
            Assert.Equal("l.s", entry.Unit);
            Assert.Equal(VatTreatment.Exempt, entry.VatTreatment);
            Assert.Equal(0, entry.VatRateBps);
            Assert.Equal(entry.UnitCostMinor, entry.GrossMinor);
            Assert.Equal(new DateOnly(2026, 10, 15), entry.EntryDate);
            Assert.Equal(CostEntryStatus.Posted, entry.Status);
        });

        // Voiding releases the attendance and voids the posted costs with the same reason.
        var voided = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = approved.Version });
        Assert.Equal(("Voided", "Wrong period"), (voided.Status, voided.VoidReason));
        var costsAfter = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
        Assert.Equal(2, costsAfter.Count);
        Assert.All(costsAfter, entry => Assert.Equal((CostEntryStatus.Voided, "Wrong period"), (entry.Status, entry.VoidReason)));
        var released = await client.GetAsync<PagedResult<AttendanceEntryDto>>($"/api/attendance?workerId={juan.Id}");
        Assert.All(released.Items, entry => Assert.Equal((null, false), (entry.PayrollRunId, entry.IsLocked)));
        var editAfter = await client.PutAsync<AttendanceEntryDto>($"/api/attendance/{released.Items[0].Id}",
            new { subProjectId = (Guid?)null, regularHours = 5m, overtimeHours = 0m, remarks = (string?)null, version = released.Items[0].Version });
        Assert.Equal(5m, editAfter.RegularHours);

        // The released attendance can be paid again in a new run.
        var second = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        Assert.Equal("Draft", second.Status);
    }

    [SqlServerFact]
    public async Task A_full_day_with_overtime_is_paid_at_the_rate_in_force_that_day_and_the_run_totals_add_up()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001", rateMinor: 80_000), project);

        // 5 Oct: 800.00 a day is 100.00 an hour. 8 h regular = 800.00 and 2 h overtime = 2 x 100.00 x 1.25 = 250.00. Total 1,050.00.
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m, 2m);
        // Raise to 960.00 a day (120.00 an hour) from 8 Oct. 7 Oct is paid at 800.00, 8 Oct at 960.00.
        await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor = 96_000L, rateUnit = "Daily", effectiveFrom = "2026-10-08", version = worker.Version
        });
        await RecordAsync(client, worker.Id, project.Id, "2026-10-07", 8m);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-08", 8m);

        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        // Regular: 800.00 + 800.00 + 960.00 = 2,560.00. Overtime: 250.00. Gross 2,810.00.
        var line = Assert.Single(draft.Lines);
        Assert.Equal(256_000, line.RegularPayMinor);
        Assert.Equal(25_000, line.OvertimePayMinor);
        Assert.Equal(281_000, line.GrossPayMinor);
        Assert.Equal((24m, 2m), (line.RegularHours, line.OvertimeHours));
        Assert.Equal("Mason", line.Designation);
        Assert.Equal(281_000, draft.TotalGrossMinor);
        Assert.Equal(281_000, draft.TotalNetMinor);
        Assert.Equal((8m, 12_500), (draft.HoursPerDay, draft.OvertimeMultiplierBps));
    }

    [SqlServerFact]
    public async Task Labor_cost_is_posted_per_project_and_sub_project_and_the_parts_equal_the_gross_pay_to_the_centavo()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        tower = await client.PostAsync<ProjectDto>($"/api/projects/{tower.Id}/sub-projects", new { name = "Building 1", startDate = "2026-02-01", version = tower.Version });
        var building = tower.SubProjects.Single().Id;
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        // 833.33 a day is 104.16625 an hour, so every figure needs rounding.
        var worker = await HireAsync(client, "W-001", rateMinor: 83_333);

        // Each entry rounds once, half away from zero (hourly rate 10,416.625 minor units):
        //  5 Oct Tower A whole project: 3 h = 31,249.875 -> 31,250;  1 h overtime = 13,020.78 -> 13,021   => 44,271
        //  5 Oct Warehouse B:           5 h = 52,083.125 -> 52,083                                         => 52,083
        //  6 Oct Tower A Building 1:    7 h = 72,916.375 -> 72,916;  2.5 h overtime = 32,551.95 -> 32,552  => 105,468
        //  7 Oct Warehouse B:           8 h = 83,333;                0.75 h overtime = 9,765.59 -> 9,766   => 93,099
        await RecordAsync(client, worker.Id, tower.Id, "2026-10-05", 3m, 1m);
        await RecordAsync(client, worker.Id, warehouse.Id, "2026-10-05", 5m);
        await RecordAsync(client, worker.Id, tower.Id, "2026-10-06", 7m, 2.5m, building);
        await RecordAsync(client, worker.Id, warehouse.Id, "2026-10-07", 8m, 0.75m);

        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        Assert.Equal(294_921, draft.TotalGrossMinor);
        Assert.Equal((239_582L, 55_339L), (draft.Lines[0].RegularPayMinor, draft.Lines[0].OvertimePayMinor));

        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });

        // Three postings: Tower A (whole project), Tower A / Building 1, and Warehouse B (52,083 + 93,099 = 145,182).
        var posted = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
        Assert.Equal(3, posted.Count);
        Assert.Equal(44_271, posted.Single(entry => entry.ProjectId == tower.Id && entry.SubProjectId == null).GrossMinor);
        Assert.Equal(105_468, posted.Single(entry => entry.ProjectId == tower.Id && entry.SubProjectId == building).GrossMinor);
        Assert.Equal(145_182, posted.Single(entry => entry.ProjectId == warehouse.Id).GrossMinor);
        Assert.Equal(approved.TotalGrossMinor, posted.Sum(entry => entry.GrossMinor));
        Assert.Equal(approved.Lines[0].Allocations.Sum(share => share.GrossMinor), approved.TotalGrossMinor);
        Assert.Contains(approved.Lines[0].Allocations, share => share is { ProjectName: "Tower A", SubProjectName: "Building 1", GrossMinor: 105_468 });
    }

    [SqlServerFact]
    public async Task The_period_must_be_sensible_and_there_must_be_unpaid_attendance_in_it()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);

        async Task<System.Text.Json.JsonElement> ErrorsFor(string start, string end)
        {
            var response = await client.PostAsync("/api/payroll-runs", new { periodStart = start, periodEnd = end });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            return (await ApiClient.ProblemAsync(response)).GetProperty("errors");
        }

        Assert.True((await ErrorsFor("2026-10-15", "2026-10-01")).TryGetProperty("periodEnd", out _));
        Assert.True((await ErrorsFor("2026-01-01", "2026-10-15")).TryGetProperty("periodEnd", out _));
        Assert.True((await ErrorsFor("2999-01-01", "2999-01-15")).TryGetProperty("periodEnd", out _));
        Assert.True((await ErrorsFor("0001-01-01", "0001-01-01")).TryGetProperty("periodStart", out _));

        var nothing = await client.PostAsync("/api/payroll-runs", new { periodStart = "2026-09-01", periodEnd = "2026-09-15" });
        Assert.Equal(HttpStatusCode.Conflict, nothing.StatusCode);
        Assert.Contains("no unpaid attendance", (await ApiClient.ProblemAsync(nothing)).GetProperty("detail").GetString());

        await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        var again = await client.PostAsync("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [SqlServerFact]
    public async Task Attendance_is_paid_once_only_the_period_decides_what_is_taken_and_a_double_submit_returns_the_same_run()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        foreach (var day in new[] { "2026-10-05", "2026-10-20" }) await RecordAsync(client, worker.Id, project.Id, day, 8m);
        var requestId = Guid.NewGuid().ToString("N");

        var first = await CreateRunAsync(client, "2026-10-01", "2026-10-15", requestId);
        var again = await CreateRunAsync(client, "2026-10-01", "2026-10-15", requestId);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(80_000, first.TotalGrossMinor);
        var attendance = (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Items;
        Assert.Equal(first.Id, attendance.Single(entry => entry.WorkDate == new DateOnly(2026, 10, 5)).PayrollRunId);
        Assert.Null(attendance.Single(entry => entry.WorkDate == new DateOnly(2026, 10, 20)).PayrollRunId);
        Assert.Equal(1, (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs")).Total);

        // The 20th is still unpaid and goes into the next period's run.
        var next = await CreateRunAsync(client, "2026-10-16", "2026-10-31");
        Assert.Equal(80_000, next.TotalGrossMinor);
    }

    [SqlServerFact]
    public async Task Deductions_replace_the_workers_set_and_can_not_be_negative_empty_or_more_than_the_pay()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var ana = await HireAsync(client, "W-002", "Ana", "Abad");
        await RecordAsync(client, juan.Id, project.Id, "2026-10-05", 8m);
        await RecordAsync(client, ana.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        var url = $"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/deductions";

        var saved = await client.PutAsync<PayrollRunDto>(url, new { version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_000L } } });
        Assert.Equal(68_000, saved.Lines.Single(line => line.WorkerId == juan.Id).NetPayMinor);
        Assert.Equal(80_000, saved.Lines.Single(line => line.WorkerId == ana.Id).NetPayMinor);

        // Replacing the set removes the old line: one cash advance of 50.00 is now the only deduction.
        var replaced = await client.PutAsync<PayrollRunDto>(url, new { version = saved.Version, deductions = new[] { new { description = "Cash advance", amountMinor = 5_000L } } });
        var line = replaced.Lines.Single(item => item.WorkerId == juan.Id);
        Assert.Equal(["Cash advance"], line.Deductions.Select(item => item.Description));
        Assert.Equal((5_000L, 75_000L), (line.DeductionsMinor, line.NetPayMinor));

        async Task<HttpResponseMessage> Put(object[] deductions) => await client.PutAsync(url, new { version = replaced.Version, deductions });
        var tooMuch = await Put([new { description = "Loan", amountMinor = 80_001L }]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        var detail = (await ApiClient.ProblemAsync(tooMuch)).GetProperty("errors").GetProperty("deductions")[0].GetString()!;
        Assert.Contains("Juan Dela Cruz", detail);
        Assert.DoesNotContain("800", detail);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Put([new { description = " ", amountMinor = 100L }])).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Put([new { description = "Zero", amountMinor = 0L }])).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Put([new { description = "Negative", amountMinor = -5L }])).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Put(Enumerable.Range(1, 26).Select(index => (object)new { description = $"Item {index}", amountMinor = 1L }).ToArray())).StatusCode);

        var cleared = await client.PutAsync<PayrollRunDto>(url, new { version = replaced.Version, deductions = Array.Empty<object>() });
        Assert.Equal(0, cleared.TotalDeductionsMinor);

        var stale = await client.PutAsync(url, new { version = draft.Version, deductions = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", (await ApiClient.ProblemAsync(stale)).GetProperty("code").GetString());
        var stranger = await client.PutAsync($"/api/payroll-runs/{draft.Id}/workers/{Guid.NewGuid()}/deductions", new { version = cleared.Version, deductions = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [SqlServerFact]
    public async Task An_approved_payroll_is_final_deductions_and_a_second_approval_are_refused()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        var stale = await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version + 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Empty(await PostedCostsAsync(_factory, user.TenantId, draft.Id));

        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });
        var twice = await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = approved.Version });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("rule_violation", (await ApiClient.ProblemAsync(twice)).GetProperty("code").GetString());
        var late = await client.PutAsync($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions",
            new { version = approved.Version, deductions = new[] { new { description = "Late", amountMinor = 100L } } });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Single(await PostedCostsAsync(_factory, user.TenantId, draft.Id));
    }

    [SqlServerFact]
    public async Task Approval_that_can_not_post_every_project_saves_nothing_and_names_the_project()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var worker = await HireAsync(client, "W-001");
        await RecordAsync(client, worker.Id, tower.Id, "2026-10-05", 4m);
        await RecordAsync(client, worker.Id, warehouse.Id, "2026-10-06", 4m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        await client.PostAsync<ProjectDto>($"/api/projects/{warehouse.Id}/archive", new { reason = "Finished", note = (string?)null, version = warehouse.Version });

        var refused = await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Warehouse B", (await ApiClient.ProblemAsync(refused)).GetProperty("detail").GetString());
        Assert.Equal("Draft", (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}")).Status);
        Assert.Empty(await PostedCostsAsync(_factory, user.TenantId, draft.Id));
    }

    [SqlServerFact]
    public async Task Voiding_a_draft_releases_its_attendance_needs_a_reason_and_can_not_be_repeated()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        var entry = await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        var noReason = await client.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = " ", version = draft.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(noReason)).GetProperty("errors").TryGetProperty("reason", out _));
        var stale = await client.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = draft.Version + 5 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var voided = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = draft.Version });
        Assert.Equal("Voided", voided.Status);
        Assert.Empty(await PostedCostsAsync(_factory, user.TenantId, draft.Id));
        var released = (await client.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Items.Single();
        Assert.Equal((entry.Id, null), (released.Id, released.PayrollRunId));

        var again = await client.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Again", version = voided.Version });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var approve = await client.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = voided.Version });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [SqlServerFact]
    public async Task The_list_pages_newest_first_filters_by_status_and_shows_each_runs_totals()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001");
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-20", 4m);
        var first = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        first = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{first.Id}/workers/{worker.Id}/deductions",
            new { version = first.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_000L } } });
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{first.Id}/approve", new { version = first.Version });
        var second = await CreateRunAsync(client, "2026-10-16", "2026-10-31");

        var list = await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs");

        Assert.Equal(2, list.Total);
        Assert.Equal([second.Id, first.Id], list.Items.Select(item => item.Id));
        var older = list.Items[1];
        Assert.Equal(("Approved", 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15)), (older.Status, older.WorkerCount, older.PeriodStart, older.PeriodEnd));
        Assert.Equal((80_000L, 12_000L, 68_000L), (older.TotalGrossMinor, older.TotalDeductionsMinor, older.TotalNetMinor));
        Assert.Equal((40_000L, 0L, 40_000L), (list.Items[0].TotalGrossMinor, list.Items[0].TotalDeductionsMinor, list.Items[0].TotalNetMinor));
        Assert.Equal([first.Id], (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs?status=Approved")).Items.Select(item => item.Id));
        Assert.Equal([second.Id], (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs?pageSize=1&page=1")).Items.Select(item => item.Id));
        Assert.Equal([first.Id], (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs?pageSize=1&page=2")).Items.Select(item => item.Id));
    }

    [SqlServerFact]
    public async Task The_register_downloads_as_a_UTF8_CSV_with_plain_amounts_and_no_cell_a_spreadsheet_could_run()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var sneaky = await HireAsync(client, "W-002", "=HYPERLINK(\"http://example.com\")", "Reyes", "Helper", 60_000);
        await RecordAsync(client, juan.Id, project.Id, "2026-10-05", 8m, 2m);
        await RecordAsync(client, sneaky.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        draft = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/deductions", new
        {
            version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_000L }, new { description = "Cash advance, March", amountMinor = 20_000L } }
        });

        var response = await client.GetAsync($"/api/payroll-runs/{draft.Id}/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("payroll-2026-10-01-to-2026-10-15.csv", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.True(response.Headers.CacheControl is { NoStore: true, Private: true });
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = new UTF8Encoding(false).GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // Juan: 800.00 + 250.00 = 1,050.00, less 120.00 and 200.00 = 730.00. The other worker: 8 h at 75.00 = 600.00.
        Assert.Equal("Employee number,Worker,Designation,Regular hours,Overtime hours,Night hours,Regular pay,Premium pay,Overtime pay,Night differential," +
            "Holiday pay,13th-month pay,Gross pay,Deductions,Net pay,Deduction details," +
            "SSS employee,SSS employer,PhilHealth employee,PhilHealth employer,Pag-IBIG employee,Pag-IBIG employer,Withholding tax,Employer contributions", lines[0]);
        Assert.Equal(4, lines.Length);
        // No contribution table is set up, so the deductions typed by hand stay in the details and the statutory columns are zero.
        Assert.Contains("W-001,Juan Dela Cruz,Mason,8,2,0,800.00,0.00,250.00,0.00,0.00,0.00,1050.00,320.00,730.00,\"Cash advance, March 200.00; SSS contribution 120.00\"," +
            "0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00", lines);
        Assert.Contains(lines, text => text.StartsWith("W-002,\"'=HYPERLINK(\"\"http://example.com\"\") Reyes\",Helper,8,0,0,600.00,0.00,0.00,0.00,0.00,0.00,600.00,0.00,600.00,", StringComparison.Ordinal));
        Assert.Equal("Total,,,16,2,0,1400.00,0.00,250.00,0.00,0.00,0.00,1650.00,320.00,1330.00,,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00", lines[^1]);
    }

    [SqlServerFact]
    public async Task Hire_to_void_never_puts_a_pay_figure_in_an_audit_line()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001", rateMinor: 123_456);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m, 2m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        draft = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_345L } } });
        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/void", new { reason = "Total of 1,543.20 was wrong", version = approved.Version });

        var lines = await AuditSummariesAsync(_factory, user.TenantId);

        Assert.Contains(lines, line => line.StartsWith("Prepared the payroll for 1 Oct - 15 Oct 2026", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("Approved the payroll for 1 Oct - 15 Oct 2026", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("Voided the approved payroll", StringComparison.Ordinal));
        AssertNoPayFigures(lines);
        Assert.DoesNotContain(lines, line => line.Contains("1,543"));
    }
}
