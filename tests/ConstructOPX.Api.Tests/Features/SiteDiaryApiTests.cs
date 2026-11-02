using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.SiteDiary;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;
using static ConstructOPX.Api.Tests.Features.SiteDiaryTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>The site diary over the real API and SQL Server: the round trip, every refusal, the filled-in panels and the fixed statement counts (docs/development/notes/site-diary-rules.md).</summary>
public sealed class SiteDiaryApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public SiteDiaryApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_report_is_written_read_listed_corrected_and_voided_and_its_day_can_then_be_written_again()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var today = DaysFromToday(0);

        var created = await client.PostAsync(Route(project.Id), NewEntry(today, "Rain", "Showers from 1 pm", issues: "Truck came late", visitors: "Client walk-through",
            safety: "Toolbox talk", subcontractors: 8));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/projects/{project.Id}/diary/{today}", created.Headers.Location!.ToString());
        var entry = await client.ReadAsync<DiaryEntryDto>(created);

        Assert.Equal((project.Id, project.Name, false, today), (entry.ProjectId, entry.ProjectName, entry.ProjectArchived, Date(entry.ReportDate)));
        Assert.Equal(("Rain", "Showers from 1 pm", "Truck came late", "Client walk-through", "Toolbox talk", 8),
            (entry.Weather, entry.WeatherNote, entry.IssuesAndDelays, entry.VisitorsAndInspections, entry.SafetyNotes, entry.SubcontractorWorkers));
        Assert.Equal(("Posted", "Test Admin", true), (entry.Status, entry.AuthorName, entry.CanChange));
        Assert.Equal(DaysFromToday(1), Date(entry.ChangeableUntil));
        Assert.Equal(0, entry.OnSite.Count);
        Assert.Empty(entry.Deliveries);

        var again = await client.GetAsync<DiaryEntryDto>(Route(project.Id, today));
        Assert.Equal((entry.Id, entry.Version, entry.WorkDone), (again.Id, again.Version, again.WorkDone));

        var list = await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id));
        var row = Assert.Single(list.Items);
        Assert.Equal((entry.Id, "Rain", "Poured the second-floor slab.", 8, 0), (row.Id, row.Weather, row.WorkDoneFirstLine, row.SubcontractorWorkers, row.WorkersOnSite));

        // A correction changes the fields and the version, and the audit line names the fields only.
        var corrected = await client.ReadAsync<DiaryEntryDto>(await CorrectAsync(client, entry, Change(entry, weather: "Storm", workDone: "SECRET-WORK-TEXT slab poured", subcontractors: 9)));
        Assert.Equal(("Storm", "SECRET-WORK-TEXT slab poured", 9, entry.Version + 1), (corrected.Weather, corrected.WorkDone, corrected.SubcontractorWorkers, corrected.Version));
        var summaries = await WorkforceTestKit.AuditSummariesAsync(_factory, user.TenantId);
        var line = Assert.Single(summaries, summary => summary.StartsWith("Corrected the site report", StringComparison.Ordinal));
        Assert.Equal($"Corrected the site report for {Today().ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} on {project.Name}: changed weather, work done, subcontractor workers.", line);
        Assert.DoesNotContain(summaries, summary => summary.Contains("SECRET-WORK-TEXT", StringComparison.Ordinal) || summary.Contains("Toolbox", StringComparison.Ordinal));

        // The same details again change nothing: no new version, no extra audit line.
        var same = await client.ReadAsync<DiaryEntryDto>(await CorrectAsync(client, corrected, Change(corrected)));
        Assert.Equal(corrected.Version, same.Version);
        Assert.Single(await WorkforceTestKit.AuditSummariesAsync(_factory, user.TenantId), summary => summary.StartsWith("Corrected the site report", StringComparison.Ordinal));

        // Voiding keeps it on record with the reason, takes it out of the list and frees the day.
        var voided = await client.ReadAsync<DiaryEntryDto>(await VoidAsync(client, same, "Wrote it on the wrong project"));
        Assert.Equal(("Void", false), (voided.Status, voided.CanChange));
        Assert.Empty((await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(project.Id, today))).StatusCode);
        var voidLine = Assert.Single(await WorkforceTestKit.AuditSummariesAsync(_factory, user.TenantId), summary => summary.StartsWith("Voided the site report", StringComparison.Ordinal));
        Assert.DoesNotContain("wrong project", voidLine);

        var rewritten = await CreateAsync(client, project.Id, today, "Second try");
        Assert.NotEqual(entry.Id, rewritten.Id);
        Assert.Equal("Second try", rewritten.WorkDone);
        var voidedAgain = await VoidAsync(client, voided);
        Assert.Equal(HttpStatusCode.Conflict, voidedAgain.StatusCode); // the live report for the day is the new one: the voided one's version is stale
    }

    [SqlServerFact]
    public async Task The_list_is_newest_first_and_pages()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        foreach (var offset in new[] { -5, -1, -3, 0, -2 }) await CreateAsync(client, project.Id, DaysFromToday(offset), $"Work on day {offset}");

        var first = await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id) + "?page=1&pageSize=2");
        var second = await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id) + "?page=2&pageSize=2");

        Assert.Equal((5, 2), (first.Total, first.Items.Count));
        Assert.Equal(new[] { DaysFromToday(0), DaysFromToday(-1) }, first.Items.Select(item => Date(item.ReportDate)).ToArray());
        Assert.Equal(new[] { DaysFromToday(-2), DaysFromToday(-3) }, second.Items.Select(item => Date(item.ReportDate)).ToArray());
    }

    [SqlServerFact]
    public async Task Every_problem_with_a_new_report_is_reported_at_once_in_plain_words()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsync(Route(project.Id), NewEntry(DaysFromToday(1), "Sunny", new string('n', 201), " ", new string('i', 1001), subcontractors: -3));

        var fields = await ErrorFieldsAsync(response);
        Assert.Equal(new[] { "issuesAndDelays", "reportDate", "subcontractorWorkers", "weather", "weatherNote", "workDone" }, fields.Order().ToArray());
        Assert.Contains("hasn't come yet", await ErrorMessageAsync(response, "reportDate"));
        Assert.Contains("Choose the weather", await ErrorMessageAsync(response, "weather"));
        // A number is not a weather word.
        Assert.Contains("weather", await ErrorFieldsAsync(await client.PostAsync(Route(project.Id), NewEntry(DaysFromToday(0), "1"))));
        Assert.Empty((await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Items);
    }

    [SqlServerFact]
    public async Task A_second_report_for_a_day_is_refused_in_plain_words_and_a_repeated_request_returns_the_first()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var day = DaysFromToday(-1);

        var first = await client.PostAsync<DiaryEntryDto>(Route(project.Id), NewEntry(day, clientRequestId: "req-diary-1"));
        var repeat = await client.PostAsync(Route(project.Id), NewEntry(day, "Storm", workDone: "Different words", clientRequestId: "req-diary-1"));
        var duplicate = await client.PostAsync(Route(project.Id), NewEntry(day, clientRequestId: "req-diary-2"));
        var reused = await client.PostAsync(Route(project.Id), NewEntry(DaysFromToday(-2), clientRequestId: "req-diary-1"));

        var repeated = await client.ReadAsync<DiaryEntryDto>(repeat);
        Assert.Equal((first.Id, first.Weather, first.WorkDone), (repeated.Id, repeated.Weather, repeated.WorkDone));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(duplicate));
        Assert.Contains("There is already a site report for", await DetailAsync(duplicate));
        Assert.Contains("Open it to correct it", await DetailAsync(duplicate));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Contains("already used for a different report", await DetailAsync(reused));
        Assert.Single((await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Items);
    }

    [SqlServerFact]
    public async Task A_stale_version_is_a_conflict_for_a_correction_and_a_void()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await CreateAsync(client, project.Id);
        await client.ReadAsync<DiaryEntryDto>(await CorrectAsync(client, entry, Change(entry, weather: "Cloudy")));

        var staleCorrection = await CorrectAsync(client, entry, Change(entry, weather: "Storm"));
        var staleVoid = await VoidAsync(client, entry);

        Assert.Equal(HttpStatusCode.Conflict, staleCorrection.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleVoid.StatusCode);
        Assert.Equal("Cloudy", (await client.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(0)))).Weather);
    }

    [SqlServerFact]
    public async Task An_archived_project_keeps_its_diary_readable_but_takes_no_change()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await CreateAsync(client, project.Id, DaysFromToday(-1));
        await ArchiveAsync(client, project);

        var read = await client.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(-1)));
        Assert.Equal((true, false), (read.ProjectArchived, read.CanChange));
        Assert.Single((await client.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Items);
        foreach (var refused in new[]
                 {
                     await client.PostAsync(Route(project.Id), NewEntry(DaysFromToday(0))),
                     await CorrectAsync(client, read, Change(read, weather: "Storm")),
                     await VoidAsync(client, read)
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("This project is archived. Restore it before making changes.", await DetailAsync(refused));
        }
        Assert.Equal(entry.Version, (await client.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(-1)))).Version);
    }

    [SqlServerFact]
    public async Task Another_companys_project_and_reports_are_not_found_on_every_route()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var project = await CreateProjectAsync(mine);
        var entry = await CreateAsync(mine, project.Id, DaysFromToday(0));
        var theirProject = await CreateProjectAsync(theirs);
        var today = DaysFromToday(0);

        // My project through their session.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(Route(project.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(Route(project.Id, today))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(Route(project.Id, today, "filled-in"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync(Route(project.Id), NewEntry(DaysFromToday(-1)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync(Route(project.Id, today), Change(entry, weather: "Storm"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await VoidAsync(theirs, entry)).StatusCode);
        // My report's date on their own project: nothing there, and nothing was changed by the attempts above.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(Route(theirProject.Id, today))).StatusCode);
        Assert.Equal(("Fair", "Posted"), ((await mine.GetAsync<DiaryEntryDto>(Route(project.Id, today))).Weather, (await mine.GetAsync<DiaryEntryDto>(Route(project.Id, today))).Status));
        Assert.Empty((await theirs.GetAsync<PagedResult<DiaryListItemDto>>(Route(theirProject.Id))).Items);
    }

    [SqlServerFact]
    public async Task The_author_changes_a_report_in_the_window_a_colleague_cannot_and_a_project_manager_always_can()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var (author, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, user.TenantId);
        var (colleague, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, user.TenantId);
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        using var _u = author;
        using var _c = colleague;
        using var _m = manager;

        // Written today and the day before yesterday: the first is inside the author window, the second is not (it ended yesterday).
        var inWindow = await author.PostAsync<DiaryEntryDto>(Route(project.Id), NewEntry(DaysFromToday(0)));
        var pastWindow = await author.PostAsync<DiaryEntryDto>(Route(project.Id), NewEntry(DaysFromToday(-2)));
        Assert.True(inWindow.CanChange);
        Assert.False(pastWindow.CanChange);

        // The author corrects inside the window; a colleague with the same role is refused; the window's last day is yesterday + 1 = today for a report of yesterday.
        var corrected = await author.ReadAsync<DiaryEntryDto>(await CorrectAsync(author, inWindow, Change(inWindow, weather: "Cloudy")));
        Assert.Equal("Cloudy", corrected.Weather);
        var notAuthor = await CorrectAsync(colleague, corrected, Change(corrected, weather: "Storm"));
        Assert.Equal(HttpStatusCode.Conflict, notAuthor.StatusCode);
        Assert.Contains("Only the person who wrote this report", await DetailAsync(notAuthor));
        Assert.False((await colleague.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(0)))).CanChange);

        // After the window the author is refused with the date it ended, and a colleague too; the manager and the administrator are not limited.
        var late = await CorrectAsync(author, pastWindow, Change(pastWindow, weather: "Storm"));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains($"until the end of {Today().AddDays(-1).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}", await DetailAsync(late));
        var lateVoid = await VoidAsync(author, pastWindow);
        Assert.Equal(HttpStatusCode.Conflict, lateVoid.StatusCode);
        var byManager = await manager.ReadAsync<DiaryEntryDto>(await CorrectAsync(manager, pastWindow, Change(pastWindow, weather: "Storm")));
        Assert.Equal("Storm", byManager.Weather);
        Assert.True((await manager.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(-2)))).CanChange);
        var byAdmin = await admin.ReadAsync<DiaryEntryDto>(await CorrectAsync(admin, byManager, Change(byManager, weather: "Rain")));
        Assert.Equal("Rain", byAdmin.Weather);

        // A void follows the same who and when: a colleague is refused, the author may void inside the window, the manager voids any.
        Assert.Equal(HttpStatusCode.Conflict, (await VoidAsync(colleague, corrected)).StatusCode);
        Assert.Equal("Void", (await author.ReadAsync<DiaryEntryDto>(await VoidAsync(author, corrected))).Status);
        Assert.Equal("Void", (await manager.ReadAsync<DiaryEntryDto>(await VoidAsync(manager, byAdmin))).Status);
    }

    [SqlServerFact]
    public async Task A_void_needs_a_reason()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var entry = await CreateAsync(client, project.Id);

        var response = await VoidAsync(client, entry, " ");

        Assert.Contains("reason", await ErrorFieldsAsync(response));
        Assert.Contains("Enter the reason", await ErrorMessageAsync(response, "reason"));
        Assert.Equal("Posted", (await client.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(0)))).Status);
    }

    // ---- What is filled in, not typed -------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_filled_in_panels_equal_the_attendance_and_deliveries_recorded_that_day_on_this_project()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin, "Tower A");
        var other = await CreateProjectAsync(admin, "Tower B");

        // Three workers present on the day; a fourth was voided; a fifth was present on another day; a sixth on another project.
        await WorkerOnSiteAsync(admin, project, "W-1", "Juan", "Reyes");
        await WorkerOnSiteAsync(admin, project, "W-2", "Ana", "Abad");
        await WorkerOnSiteAsync(admin, project, "W-3", "Ben", "Cruz");
        var voided = await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, "W-4", "Vic", "Zamora"), project);
        var attendance = await WorkforceTestKit.RecordAsync(admin, voided.Id, project.Id, SiteDay, 8m);
        await admin.PostAsync($"/api/attendance/{attendance.Id}/void", new { reason = "Entered twice", version = attendance.Version });
        var elsewhere = await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, "W-5", "Eli", "Yap"), project);
        await WorkforceTestKit.RecordAsync(admin, elsewhere.Id, project.Id, "2026-10-13", 8m);
        var onOther = await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, "W-6", "Gil", "Xu"), other);
        await WorkforceTestKit.RecordAsync(admin, onOther.Id, other.Id, SiteDay, 8m);

        // Deliveries: two receipts of rebar on the day sum up, a second item, a voided receipt, another day and another project's order are left out.
        var firm = await CreateFirmAsync(admin, "Visayas Steel Corp.");
        var order = await DeliveredAsync(admin, project, firm, SiteDay, "DR-1", ("Rebar 16 mm", "pc", 100m, 40m), ("Cement", "bag", 50m, 10m));
        var second = await admin.PostAsync($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 15m)], "DR-2", SiteDay));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var wrong = await admin.PostAsync($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(2, 7m)], "DR-3", SiteDay));
        var wrongReceipt = (await admin.ReadAsync<PurchaseOrderDto>(wrong)).Receipts.Single(receipt => receipt.DeliveryReceiptNumber == "DR-3");
        await admin.PostAsync($"/api/purchase-orders/{order.Id}/receipts/{wrongReceipt.Id}/void", new { reason = "Counted the wrong truck", version = wrongReceipt.Version });
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 5m)], "DR-4", "2026-10-13"))).StatusCode);
        var otherFirm = await CreateFirmAsync(admin, "Other Supply");
        await DeliveredAsync(admin, other, otherFirm, SiteDay, "DR-5", ("Gravel", "m3", 20m, 20m));

        var entry = await admin.PostAsync<DiaryEntryDto>(Route(project.Id), NewEntry(SiteDay));

        // Counted once each (each worker has one entry a day), and the names are sorted by last name, for a caller who may see the worker list.
        Assert.Equal(3, entry.OnSite.Count);
        Assert.Equal(new[] { "Ana Abad", "Ben Cruz", "Juan Reyes" }, entry.OnSite.Names);
        Assert.Equal(new[] { ("Visayas Steel Corp.", "Cement", "bag", 10m), ("Visayas Steel Corp.", "Rebar 16 mm", "pc", 55m) },
            entry.Deliveries.Select(item => (item.SupplierName, item.Description, item.Unit, item.Quantity)).ToArray());
        Assert.Equal(3, (await admin.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Items.Single().WorkersOnSite);

        // The day's panels can be read before the report is written, and they are the same.
        var preview = await admin.GetAsync<DiaryFilledInDto>(Route(project.Id, SiteDay, "filled-in"));
        Assert.Equal(entry.OnSite.Count, preview.OnSite.Count);
        Assert.Equal(entry.OnSite.Names, preview.OnSite.Names);
        Assert.Equal(entry.Deliveries.ToArray(), preview.Deliveries.ToArray());
        var quiet = await admin.GetAsync<DiaryFilledInDto>(Route(project.Id, "2026-10-12", "filled-in"));
        Assert.Equal(0, quiet.OnSite.Count);
        Assert.Empty(quiet.OnSite.Names!);
        Assert.Empty(quiet.Deliveries);
        _ = user;
    }

    [SqlServerFact]
    public async Task A_reader_without_the_worker_list_gets_the_count_but_no_names_and_nobody_gets_money()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        await WorkerOnSiteAsync(admin, project, "W-1", "Juan", "Reyes");
        await WorkerOnSiteAsync(admin, project, "W-2", "Ana", "Abad");
        var firm = await CreateFirmAsync(admin, "Visayas Steel Corp.");
        await DeliveredAsync(admin, project, firm, SiteDay, "DR-1", ("Rebar 16 mm", "pc", 100m, 40m));
        var entry = await admin.PostAsync<DiaryEntryDto>(Route(project.Id), NewEntry(SiteDay));
        var moneyWords = new[] { "minor", "cost", "price", "vat", "total", "invoice", "committed", "amount", "currency", "rate", "gross", "net", "pay", "wage", "salary", "hours" };

        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.CostEngineer })
        {
            var (reader, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _r = reader;
            var raw = await RawAsync(await reader.GetAsync(Route(project.Id, SiteDay)));
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNames(raw, names);
            CollectNames(await RawAsync(await reader.GetAsync(Route(project.Id, SiteDay, "filled-in"))), names);

            Assert.Equal(2, raw.GetProperty("onSite").GetProperty("count").GetInt32());
            Assert.False(raw.GetProperty("onSite").TryGetProperty("names", out _), $"{role} must not receive the workers' names");
            Assert.DoesNotContain("Reyes", raw.ToString());
            Assert.DoesNotContain("Abad", raw.ToString());
            Assert.Equal("Rebar 16 mm", raw.GetProperty("deliveries")[0].GetProperty("description").GetString());
            Assert.DoesNotContain(names, name => moneyWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }

        // Names appear for every reader who holds the worker list (here the Accountant and the Viewer), and money is still absent.
        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer, AccessRole.ProjectManager })
        {
            var (reader, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _r = reader;
            var read = await reader.GetAsync<DiaryEntryDto>(Route(project.Id, SiteDay));
            Assert.Equal(new[] { "Ana Abad", "Juan Reyes" }, read.OnSite.Names);
        }

        // The exact names the Angular models (web/src/app/features/site-diary/site-diary.models.ts) read: the two sides of the contract agree.
        var all = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectNames(await RawAsync(await admin.GetAsync(Route(project.Id, SiteDay))), all);
        CollectNames(await RawAsync(await admin.GetAsync(Route(project.Id))), all);
        Assert.Equal(new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "projectId", "projectName", "projectArchived", "reportDate", "weather", "weatherNote", "workDone", "issuesAndDelays", "visitorsAndInspections",
            "safetyNotes", "subcontractorWorkers", "status", "authorName", "createdAtUtc", "updatedAtUtc", "changeableUntil", "canChange", "onSite", "count", "names",
            "deliveries", "supplierName", "description", "unit", "quantity", "version", "items", "total", "page", "pageSize", "workersOnSite", "workDoneFirstLine"
        }, all);
        _ = entry;
    }

    // ---- Statement counts -------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_list_and_the_report_cost_a_fixed_number_of_statements_however_many_reports_workers_and_deliveries_exist()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _k = keeper;
        var firm = await CreateFirmAsync(admin);
        await WorkerOnSiteAsync(admin, project, "W-1", "Juan", "Reyes");
        await DeliveredAsync(admin, project, firm, SiteDay, "DR-1", ("Rebar 16 mm", "pc", 100m, 40m));
        await CreateAsync(admin, project.Id, SiteDay);

        async Task<(int List, int Entry, int Keeper, int Preview)> MeasureAsync() =>
        (
            await StatementsAsync(_factory, async () => await admin.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))),
            await StatementsAsync(_factory, async () => await admin.GetAsync<DiaryEntryDto>(Route(project.Id, SiteDay))),
            await StatementsAsync(_factory, async () => await keeper.GetAsync<DiaryEntryDto>(Route(project.Id, SiteDay))),
            await StatementsAsync(_factory, async () => await admin.GetAsync<DiaryFilledInDto>(Route(project.Id, SiteDay, "filled-in")))
        );

        var one = await MeasureAsync();

        // Nine more reports, nine more workers and several more delivery lines on the same day.
        for (var offset = 1; offset <= 9; offset++) await CreateAsync(admin, project.Id, DaysFromToday(-offset));
        for (var worker = 2; worker <= 10; worker++) await WorkerOnSiteAsync(admin, project, $"W-{worker}", $"First{worker}", $"Last{worker}");
        await DeliveredAsync(admin, project, firm, SiteDay, "DR-2", ("Cement", "bag", 50m, 10m), ("Sand", "m3", 30m, 12m), ("Gravel", "m3", 30m, 12m));
        var ten = await MeasureAsync();

        Assert.Equal(one, ten);
        Assert.Equal(10, (await admin.GetAsync<PagedResult<DiaryListItemDto>>(Route(project.Id))).Total);
        Assert.Equal(10, (await admin.GetAsync<DiaryEntryDto>(Route(project.Id, SiteDay))).OnSite.Count);
        // The list: the project, the count and the page (the headcount rides in the page). The report: the project, the company's calendar day, the
        // report, the workers' count, their names (only for a caller who may see the worker list) and the deliveries. The preview: the project
        // and those three. Nothing grows with the data.
        Assert.Equal((3, 6, 5, 4), ten);
    }
}
