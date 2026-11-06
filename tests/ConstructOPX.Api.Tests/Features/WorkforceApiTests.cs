using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

public sealed class WorkforceApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public WorkforceApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_worker_is_hired_with_a_first_pay_line_then_listed_read_and_found_in_the_lookup()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var response = await client.PostAsync("/api/workers", new
        {
            employeeNumber = "W-001", firstName = " Juan ", middleName = "Santos", lastName = "Dela Cruz", dateHired = HireDate,
            designation = "Mason", rateMinor = 80_000L, rateUnit = "Daily", clientRequestId = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hired = await client.ReadAsync<WorkerDetailDto>(response);
        Assert.Equal($"/api/workers/{hired.Id}", response.Headers.Location!.ToString());
        Assert.Equal("Juan Santos Dela Cruz", hired.FullName);
        Assert.Equal("Active", hired.Status);
        Assert.Equal("Mason", hired.Designation);
        Assert.Equal(80_000, hired.CurrentRateMinor);
        Assert.Equal("Daily", hired.CurrentRateUnit);
        var first = Assert.Single(hired.Rates);
        Assert.Equal("Initial", first.Category);
        Assert.Equal(new DateOnly(2026, 1, 5), first.EffectiveFrom);
        Assert.Null(hired.CurrentAssignment);

        var list = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers");
        var item = Assert.Single(list.Items);
        Assert.Equal(("W-001", "Juan Santos Dela Cruz", "Active", "Mason"), (item.EmployeeNumber, item.FullName, item.Status, item.Designation));
        Assert.Null(item.CurrentProjectName);

        var lookup = await client.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup");
        Assert.Equal([hired.Id], lookup.Select(worker => worker.Id));
        Assert.Equal(hired.Id, (await client.GetAsync<WorkerDetailDto>($"/api/workers/{hired.Id}")).Id);
    }

    [SqlServerFact]
    public async Task Hiring_reports_every_field_that_needs_attention()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var invalid = await client.PostAsync("/api/workers", new
        {
            employeeNumber = " ", firstName = "", lastName = new string('x', 81), dateHired = "0001-01-01", designation = " ", rateMinor = 0L, rateUnit = "Weekly"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        foreach (var field in new[] { "employeeNumber", "firstName", "lastName", "dateHired", "designation", "rateMinor", "rateUnit" })
            Assert.True(errors.TryGetProperty(field, out _), $"Expected an error for {field}.");

        var future = await client.PostAsync("/api/workers", new
        {
            employeeNumber = "W-9", firstName = "A", lastName = "B", dateHired = "2999-01-01", designation = "Mason", rateMinor = 1L, rateUnit = "Daily"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(future)).GetProperty("errors").TryGetProperty("dateHired", out _));
    }

    [SqlServerFact]
    public async Task An_employee_number_is_unique_within_a_company_but_free_in_another()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _a = mine;
        using var _b = theirs;
        await HireAsync(mine, "W-001");

        var duplicate = await mine.PostAsync("/api/workers", new
        {
            employeeNumber = "  w-001 ", firstName = "Ana", lastName = "Abad", dateHired = HireDate, designation = "Helper", rateMinor = 60_000L, rateUnit = "Daily"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(duplicate)).GetProperty("errors").TryGetProperty("employeeNumber", out _));
        Assert.Equal(1, (await mine.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers")).Total);
        await HireAsync(theirs, "W-001");
    }

    [SqlServerFact]
    public async Task Hiring_twice_from_one_form_submission_returns_the_same_worker()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var requestId = Guid.NewGuid().ToString("N");

        var first = await HireAsync(client, "W-001", clientRequestId: requestId);
        var again = await HireAsync(client, "W-001", clientRequestId: requestId);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, (await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers")).Total);
        Assert.Single(await AuditSummariesAsync(_factory, user.TenantId), line => line.StartsWith("Hired ", StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task Two_simultaneous_submissions_of_one_hire_form_hire_a_single_worker_and_both_get_that_worker()
    {
        var (first, user) = await SignedInAsync(_factory);
        using var _a = first;
        var second = new ApiClient(_factory);
        using var _b = second;
        await second.SignInAsync(user.Username, user.Password);
        await first.GetAsync("/api/workers");
        await second.GetAsync("/api/workers");
        var form = new
        {
            employeeNumber = "W-001", firstName = "Juan", lastName = "Dela Cruz", dateHired = HireDate, designation = "Mason", rateMinor = 80_000L, rateUnit = "Daily",
            clientRequestId = Guid.NewGuid().ToString("N")
        };

        var responses = await Task.WhenAll(first.PostAsync("/api/workers", form), second.PostAsync("/api/workers", form));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var ids = await Task.WhenAll(responses.Select(async response => (await first.ReadAsync<WorkerDetailDto>(response)).Id));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, (await first.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers")).Total);
    }

    [SqlServerFact]
    public async Task The_profile_is_updated_and_a_stale_version_is_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var worker = await HireAsync(client, "W-001");

        var updated = await client.PutAsync<WorkerDetailDto>($"/api/workers/{worker.Id}", new
        {
            firstName = "Juan", middleName = "Santos", lastName = "Dela Cruz", dateOfBirth = "1990-04-02", maritalStatus = "Married", spouseName = "Maria Dela Cruz",
            religion = "Catholic", educationalAttainment = "High school", presentAddress = "Mandaue City", provincialAddress = "Bohol", contactNumber = "0917 000 0000",
            reference = "Referred by the foreman", version = worker.Version
        });
        Assert.Equal("Juan Santos Dela Cruz", updated.FullName);
        Assert.Equal(new DateOnly(1990, 4, 2), updated.DateOfBirth);
        Assert.Equal("Mandaue City", updated.PresentAddress);
        Assert.True(updated.Version > worker.Version);

        var stale = await client.PutAsync($"/api/workers/{worker.Id}", new { firstName = "Other", lastName = "Name", version = worker.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", (await ApiClient.ProblemAsync(stale)).GetProperty("code").GetString());

        var invalid = await client.PutAsync($"/api/workers/{worker.Id}", new { firstName = " ", lastName = "", dateOfBirth = "2999-01-01", version = updated.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        foreach (var field in new[] { "firstName", "lastName", "dateOfBirth" }) Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [SqlServerFact]
    public async Task A_promotion_adds_to_pay_history_and_the_audit_line_names_no_amount()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var worker = await HireAsync(client, "W-001");

        var promoted = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "Promotion", designation = "Foreman", rateMinor = 120_000L, rateUnit = "Daily", effectiveFrom = "2026-10-10", reason = "Leads the formwork crew",
            version = worker.Version
        });

        Assert.Equal("Foreman", promoted.Designation);
        Assert.Equal(120_000, promoted.CurrentRateMinor);
        Assert.Equal(["Promotion", "Initial"], promoted.Rates.Select(rate => rate.Category));
        var audit = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=Worker&entityId={worker.Id}");
        var line = Assert.Single(audit.Items, entry => entry.Action == "Worker.RateChanged");
        Assert.Equal("Changed pay rate for Juan Dela Cruz (promotion from Mason to Foreman) effective 10 Oct 2026.", line.Summary);
        AssertNoPayFigures(await AuditSummariesAsync(_factory, user.TenantId));
    }

    [SqlServerFact]
    public async Task Pay_history_is_never_rewritten_and_the_category_must_match_the_change()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var worker = await HireAsync(client, "W-001");
        var raised = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor = 96_000L, rateUnit = "Daily", effectiveFrom = "2026-10-10", reason = (string?)null, version = worker.Version
        });

        var earlier = await client.PostAsync("/api/workers/" + worker.Id + "/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor = 99_000L, rateUnit = "Daily", effectiveFrom = "2026-06-01", version = raised.Version
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlier.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(earlier)).GetProperty("errors").TryGetProperty("effectiveFrom", out _));

        var wrongCategory = await client.PostAsync("/api/workers/" + worker.Id + "/change-rate", new
        {
            category = "Promotion", designation = "Mason", rateMinor = 99_000L, rateUnit = "Daily", effectiveFrom = "2026-10-20", version = raised.Version
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCategory.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(wrongCategory)).GetProperty("errors").TryGetProperty("designation", out _));

        var unknown = await client.PostAsync("/api/workers/" + worker.Id + "/change-rate", new
        {
            category = "Bonus", designation = " ", rateMinor = 0L, rateUnit = "Weekly", effectiveFrom = "2026-10-20", version = raised.Version
        });
        var errors = (await ApiClient.ProblemAsync(unknown)).GetProperty("errors");
        foreach (var field in new[] { "category", "designation", "rateMinor", "rateUnit" }) Assert.True(errors.TryGetProperty(field, out _), field);

        var stale = await client.PostAsync("/api/workers/" + worker.Id + "/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor = 99_000L, rateUnit = "Daily", effectiveFrom = "2026-10-20", version = worker.Version
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(2, (await client.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}")).Rates.Count);
    }

    [SqlServerFact]
    public async Task A_worker_is_assigned_moved_and_released_and_the_history_keeps_project_names()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var worker = await HireAsync(client, "W-001");

        var onTower = await AssignAsync(client, worker, tower, "2026-02-01");
        Assert.Equal("Tower A", onTower.CurrentAssignment!.ProjectName);

        var again = await client.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = tower.Id, startDate = "2026-03-01", version = onTower.Version });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var moved = await AssignAsync(client, onTower, warehouse, "2026-06-01");
        Assert.Equal("Warehouse B", moved.CurrentAssignment!.ProjectName);
        Assert.Equal(["Warehouse B", "Tower A"], moved.Assignments.Select(item => item.ProjectName));
        Assert.Equal(new DateOnly(2026, 6, 1), moved.Assignments[1].EndDate);

        var released = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/release", new { endDate = "2026-09-01", version = moved.Version });
        Assert.Null(released.CurrentAssignment);
        Assert.All(released.Assignments, item => Assert.False(item.IsCurrent));
        var releaseAgain = await client.PostAsync($"/api/workers/{worker.Id}/release", new { endDate = "2026-09-02", version = released.Version });
        Assert.Equal(HttpStatusCode.Conflict, releaseAgain.StatusCode);

        var past = await client.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = tower.Id, startDate = "2025-12-31", version = released.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(past)).GetProperty("errors").TryGetProperty("startDate", out _));

        var stale = await client.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = tower.Id, startDate = "2026-09-10", version = worker.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [SqlServerFact]
    public async Task An_archived_project_or_a_suspended_worker_can_not_be_assigned()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client, "Old Mall");
        var worker = await HireAsync(client, "W-001");
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/archive", new { reason = "Finished", note = (string?)null, version = project.Version });

        var onArchived = await client.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = project.Id, startDate = "2026-02-01", version = worker.Version });
        Assert.Equal(HttpStatusCode.Conflict, onArchived.StatusCode);

        var live = await NewProjectAsync(client, "Live Mall");
        var suspended = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/status",
            new { status = "Suspended", effectiveDate = "2026-05-01", reason = "Safety breach", version = worker.Version });
        var blocked = await client.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = live.Id, startDate = "2026-06-01", version = suspended.Version });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [SqlServerFact]
    public async Task A_crew_is_assigned_in_one_step_or_not_at_all_and_the_refusal_lists_each_worker_and_why()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var warehouse = await NewProjectAsync(client, "Warehouse B");
        var juan = await HireAsync(client, "W-001", "Juan", "Dela Cruz");
        var ana = await HireAsync(client, "W-002", "Ana", "Abad");
        var pedro = await HireAsync(client, "W-003", "Pedro", "Reyes");
        var left = await HireAsync(client, "W-004", "Lito", "Santos");
        left = await client.PostAsync<WorkerDetailDto>($"/api/workers/{left.Id}/status", new { status = "Resigned", effectiveDate = "2026-06-30", reason = (string?)null, version = left.Version });
        pedro = await AssignAsync(client, pedro, tower, "2026-02-01");

        // Pedro is already on Tower A and Lito has resigned, so nobody is posted, not even Juan and Ana, who could be.
        var refused = await client.PostAsync("/api/workers/assign", new
        {
            workerIds = new[] { juan.Id, ana.Id, pedro.Id, left.Id }, projectId = tower.Id, subProjectId = (Guid?)null, startDate = "2026-08-01"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problems = (await ApiClient.ProblemAsync(refused)).GetProperty("errors").GetProperty("workers").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(2, problems.Length);
        Assert.Contains(problems, text => text.Contains("Pedro Reyes") && text.Contains("already assigned"));
        Assert.Contains(problems, text => text.Contains("Lito Santos") && text.Contains("resigned"));
        Assert.Null((await client.GetAsync<WorkerDetailDto>($"/api/workers/{juan.Id}")).CurrentAssignment);
        Assert.Null((await client.GetAsync<WorkerDetailDto>($"/api/workers/{ana.Id}")).CurrentAssignment);

        // Without the two who can't be posted, the crew is posted together.
        var assigned = await client.PostAsync<List<WorkerListItemDto>>("/api/workers/assign", new
        {
            workerIds = new[] { juan.Id, ana.Id }, projectId = warehouse.Id, subProjectId = (Guid?)null, startDate = "2026-08-01"
        });
        Assert.Equal(["Ana Abad", "Juan Dela Cruz"], assigned.Select(item => item.FullName));
        Assert.All(assigned, item => Assert.Equal("Warehouse B", item.CurrentProjectName));
        var onWarehouse = await client.GetAsync<PagedResult<WorkerListItemDto>>($"/api/workers?projectId={warehouse.Id}");
        Assert.Equal(2, onWarehouse.Total);

        var none = await client.PostAsync("/api/workers/assign", new { workerIds = Array.Empty<Guid>(), projectId = warehouse.Id, startDate = "2026-08-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, none.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(none)).GetProperty("errors").TryGetProperty("workerIds", out _));
        var missing = await client.PostAsync("/api/workers/assign", new { workerIds = new[] { Guid.NewGuid() }, projectId = warehouse.Id, startDate = "2026-08-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
    }

    [SqlServerFact]
    public async Task Status_changes_need_a_reason_where_it_matters_and_a_worker_who_left_can_be_reinstated()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001"), project, "2026-02-01");

        var noReason = await client.PostAsync($"/api/workers/{worker.Id}/status", new { status = "Suspended", effectiveDate = "2026-05-01", reason = " ", version = worker.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(noReason)).GetProperty("errors").TryGetProperty("reason", out _));
        var badStatus = await client.PostAsync($"/api/workers/{worker.Id}/status", new { status = "Gone", effectiveDate = "2026-05-01", version = worker.Version });
        Assert.True((await ApiClient.ProblemAsync(badStatus)).GetProperty("errors").TryGetProperty("status", out _));

        var left = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/status",
            new { status = "Terminated", effectiveDate = "2026-05-01", reason = "Repeated absences", version = worker.Version });
        Assert.Equal("Terminated", left.Status);
        Assert.Null(left.CurrentAssignment);
        Assert.Empty((await client.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup")));
        Assert.Single(await client.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup?includeLeft=true"));

        var tooEarly = await client.PostAsync($"/api/workers/{worker.Id}/reinstate", new { effectiveDate = "2026-06-01", reason = "", version = left.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly.StatusCode);
        var back = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/reinstate", new { effectiveDate = "2026-06-01", reason = "Appeal upheld", version = left.Version });
        Assert.Equal("Active", back.Status);
        var notLeft = await client.PostAsync($"/api/workers/{worker.Id}/reinstate", new { effectiveDate = "2026-06-02", reason = "again", version = back.Version });
        Assert.Equal(HttpStatusCode.Conflict, notLeft.StatusCode);
    }

    [SqlServerFact]
    public async Task Cases_are_added_with_an_optional_project_and_never_changed()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client, "Tower A");
        var worker = await HireAsync(client, "W-001");

        var recorded = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/cases",
            new { title = "Absent without notice", remarks = "Warned verbally", caseDate = "2026-10-12", projectId = project.Id, version = worker.Version });

        var workerCase = Assert.Single(recorded.Cases);
        Assert.Equal(("Absent without notice", "Tower A"), (workerCase.Title, workerCase.ProjectName));

        var invalid = await client.PostAsync($"/api/workers/{worker.Id}/cases", new { title = "", caseDate = "2025-01-01", version = recorded.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("caseDate", out _));
        var unknownProject = await client.PostAsync($"/api/workers/{worker.Id}/cases", new { title = "Late", caseDate = "2026-10-12", projectId = Guid.NewGuid(), version = recorded.Version });
        Assert.Equal(HttpStatusCode.NotFound, unknownProject.StatusCode);
    }

    [SqlServerFact]
    public async Task The_list_pages_filters_and_takes_search_text_literally()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var tower = await NewProjectAsync(client, "Tower A");
        var juan = await AssignAsync(client, await HireAsync(client, "W-001", "Juan", "Dela Cruz"), tower, "2026-02-01");
        await HireAsync(client, "W-002", "Ana", "Abad", "Helper");
        await HireAsync(client, "W-50%", "Pedro", "Reyes", "Carpenter");
        var left = await HireAsync(client, "W-004", "Lito", "Santos");
        await client.PostAsync<WorkerDetailDto>($"/api/workers/{left.Id}/status", new { status = "Resigned", effectiveDate = "2026-06-30", reason = (string?)null, version = left.Version });

        var page1 = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?pageSize=2&page=1");
        var page2 = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?pageSize=2&page=2");
        Assert.Equal(4, page1.Total);
        Assert.Equal(["Ana Abad", "Juan Dela Cruz"], page1.Items.Select(item => item.FullName));
        Assert.Equal(["Pedro Reyes", "Lito Santos"], page2.Items.Select(item => item.FullName));

        var byNumber = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?search=w-002");
        Assert.Equal(["Ana Abad"], byNumber.Items.Select(item => item.FullName));
        var byFullName = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?search=juan%20dela");
        Assert.Equal(["Juan Dela Cruz"], byFullName.Items.Select(item => item.FullName));
        var percent = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?search=50%25");
        Assert.Equal(["Pedro Reyes"], percent.Items.Select(item => item.FullName));
        var wildcard = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?search=%25");
        Assert.Equal(1, wildcard.Total);

        var resigned = await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?status=Resigned");
        Assert.Equal(["Lito Santos"], resigned.Items.Select(item => item.FullName));
        var onTower = await client.GetAsync<PagedResult<WorkerListItemDto>>($"/api/workers?projectId={tower.Id}");
        var member = Assert.Single(onTower.Items);
        Assert.Equal((juan.Id, "Tower A", "Mason"), (member.Id, member.CurrentProjectName, member.Designation));
    }

    [SqlServerFact]
    public async Task Pay_is_visible_only_to_people_who_hold_payroll_read()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var worker = await HireAsync(admin, "W-001");
        worker = await admin.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "Promotion", designation = "Foreman", rateMinor = 120_000L, rateUnit = "Daily", effectiveFrom = "2026-10-10", version = worker.Version
        });
        worker = await AssignAsync(admin, worker, project, "2026-02-01");

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.Viewer })
        {
            var (reader, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _r = reader;
            var raw = await RawAsync(reader, $"/api/workers/{worker.Id}");
            AssertNoPayFields(raw);
            // They still see who the worker is, what the job is and where the worker is posted, but no rate and no pay history.
            Assert.Equal("Foreman", raw.GetProperty("designation").GetString());
            Assert.Equal("Tower A", raw.GetProperty("currentAssignment").GetProperty("projectName").GetString());
            Assert.Equal(0, raw.GetProperty("rates").GetArrayLength());
            AssertNoPayFields(await RawAsync(reader, "/api/workers"));
            AssertNoPayFields(await RawAsync(reader, "/api/workers/lookup"));
        }

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.Accountant })
        {
            var (reader, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _r = reader;
            var seen = await reader.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}");
            Assert.Equal(120_000, seen.CurrentRateMinor);
            Assert.Equal(2, seen.Rates.Count);
        }
    }

    [SqlServerFact]
    public async Task Hiring_and_pay_changes_need_workforce_manage_and_reading_needs_workforce_read()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var worker = await HireAsync(admin, "W-001");
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, adminUser.TenantId);
        var (warehouse, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, adminUser.TenantId);
        using var _m = manager;
        using var _v = viewer;
        using var _h = hr;
        using var _w = warehouse;
        var hire = new { employeeNumber = "W-002", firstName = "Ana", lastName = "Abad", dateHired = HireDate, designation = "Helper", rateMinor = 60_000L, rateUnit = "Daily" };
        var rateChange = new { category = "RateAdjustment", designation = "Mason", rateMinor = 90_000L, rateUnit = "Daily", effectiveFrom = "2026-10-10", version = worker.Version };

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/workers", hire)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/workers/{worker.Id}/change-rate", rateChange)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/workers", hire)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/workers/assign", new { workerIds = new[] { worker.Id }, projectId = Guid.NewGuid(), startDate = "2026-08-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/api/workers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/workers/{worker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/workers/lookup")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await hr.PostAsync("/api/workers", hire)).StatusCode);
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_workers()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _a = mine;
        using var _b = theirs;
        var project = await NewProjectAsync(mine);
        var worker = await HireAsync(mine, "W-001");
        var theirProject = await NewProjectAsync(theirs, "Their Tower");
        var theirWorker = await HireAsync(theirs, "W-777");

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/workers/{worker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync($"/api/workers/{worker.Id}", new { firstName = "X", lastName = "Y", version = worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/workers/{worker.Id}/change-rate",
            new { category = "RateAdjustment", designation = "Mason", rateMinor = 1L, rateUnit = "Daily", effectiveFrom = "2026-10-10", version = worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/workers/{worker.Id}/assign", new { projectId = theirProject.Id, startDate = "2026-08-01", version = worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/workers/{worker.Id}/status", new { status = "Resigned", effectiveDate = "2026-08-01", version = worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/workers/{worker.Id}/cases", new { title = "Late", caseDate = "2026-08-01", version = worker.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/workers/{theirWorker.Id}/assign", new { projectId = project.Id, startDate = "2026-08-01", version = theirWorker.Version })).StatusCode);
        var bulk = await theirs.PostAsync("/api/workers/assign", new { workerIds = new[] { worker.Id }, projectId = theirProject.Id, startDate = "2026-08-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bulk.StatusCode);
        Assert.Equal(["W-777"], (await theirs.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers")).Items.Select(item => item.EmployeeNumber));
        Assert.DoesNotContain(await theirs.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup"), item => item.Id == worker.Id);
        Assert.Null((await mine.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}")).CurrentAssignment);
    }

    [SqlServerFact]
    public async Task Every_change_is_audited_and_no_audit_line_carries_a_pay_figure()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        var worker = await HireAsync(client, "W-001", rateMinor: 123_456);
        worker = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/change-rate", new
        {
            category = "RateAdjustment", designation = "Mason", rateMinor = 654_321L, rateUnit = "Daily", effectiveFrom = "2026-10-10", reason = "Raised to 6,543.21 a day", version = worker.Version
        });
        worker = await AssignAsync(client, worker, project, "2026-10-11");
        worker = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/release", new { endDate = "2026-10-20", version = worker.Version });
        worker = await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/cases", new { title = "Late", caseDate = "2026-10-12", version = worker.Version });
        await client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/status", new { status = "Resigned", effectiveDate = "2026-10-25", reason = (string?)null, version = worker.Version });

        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=Worker&entityId={worker.Id}&pageSize=50");
        var actions = trail.Items.Select(entry => entry.Action).ToHashSet();
        foreach (var action in new[] { "Worker.Hired", "Worker.RateChanged", "Worker.Assigned", "Worker.Released", "Worker.CaseRecorded", "Worker.StatusChanged" })
            Assert.Contains(action, actions);
        AssertNoPayFigures(await AuditSummariesAsync(_factory, user.TenantId));
        Assert.DoesNotContain(trail.Items, entry => entry.Summary.Contains("6,543"));
    }

    [SqlServerFact]
    public async Task The_worker_list_costs_the_same_number_of_statements_for_five_workers_as_for_forty()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await NewProjectAsync(client);
        for (var index = 1; index <= 5; index++)
            await AssignAsync(client, await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}"), project);
        var five = await _factory.Sql.MeasureAsync(async () => Assert.Equal(5, (await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?pageSize=100")).Items.Count));
        var fiveLookup = await _factory.Sql.MeasureAsync(async () => Assert.Equal(5, (await client.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup")).Count));

        for (var index = 6; index <= 40; index++)
            await AssignAsync(client, await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}"), project);
        var forty = await _factory.Sql.MeasureAsync(async () => Assert.Equal(40, (await client.GetAsync<PagedResult<WorkerListItemDto>>("/api/workers?pageSize=100")).Items.Count));
        var fortyLookup = await _factory.Sql.MeasureAsync(async () => Assert.Equal(40, (await client.GetAsync<List<WorkerLookupDto>>("/api/workers/lookup")).Count));

        Assert.Equal(five, forty);
        Assert.Equal(fiveLookup, fortyLookup);
    }
}
