using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;

namespace ConstructOPX.Api.Tests.Features;

public sealed class ProjectsApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public ProjectsApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private async Task<(ApiClient Client, SeededUser User)> SignedInAsync(AccessRole roles = AccessRole.Administrator, Guid? tenantId = null)
    {
        var user = tenantId is null
            ? await TenantSeeder.CreateTenantAsync(_factory, roles)
            : await TenantSeeder.AddUserAsync(_factory, tenantId.Value, roles);
        var client = new ApiClient(_factory);
        await client.SignInAsync(user.Username, user.Password);
        return (client, user);
    }

    private static object NewProject(string name, long? price = 5_000_000_00) =>
        new { name, client = "ACME Realty", location = "Cebu City", contractPriceMinor = price, startDate = "2026-01-05", targetEndDate = (string?)null };

    [SqlServerFact]
    public async Task A_project_is_created_listed_read_and_updated()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;

        var created = await client.PostAsync<ProjectDto>("/api/projects", NewProject("Tower A"));
        Assert.Equal("Planning", created.Status);
        Assert.Equal(5_000_000_00, created.ContractPriceMinor);

        var list = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects");
        Assert.Contains(list.Items, item => item.Id == created.Id && item.SubProjectCount == 0);

        var updated = await client.PutAsync<ProjectDto>($"/api/projects/{created.Id}",
            new { name = "Tower A (Phase 1)", client = "ACME Realty", location = "Cebu City", contractPriceMinor = 5_500_000_00, startDate = "2026-01-05", targetEndDate = "2027-06-30", version = created.Version });
        Assert.Equal("Tower A (Phase 1)", updated.Name);
        Assert.True(updated.Version > created.Version);
        Assert.Equal("2027-06-30", updated.TargetEndDate!.Value.ToString("yyyy-MM-dd"));
    }

    [SqlServerFact]
    public async Task The_contract_price_is_shown_only_to_people_who_see_money()
    {
        var (admin, adminUser) = await SignedInAsync();
        using var _a = admin;
        var created = await admin.PostAsync<ProjectDto>("/api/projects", NewProject("Tower P"));

        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (client, _) = await SignedInAsync(role, adminUser.TenantId);
            using var _c = client;
            var list = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects");
            Assert.Null(Assert.Single(list.Items, item => item.Id == created.Id).ContractPriceMinor);
            Assert.Null((await client.GetAsync<ProjectDto>($"/api/projects/{created.Id}")).ContractPriceMinor);
        }

        var (viewer, _) = await SignedInAsync(AccessRole.Viewer, adminUser.TenantId);
        using var _v = viewer;
        Assert.Equal(5_000_000_00, (await viewer.GetAsync<ProjectDto>($"/api/projects/{created.Id}")).ContractPriceMinor);
    }

    [SqlServerFact]
    public async Task A_contract_price_beyond_the_limit_and_a_silly_page_number_are_handled_not_crashed()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;

        var tooBig = await client.PostAsync("/api/projects", NewProject("Tower Q", price: long.MaxValue));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooBig.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(tooBig)).GetProperty("errors").TryGetProperty("contractPriceMinor", out _));

        var page = await client.GetAsync($"/api/projects?page={int.MaxValue}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Empty((await client.ReadAsync<PagedResult<ProjectListItemDto>>(page)).Items);
    }

    [SqlServerFact]
    public async Task Invalid_input_reports_every_field_and_a_duplicate_name_is_refused()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;
        await client.PostAsync<ProjectDto>("/api/projects", NewProject("Warehouse B"));

        var invalid = await client.PostAsync("/api/projects", new { name = " ", contractPriceMinor = -5, startDate = "0001-01-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("contractPriceMinor", out _));
        Assert.True(errors.TryGetProperty("startDate", out _));

        var duplicate = await client.PostAsync("/api/projects", NewProject("  WAREHOUSE b "));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(duplicate)).GetProperty("errors").TryGetProperty("name", out _));
    }

    [SqlServerFact]
    public async Task Two_people_editing_the_same_project_cannot_overwrite_each_other()
    {
        var (client, user) = await SignedInAsync();
        using var _c = client;
        var created = await client.PostAsync<ProjectDto>("/api/projects", NewProject("Bridge Rehab"));
        await client.PutAsync<ProjectDto>($"/api/projects/{created.Id}",
            new { name = "Bridge Rehab", client = "DPWH", location = (string?)null, contractPriceMinor = (long?)null, startDate = "2026-01-05", targetEndDate = (string?)null, version = created.Version });

        var stale = await client.PutAsync($"/api/projects/{created.Id}",
            new { name = "Bridge Rehab v2", client = "DPWH", location = (string?)null, contractPriceMinor = (long?)null, startDate = "2026-01-05", targetEndDate = (string?)null, version = created.Version });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", (await ApiClient.ProblemAsync(stale)).GetProperty("code").GetString());
        Assert.Equal("Bridge Rehab", (await client.GetAsync<ProjectDto>($"/api/projects/{created.Id}")).Name);
    }

    [SqlServerFact]
    public async Task The_lifecycle_runs_start_hold_resume_complete_and_rejects_impossible_moves()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;
        var project = await client.PostAsync<ProjectDto>("/api/projects", NewProject("Clinic"));

        var hold = await client.PostAsync($"/api/projects/{project.Id}/hold", new { version = project.Version });
        Assert.Equal(HttpStatusCode.Conflict, hold.StatusCode);

        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/hold", new { version = project.Version });
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/resume", new { version = project.Version });
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/complete", new { version = project.Version });
        Assert.Equal("Completed", project.Status);
    }

    [SqlServerFact]
    public async Task Archiving_needs_a_reason_makes_the_project_read_only_and_restore_brings_it_back()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;
        var project = await client.PostAsync<ProjectDto>("/api/projects", NewProject("School Annex"));
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });

        var noNote = await client.PostAsync($"/api/projects/{project.Id}/archive", new { reason = "Other", note = " ", version = project.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noNote.StatusCode);

        var archived = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/archive", new { reason = "Finished", note = (string?)null, version = project.Version });
        Assert.Equal("Archived", archived.Status);
        Assert.NotNull(archived.ArchivedAtUtc);

        var edit = await client.PutAsync($"/api/projects/{project.Id}",
            new { name = "Renamed", client = (string?)null, location = (string?)null, contractPriceMinor = (long?)null, startDate = "2026-01-05", targetEndDate = (string?)null, version = archived.Version });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        var hiddenByDefault = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects");
        Assert.DoesNotContain(hiddenByDefault.Items, item => item.Id == project.Id);
        var withArchived = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?includeArchived=true");
        Assert.Contains(withArchived.Items, item => item.Id == project.Id);

        var restored = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/restore", new { version = archived.Version });
        Assert.Equal("Active", restored.Status);
        Assert.Null(restored.ArchivedAtUtc);
    }

    [SqlServerFact]
    public async Task Sub_projects_are_added_renamed_archived_and_unique_within_the_project()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;
        var project = await client.PostAsync<ProjectDto>("/api/projects", NewProject("Mall"));

        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects", new { name = "Building 1", startDate = "2026-02-01", version = project.Version });
        var sub = Assert.Single(project.SubProjects);
        var duplicate = await client.PostAsync($"/api/projects/{project.Id}/sub-projects", new { name = "building 1", startDate = "2026-02-01", version = project.Version });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        project = await client.PutAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects/{sub.Id}", new { name = "Building One", startDate = "2026-02-15", version = project.Version });
        Assert.Equal("Building One", project.SubProjects[0].Name);
        project = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects/{sub.Id}/archive", new { version = project.Version });
        Assert.Equal("Archived", project.SubProjects[0].Status);

        var lookup = await client.GetAsync<List<ProjectLookupDto>>("/api/projects/lookup");
        Assert.Contains(lookup, item => item.Id == project.Id && item.SubProjects.Count == 1);
    }

    [SqlServerFact]
    public async Task The_list_pages_filters_and_takes_search_text_literally()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;
        foreach (var name in new[] { "Alpha 50% Phase", "Alpha Plaza", "Beta Tower", "Gamma [Block]" })
            await client.PostAsync<ProjectDto>("/api/projects", NewProject(name));

        var page1 = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?pageSize=2&page=1");
        var page2 = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?pageSize=2&page=2");
        Assert.Equal(4, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(item => item.Id).Intersect(page2.Items.Select(item => item.Id)));

        var alpha = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?search=alpha");
        Assert.Equal(2, alpha.Total);
        var percent = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?search=50%25");
        Assert.Equal(["Alpha 50% Phase"], percent.Items.Select(item => item.Name));
        var bracket = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?search=%5BBlock");
        Assert.Equal(["Gamma [Block]"], bracket.Items.Select(item => item.Name));
        var wildcard = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects?search=%25");
        Assert.Equal(1, wildcard.Total);
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_projects()
    {
        var (mine, _) = await SignedInAsync();
        var (theirs, _) = await SignedInAsync();
        using var _a = mine;
        using var _b = theirs;
        var project = await mine.PostAsync<ProjectDto>("/api/projects", NewProject("Secret Project"));

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/projects/{project.Id}/start", new { version = project.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync($"/api/projects/{project.Id}",
            new { name = "Hijacked", client = (string?)null, location = (string?)null, contractPriceMinor = (long?)null, startDate = "2026-01-05", targetEndDate = (string?)null, version = project.Version })).StatusCode);
        Assert.Empty((await theirs.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects")).Items);
        Assert.Empty(await theirs.GetAsync<List<ProjectLookupDto>>("/api/projects/lookup"));

        // The same name is free in the other company: names are unique per company, not across the system.
        await theirs.PostAsync<ProjectDto>("/api/projects", NewProject("Secret Project"));
    }

    [SqlServerFact]
    public async Task Roles_decide_who_can_read_and_who_can_change_projects()
    {
        var (admin, adminUser) = await SignedInAsync();
        using var _a = admin;
        var project = await admin.PostAsync<ProjectDto>("/api/projects", NewProject("Role Check"));
        var (viewer, _) = await SignedInAsync(AccessRole.Viewer, adminUser.TenantId);
        var (hr, _) = await SignedInAsync(AccessRole.HrOfficer, adminUser.TenantId);
        var (manager, _) = await SignedInAsync(AccessRole.ProjectManager, adminUser.TenantId);
        using var _v = viewer;
        using var _h = hr;
        using var _m = manager;

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/projects", NewProject("Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync($"/api/projects/{project.Id}/start", new { version = project.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/projects/{project.Id}/start", new { version = project.Version })).StatusCode);
    }

    [SqlServerFact]
    public async Task Changes_are_written_to_the_audit_trail_that_only_administrators_and_accountants_can_read()
    {
        var (admin, adminUser) = await SignedInAsync();
        using var _a = admin;
        var project = await admin.PostAsync<ProjectDto>("/api/projects", NewProject("Audit Me"));
        await admin.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });

        var trail = await admin.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=Project&entityId={project.Id}");
        Assert.Contains(trail.Items, entry => entry.Action == "Project.Created" && entry.Summary.Contains("Audit Me"));
        Assert.Contains(trail.Items, entry => entry.Action == "Project.Started");
        Assert.All(trail.Items, entry => Assert.Equal(adminUser.Username, entry.UserName));

        var (viewer, _) = await SignedInAsync(AccessRole.Viewer, adminUser.TenantId);
        using var _v = viewer;
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/audit")).StatusCode);
    }

    [SqlServerFact]
    public async Task Errors_carry_a_trace_id_and_never_internal_details()
    {
        var (client, _) = await SignedInAsync();
        using var _c = client;

        var missing = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var problem = await ApiClient.ProblemAsync(missing);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("Exception", problem.GetRawText());
        Assert.DoesNotContain("SELECT", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(missing.Headers.Contains("X-Correlation-Id"));
    }
}
