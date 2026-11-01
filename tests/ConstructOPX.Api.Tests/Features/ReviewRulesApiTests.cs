using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Scenarios an independent review found open, run through the real API.</summary>
public sealed class ReviewRulesApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public ReviewRulesApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> ChangeRateAsync(ApiClient client, WorkerDetailDto worker, string effectiveFrom, long rateMinor = 96_000) =>
        await client.PostAsync($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor, rateUnit = "Daily", effectiveFrom, version = worker.Version
        });

    [SqlServerFact]
    public async Task A_rate_can_not_start_on_a_day_a_payroll_run_has_already_paid_at_the_old_rate()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001", rateMinor: 80_000), project);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-12", 8m);
        await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        // 8 Oct is before the last paid day (12 Oct), and 12 Oct is that day itself: either would disagree with the pay in the run.
        foreach (var day in new[] { "2026-10-08", "2026-10-12" })
        {
            var refused = await ChangeRateAsync(client, worker, day);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.True((await ApiClient.ProblemAsync(refused)).GetProperty("errors").TryGetProperty("effectiveFrom", out _));
        }
        Assert.Equal(80_000, (await client.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}")).CurrentRateMinor);

        // From the day after the last paid day it is fine.
        var accepted = await client.ReadAsync<WorkerDetailDto>(await ChangeRateAsync(client, worker, "2026-10-13"));
        Assert.Equal(96_000, accepted.CurrentRateMinor);
    }

    [SqlServerFact]
    public async Task Voiding_an_approved_payroll_is_refused_while_a_project_it_charged_is_archived()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var worker = await AssignAsync(client, await HireAsync(client, "W-001"), tower);
        await RecordAsync(client, worker.Id, tower.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        var approved = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });
        var current = await client.GetAsync<ProjectDto>($"/api/projects/{tower.Id}");
        var archived = await client.PostAsync<ProjectDto>($"/api/projects/{tower.Id}/archive", new { reason = "Finished", note = (string?)null, version = current.Version });

        var refused = await client.PostAsync($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = approved.Version });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var detail = (await ApiClient.ProblemAsync(refused)).GetProperty("detail").GetString();
        Assert.Contains("Tower A", detail);
        Assert.Contains("Restore it", detail);
        Assert.Equal("Approved", (await client.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}")).Status);

        await client.PostAsync<ProjectDto>($"/api/projects/{tower.Id}/restore", new { version = archived.Version });
        Assert.Equal("Voided", (await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/void", new { reason = "Wrong period", version = approved.Version })).Status);
    }

    [SqlServerFact]
    public async Task Removing_an_attendance_entry_is_refused_while_its_project_is_archived()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var worker = await AssignAsync(client, await HireAsync(client, "W-001"), tower);
        var entry = await RecordAsync(client, worker.Id, tower.Id, "2026-10-05", 8m);
        var current = await client.GetAsync<ProjectDto>($"/api/projects/{tower.Id}");
        await client.PostAsync<ProjectDto>($"/api/projects/{tower.Id}/archive", new { reason = "Finished", note = (string?)null, version = current.Version });

        var refused = await client.PostAsync($"/api/attendance/{entry.Id}/void", new { reason = "Entered twice", version = entry.Version });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("archived", (await ApiClient.ProblemAsync(refused)).GetProperty("detail").GetString());

        // Nothing changed: once the project is restored the entry can be removed as usual.
        var archived = await client.GetAsync<ProjectDto>($"/api/projects/{tower.Id}");
        await client.PostAsync<ProjectDto>($"/api/projects/{tower.Id}/restore", new { version = archived.Version });
        Assert.Equal("Voided", (await client.PostAsync<AttendanceEntryDto>($"/api/attendance/{entry.Id}/void", new { reason = "Entered twice", version = entry.Version })).Status);
    }

    [SqlServerFact]
    public async Task A_request_id_reused_for_a_different_worker_or_day_is_refused_not_answered_with_the_old_entry()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var juan = await AssignAsync(client, await HireAsync(client, "W-001"), project);
        var ana = await AssignAsync(client, await HireAsync(client, "W-002", first: "Ana", last: "Abad"), project);
        var requestId = Guid.NewGuid().ToString("N");
        var first = await RecordAsync(client, juan.Id, project.Id, "2026-10-05", 8m, clientRequestId: requestId);

        var same = await RecordAsync(client, juan.Id, project.Id, "2026-10-05", 8m, clientRequestId: requestId);
        Assert.Equal(first.Id, same.Id);

        var reused = await client.PostAsync("/api/attendance", new
        {
            workerId = ana.Id, projectId = project.Id, subProjectId = (Guid?)null, workDate = "2026-10-05", regularHours = 8m, overtimeHours = 0m,
            remarks = (string?)null, clientRequestId = requestId
        });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }
}
