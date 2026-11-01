using System.Net;
using System.Text.RegularExpressions;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Audit;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Projects;
using Cms.Application.Common;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.CostingTestKit;

namespace Cms.Api.Tests.Features;

public sealed class CostingBudgetLineApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public CostingBudgetLineApiTests(CmsApiFactory factory) => _factory = factory;

    private Task<(ApiClient Client, SeededUser User)> AdminAsync() => SignedInAsync(_factory);

    [SqlServerFact]
    public async Task Money_is_exact_for_inclusive_exclusive_and_exempt_lines_at_the_company_rate_and_every_figure_is_returned()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var phase = (await AddSubProjectAsync(client, project, "Phase 1")).SubProjects.Single();

        // Inclusive at 12%: 10 x 500.00 = 5,000.00 with VAT in it. Net = 5,000.00 / 1.12 = 4,464.2857 -> 4,464.29, VAT = 535.71.
        var inclusive = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines",
            Line("Ready-mix concrete", quantity: 10m, unit: "m3", unitCostMinor: 50_000, vatTreatment: "Inclusive", wbsCode: "3300",
                subProjectId: phase.Id, remarks: "Grade 4000 psi"));
        Assert.Equal(446_429, inclusive.NetMinor);
        Assert.Equal(53_571, inclusive.VatMinor);
        Assert.Equal(500_000, inclusive.GrossMinor);
        Assert.Equal(1_200, inclusive.VatRateBps);

        // Every stored figure and name comes back.
        Assert.Equal(project.Id, inclusive.ProjectId);
        Assert.Equal(phase.Id, inclusive.SubProjectId);
        Assert.Equal("Phase 1", inclusive.SubProjectName);
        Assert.Equal(3, inclusive.Division);
        Assert.Equal("Concrete", inclusive.MainScope);
        Assert.Equal("Cast-In-Place Concrete", inclusive.SubScope);
        Assert.Equal("3300", inclusive.WbsCode);
        Assert.Equal("Ready-mix concrete", inclusive.Description);
        Assert.Equal("Material", inclusive.ItemType);
        Assert.Equal(10m, inclusive.Quantity);
        Assert.Equal("m³", inclusive.Unit);
        Assert.Equal(50_000, inclusive.UnitCostMinor);
        Assert.Equal("Inclusive", inclusive.VatTreatment);
        Assert.Equal(new DateOnly(2026, 2, 1), inclusive.EntryDate);
        Assert.Equal("Grade 4000 psi", inclusive.Remarks);
        Assert.Equal("Active", inclusive.Status);
        Assert.Null(inclusive.RemovedAtUtc);
        Assert.Null(inclusive.RemovedReason);

        // Exclusive at 12%: 10 x 100.00 = 1,000.00 before VAT, VAT 120.00, total 1,120.00.
        var exclusive = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines",
            Line("Rebar", quantity: 10m, unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        Assert.Equal((100_000L, 12_000L, 112_000L), (exclusive.NetMinor, exclusive.VatMinor, exclusive.GrossMinor));

        // Exempt: 4 x 250.00 = 1,000.00, no VAT.
        var exempt = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines",
            Line("Labor allowance", "Labor", quantity: 4m, unitCostMinor: 25_000, vatTreatment: "Exempt"));
        Assert.Equal((100_000L, 0L, 100_000L), (exempt.NetMinor, exempt.VatMinor, exempt.GrossMinor));

        // Reading it again returns the same figures.
        var read = await client.GetAsync<BudgetLineDto>($"/api/budget-lines/{inclusive.Id}");
        Assert.Equal(inclusive, read);
    }

    [SqlServerFact]
    public async Task An_old_line_keeps_the_vat_rate_it_was_entered_with_when_the_company_rate_changes()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/budget-lines";
        var old = await client.PostAsync<BudgetLineDto>(url, Line("Old line", quantity: 10m, unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        Assert.Equal((100_000L, 12_000L, 112_000L), (old.NetMinor, old.VatMinor, old.GrossMinor));

        await ChangeOrganizationAsync(client, vatRateBps: 1_000);

        // A new line uses the new 10% rate. Exclusive: 1,000.00 + 100.00 = 1,100.00.
        var added = await client.PostAsync<BudgetLineDto>(url, Line("New line", quantity: 10m, unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        Assert.Equal(1_000, added.VatRateBps);
        Assert.Equal((100_000L, 10_000L, 110_000L), (added.NetMinor, added.VatMinor, added.GrossMinor));

        // Inclusive at 10%: 5,000.00 / 1.10 = 4,545.4545 -> 4,545.45, VAT 454.55.
        var inclusive = await client.PostAsync<BudgetLineDto>(url, Line("New inclusive", quantity: 10m, unitCostMinor: 50_000, vatTreatment: "Inclusive"));
        Assert.Equal((454_545L, 45_455L, 500_000L), (inclusive.NetMinor, inclusive.VatMinor, inclusive.GrossMinor));

        // The old line is untouched ...
        var reread = await client.GetAsync<BudgetLineDto>($"/api/budget-lines/{old.Id}");
        Assert.Equal(1_200, reread.VatRateBps);
        Assert.Equal((100_000L, 12_000L, 112_000L), (reread.NetMinor, reread.VatMinor, reread.GrossMinor));

        // ... and editing its quantity keeps its own 12% rather than silently re-pricing it at 10%: 20 x 100.00 = 2,000.00 + 240.00.
        var edited = await client.PutAsync<BudgetLineDto>($"/api/budget-lines/{old.Id}",
            Line("Old line", quantity: 20m, unitCostMinor: 10_000, vatTreatment: "Exclusive").With("version", reread.Version));
        Assert.Equal(1_200, edited.VatRateBps);
        Assert.Equal((200_000L, 24_000L, 224_000L), (edited.NetMinor, edited.VatMinor, edited.GrossMinor));
        Assert.True(edited.Version > reread.Version);
    }

    [SqlServerFact]
    public async Task Every_problem_is_reported_at_once_and_a_bad_choice_is_a_field_error_not_a_bad_request()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsync($"/api/projects/{project.Id}/budget-lines",
            Line(" ", "Gadget", 0m, " ", -5, "Maybe", entryDate: "0001-01-01", division: 99));

        var fields = await ErrorFieldsAsync(response);
        Assert.Superset(new HashSet<string> { "division", "description", "itemType", "quantity", "unit", "unitCostMinor", "vatTreatment", "entryDate" }, fields);

        // A missing item type and VAT treatment are reported the same way.
        var missing = await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line(itemType: null, vatTreatment: null));
        Assert.Superset(new HashSet<string> { "itemType", "vatTreatment" }, await ErrorFieldsAsync(missing));

        // Nothing was saved by the failed requests.
        Assert.Equal(0, (await client.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{project.Id}/budget-lines")).Total);
    }

    [SqlServerFact]
    public async Task A_stale_version_is_a_conflict_for_update_and_remove_and_nothing_is_overwritten()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var line = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Original"));
        var current = await client.PutAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}", Line("Edited once").With("version", line.Version));

        var stale = await client.PutAsync($"/api/budget-lines/{line.Id}", Line("Edited from an old screen").With("version", line.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));

        var staleRemove = await client.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = "Mistake", version = line.Version });
        Assert.Equal(HttpStatusCode.Conflict, staleRemove.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(staleRemove));

        var reread = await client.GetAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}");
        Assert.Equal("Edited once", reread.Description);
        Assert.Equal("Active", reread.Status);
        Assert.Equal(current.Version, reread.Version);
    }

    [SqlServerFact]
    public async Task Roles_decide_who_can_read_and_who_can_change_budget_lines()
    {
        var (admin, adminUser) = await AdminAsync();
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var line = await admin.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line());
        var listUrl = $"/api/projects/{project.Id}/budget-lines";
        var update = Line("Changed").With("version", line.Version);

        // HR and the warehouse have no business with costs.
        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(listUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/budget-lines/{line.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{listUrl}/export")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(listUrl, Line())).StatusCode);
        }

        // A viewer and a project manager read but never change.
        foreach (var role in new[] { AccessRole.Viewer, AccessRole.ProjectManager })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _v = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(listUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/budget-lines/{line.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(listUrl, Line())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/budget-lines/{line.Id}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = "No", version = line.Version })).StatusCode);
        }

        // The cost engineer and the accountant do the work.
        foreach (var role in new[] { AccessRole.CostEngineer, AccessRole.Accountant })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _w = client;
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(listUrl, Line($"By {role}"))).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_budget_lines()
    {
        var (mine, _) = await AdminAsync();
        var (theirs, _) = await AdminAsync();
        using var _a = mine;
        using var _b = theirs;
        var project = await CreateProjectAsync(mine);
        var line = await mine.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Secret estimate"));

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/budget-lines/{line.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PutAsync($"/api/budget-lines/{line.Id}", Line("Hijacked").With("version", line.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = "Hijack", version = line.Version })).StatusCode);
        // Their list for my project is "no such project", never an empty list that hints the project exists.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/budget-lines")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/budget-lines/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/projects/{project.Id}/budget-lines", Line("Planted"))).StatusCode);

        // Their own project's list shows none of mine, and mine is unchanged.
        var theirProject = await CreateProjectAsync(theirs);
        Assert.Empty((await theirs.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{theirProject.Id}/budget-lines")).Items);
        var mineStill = await mine.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{project.Id}/budget-lines");
        Assert.Equal("Secret estimate", Assert.Single(mineStill.Items).Description);
    }

    [SqlServerFact]
    public async Task An_archived_project_takes_no_new_lines_and_cannot_have_lines_changed_until_restored()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var line = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Before archive"));
        var archived = await ArchiveAsync(client, project);

        var add = await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line("After archive"));
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(add));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsync($"/api/budget-lines/{line.Id}", Line("Edit").With("version", line.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = "Cleanup", version = line.Version })).StatusCode);

        // Reading still works.
        Assert.Single((await client.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{project.Id}/budget-lines")).Items);

        await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/restore", new { version = archived.Version });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line("After restore"))).StatusCode);
    }

    [SqlServerFact]
    public async Task A_sub_project_of_another_project_or_an_archived_one_is_refused()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var other = await CreateProjectAsync(client);
        var otherSub = (await AddSubProjectAsync(client, other, "Elsewhere")).SubProjects.Single();

        var foreign = await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line(subProjectId: otherSub.Id));
        Assert.Contains("subProjectId", await ErrorFieldsAsync(foreign));

        var withSub = await AddSubProjectAsync(client, project, "Building 1");
        var sub = withSub.SubProjects.Single();
        var archived = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects/{sub.Id}/archive", new { version = withSub.Version });
        Assert.Equal("Archived", archived.SubProjects.Single().Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line(subProjectId: sub.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task A_repeated_client_request_id_returns_the_original_line_and_never_a_second_one()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/budget-lines";
        var requestId = Guid.NewGuid().ToString();

        var first = await client.PostAsync<BudgetLineDto>(url, Line("Double-clicked", clientRequestId: requestId));
        var second = await client.PostAsync<BudgetLineDto>(url, Line("Double-clicked", clientRequestId: $"  {requestId}  "));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first, second);
        var list = await client.GetAsync<TotaledPage<BudgetLineDto>>(url);
        Assert.Equal(1, list.Total);

        // The same id used for another project is refused rather than quietly returning a line of the wrong project.
        var other = await CreateProjectAsync(client);
        var reuse = await client.PostAsync($"/api/projects/{other.Id}/budget-lines", Line("Elsewhere", clientRequestId: requestId));
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }

    [SqlServerFact]
    public async Task Removing_needs_a_reason_keeps_the_record_and_removed_lines_are_final()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var line = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Duplicate estimate"));

        var noReason = await client.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = " ", version = line.Version });
        Assert.Contains("reason", await ErrorFieldsAsync(noReason));

        var removed = await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}/remove", new { reason = "Superseded by the revised estimate", version = line.Version });
        Assert.Equal("Removed", removed.Status);
        Assert.Equal("Superseded by the revised estimate", removed.RemovedReason);
        Assert.NotNull(removed.RemovedAtUtc);

        // Still readable by id, hidden from the default list, never editable, never removed twice.
        Assert.Equal("Removed", (await client.GetAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}")).Status);
        Assert.Empty((await client.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{project.Id}/budget-lines")).Items);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsync($"/api/budget-lines/{line.Id}", Line("Revive").With("version", removed.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/budget-lines/{line.Id}/remove", new { reason = "Again", version = removed.Version })).StatusCode);
    }

    [SqlServerFact]
    public async Task List_totals_cover_the_whole_filtered_set_not_the_page_and_removed_lines_never_count()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/budget-lines";

        // Five exempt lines (100.00 to 500.00) plus one exclusive line: 100.00 + 12.00 VAT.
        var amounts = new[] { (10_000L, "Material"), (20_000L, "Material"), (30_000L, "Labor"), (40_000L, "Labor"), (50_000L, "Equipment") };
        var ids = new List<BudgetLineDto>();
        foreach (var (amount, type) in amounts) ids.Add(await client.PostAsync<BudgetLineDto>(url, Line($"Line {amount}", type, unitCostMinor: amount)));
        await client.PostAsync<BudgetLineDto>(url, Line("Taxed line", "Material", unitCostMinor: 10_000, vatTreatment: "Exclusive"));

        var first = await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?pageSize=2&page=1");
        var third = await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?pageSize=2&page=3");
        Assert.Equal(6, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, third.Items.Count);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        // Whole set, whichever page is asked for: net 1,600.00, VAT 12.00, gross 1,612.00.
        Assert.Equal(new MoneyTotalsDto(160_000, 1_200, 161_200), first.Totals);
        Assert.Equal(first.Totals, third.Totals);

        // A filter narrows the totals with the rows: the two labor lines, 300.00 + 400.00.
        var labor = await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?itemType=labor&pageSize=1");
        Assert.Equal(2, labor.Total);
        Assert.Single(labor.Items);
        Assert.Equal(new MoneyTotalsDto(70_000, 0, 70_000), labor.Totals);

        // A removed line leaves the default list and the totals ...
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{ids[4].Id}/remove", new { reason = "Not needed", version = ids[4].Version });
        var afterRemove = await client.GetAsync<TotaledPage<BudgetLineDto>>(url);
        Assert.Equal(5, afterRemove.Total);
        Assert.Equal(new MoneyTotalsDto(110_000, 1_200, 111_200), afterRemove.Totals);

        // ... and is listed again on request, but still adds nothing to the totals.
        var withRemoved = await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?includeRemoved=true");
        Assert.Equal(6, withRemoved.Total);
        Assert.Contains(withRemoved.Items, item => item.Status == "Removed");
        Assert.Equal(new MoneyTotalsDto(110_000, 1_200, 111_200), withRemoved.Totals);

        // An empty result has zero totals.
        var none = await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=nothing-matches-this");
        Assert.Equal(0, none.Total);
        Assert.Empty(none.Items);
        Assert.Equal(new MoneyTotalsDto(0, 0, 0), none.Totals);
    }

    [SqlServerFact]
    public async Task The_list_filters_by_sub_project_and_division_and_searches_text_literally()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var sub = (await AddSubProjectAsync(client, project, "Building 1")).SubProjects.Single();
        var url = $"/api/projects/{project.Id}/budget-lines";
        await client.PostAsync<BudgetLineDto>(url, Line("Rebar 50% extra", wbsCode: "3200", subProjectId: sub.Id));
        await client.PostAsync<BudgetLineDto>(url, Line("Gravel_mix", wbsCode: "2300"));
        await client.PostAsync<BudgetLineDto>(url, Line("Plain item"));

        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?subProjectId={sub.Id}")).Items.Select(item => item.Description));
        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?division=3")).Items.Select(item => item.Description));
        Assert.Equal(["Gravel_mix"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?division=2")).Items.Select(item => item.Description));

        // Search reads description, scope names and the code; % and _ mean themselves.
        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=50%25")).Items.Select(item => item.Description));
        Assert.Equal(["Gravel_mix"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=l_m")).Items.Select(item => item.Description));
        Assert.Equal(["Gravel_mix"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=earthwork")).Items.Select(item => item.Description));
        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=3200")).Items.Select(item => item.Description));
        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=reinforcement")).Items.Select(item => item.Description));
        Assert.Equal(["Rebar 50% extra"], (await client.GetAsync<TotaledPage<BudgetLineDto>>($"{url}?search=%25")).Items.Select(item => item.Description));

        // A filter that makes no sense is a field error.
        Assert.Contains("itemType", await ErrorFieldsAsync(await client.GetAsync($"{url}?itemType=gadget")));
    }

    [SqlServerFact]
    public async Task Budget_line_changes_are_written_to_the_audit_trail()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client, "Audit Tower");
        var line = await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Audited cement", unitCostMinor: 12_345));
        var edited = await client.PutAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}", Line("Audited cement", unitCostMinor: 20_000).With("version", line.Version));
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}/remove", new { reason = "Wrong supplier", version = edited.Version });

        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=BudgetLine&entityId={line.Id}");
        Assert.Contains(trail.Items, entry => entry.Action == "BudgetLine.Created" && entry.Summary.Contains("Audited cement") && entry.Summary.Contains("Audit Tower"));
        Assert.Contains(trail.Items, entry => entry.Action == "BudgetLine.Updated" && entry.Summary.Contains("PHP 123.45") && entry.Summary.Contains("PHP 200.00"));
        Assert.Contains(trail.Items, entry => entry.Action == "BudgetLine.Removed" && entry.Summary.Contains("Wrong supplier"));
    }

    [SqlServerFact]
    public async Task The_budget_export_is_a_safe_utf8_csv_with_plain_decimals_iso_dates_and_the_same_filters_as_the_list()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client, "Nueva Biñan / Block 2");
        var url = $"/api/projects/{project.Id}/budget-lines";
        await client.PostAsync<BudgetLineDto>(url, Line("Ready-mix concrete", quantity: 10m, unit: "m3", unitCostMinor: 50_000, vatTreatment: "Inclusive",
            wbsCode: "3300", remarks: "Grade 4000 psi"));
        // Cells that a spreadsheet would run as formulas, and one that needs quoting.
        await client.PostAsync<BudgetLineDto>(url, Line("=HYPERLINK(\"http://evil.example\",\"click\")", remarks: "+cmd|' /C calc'!A0"));
        await client.PostAsync<BudgetLineDto>(url, Line("@SUM(1+1)", remarks: "-2+3"));
        await client.PostAsync<BudgetLineDto>(url, Line("Pipe, 2\" PVC", remarks: "line one\nline two"));

        var file = await DownloadCsvAsync(client, $"{url}/export");

        Assert.Equal("text/csv; charset=utf-8", file.Response.Content.Headers.ContentType?.ToString());
        // A download of company figures is never cached.
        Assert.True(file.Response.Headers.CacheControl?.NoStore);
        Assert.True(file.Response.Headers.CacheControl?.Private);
        var fileName = FileNameOf(file.Response);
        Assert.Matches(new Regex(@"^Nueva-Binan-Block-2-budget-\d{4}-\d{2}-\d{2}\.csv$"), fileName);
        Assert.Equal(5, file.Rows.Count);

        var concrete = file.Rows.Single(row => row[file.Header.ToList().IndexOf("Description")] == "Ready-mix concrete");
        string Column(string[] row, string name) => row[Array.IndexOf(file.Header, name)];
        Assert.Equal("2026-02-01", Column(concrete, "Date"));
        Assert.Equal("3", Column(concrete, "Division"));
        Assert.Equal("Cast-In-Place Concrete", Column(concrete, "Sub-scope"));
        Assert.Equal("10", Column(concrete, "Quantity"));
        Assert.Equal("m³", Column(concrete, "Unit"));
        Assert.Equal("500.00", Column(concrete, "Unit cost"));
        Assert.Equal("Unit cost includes VAT", Column(concrete, "VAT treatment"));
        Assert.Equal("12.00", Column(concrete, "VAT rate %"));
        Assert.Equal("4464.29", Column(concrete, "Net (before VAT)"));
        Assert.Equal("535.71", Column(concrete, "VAT"));
        Assert.Equal("5000.00", Column(concrete, "Gross (with VAT)"));
        Assert.Equal("Active", Column(concrete, "Status"));

        // Formula injection: a leading apostrophe turns each into plain text.
        var descriptions = file.Rows.Skip(1).Select(row => Column(row, "Description")).ToArray();
        Assert.Contains("'=HYPERLINK(\"http://evil.example\",\"click\")", descriptions);
        Assert.Contains("'@SUM(1+1)", descriptions);
        var remarks = file.Rows.Skip(1).Select(row => Column(row, "Remarks")).ToArray();
        Assert.Contains("'+cmd|' /C calc'!A0", remarks);
        Assert.Contains("'-2+3", remarks);
        Assert.DoesNotContain(descriptions.Concat(remarks), text => text.StartsWith('=') || text.StartsWith('@') || text.StartsWith('+'));
        // Quoting keeps commas, quotes and line breaks inside their cell.
        Assert.Contains("Pipe, 2\" PVC", descriptions);
        Assert.Contains("line one\nline two", remarks);

        // The same filters as the list.
        var filtered = await DownloadCsvAsync(client, $"{url}/export?search=ready-mix");
        Assert.Equal(2, filtered.Rows.Count);
        Assert.Equal(Regex.Replace(fileName, @"\d{4}-\d{2}-\d{2}", "DATE"), Regex.Replace(FileNameOf(filtered.Response), @"\d{4}-\d{2}-\d{2}", "DATE"));
    }

    [SqlServerFact]
    public async Task Reading_lines_costs_a_fixed_number_of_statements_however_many_lines_there_are()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/budget-lines?pageSize=25";
        for (var index = 0; index < 3; index++) await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line($"Small {index}", wbsCode: "3300"));
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        for (var index = 0; index < 40; index++) await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line($"Large {index}", wbsCode: "3200"));
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        Assert.Equal(small, large);
        // The project, the count with the totals, and the page.
        Assert.Equal(3, large);
    }
}
