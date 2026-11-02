using System.Globalization;
using System.Net;
using System.Text.Json;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.DocumentTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Who may read and change documents, Contract and Insurance kept from people who may not read costs, companies kept apart, and fixed statement counts.</summary>
public sealed class DocumentAccessApiTests(DocumentsApiFixture fixture) : IClassFixture<DocumentsApiFixture>
{
    [SqlServerFact]
    public async Task A_warehouse_keeper_reads_drawings_but_never_sees_a_contract_or_an_insurance_document()
    {
        var (admin, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        var (store, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, AccessRole.WarehouseKeeper, user.TenantId);
        using var _a = admin;
        using var _s = store;
        var project = await admin.CreateProjectAsync();
        var drawing = await UploadedAsync(admin, project.Id, "plan.pdf", Pdf());
        var contract = await UploadedAsync(admin, project.Id, "contract.pdf", Pdf(), Fields("Signed contract", "Contract"));
        var insurance = await LinkedAsync(admin, project.Id, "Insurance", "All-risk policy");

        // The list leaves them out entirely, also when asked for by category.
        var list = await store.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        Assert.Equal([drawing.Id], list.Items.Select(item => item.Id));
        Assert.Equal(1, list.Total);
        foreach (var category in new[] { "Contract", "Insurance" })
            Assert.Empty((await store.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents?category={category}")).Items);

        // By id and by download they don't exist.
        foreach (var hidden in new[] { contract.Id, insurance.Id })
            Assert.Equal(HttpStatusCode.NotFound, (await store.GetAsync($"/api/documents/{hidden}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await store.GetAsync(FileUrl(contract.Id))).StatusCode);

        // The drawing reads and downloads; nothing can be changed.
        Assert.Equal(HttpStatusCode.OK, (await store.GetAsync($"/api/documents/{drawing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await store.GetAsync(FileUrl(drawing.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await store.PostAsync($"/api/projects/{project.Id}/documents", Link())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(store, project.Id, "plan.pdf", Pdf())).StatusCode);

        // The administrator sees all three.
        Assert.Equal(3, (await admin.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents")).Total);
    }

    [SqlServerFact]
    public async Task A_viewer_reads_and_downloads_but_cannot_manage_and_an_hr_officer_cannot_read()
    {
        var (admin, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        var (viewer, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, AccessRole.Viewer, user.TenantId);
        var (hr, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, AccessRole.HrOfficer, user.TenantId);
        using var _a = admin;
        using var _v = viewer;
        using var _h = hr;
        var project = await admin.CreateProjectAsync();
        var contract = await UploadedAsync(admin, project.Id, "contract.pdf", Pdf(), Fields("Signed contract", "Contract"));

        // A viewer may read costs, so a contract is visible to them.
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/projects/{project.Id}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/documents/{contract.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(FileUrl(contract.Id))).StatusCode);
        foreach (var refused in new[]
                 {
                     await viewer.PostAsync($"/api/projects/{project.Id}/documents", Link()),
                     await UploadAsync(viewer, project.Id, "plan.pdf", Pdf()),
                     await viewer.PostAsync($"/api/documents/{contract.Id}/versions", new { url = "https://e.com/a", note = (string?)null, version = contract.Version }),
                     await viewer.PutAsync($"/api/documents/{contract.Id}",
                         new { title = "x", description = (string?)null, category = "Contract", validUntil = (string?)null, version = contract.Version }),
                     await viewer.PostAsync($"/api/documents/{contract.Id}/void", new { reason = "x", version = contract.Version })
                 })
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        foreach (var url in new[] { $"/api/projects/{project.Id}/documents", $"/api/documents/{contract.Id}", FileUrl(contract.Id) })
            Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync(url)).StatusCode);
        Assert.Equal(1, contract.CurrentVersionNumber);
    }

    [SqlServerTheory]
    [InlineData(AccessRole.ProjectManager)]
    [InlineData(AccessRole.CostEngineer)]
    [InlineData(AccessRole.Accountant)]
    public async Task Managers_add_replace_change_and_void_documents(AccessRole role)
    {
        var (admin, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        var (manager, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, role, user.TenantId);
        using var _a = admin;
        using var _m = manager;
        var project = await admin.CreateProjectAsync();

        var document = await UploadedAsync(manager, project.Id, "contract.pdf", Pdf(), Fields("Signed contract", "Contract"));
        var next = await manager.ReadAsync<DocumentDto>(await manager.PostAsync($"/api/documents/{document.Id}/versions",
            new { url = "https://files.example.com/contract-v2", note = (string?)null, version = document.Version }));
        var changed = await manager.ReadAsync<DocumentDto>(await manager.PutAsync($"/api/documents/{document.Id}",
            new { title = "Contract (signed)", description = (string?)null, category = "Contract", validUntil = (string?)null, version = next.Version }));
        var voided = await manager.ReadAsync<DocumentDto>(await manager.PostAsync($"/api/documents/{document.Id}/void",
            new { reason = "Wrong project", version = changed.Version }));

        Assert.Equal(("Void", 2, "Contract (signed)"), (voided.Status, voided.CurrentVersionNumber, voided.Title));
    }

    [SqlServerFact]
    public async Task Another_company_finds_nothing_by_id_by_download_or_by_project()
    {
        var (mine, me) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        var (theirs, them) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _m = mine;
        using var _t = theirs;
        var project = await mine.CreateProjectAsync();
        var document = await UploadedAsync(mine, project.Id, "plan.pdf", Pdf());
        var theirProject = await theirs.CreateProjectAsync();
        await UploadedAsync(theirs, theirProject.Id, "theirs.pdf", Pdf());

        foreach (var response in new[]
                 {
                     await theirs.GetAsync($"/api/documents/{document.Id}"),
                     await theirs.GetAsync(FileUrl(document.Id)),
                     await theirs.GetAsync($"/api/projects/{project.Id}/documents"),
                     await theirs.PostAsync($"/api/projects/{project.Id}/documents", Link()),
                     await UploadAsync(theirs, project.Id, "plan.pdf", Pdf()),
                     await theirs.PostAsync($"/api/documents/{document.Id}/versions", new { url = "https://e.com/a", note = (string?)null, version = document.Version }),
                     await theirs.UploadAsync($"/api/documents/{document.Id}/versions",
                         new Dictionary<string, string?> { ["version"] = document.Version.ToString() }, "plan.pdf", Pdf()),
                     await theirs.PutAsync($"/api/documents/{document.Id}",
                         new { title = "x", description = (string?)null, category = "Drawing", validUntil = (string?)null, version = document.Version }),
                     await theirs.PostAsync($"/api/documents/{document.Id}/void", new { reason = "x", version = document.Version }),
                     // A guessed id is the same as another company's.
                     await theirs.GetAsync(FileUrl(Guid.NewGuid()))
                 })
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Their uploads to my project stored nothing; my document is unchanged.
        Assert.Single(fixture.FilesOf(me.TenantId, project.Id));
        Assert.Empty(fixture.FilesOf(them.TenantId, project.Id));
        var untouched = await mine.GetAsync<DocumentDto>($"/api/documents/{document.Id}");
        Assert.Equal((document.Version, "Active", 1), (untouched.Version, untouched.Status, untouched.CurrentVersionNumber));
    }

    [SqlServerFact]
    public async Task The_list_and_the_read_cost_a_fixed_number_of_statements_however_many_documents_and_versions_exist()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var first = await UploadedAsync(client, project.Id, "plan.pdf", Pdf(1000));

        async Task<(int List, int Filtered, int Read, int Download)> MeasureAsync(Guid documentId) => (
            await CostingTestKit.StatementsAsync(fixture.Factory, async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/documents?pageSize=100")).StatusCode)),
            await CostingTestKit.StatementsAsync(fixture.Factory, async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/documents?category=Drawing&pageSize=100")).StatusCode)),
            await CostingTestKit.StatementsAsync(fixture.Factory, async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{documentId}")).StatusCode)),
            await CostingTestKit.StatementsAsync(fixture.Factory, async () =>
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(FileUrl(documentId))).StatusCode)));

        var small = await MeasureAsync(first.Id);

        // Nine more documents of several kinds, and one document with many versions.
        for (var index = 0; index < 9; index++)
            await LinkedAsync(client, project.Id, index % 2 == 0 ? "Drawing" : "Report", $"Sheet {index}");
        var busy = first;
        for (var step = 0; step < 8; step++)
            busy = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/documents/{busy.Id}/versions",
                new { url = $"https://files.example.com/rev{step}", note = (string?)null, version = busy.Version }));
        Assert.Equal(10, (await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents")).Total);

        var large = await MeasureAsync(busy.Id);

        Assert.Equal(small, large);
        // The project and the organization (for today's date) plus the count and the page; the organization, the document and its versions;
        // the file's location (no organization, no project).
        Assert.Equal((4, 4, 3, 1), large);
        Assert.Equal(9, busy.Versions.Count);
    }

    [SqlServerFact]
    public async Task The_expiring_documents_group_goes_only_to_people_who_manage_documents_and_never_carries_an_amount()
    {
        var (admin, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _a = admin;
        var project = await admin.CreateProjectAsync();
        var today = BondTestKit.Today();
        var expired = await LinkedAsync(admin, project.Id, "Permit", "Excavation permit", BondTestKit.Date(today.AddDays(-2)));
        var soon = await LinkedAsync(admin, project.Id, "Certificate", "Fire safety certificate", BondTestKit.Date(today.AddDays(14)));
        await LinkedAsync(admin, project.Id, "Insurance", "All-risk policy", BondTestKit.Date(today.AddDays(31)));
        var voided = await LinkedAsync(admin, project.Id, "Permit", "Old permit", BondTestKit.Date(today.AddDays(-5)));
        await admin.ReadAsync<DocumentDto>(await admin.PostAsync($"/api/documents/{voided.Id}/void", new { reason = "Replaced", version = voided.Version }));

        foreach (var role in new[] { AccessRole.Administrator, AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant })
        {
            var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, role, user.TenantId);
            using var _c = client;
            var response = await client.GetAsync("/api/needs-action");
            var text = await response.Content.ReadAsStringAsync();
            var queue = JsonSerializer.Deserialize<NeedsActionDto>(text, Infrastructure.ApiClient.Json)!;
            var group = Assert.Single(queue.Groups, item => item.Key == "documents-expiring");
            Assert.Equal(2, group.Count);
            Assert.Equal([expired.Id, soon.Id], group.Rows.Select(row => row.Id));
            Assert.Equal(("Excavation permit", project.Name, true), (group.Rows[0].Label, group.Rows[0].ProjectName, group.Rows[0].IsUrgent));
            Assert.Equal($"Expired 2 days ago, on {today.AddDays(-2).ToString("d MMM yyyy", CultureInfo.InvariantCulture)}", group.Rows[0].Detail);
            Assert.Equal($"Expires in 14 days, on {today.AddDays(14).ToString("d MMM yyyy", CultureInfo.InvariantCulture)}", group.Rows[1].Detail);
            Assert.False(group.Rows[1].IsUrgent);

            using var json = JsonDocument.Parse(text);
            var rows = json.RootElement.GetProperty("groups").EnumerateArray()
                .Single(item => item.GetProperty("key").GetString() == "documents-expiring").GetProperty("rows");
            Assert.All(rows.EnumerateArray(), row => Assert.False(row.TryGetProperty("amountMinor", out _)));
        }

        foreach (var role in new[] { AccessRole.Viewer, AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, role, user.TenantId);
            using var _c = client;
            var queue = await client.GetAsync<NeedsActionDto>("/api/needs-action");
            Assert.DoesNotContain(queue.Groups, item => item.Key == "documents-expiring");
        }
    }
}
