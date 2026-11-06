using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.TestSupport;
using Microsoft.AspNetCore.Hosting;
using static ConstructOPX.Api.Tests.Features.DocumentTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Uploading, linking, downloading and changing project documents on the real SQL Server host, with files in a temporary folder.</summary>
public sealed class DocumentApiTests(DocumentsApiFixture fixture) : IClassFixture<DocumentsApiFixture>
{
    [SqlServerFact]
    public async Task An_uploaded_file_downloads_with_the_exact_bytes_its_hash_and_safe_headers()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var bytes = Pdf(200_000);

        var created = await UploadedAsync(client, project.Id, "Ground floor plan.pdf", bytes, Fields(note: "Issued for construction"));

        Assert.Equal(("Ground floor plan", "Drawing", "Active", 1), (created.Title, created.Category, created.Status, created.CurrentVersionNumber));
        var version = Assert.Single(created.Versions);
        Assert.Equal(("File", "Ground floor plan.pdf", "application/pdf", (long?)200_000), (version.Kind, version.FileName, version.ContentType, version.SizeBytes));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), version.Sha256);
        Assert.Equal("Issued for construction", version.Note);
        Assert.Equal("Test Admin", version.AddedByName);

        var download = await client.GetAsync(FileUrl(created.Id));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Ground floor plan.pdf", download.Content.Headers.ContentDisposition?.FileNameStar ?? download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(["nosniff"], download.Headers.GetValues("X-Content-Type-Options"));
        Assert.True(download.Headers.CacheControl is { Private: true, NoStore: true }, download.Headers.CacheControl?.ToString());

        // The file sits under the company and project, named by a random key, not by its name.
        var stored = Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(stored));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(stored));
    }

    [SqlServerFact]
    public async Task A_file_exactly_at_the_limit_is_kept_and_one_byte_more_leaves_no_file_and_no_row()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var tooLarge = await UploadAsync(client, project.Id, "big.pdf", Pdf(DocumentsApiFixture.LimitBytes + 1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLarge.StatusCode);
        Assert.Equal("The file is larger than 1 MB. Make it smaller, or add a link to where it is kept.",
            (await ProblemAsync(tooLarge)).GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));

        var atLimit = await UploadedAsync(client, project.Id, "limit.pdf", Pdf(DocumentsApiFixture.LimitBytes));
        Assert.Equal(DocumentsApiFixture.LimitBytes, atLimit.Versions[0].SizeBytes);
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
    }

    [SqlServerTheory]
    [InlineData("page.html")]
    [InlineData("logo.svg")]
    [InlineData("script.js")]
    [InlineData("setup.exe")]
    [InlineData("plan.pdf.exe")]
    [InlineData("noextension")]
    public async Task A_type_that_is_not_allowed_is_refused_before_anything_is_stored(string fileName)
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var response = await UploadAsync(client, project.Id, fileName, Pdf());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.StartsWith("This type of file can't be added. Use one of: pdf, jpg,",
            (await ProblemAsync(response)).GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerTheory]
    [InlineData("plan.pdf", "This file isn't a real PDF. Check that you chose the right file.")]
    [InlineData("photo.png", "This file isn't a real PNG image. Check that you chose the right file.")]
    [InlineData("photo.jpg", "This file isn't a real JPEG image. Check that you chose the right file.")]
    public async Task A_file_whose_first_bytes_do_not_match_its_type_is_removed_and_refused(string fileName, string message)
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var response = await UploadAsync(client, project.Id, fileName, "<html><script>alert(1)</script></html>"u8.ToArray());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(message, (await ProblemAsync(response)).GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerFact]
    public async Task An_empty_file_or_a_missing_file_is_refused_with_the_other_problems()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var empty = await UploadAsync(client, project.Id, "notes.txt", []);
        Assert.Equal("The file is empty. Choose the file again.", (await ProblemAsync(empty)).GetProperty("errors").GetProperty("file")[0].GetString());

        var missing = await client.UploadAsync($"/api/projects/{project.Id}/documents", Fields(title: " ", category: "Nonsense"), null, []);
        var errors = (await ProblemAsync(missing)).GetProperty("errors");
        Assert.Equal(["category", "file", "title"], errors.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("Choose a file to upload.", errors.GetProperty("file")[0].GetString());

        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerTheory]
    [InlineData("../../../etc/plan.pdf", "plan.pdf")]
    [InlineData("C:\\Users\\ana\\Desktop\\plan.pdf", "plan.pdf")]
    [InlineData("a<b>c:d|e?f*g;h%i.pdf", "abcdefghi.pdf")]
    [InlineData("Site \"report\".pdf", "Site report.pdf")]
    [InlineData("right\u202Eto-left.pdf", "rightto-left.pdf")]
    [InlineData("Plano de cimentación.pdf", "Plano de cimentación.pdf")]
    public async Task A_name_with_folders_or_odd_characters_is_stored_under_a_key_and_downloaded_with_a_clean_name(string sent, string expected)
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var created = await UploadedAsync(client, project.Id, sent, Pdf());

        Assert.Equal(expected, created.Versions[0].FileName);
        var download = await client.GetAsync(FileUrl(created.Id));
        var disposition = download.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal(expected, disposition.FileNameStar ?? disposition.FileName?.Trim('"'));

        // Nothing was written outside the storage folder, and inside it only key-named files exist.
        var all = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories);
        Assert.All(all, path => Assert.Matches(new Regex("^[0-9a-f-]{36}/[0-9a-f-]{36}/[0-9a-f]{32}$"),
            Path.GetRelativePath(fixture.Root, path).Replace('\\', '/')));
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
    }

    [SqlServerTheory]
    [InlineData("http://files.example.com/plans.pdf")]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("files.example.com/plans.pdf")]
    public async Task A_link_must_be_https(string url)
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();

        var response = await client.PostAsync($"/api/projects/{project.Id}/documents", Link(url: url));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Enter a full link that starts with https://, for example https://drive.example.com/plans.",
            (await ProblemAsync(response)).GetProperty("errors").GetProperty("url")[0].GetString());
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerFact]
    public async Task A_link_document_has_no_file_to_download_and_a_version_never_replaces_the_older_ones()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var linked = await LinkedAsync(client, project.Id, "Drawing", "Structural set");
        Assert.Equal(("Link", "https://files.example.com/photos"), (linked.Versions[0].Kind, linked.Versions[0].Url));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(FileUrl(linked.Id))).StatusCode);

        var first = Pdf(1000);
        var second = await client.ReadAsync<DocumentDto>(await client.UploadAsync($"/api/documents/{linked.Id}/versions",
            new Dictionary<string, string?> { ["note"] = "Revision 1", ["version"] = linked.Version.ToString() }, "rev1.pdf", first));
        var third = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/documents/{linked.Id}/versions",
            new { url = "https://files.example.com/rev2", note = (string?)null, version = second.Version }));

        Assert.Equal(3, third.CurrentVersionNumber);
        Assert.Equal([3, 2, 1], third.Versions.Select(version => version.Number));
        Assert.Equal(["Link", "File", "Link"], third.Versions.Select(version => version.Kind));
        Assert.Equal(first, await (await client.GetAsync(FileUrl(linked.Id, 2))).Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(FileUrl(linked.Id, 4))).StatusCode);
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
    }

    [SqlServerFact]
    public async Task A_stale_version_is_refused_and_a_refused_file_version_leaves_no_file()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var document = await LinkedAsync(client, project.Id, "Permit", "Building permit");
        var changed = await client.ReadAsync<DocumentDto>(await client.PutAsync($"/api/documents/{document.Id}",
            new { title = "Building permit (renewed)", description = (string?)null, category = "Permit", validUntil = "2027-06-30", version = document.Version }));
        Assert.Equal(new DateOnly(2027, 6, 30), changed.ValidUntil);

        var stalePut = await client.PutAsync($"/api/documents/{document.Id}",
            new { title = "Old", description = (string?)null, category = "Permit", validUntil = (string?)null, version = document.Version });
        var staleVoid = await client.PostAsync($"/api/documents/{document.Id}/void", new { reason = "x", version = document.Version });
        var staleLink = await client.PostAsync($"/api/documents/{document.Id}/versions", new { url = "https://e.com/a", note = (string?)null, version = document.Version });
        var staleFile = await client.UploadAsync($"/api/documents/{document.Id}/versions",
            new Dictionary<string, string?> { ["version"] = document.Version.ToString() }, "permit.pdf", Pdf());
        foreach (var stale in new[] { stalePut, staleVoid, staleLink, staleFile })
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("concurrency_conflict", (await ProblemAsync(stale)).GetProperty("code").GetString());
        }
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal("Building permit (renewed)", (await client.GetAsync<DocumentDto>($"/api/documents/{document.Id}")).Title);
    }

    [SqlServerFact]
    public async Task Voiding_needs_a_reason_keeps_the_versions_downloadable_and_stops_changes()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var bytes = Pdf();
        var document = await UploadedAsync(client, project.Id, "contract.pdf", bytes, Fields("Signed contract", "Contract"));

        var noReason = await client.PostAsync($"/api/documents/{document.Id}/void", new { reason = " ", version = document.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);

        var voided = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/documents/{document.Id}/void",
            new { reason = "Uploaded to the wrong project", version = document.Version }));
        Assert.Equal(("Void", "Uploaded to the wrong project", "Test Admin"), (voided.Status, voided.VoidReason, voided.VoidedByName));
        Assert.Equal(bytes, await (await client.GetAsync(FileUrl(document.Id))).Content.ReadAsByteArrayAsync());

        var again = await client.PostAsync($"/api/documents/{document.Id}/versions", new { url = "https://e.com/a", note = (string?)null, version = voided.Version });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("This document was voided, so it can't be given a new version.", (await ProblemAsync(again)).GetProperty("detail").GetString());

        // The list keeps it, after the Active ones.
        await LinkedAsync(client, project.Id);
        var list = await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        Assert.Equal(["Active", "Void"], list.Items.Select(item => item.Status));
    }

    [SqlServerFact]
    public async Task A_repeated_request_returns_the_first_document_and_stores_one_file()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var other = await client.CreateProjectAsync();

        var fields = Fields(clientRequestId: "upload-" + Guid.NewGuid());
        var first = await UploadedAsync(client, project.Id, "plan.pdf", Pdf(), fields);
        var repeat = await UploadedAsync(client, project.Id, "plan.pdf", Pdf(), fields);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(1, await fixture.DocumentRowsAsync(project.Id));

        var linkRequest = "link-" + Guid.NewGuid();
        var linkFirst = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/projects/{project.Id}/documents", Link(clientRequestId: linkRequest)));
        var linkRepeat = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/projects/{project.Id}/documents", Link(clientRequestId: linkRequest)));
        Assert.Equal(linkFirst.Id, linkRepeat.Id);
        Assert.Equal(2, await fixture.DocumentRowsAsync(project.Id));

        // The same request id for another project is refused.
        var reused = await client.PostAsync($"/api/projects/{other.Id}/documents", Link(clientRequestId: linkRequest));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }

    [SqlServerFact]
    public async Task An_upload_without_the_anti_forgery_header_is_refused_and_stores_nothing()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var response = await client.UploadAsync($"/api/projects/{project.Id}/documents", Fields(), "plan.pdf", Pdf(), withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", (await ProblemAsync(response)).GetProperty("code").GetString());
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerFact]
    public async Task An_archived_project_takes_no_document_and_its_documents_stay_readable()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var document = await UploadedAsync(client, project.Id, "plan.pdf", Pdf());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/projects/{project.Id}/archive",
            new { reason = "Finished", note = (string?)null, version = project.Version })).StatusCode);

        var upload = await UploadAsync(client, project.Id, "late.pdf", Pdf());
        var link = await client.PostAsync($"/api/projects/{project.Id}/documents", Link());
        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (upload.StatusCode, link.StatusCode));
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));

        var read = await client.GetAsync<DocumentDto>($"/api/documents/{document.Id}");
        Assert.True(read.ProjectArchived);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(FileUrl(document.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task No_response_ever_carries_a_storage_key_or_a_server_path()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var document = await UploadedAsync(client, project.Id, "plan.pdf", Pdf());
        var next = await client.ReadAsync<DocumentDto>(await client.UploadAsync($"/api/documents/{document.Id}/versions",
            new Dictionary<string, string?> { ["version"] = document.Version.ToString() }, "photo.png", Png()));
        await client.ReadAsync<DocumentDto>(await client.PutAsync($"/api/documents/{document.Id}",
            new { title = "Plan", description = "d", category = "Drawing", validUntil = (string?)null, version = next.Version }));
        await client.GetAsync<DocumentDto>($"/api/documents/{document.Id}");
        await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents?category=Drawing");

        var keys = await fixture.StoreKeysAsync(project.Id);
        Assert.Equal(2, keys.Length);
        Assert.True(client.Bodies.Count >= 6);
        foreach (var body in client.Bodies)
        {
            using var json = JsonDocument.Parse(body);
            Assert.DoesNotContain(PropertyNames(json.RootElement), name =>
                name.Contains("key", StringComparison.OrdinalIgnoreCase) || name.Contains("path", StringComparison.OrdinalIgnoreCase));
            var values = StringValues(json.RootElement).ToArray();
            Assert.DoesNotContain(values, value => keys.Any(key => value.Contains(key, StringComparison.OrdinalIgnoreCase)) ||
                value.Contains(fixture.Root, StringComparison.OrdinalIgnoreCase) || value.Contains(Path.GetFileName(keys[0]), StringComparison.OrdinalIgnoreCase));
        }
    }

    [SqlServerFact]
    public async Task The_list_says_whether_files_can_be_uploaded_and_pages_through_the_documents()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        await LinkedAsync(client, project.Id, "Report", "Weekly report");
        await LinkedAsync(client, project.Id, "Drawing", "Plan B");
        await LinkedAsync(client, project.Id, "Drawing", "Plan A");

        var list = await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents?pageSize=2");
        Assert.Equal((3, 1, 2), (list.Total, list.Page, list.PageSize));
        Assert.Equal(["Plan A", "Plan B"], list.Items.Select(item => item.Title));
        Assert.True(list.Uploads.Available);
        Assert.Equal(("Available", (long)DocumentsApiFixture.LimitBytes), (list.Uploads.State, list.Uploads.MaximumBytes));
        Assert.Contains("dwg", list.Uploads.AllowedExtensions);

        var drawings = await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents?category=drawing");
        Assert.Equal(2, drawings.Total);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/projects/{project.Id}/documents?category=Blueprint")).StatusCode);
    }

    [SqlServerFact]
    public async Task A_permit_shows_its_expiry_the_way_bonds_do()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var today = BondTestKit.Today();

        var soon = await LinkedAsync(client, project.Id, "Permit", "Building permit", BondTestKit.Date(today.AddDays(30)));
        var expired = await LinkedAsync(client, project.Id, "Certificate", "Occupancy certificate", BondTestKit.Date(today.AddDays(-1)));
        var valid = await LinkedAsync(client, project.Id, "Insurance", "All-risk policy", BondTestKit.Date(today.AddDays(31)));

        Assert.Equal(("ExpiringSoon", (int?)30), (soon.Expiry, soon.DaysLeft));
        Assert.Equal(("Expired", (int?)-1), (expired.Expiry, expired.DaysLeft));
        Assert.Equal(("Valid", (int?)31), (valid.Expiry, valid.DaysLeft));

        var drawing = await client.PostAsync($"/api/projects/{project.Id}/documents", Link(category: "Drawing", validUntil: BondTestKit.Date(today)));
        Assert.Equal("Only a permit, an insurance policy or a certificate has a valid-until date. Leave it empty for this kind of document.",
            (await ProblemAsync(drawing)).GetProperty("errors").GetProperty("validUntil")[0].GetString());
    }

    [SqlServerFact]
    public async Task A_renewed_permit_takes_its_new_valid_until_date_with_the_new_version_and_a_drawing_cannot()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var today = BondTestKit.Today();
        var permit = await LinkedAsync(client, project.Id, "Permit", "Building permit", BondTestKit.Date(today.AddDays(5)));

        // A renewed permit uploaded as a file, with its new date in the same form.
        var renewed = await client.ReadAsync<DocumentDto>(await client.UploadAsync($"/api/documents/{permit.Id}/versions",
            new Dictionary<string, string?> { ["version"] = permit.Version.ToString(), ["validUntil"] = BondTestKit.Date(today.AddDays(365)) },
            "renewed-permit.pdf", Pdf()));
        Assert.Equal((2, "Valid", (DateOnly?)today.AddDays(365)), (renewed.CurrentVersionNumber, renewed.Expiry, renewed.ValidUntil));

        // A link version without a date keeps the date.
        var linked = await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/documents/{permit.Id}/versions",
            new { url = "https://files.example.com/permit-scan", note = (string?)null, validUntil = (string?)null, version = renewed.Version }));
        Assert.Equal((3, (DateOnly?)today.AddDays(365)), (linked.CurrentVersionNumber, linked.ValidUntil));

        // A drawing has no date: the version is refused before its file is stored, and nothing changes.
        var drawing = await UploadedAsync(client, project.Id, "plan.pdf", Pdf());
        var refused = await client.UploadAsync($"/api/documents/{drawing.Id}/versions",
            new Dictionary<string, string?> { ["version"] = drawing.Version.ToString(), ["validUntil"] = BondTestKit.Date(today) }, "plan-2.pdf", Pdf());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("validUntil", PropertyNames((await ProblemAsync(refused)).GetProperty("errors")));
        // Two files stored (the renewed permit and the drawing), none for the refused version.
        Assert.Equal(2, (await fixture.StoreKeysAsync(project.Id)).Length);
        Assert.Equal(2, fixture.FilesOf(user.TenantId, project.Id).Length);
        Assert.Equal(1, (await client.GetAsync<DocumentDto>($"/api/documents/{drawing.Id}")).CurrentVersionNumber);
    }

    [SqlServerFact]
    public async Task Audit_lines_name_the_title_and_category_but_never_the_file_name_the_link_or_the_note()
    {
        var (client, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _c = client;
        var project = await client.CreateProjectAsync();
        var document = await UploadedAsync(client, project.Id, "secret-file-name.pdf", Pdf(), Fields("Soil test", "Report", note: "private-note-text"));
        await client.ReadAsync<DocumentDto>(await client.PostAsync($"/api/documents/{document.Id}/versions",
            new { url = "https://files.example.com/private-link", note = "another-private-note", version = document.Version }));

        var audit = await client.GetAsync<PagedResult<Application.Features.Audit.AuditEntryDto>>($"/api/audit?entityId={document.Id}&pageSize=100");
        var summaries = audit.Items.Select(item => item.Summary).ToArray();
        Assert.Contains($"Added document \"Soil test\" (Report) to {project.Name}.", summaries);
        Assert.Contains($"Added version 2 of document \"Soil test\" (Report) on {project.Name}.", summaries);
        var text = string.Join("\n", summaries);
        foreach (var secret in new[] { "secret-file-name", "private-note-text", "private-link", "another-private-note" })
            Assert.DoesNotContain(secret, text);
    }
}

