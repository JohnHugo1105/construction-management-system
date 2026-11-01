using System.Net;
using System.Text.RegularExpressions;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

public sealed class CostingCostEntryApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public CostingCostEntryApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private Task<(ApiClient Client, SeededUser User)> AdminAsync() => SignedInAsync(_factory);

    [SqlServerFact]
    public async Task A_supplier_invoice_is_recorded_with_exact_money_and_every_figure_is_returned()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var phase = (await AddSubProjectAsync(client, project, "Phase 1")).SubProjects.Single();
        var url = $"/api/projects/{project.Id}/cost-entries";

        // Inclusive at 12%: 100 x 250.00 = 25,000.00 with VAT in it. Net = 22,321.43, VAT = 2,678.57.
        var inclusive = await client.PostAsync<CostEntryDto>(url, Cost("12mm rebar", quantity: 100m, unit: "pcs", unitCostMinor: 25_000,
            vatTreatment: "Inclusive", wbsCode: "3200", subProjectId: phase.Id, sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0001",
            remarks: "Partial delivery"));
        Assert.Equal((2_232_143L, 267_857L, 2_500_000L), (inclusive.NetMinor, inclusive.VatMinor, inclusive.GrossMinor));
        Assert.Equal(1_200, inclusive.VatRateBps);

        Assert.Equal(project.Id, inclusive.ProjectId);
        Assert.Equal("Phase 1", inclusive.SubProjectName);
        Assert.Equal(3, inclusive.Division);
        Assert.Equal("Concrete", inclusive.MainScope);
        Assert.Equal("Concrete Reinforcement", inclusive.SubScope);
        Assert.Equal("pc", inclusive.Unit);
        Assert.Equal("Material", inclusive.ItemType);
        Assert.Equal("Inclusive", inclusive.VatTreatment);
        Assert.Equal(new DateOnly(2025, 12, 1), inclusive.EntryDate);
        Assert.Equal("Cebu Builders Depot", inclusive.SourceDealer);
        Assert.Equal("SI-0001", inclusive.InvoiceNumber);
        Assert.Equal("Manual", inclusive.Origin);
        Assert.Null(inclusive.OriginReferenceId);
        Assert.Equal("Posted", inclusive.Status);
        Assert.True(inclusive.CanEdit);

        // Exclusive: 10 x 100.00 = 1,000.00 + 120.00 VAT. Exempt: 4 x 250.00 = 1,000.00.
        var exclusive = await client.PostAsync<CostEntryDto>(url, Cost(quantity: 10m, unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        Assert.Equal((100_000L, 12_000L, 112_000L), (exclusive.NetMinor, exclusive.VatMinor, exclusive.GrossMinor));
        var exempt = await client.PostAsync<CostEntryDto>(url, Cost(quantity: 4m, unitCostMinor: 25_000, vatTreatment: "Exempt"));
        Assert.Equal((100_000L, 0L, 100_000L), (exempt.NetMinor, exempt.VatMinor, exempt.GrossMinor));

        Assert.Equal(inclusive, await client.GetAsync<CostEntryDto>($"/api/cost-entries/{inclusive.Id}"));

        // A company rate change applies to new entries only; the old entry keeps its rate, even when it is edited.
        await ChangeOrganizationAsync(client, vatRateBps: 1_000);
        var changed = await client.PostAsync<CostEntryDto>(url, Cost(quantity: 10m, unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        Assert.Equal((100_000L, 10_000L, 110_000L), (changed.NetMinor, changed.VatMinor, changed.GrossMinor));
        var edited = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{exclusive.Id}",
            Cost(quantity: 20m, unitCostMinor: 10_000, vatTreatment: "Exclusive", invoiceNumber: exclusive.InvoiceNumber).With("version", exclusive.Version));
        Assert.Equal(1_200, edited.VatRateBps);
        Assert.Equal((200_000L, 24_000L, 224_000L), (edited.NetMinor, edited.VatMinor, edited.GrossMinor));
    }

    [SqlServerFact]
    public async Task Every_problem_is_reported_at_once_including_the_supplier_and_invoice()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsync($"/api/projects/{project.Id}/cost-entries",
            Cost(" ", "Gadget", 0m, " ", -5, "Maybe", entryDate: "2999-01-01", division: 99, sourceDealer: "", invoiceNumber: "---"));

        var fields = await ErrorFieldsAsync(response);
        Assert.Superset(new HashSet<string>
        {
            "division", "description", "itemType", "quantity", "unit", "unitCostMinor", "vatTreatment", "entryDate", "sourceDealer", "invoiceNumber"
        }, fields);

        var blank = await client.PostAsync($"/api/projects/{project.Id}/cost-entries", Cost(sourceDealer: null).With("invoiceNumber", null));
        Assert.Superset(new HashSet<string> { "sourceDealer", "invoiceNumber" }, await ErrorFieldsAsync(blank));
        Assert.Equal(0, (await client.GetAsync<TotaledPage<CostEntryDto>>($"/api/projects/{project.Id}/cost-entries")).Total);
    }

    [SqlServerFact]
    public async Task A_cost_may_not_be_dated_in_the_future_in_the_company_time_zone_not_the_utc_day()
    {
        // At any moment at least one of these two zones is on a different calendar day than UTC: Kiritimati (UTC+14) is a day ahead from
        // 10:00 UTC, Pago Pago (UTC-11) a day behind until 11:00 UTC. So "today in the company's zone" and "today in UTC" disagree for one
        // of them, and the answer shows which one the API uses.
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var disagreed = false;
        foreach (var zoneId in new[] { "Pacific/Kiritimati", "Pacific/Pago_Pago" })
        {
            var (client, _) = await AdminAsync();
            using var _c = client;
            await ChangeOrganizationAsync(client, timeZoneId: zoneId);
            var project = await CreateProjectAsync(client);
            var url = $"/api/projects/{project.Id}/cost-entries";
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
            disagreed |= localToday != utcToday;

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(url, Cost(entryDate: localToday.ToString("yyyy-MM-dd")))).StatusCode);
            var tomorrow = await client.PostAsync(url, Cost(entryDate: localToday.AddDays(1).ToString("yyyy-MM-dd")));
            Assert.Contains("entryDate", await ErrorFieldsAsync(tomorrow));
            Assert.Contains("future", await ErrorMessageAsync(tomorrow, "entryDate"));
        }

        Assert.True(disagreed, "One of the two zones should have been on a different day than UTC.");
    }

    [SqlServerFact]
    public async Task The_same_supplier_invoice_is_refused_once_but_allowed_after_a_void_and_for_another_project()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var other = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-entries";
        var first = await client.PostAsync<CostEntryDto>(url, Cost(sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0001", entryDate: "2025-11-20"));

        // Same supplier and invoice, differing only by case, spaces and punctuation.
        foreach (var (dealer, invoice) in new[] { ("CEBU BUILDERS DEPOT", "si 0001"), (" cebu builders depot ", "SI0001"), ("Cebu Builders Depot", "S.I.-0001") })
        {
            var duplicate = await client.PostAsync(url, Cost(sourceDealer: dealer, invoiceNumber: invoice));
            Assert.Equal(["invoiceNumber"], await ErrorFieldsAsync(duplicate));
            var message = await ErrorMessageAsync(duplicate, "invoiceNumber");
            Assert.Contains("already recorded for this project", message);
            Assert.Contains("20 Nov 2025", message);
        }

        // A different invoice from the same supplier, and the same invoice from another supplier, are fine.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(url, Cost(sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0002"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(url, Cost(sourceDealer: "Mandaue Hardware", invoiceNumber: "SI-0001"))).StatusCode);
        // The same invoice for another project is fine too.
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsync($"/api/projects/{other.Id}/cost-entries", Cost(sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0001"))).StatusCode);

        // Once the first is voided the invoice can be recorded again.
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{first.Id}/void", new { reason = "Recorded against the wrong project", version = first.Version });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(url, Cost(sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0001"))).StatusCode);
    }

    [SqlServerFact]
    public async Task Editing_an_entry_into_a_duplicate_is_refused_but_saving_it_unchanged_is_fine()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-entries";
        var first = await client.PostAsync<CostEntryDto>(url, Cost(invoiceNumber: "SI-0001"));
        var second = await client.PostAsync<CostEntryDto>(url, Cost(invoiceNumber: "SI-0002"));

        var same = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{second.Id}",
            Cost("Cement, corrected", invoiceNumber: "SI-0002").With("version", second.Version));
        Assert.Equal("Cement, corrected", same.Description);

        var clash = await client.PutAsync($"/api/cost-entries/{second.Id}", Cost(invoiceNumber: "si0001").With("version", same.Version));
        Assert.Equal(["invoiceNumber"], await ErrorFieldsAsync(clash));
        Assert.Equal("SI-0002", (await client.GetAsync<CostEntryDto>($"/api/cost-entries/{second.Id}")).InvoiceNumber);
        Assert.Equal("SI-0001", (await client.GetAsync<CostEntryDto>($"/api/cost-entries/{first.Id}")).InvoiceNumber);
    }

    [SqlServerFact]
    public async Task Two_people_saving_the_same_invoice_at_the_same_moment_record_it_only_once()
    {
        var (admin, user) = await AdminAsync();
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var url = $"/api/projects/{project.Id}/cost-entries";
        var sessions = new List<ApiClient> { admin };
        for (var index = 0; index < 5; index++) sessions.Add(await AnotherSessionAsync(_factory, user));

        try
        {
            // Several rounds, because who wins the race differs from run to run.
            for (var round = 1; round <= 6; round++)
            {
                var invoice = $"RACE-{round}";
                // Different request ids (separate forms), same supplier and invoice.
                var attempts = sessions.Select(session => session.PostAsync(url, Cost(sourceDealer: "Race Supplier", invoiceNumber: invoice,
                    clientRequestId: Guid.NewGuid().ToString()))).ToArray();
                var responses = await Task.WhenAll(attempts);

                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
                // The losers are told, not crashed: a clear field error when the check saw the first entry, a friendly conflict when the
                // database's unique index caught the race.
                Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.Created),
                    response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.UnprocessableEntity, HttpStatusCode.Conflict }));
                Assert.Equal(round, (await admin.GetAsync<TotaledPage<CostEntryDto>>(url)).Total);
            }
        }
        finally
        {
            foreach (var session in sessions.Skip(1)) session.Dispose();
        }
    }

    [SqlServerFact]
    public async Task The_database_index_is_the_backstop_that_turns_a_duplicate_invoice_into_a_friendly_conflict()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);

        // Saved straight through the unit of work, bypassing the service's own check, as a concurrent request would.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        var domainProject = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == project.Id);
        CostEntry Make(string invoice, string dealer = "Cebu Builders Depot") => CostEntry.RecordManual(Guid.NewGuid(), user.TenantId, domainProject, null,
            null, null, null, null, "Cement", ItemType.Material, 1m, "bag", 10_000, VatTreatment.Exempt, 1_200, new DateOnly(2025, 12, 1), null, dealer,
            invoice, user.UserId, null, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow);

        var first = Make("SI-1");
        db.Add(first);
        await new UnitOfWork(db).SaveChangesAsync();

        db.Add(Make("si 1"));
        var refused = await Assert.ThrowsAsync<DomainRuleException>(() => new UnitOfWork(db).SaveChangesAsync());
        Assert.Contains("already exists", refused.Message);

        // The index only covers posted manual entries: once the first is voided the invoice is free, and another supplier was never blocked.
        db.ChangeTracker.Clear();
        var tracked = await db.Set<CostEntry>().IgnoreQueryFilters().SingleAsync(item => item.Id == first.Id);
        tracked.Void(user.UserId, "Wrong project", DateTimeOffset.UtcNow);
        await new UnitOfWork(db).SaveChangesAsync();
        db.Add(Make("si 1"));
        db.Add(Make("si 1", dealer: "Mandaue Hardware"));
        await new UnitOfWork(db).SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task A_stale_version_is_a_conflict_for_update_and_void()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("Original", invoiceNumber: "SI-1"));
        var current = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{entry.Id}", Cost("Edited once", invoiceNumber: "SI-1").With("version", entry.Version));

        var stale = await client.PutAsync($"/api/cost-entries/{entry.Id}", Cost("Edited from an old screen", invoiceNumber: "SI-1").With("version", entry.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        var staleVoid = await client.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "Mistake", version = entry.Version });
        Assert.Equal(HttpStatusCode.Conflict, staleVoid.StatusCode);

        var reread = await client.GetAsync<CostEntryDto>($"/api/cost-entries/{entry.Id}");
        Assert.Equal("Edited once", reread.Description);
        Assert.Equal("Posted", reread.Status);
        Assert.Equal(current.Version, reread.Version);
    }

    [SqlServerFact]
    public async Task Roles_decide_who_can_read_and_who_can_change_cost_entries()
    {
        var (admin, adminUser) = await AdminAsync();
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var entry = await admin.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost());
        var listUrl = $"/api/projects/{project.Id}/cost-entries";
        var update = Cost("Changed", invoiceNumber: entry.InvoiceNumber).With("version", entry.Version);

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(listUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/cost-entries/{entry.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{listUrl}/export")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(listUrl, Cost())).StatusCode);
        }

        foreach (var role in new[] { AccessRole.Viewer, AccessRole.ProjectManager })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _v = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(listUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/cost-entries/{entry.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(listUrl, Cost())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/cost-entries/{entry.Id}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "No", version = entry.Version })).StatusCode);
        }

        foreach (var role in new[] { AccessRole.CostEngineer, AccessRole.Accountant })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _w = client;
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(listUrl, Cost($"By {role}"))).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_cost_entries()
    {
        var (mine, _) = await AdminAsync();
        var (theirs, _) = await AdminAsync();
        using var _a = mine;
        using var _b = theirs;
        var project = await CreateProjectAsync(mine);
        var entry = await mine.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("Secret invoice", sourceDealer: "Secret Supplier", invoiceNumber: "SI-9"));

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/cost-entries/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PutAsync($"/api/cost-entries/{entry.Id}", Cost(invoiceNumber: "SI-9").With("version", entry.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "Hijack", version = entry.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/cost-entries")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/cost-entries/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/projects/{project.Id}/cost-entries", Cost())).StatusCode);

        var theirProject = await CreateProjectAsync(theirs);
        Assert.Empty((await theirs.GetAsync<TotaledPage<CostEntryDto>>($"/api/projects/{theirProject.Id}/cost-entries")).Items);
        // Their identical invoice number is no clash: invoices are unique within a company's project.
        Assert.Equal(HttpStatusCode.Created,
            (await theirs.PostAsync($"/api/projects/{theirProject.Id}/cost-entries", Cost(sourceDealer: "Secret Supplier", invoiceNumber: "SI-9"))).StatusCode);
    }

    [SqlServerFact]
    public async Task An_archived_project_takes_no_new_costs_and_cannot_have_costs_changed_until_restored()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost(invoiceNumber: "SI-1"));
        var archived = await ArchiveAsync(client, project);

        var add = await client.PostAsync($"/api/projects/{project.Id}/cost-entries", Cost());
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(add));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PutAsync($"/api/cost-entries/{entry.Id}", Cost(invoiceNumber: "SI-1").With("version", entry.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "Cleanup", version = entry.Version })).StatusCode);
        Assert.Single((await client.GetAsync<TotaledPage<CostEntryDto>>($"/api/projects/{project.Id}/cost-entries")).Items);

        await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/restore", new { version = archived.Version });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/projects/{project.Id}/cost-entries", Cost())).StatusCode);
    }

    [SqlServerFact]
    public async Task A_repeated_client_request_id_returns_the_original_entry_and_never_a_second_one()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-entries";
        var requestId = Guid.NewGuid().ToString();
        var body = Cost("Double-clicked", invoiceNumber: "SI-DBL", clientRequestId: requestId);

        var first = await client.PostAsync<CostEntryDto>(url, body);
        // The second click carries the very same form, including the invoice: it must not be reported as a duplicate invoice.
        var second = await client.PostAsync<CostEntryDto>(url, body);

        Assert.Equal(first, second);
        Assert.Equal(1, (await client.GetAsync<TotaledPage<CostEntryDto>>(url)).Total);
    }

    [SqlServerFact]
    public async Task Voiding_needs_a_reason_keeps_the_record_and_voided_entries_are_final()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost("Wrong project", invoiceNumber: "SI-1"));

        var noReason = await client.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "", version = entry.Version });
        Assert.Contains("reason", await ErrorFieldsAsync(noReason));

        var voided = await client.PostAsync<CostEntryDto>($"/api/cost-entries/{entry.Id}/void", new { reason = "Recorded against the wrong project", version = entry.Version });
        Assert.Equal("Voided", voided.Status);
        Assert.Equal("Recorded against the wrong project", voided.VoidReason);
        Assert.NotNull(voided.VoidedAtUtc);
        Assert.False(voided.CanEdit);

        Assert.Equal("Voided", (await client.GetAsync<CostEntryDto>($"/api/cost-entries/{entry.Id}")).Status);
        Assert.Empty((await client.GetAsync<TotaledPage<CostEntryDto>>($"/api/projects/{project.Id}/cost-entries")).Items);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PutAsync($"/api/cost-entries/{entry.Id}", Cost(invoiceNumber: "SI-1").With("version", voided.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/cost-entries/{entry.Id}/void", new { reason = "Again", version = voided.Version })).StatusCode);
    }

    [SqlServerFact]
    public async Task System_made_costs_are_listed_with_their_origin_cannot_be_edited_and_can_be_voided_at_the_entry_with_an_audit_line()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-entries";
        await client.PostAsync<CostEntryDto>(url, Cost("Cement", unitCostMinor: 50_000, invoiceNumber: "SI-1"));
        var payrollId = await SeedSystemCostAsync(_factory, user, project.Id, CostOrigin.Payroll, 105_000);
        await SeedSystemCostAsync(_factory, user, project.Id, CostOrigin.Inventory, 20_000, "Cement from the warehouse");

        var all = await client.GetAsync<TotaledPage<CostEntryDto>>(url);
        Assert.Equal(3, all.Total);
        Assert.Equal(175_000, all.Totals.GrossMinor);
        var payroll = Assert.Single(all.Items, item => item.Origin == "Payroll");
        Assert.False(payroll.CanEdit);
        Assert.Null(payroll.InvoiceNumber);
        Assert.NotNull(payroll.OriginReferenceId);
        Assert.Equal("Payroll", payroll.SourceDealer);
        Assert.Equal(["Payroll"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?origin=Payroll")).Items.Select(item => item.Origin));
        Assert.Equal(["Inventory"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?origin=inventory")).Items.Select(item => item.Origin));
        Assert.Equal(["Manual"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?origin=Manual")).Items.Select(item => item.Origin));
        Assert.Contains("origin", await ErrorFieldsAsync(await client.GetAsync($"{url}?origin=Bogus")));

        // Not editable by hand: the cost is corrected where it came from.
        var edit = await client.PutAsync($"/api/cost-entries/{payrollId}", Cost(invoiceNumber: "SI-2").With("version", payroll.Version));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(edit));

        // A cost made by payroll or by a stock transfer is voided where it came from, never at the entry: refused, and nothing changes.
        var voidPayroll = await client.PostAsync($"/api/cost-entries/{payrollId}/void", new { reason = "Payroll was posted to the wrong project", version = payroll.Version });
        Assert.Equal(HttpStatusCode.Conflict, voidPayroll.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(voidPayroll));
        Assert.Contains("Void the payroll run", await DetailAsync(voidPayroll));
        var inventory = Assert.Single(all.Items, item => item.Origin == "Inventory");
        var voidTransfer = await client.PostAsync($"/api/cost-entries/{inventory.Id}/void", new { reason = "Wrong project", version = inventory.Version });
        Assert.Equal(HttpStatusCode.Conflict, voidTransfer.StatusCode);
        Assert.Contains("Reverse the transfer", await DetailAsync(voidTransfer));

        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=CostEntry&entityId={payrollId}");
        Assert.Empty(trail.Items);

        // Both are still in the list and the totals.
        var after = await client.GetAsync<TotaledPage<CostEntryDto>>(url);
        Assert.Equal(3, after.Total);
        Assert.Equal(175_000, after.Totals.GrossMinor);
    }

    [SqlServerFact]
    public async Task List_totals_cover_the_whole_filtered_set_and_the_filters_work()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var sub = (await AddSubProjectAsync(client, project, "Building 1")).SubProjects.Single();
        var url = $"/api/projects/{project.Id}/cost-entries";

        // 100.00 x 5 exempt entries on five days, plus one taxed entry of 100.00 + 12.00 VAT.
        var created = new List<CostEntryDto>();
        for (var day = 1; day <= 5; day++)
            created.Add(await client.PostAsync<CostEntryDto>(url, Cost($"Day {day}", unitCostMinor: 10_000 * day, entryDate: $"2025-11-0{day}",
                sourceDealer: "Daily Supplier", invoiceNumber: $"D-{day}", itemType: day <= 2 ? "Material" : "Labor", subProjectId: day == 1 ? sub.Id : null,
                wbsCode: day == 1 ? "3200" : null)));
        await client.PostAsync<CostEntryDto>(url, Cost("Taxed", unitCostMinor: 10_000, vatTreatment: "Exclusive", entryDate: "2025-11-06",
            sourceDealer: "Other Supplier", invoiceNumber: "T-1"));
        await SeedSystemCostAsync(_factory, user, project.Id, CostOrigin.Payroll, 7_000);

        var first = await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?pageSize=2&page=1");
        var last = await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?pageSize=2&page=4");
        Assert.Equal(7, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(last.Items);
        // Whole set: 100+200+300+400+500 (exempt) + 100 net taxed + 70 payroll = net 1,670.00, VAT 12.00, gross 1,682.00.
        Assert.Equal(new MoneyTotalsDto(167_000, 1_200, 168_200), first.Totals);
        Assert.Equal(first.Totals, last.Totals);
        // Newest entry date first.
        Assert.Equal(first.Items.Select(item => item.EntryDate).OrderDescending(), first.Items.Select(item => item.EntryDate));

        // Date range is inclusive at both ends: 2 Nov to 4 Nov is the three entries of those days.
        var range = await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?from=2025-11-02&to=2025-11-04");
        Assert.Equal(3, range.Total);
        Assert.Equal(new MoneyTotalsDto(90_000, 0, 90_000), range.Totals);
        Assert.Contains("to", await ErrorFieldsAsync(await client.GetAsync($"{url}?from=2025-11-04&to=2025-11-02")));

        var labor = await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?itemType=Labor&origin=Manual");
        Assert.Equal(3, labor.Total);
        Assert.Equal(120_000, labor.Totals.GrossMinor);
        Assert.Equal(["Day 1"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?subProjectId={sub.Id}")).Items.Select(item => item.Description));
        Assert.Equal(["Day 1"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?division=3")).Items.Select(item => item.Description));

        // Search reads the description, the supplier and the invoice number (punctuation is literal).
        Assert.Equal(5, (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?search=daily")).Total);
        Assert.Equal(["Taxed"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?search=T-1")).Items.Select(item => item.Description));
        Assert.Equal(["Day 3"], (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?search=d-3")).Items.Select(item => item.Description));
        Assert.Equal(0, (await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?search=%25")).Total);

        // A voided entry leaves the default list and the totals, and is listed again on request without adding to the totals.
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{created[4].Id}/void", new { reason = "Duplicate delivery note", version = created[4].Version });
        var afterVoid = await client.GetAsync<TotaledPage<CostEntryDto>>(url);
        Assert.Equal(6, afterVoid.Total);
        Assert.Equal(new MoneyTotalsDto(117_000, 1_200, 118_200), afterVoid.Totals);
        var withVoided = await client.GetAsync<TotaledPage<CostEntryDto>>($"{url}?includeVoided=true");
        Assert.Equal(7, withVoided.Total);
        Assert.Contains(withVoided.Items, item => item.Status == "Voided");
        Assert.Equal(afterVoid.Totals, withVoided.Totals);
    }

    [SqlServerFact]
    public async Task The_cost_export_is_a_safe_utf8_csv_with_plain_decimals_and_the_same_filters_as_the_list()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client, "Tower A");
        var url = $"/api/projects/{project.Id}/cost-entries";
        await client.PostAsync<CostEntryDto>(url, Cost("12mm rebar", quantity: 100m, unit: "pcs", unitCostMinor: 25_000, vatTreatment: "Inclusive",
            wbsCode: "3200", sourceDealer: "Cebu Builders Depot", invoiceNumber: "SI-0001", entryDate: "2025-12-01"));
        await client.PostAsync<CostEntryDto>(url, Cost("=1+1", sourceDealer: "=HYPERLINK(\"http://evil.example\")", invoiceNumber: "@SUM(A1)", remarks: "-cmd"));
        await SeedSystemCostAsync(_factory, user, project.Id, CostOrigin.Payroll, 105_000);

        var file = await DownloadCsvAsync(client, $"{url}/export");

        Assert.Matches(new Regex(@"^Tower-A-costs-\d{4}-\d{2}-\d{2}\.csv$"), FileNameOf(file.Response));
        Assert.Equal(4, file.Rows.Count);
        var rebar = file.Rows.Single(row => row[Array.IndexOf(file.Header, "Description")] == "12mm rebar");
        string Column(string[] row, string name) => row[Array.IndexOf(file.Header, name)];
        Assert.Equal("2025-12-01", Column(rebar, "Date"));
        Assert.Equal("Cebu Builders Depot", Column(rebar, "Supplier"));
        Assert.Equal("SI-0001", Column(rebar, "Invoice number"));
        Assert.Equal("Entered by a person", Column(rebar, "Source"));
        Assert.Equal("pc", Column(rebar, "Unit"));
        Assert.Equal("250.00", Column(rebar, "Unit cost"));
        Assert.Equal("22321.43", Column(rebar, "Net (before VAT)"));
        Assert.Equal("2678.57", Column(rebar, "VAT"));
        Assert.Equal("25000.00", Column(rebar, "Gross (with VAT)"));
        Assert.Equal("Posted", Column(rebar, "Status"));

        var risky = file.Rows.Single(row => Column(row, "Invoice number") == "'@SUM(A1)");
        Assert.Equal("'=1+1", Column(risky, "Description"));
        Assert.Equal("'=HYPERLINK(\"http://evil.example\")", Column(risky, "Supplier"));
        Assert.Equal("'-cmd", Column(risky, "Remarks"));

        var payroll = file.Rows.Single(row => Column(row, "Source") == "Payroll");
        Assert.Equal("1050.00", Column(payroll, "Gross (with VAT)"));

        var manualOnly = await DownloadCsvAsync(client, $"{url}/export?origin=Manual&search=rebar");
        Assert.Equal(2, manualOnly.Rows.Count);
    }

    [SqlServerFact]
    public async Task Reading_entries_costs_a_fixed_number_of_statements_however_many_entries_there_are()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-entries?pageSize=25";
        for (var index = 0; index < 3; index++) await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost());
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        for (var index = 0; index < 40; index++) await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost());
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        Assert.Equal(small, large);
        Assert.Equal(3, large);
    }
}
