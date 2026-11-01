using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.WorkforceTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>Who may do what in payroll, that one company never reaches another's pay, concurrency, and that reads stay cheap as the payroll grows.</summary>
public sealed class PayrollSecurityApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public PayrollSecurityApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_project_manager_records_attendance_but_can_not_touch_payroll_or_pay()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var worker = await HireAsync(admin, "W-001");
        await RecordAsync(admin, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(admin, "2026-10-01", "2026-10-15");
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        using var _m = manager;

        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsync("/api/attendance",
            new { workerId = worker.Id, projectId = project.Id, workDate = "2026-10-06", regularHours = 8m, overtimeHours = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/payroll-runs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/payroll-runs/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/payroll-runs/{draft.Id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsync($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions", new { version = draft.Version, deductions = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "No", version = draft.Version })).StatusCode);
    }

    [SqlServerFact]
    public async Task Viewers_and_warehouse_keepers_have_no_access_to_payroll()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var worker = await HireAsync(admin, "W-001");
        await RecordAsync(admin, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(admin, "2026-10-01", "2026-10-15");

        foreach (var role in new[] { AccessRole.Viewer, AccessRole.WarehouseKeeper, AccessRole.CostEngineer })
        {
            var (other, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _o = other;
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/payroll-runs")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/payroll-runs/{draft.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version })).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task An_HR_officer_runs_payroll_an_accountant_approves_it_and_only_an_approver_can_void_an_approved_one()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var worker = await HireAsync(admin, "W-001");
        foreach (var day in new[] { "2026-10-05", "2026-10-06" }) await RecordAsync(admin, worker.Id, project.Id, day, 8m);
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, adminUser.TenantId);
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, adminUser.TenantId);
        using var _h = hr;
        using var _c = accountant;

        // HR prepares the payroll, enters a deduction, reads it, and may void it while it is a draft.
        var draft = await hr.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = "2026-10-05", periodEnd = "2026-10-05" });
        var withDeduction = await hr.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 5_000L } } });
        Assert.Equal(HttpStatusCode.OK, (await hr.GetAsync($"/api/payroll-runs/{draft.Id}/export")).StatusCode);
        var voidedDraft = await hr.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/void", new { reason = "Mistake", version = withDeduction.Version });
        Assert.Equal("Voided", voidedDraft.Status);

        // A second payroll: HR can't approve it, the accountant can; HR then can't void the approved payroll, the accountant can.
        var second = await hr.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = "2026-10-05", periodEnd = "2026-10-06" });
        var refused = await hr.PostAsync($"/api/payroll-runs/{second.Id}/approve", new { version = second.Version });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var approved = await accountant.PostAsync<PayrollRunDto>($"/api/payroll-runs/{second.Id}/approve", new { version = second.Version });
        Assert.Equal("Approved", approved.Status);

        var hrVoid = await hr.PostAsync($"/api/payroll-runs/{second.Id}/void", new { reason = "Mistake", version = approved.Version });
        Assert.Equal(HttpStatusCode.Forbidden, hrVoid.StatusCode);
        Assert.Equal("Approved", (await hr.GetAsync<PayrollRunDto>($"/api/payroll-runs/{second.Id}")).Status);
        Assert.All(await PostedCostsAsync(_factory, adminUser.TenantId, second.Id), entry => Assert.Equal(CostEntryStatus.Posted, entry.Status));
        var accountantVoid = await accountant.PostAsync<PayrollRunDto>($"/api/payroll-runs/{second.Id}/void", new { reason = "Wrong period", version = approved.Version });
        Assert.Equal("Voided", accountantVoid.Status);
        Assert.All(await PostedCostsAsync(_factory, adminUser.TenantId, second.Id), entry => Assert.Equal(CostEntryStatus.Voided, entry.Status));
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_payroll()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, theirUser) = await SignedInAsync(_factory);
        using var _a = mine;
        using var _b = theirs;
        var project = await NewProjectAsync(mine);
        var worker = await HireAsync(mine, "W-001");
        await RecordAsync(mine, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(mine, "2026-10-01", "2026-10-15");

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/payroll-runs/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/payroll-runs/{draft.Id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Hijacked", version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions", new { version = draft.Version, deductions = Array.Empty<object>() })).StatusCode);
        Assert.Empty((await theirs.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs")).Items);

        // Their payroll for the same period finds none of my attendance, so my pay can't be swept into their run.
        var theirs409 = await theirs.PostAsync("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" });
        Assert.Equal(HttpStatusCode.Conflict, theirs409.StatusCode);
        Assert.Equal("Draft", (await mine.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}")).Status);
        Assert.Empty(await PostedCostsAsync(_factory, theirUser.TenantId, draft.Id));
    }

    [SqlServerFact]
    public async Task Two_simultaneous_approvals_make_exactly_one_approval_and_one_set_of_cost_entries()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/projects");
        await second.GetAsync("/api/projects");
        var tower = await NewProjectAsync(first, "Tower A");
        var warehouse = await NewProjectAsync(first, "Warehouse B");
        var worker = await HireAsync(first, "W-001");

        // Several rounds, each on its own day, so the race is run more than once. Each round posts two project costs of 400.00 and 200.00.
        foreach (var day in new[] { "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08" })
        {
            await RecordAsync(first, worker.Id, tower.Id, day, 4m);
            await RecordAsync(first, worker.Id, warehouse.Id, day, 2m);
            var draft = await CreateRunAsync(first, day, day);

            var responses = await Task.WhenAll(
                first.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version }),
                second.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version }));

            var codes = responses.Select(response => response.StatusCode).Order().ToArray();
            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);
            var posted = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
            Assert.Equal(2, posted.Count);
            Assert.Equal(60_000, posted.Sum(entry => entry.GrossMinor));
            Assert.Equal("Approved", (await first.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}")).Status);
        }
    }

    [SqlServerFact]
    public async Task Two_simultaneous_voids_release_the_attendance_and_reverse_the_costs_once()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/projects");
        await second.GetAsync("/api/projects");
        var project = await NewProjectAsync(first);
        var worker = await HireAsync(first, "W-001");
        await RecordAsync(first, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(first, "2026-10-05", "2026-10-05");
        var approved = await first.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });

        var responses = await Task.WhenAll(
            first.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = approved.Version }),
            second.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = approved.Version }));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(response => response.StatusCode).Order().ToArray());
        var posted = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
        Assert.Equal((1, CostEntryStatus.Voided), (posted.Count, posted[0].Status));
        Assert.Null((await first.GetAsync<PagedResult<AttendanceEntryDto>>("/api/attendance")).Items.Single().PayrollRunId);
    }

    [SqlServerFact]
    public async Task Two_simultaneous_payroll_runs_for_the_same_period_pay_the_attendance_only_once()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/projects");
        await second.GetAsync("/api/projects");
        var project = await NewProjectAsync(first);
        var worker = await HireAsync(first, "W-001");
        await RecordAsync(first, worker.Id, project.Id, "2026-10-05", 8m);
        var body = new { periodStart = "2026-10-01", periodEnd = "2026-10-15" };

        var responses = await Task.WhenAll(first.PostAsync("/api/payroll-runs", body), second.PostAsync("/api/payroll-runs", body));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(response => response.StatusCode).Order().ToArray());
        Assert.Equal(1, (await first.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs")).Total);
    }

    [SqlServerFact]
    public async Task One_payroll_form_submitted_twice_at_once_prepares_one_run_and_answers_both_with_it()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/projects");
        await second.GetAsync("/api/projects");
        var project = await NewProjectAsync(first);
        var worker = await HireAsync(first, "W-001");
        await RecordAsync(first, worker.Id, project.Id, "2026-10-05", 8m);
        var body = new { periodStart = "2026-10-01", periodEnd = "2026-10-15", clientRequestId = Guid.NewGuid().ToString("N") };

        var responses = await Task.WhenAll(first.PostAsync("/api/payroll-runs", body), second.PostAsync("/api/payroll-runs", body));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(async response => (await first.ReadAsync<PayrollRunDto>(response)).Id))).Distinct());
        Assert.Equal(1, (await first.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs")).Total);
    }

    [SqlServerFact]
    public async Task Reading_a_payroll_and_the_payroll_list_cost_the_same_number_of_statements_for_five_workers_as_for_forty()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var workers = new List<WorkerDetailDto>();
        for (var index = 1; index <= 40; index++) workers.Add(await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}"));

        async Task RecordCrewAsync(string date, IEnumerable<WorkerDetailDto> crew, Guid projectId) =>
            await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", new
            {
                projectId, workDate = date, entries = crew.Select(worker => new { workerId = worker.Id, regularHours = 8m, overtimeHours = 1m }).ToArray()
            });

        // 40 workers in the first half of September, split across two projects; 5 in the second half.
        await RecordCrewAsync("2026-09-02", workers.Take(20), tower.Id);
        await RecordCrewAsync("2026-09-02", workers.Skip(20), warehouse.Id);
        await RecordCrewAsync("2026-09-17", workers.Take(5), tower.Id);
        var large = await CreateRunAsync(client, "2026-09-01", "2026-09-15");
        var small = await CreateRunAsync(client, "2026-09-16", "2026-09-30");
        foreach (var worker in workers.Take(10))
            await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}/workers/{worker.Id}/deductions",
                new { version = (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}")).Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 100L } } });

        var smallRead = await _factory.Sql.MeasureAsync(async () => Assert.Equal(5, (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{small.Id}")).Lines.Count));
        var largeRead = await _factory.Sql.MeasureAsync(async () => Assert.Equal(40, (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}")).Lines.Count));
        var smallExport = await _factory.Sql.MeasureAsync(async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payroll-runs/{small.Id}/export")).StatusCode));
        var largeExport = await _factory.Sql.MeasureAsync(async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payroll-runs/{large.Id}/export")).StatusCode));
        var oneRun = await _factory.Sql.MeasureAsync(async () => Assert.Equal(2, (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs?pageSize=1&page=1")).Total));
        var bothRuns = await _factory.Sql.MeasureAsync(async () => Assert.Equal(2, (await client.GetAsync<PagedResult<PayrollRunListItemDto>>("/api/payroll-runs?pageSize=100")).Items.Count));

        Assert.Equal(smallRead, largeRead);
        Assert.Equal(smallExport, largeExport);
        Assert.Equal(oneRun, bothRuns);

        // Each worker earns 800.00 + 1 h overtime of 125.00 = 925.00 (92,500), so 40 workers earn 37,000.00 (3,700,000). The 40-worker run has 20
        // workers on each project, so approval posts two entries of 18,500.00 however many workers there are.
        var fresh = await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}");
        Assert.Equal(3_700_000, fresh.TotalGrossMinor);
        Assert.Equal(1_000, fresh.TotalDeductionsMinor);
        await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}/approve", new { version = fresh.Version });
        var posted = await PostedCostsAsync(_factory, user.TenantId, large.Id);
        Assert.Equal([1_850_000L, 1_850_000L], posted.Select(entry => entry.GrossMinor));
    }
}