/// <summary>When file storage is not set up or not working, uploads are refused in plain words and links still work.</summary>
public sealed class DocumentCapabilityApiTests(DocumentsApiFixture fixture) : IClassFixture<DocumentsApiFixture>
{
    [SqlServerFact]
    public async Task Without_file_storage_an_upload_is_refused_in_plain_words_and_a_link_still_works()
    {
        // The factory itself has no storage folder configured.
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Factory);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var list = await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        Assert.False(list.Uploads.Available);
        Assert.Equal("NeedsSetup", list.Uploads.State);
        Assert.Equal("File storage is not set up yet, so files can't be uploaded. Your administrator sets the storage folder for the server. " +
            "Add a link to the document instead.", list.Uploads.Message);

        var upload = await UploadAsync(client, project.Id, "plan.pdf", Pdf());
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        Assert.Equal(list.Uploads.Message, (await ProblemAsync(upload)).GetProperty("detail").GetString());
        Assert.Equal(0, await fixture.DocumentRowsAsync(project.Id));

        var link = await client.PostAsync($"/api/projects/{project.Id}/documents", Link());
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
    }

    [SqlServerFact]
    public async Task When_file_storage_is_failing_an_upload_is_refused_and_a_link_still_works()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.FailingHost);
        using var _ = client;
        var project = await client.CreateProjectAsync();

        var list = await client.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        Assert.Equal((false, "Failing"), (list.Uploads.Available, list.Uploads.State));

        var upload = await UploadAsync(client, project.Id, "plan.pdf", Pdf());
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        Assert.Equal("File storage can't be written to right now, so files can't be uploaded. Add a link to the document instead.",
            (await ProblemAsync(upload)).GetProperty("detail").GetString());
        var document = await LinkedAsync(client, project.Id);
        var version = await client.UploadAsync($"/api/documents/{document.Id}/versions",
            new Dictionary<string, string?> { ["version"] = document.Version.ToString() }, "plan.pdf", Pdf());
        Assert.Equal(HttpStatusCode.Conflict, version.StatusCode);

        Assert.Empty(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(1, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerFact]
    public async Task When_uploads_are_switched_off_the_capability_is_unavailable_links_still_work_and_stored_files_still_download()
    {
        var (client, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        using var _ = client;
        var project = await client.CreateProjectAsync();
        var stored = await UploadedAsync(client, project.Id, "plan.pdf", Pdf());

        using var switchedOff = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FileStorage:RootPath", fixture.Root);
            builder.UseSetting("FileStorage:UploadsEnabled", "false");
        });
        var (off, _) = await DocumentsClient.SignInAsync(fixture, switchedOff, tenantId: user.TenantId);
        using var _o = off;

        var list = await off.GetAsync<DocumentListDto>($"/api/projects/{project.Id}/documents");
        Assert.Equal((false, "Unavailable"), (list.Uploads.Available, list.Uploads.State));
        Assert.Equal("Uploading files is switched off for now. Files already added can still be downloaded. Add a link to the document instead.",
            list.Uploads.Message);
        var ready = await off.GetAsync<Application.Features.Capabilities.CapabilitiesDto>("/api/capabilities");
        Assert.Equal("Unavailable", ready.Items[0].State);

        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(off, project.Id, "other.pdf", Pdf())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await off.GetAsync(FileUrl(stored.Id))).StatusCode);
        await LinkedAsync(off, project.Id);
        Assert.Single(fixture.FilesOf(user.TenantId, project.Id));
        Assert.Equal(2, await fixture.DocumentRowsAsync(project.Id));
    }

    [SqlServerFact]
    public async Task The_readiness_list_is_for_administrators_and_names_every_capability()
    {
        var (admin, user) = await DocumentsClient.SignInAsync(fixture, fixture.Host);
        var (manager, _) = await DocumentsClient.SignInAsync(fixture, fixture.Host, Domain.Features.Identity.AccessRole.ProjectManager, user.TenantId);
        var (unset, _) = await DocumentsClient.SignInAsync(fixture, fixture.Factory, tenantId: user.TenantId);
        using var _a = admin;
        using var _m = manager;
        using var _u = unset;

        var ready = await admin.GetAsync<Application.Features.Capabilities.CapabilitiesDto>("/api/capabilities");
        Assert.Equal(["file-storage", "virus-scanning", "email-delivery", "time-clock"], ready.Items.Select(item => item.Key));
        Assert.Equal(["Available", "NeedsSetup", "NeedsSetup", "NeedsSetup"], ready.Items.Select(item => item.State));
        Assert.Equal("Files are stored and offered only as downloads; they are not scanned for viruses.", ready.Items[1].Message);
        Assert.Equal("Notifications appear in the app; email is not set up.", ready.Items[2].Message);
        // No time clock is registered in this company, so it needs setup and attendance by hand still works.
        Assert.Equal("Record attendance by hand on the project's Team tab.", ready.Items[3].Fallback);
        Assert.Null(ready.Items[0].Fallback);

        var notSet = await unset.GetAsync<Application.Features.Capabilities.CapabilitiesDto>("/api/capabilities");
        Assert.Equal(("NeedsSetup", "Add a link to the document instead."), (notSet.Items[0].State, notSet.Items[0].Fallback));
        Assert.DoesNotContain(fixture.Root, unset.Bodies.Single());

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/capabilities")).StatusCode);
    }
}
