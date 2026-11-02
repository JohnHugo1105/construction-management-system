using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The project document rules: details, versions, links, files, voiding and the valid-until boundaries (docs/development/notes/documents-rules.md).</summary>
public sealed class DocumentTests
{
    private static readonly DateOnly Today = TestData.Today; // 2 Nov 2026
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly Guid User = TestData.UserId;
    private const string Link = "https://files.example.com/plans.pdf";
    private static readonly string Hash = new('a', 64);

    private static DocumentDetails Details(string? title = "Ground floor plan", string? description = null,
        DocumentCategory category = DocumentCategory.Drawing, DateOnly? validUntil = null) => new(title, description, category, validUntil);

    private static StoredDocumentFile File(string name = "plan.pdf", long size = 2048) =>
        new(name, "application/pdf", size, Hash, $"{TestData.Tenant:D}/{Guid.NewGuid():D}/{Guid.NewGuid():N}");

    private static (ProjectDocument Document, Project Project) WithLink(DocumentDetails? details = null)
    {
        var project = TestData.NewProject();
        return (ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project, details ?? Details(), null, Link, null, User, null, Now), project);
    }

    private static IReadOnlyDictionary<string, string[]> Errors(Action action) => Assert.Throws<ValidationFailedException>(action).Errors;

    // ---- Creating ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_document_is_active_with_version_one_and_trimmed_details()
    {
        var project = TestData.NewProject();
        var document = ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project,
            Details("  Building permit ", "  City permit ", DocumentCategory.Permit, new DateOnly(2027, 1, 31)), File(), null, " original ", User, " r-1 ", Now);

        Assert.Equal((DocumentStatus.Active, 1, "Building permit", "City permit"), (document.Status, document.CurrentVersionNumber, document.Title, document.Description));
        Assert.Equal((DocumentCategory.Permit, (DateOnly?)new DateOnly(2027, 1, 31), "r-1"), (document.Category, document.ValidUntil, document.ClientRequestId));
        var version = Assert.Single(document.Versions);
        Assert.Equal((1, DocumentVersionKind.File, "plan.pdf", 2048L, "original"), (version.Number, version.Kind, version.FileName, version.SizeBytes, version.Note));
        Assert.Equal((document.Id, TestData.Tenant, User, Now), (version.DocumentId, version.TenantId, version.AddedByUserId, version.AddedAtUtc));
        Assert.Null(version.Url);
    }

    [Fact]
    public void Every_problem_in_the_details_is_reported_at_once()
    {
        var project = TestData.NewProject();
        var errors = Errors(() => ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project,
            new DocumentDetails(" ", new string('d', 301), (DocumentCategory)99, new DateOnly(2027, 1, 1)), null, "http://files.example.com", new string('n', 301),
            User, null, Now));

        Assert.Equal(["category", "description", "note", "title", "url"], errors.Keys.Order());
        Assert.Equal("Enter the document's title.", errors["title"][0]);
        Assert.Equal("The description can be at most 300 characters.", errors["description"][0]);
        Assert.Equal("The note can be at most 300 characters.", errors["note"][0]);
    }

    [Fact]
    public void A_title_of_120_characters_is_kept_and_121_is_refused()
    {
        Assert.Equal(120, WithLink(Details(new string('t', 120))).Document.Title.Length);
        Assert.Equal("The document's title can be at most 120 characters.", Errors(() => WithLink(Details(new string('t', 121))))["title"][0]);
    }

    [Theory]
    [InlineData(DocumentCategory.Permit)]
    [InlineData(DocumentCategory.Insurance)]
    [InlineData(DocumentCategory.Certificate)]
    public void Permits_insurance_and_certificates_may_carry_a_valid_until_date(DocumentCategory category) =>
        Assert.Equal(new DateOnly(2027, 3, 1), WithLink(Details(category: category, validUntil: new DateOnly(2027, 3, 1))).Document.ValidUntil);

    [Theory]
    [InlineData(DocumentCategory.Contract)]
    [InlineData(DocumentCategory.Drawing)]
    [InlineData(DocumentCategory.Photo)]
    [InlineData(DocumentCategory.Report)]
    [InlineData(DocumentCategory.Other)]
    public void Other_kinds_of_document_have_no_valid_until_date(DocumentCategory category) =>
        Assert.Equal("Only a permit, an insurance policy or a certificate has a valid-until date. Leave it empty for this kind of document.",
            Errors(() => WithLink(Details(category: category, validUntil: new DateOnly(2027, 3, 1))))["validUntil"][0]);

    [Fact]
    public void A_valid_until_date_outside_2000_to_2100_is_refused() =>
        Assert.Equal("Choose a valid date.", Errors(() => WithLink(Details(category: DocumentCategory.Permit, validUntil: new DateOnly(1999, 12, 31))))["validUntil"][0]);

    [Fact]
    public void A_document_cannot_be_added_to_an_archived_project()
    {
        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, Now);

        Assert.Throws<DomainRuleException>(() =>
            ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project, Details(), null, Link, null, User, null, Now));
    }

    [Fact]
    public void An_empty_file_is_refused()
    {
        var project = TestData.NewProject();
        Assert.Equal("The file is empty. Choose the file again.",
            Errors(() => ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project, Details(), File(size: 0), null, null, User, null, Now))["file"][0]);
    }

    [Fact]
    public void A_file_whose_name_was_not_cleaned_and_checked_is_a_programming_error()
    {
        var project = TestData.NewProject();
        Assert.Throws<ArgumentException>(() =>
            ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project, Details(), File("page.html"), null, null, User, null, Now));
        Assert.Throws<ArgumentException>(() =>
            ProjectDocument.Create(Guid.NewGuid(), TestData.Tenant, project, Details(), File(), Link, null, User, null, Now));
    }

    // ---- Links -------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("https://files.example.com/plans.pdf")]
    [InlineData("  HTTPS://drive.example.com/a?b=c#d  ")]
    public void An_https_link_is_accepted_and_trimmed(string url) =>
        Assert.Equal(url.Trim(), DocumentLinks.Clean(url));

    [Theory]
    [InlineData("http://files.example.com/plans.pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://files.example.com/a")]
    [InlineData("files.example.com/plans.pdf")]
    [InlineData("https:// files.example.com")]
    [InlineData("/relative/path")]
    public void Every_other_scheme_or_a_partial_address_is_refused(string url) =>
        Assert.Equal("Enter a full link that starts with https://, for example https://drive.example.com/plans.",
            Assert.Throws<ValidationFailedException>(() => DocumentLinks.Clean(url)).Errors["url"][0]);

    [Fact]
    public void A_link_with_a_password_in_it_or_over_500_characters_is_refused()
    {
        Assert.Equal("Use a link without a user name or password in it.",
            Assert.Throws<ValidationFailedException>(() => DocumentLinks.Clean("https://ana:secret@files.example.com/a")).Errors["url"][0]);
        Assert.Equal(500, DocumentLinks.Clean("https://e.com/" + new string('a', 486)).Length);
        Assert.Equal("The link can be at most 500 characters.",
            Assert.Throws<ValidationFailedException>(() => DocumentLinks.Clean("https://e.com/" + new string('a', 487))).Errors["url"][0]);
        Assert.Equal("Enter the link to the document.", Assert.Throws<ValidationFailedException>(() => DocumentLinks.Clean("  ")).Errors["url"][0]);
    }

    // ---- Versions ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void New_versions_are_numbered_in_order_and_never_replace_the_old_ones()
    {
        var (document, project) = WithLink();
        var later = Now.AddHours(1);

        var second = document.AddVersion(project, File("rev-1.pdf"), null, "Revision 1", User, later);
        var third = document.AddVersion(project, null, "https://files.example.com/rev-2", null, User, later);

        Assert.Equal((2, 3, 3), (second.Number, third.Number, document.CurrentVersionNumber));
        Assert.Equal([1, 2, 3], document.Versions.Select(version => version.Number));
        Assert.Equal([DocumentVersionKind.Link, DocumentVersionKind.File, DocumentVersionKind.Link], document.Versions.Select(version => version.Kind));
        Assert.Equal(Link, document.Versions[0].Url);
        Assert.Equal(later, document.UpdatedAtUtc);
    }

    [Fact]
    public void A_voided_document_or_an_archived_project_takes_no_new_version()
    {
        var (document, project) = WithLink();
        document.Void(project, "Wrong project", User, Now);
        Assert.Equal("This document was voided, so it can't be given a new version.",
            Assert.Throws<DomainRuleException>(() => document.AddVersion(project, null, Link, null, User, Now)).Message);

        var (other, otherProject) = WithLink();
        otherProject.Archive(ArchiveReason.Finished, null, Now);
        Assert.Throws<DomainRuleException>(() => other.AddVersion(otherProject, null, Link, null, User, Now));
        Assert.Equal(1, other.CurrentVersionNumber);
    }

    [Fact]
    public void A_new_version_can_carry_a_renewed_valid_until_date_only_for_a_dated_document()
    {
        var (permit, project) = WithLink(Details(category: DocumentCategory.Permit, validUntil: Today.AddDays(3)));
        permit.AddVersion(project, null, Link, "Renewed", User, Now, Today.AddDays(365));
        Assert.Equal((2, (DateOnly?)Today.AddDays(365)), (permit.CurrentVersionNumber, permit.ValidUntil));
        permit.AddVersion(project, null, Link, null, User, Now);
        Assert.Equal((3, (DateOnly?)Today.AddDays(365)), (permit.CurrentVersionNumber, permit.ValidUntil));

        var (drawing, drawingProject) = WithLink();
        Assert.Contains("validUntil", Errors(() => drawing.CheckNewVersion(null, Today)).Keys);
        Assert.Equal(["note", "validUntil"], Errors(() => permit.CheckNewVersion(new string('n', 301), new DateOnly(1999, 1, 1))).Keys.Order());
        Assert.Contains("validUntil", Errors(() => drawing.AddVersion(drawingProject, null, Link, null, User, Now, Today)).Keys);
        Assert.Equal((1, (DateOnly?)null), (drawing.CurrentVersionNumber, drawing.ValidUntil));
    }

    [Fact]
    public void A_version_with_a_bad_link_changes_nothing()
    {
        var (document, project) = WithLink();
        Assert.Contains("url", Errors(() => document.AddVersion(project, null, "javascript:alert(1)", null, User, Now)).Keys);
        Assert.Equal((1, 1), (document.CurrentVersionNumber, document.Versions.Count));
    }

    // ---- Editing and voiding -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Editing_changes_the_details_and_an_unchanged_edit_changes_nothing()
    {
        var (document, project) = WithLink();
        Assert.False(document.Edit(project, Details(), Now.AddHours(1)));
        Assert.Equal(Now, document.UpdatedAtUtc);

        Assert.True(document.Edit(project, Details("Fire safety permit", null, DocumentCategory.Permit, new DateOnly(2027, 2, 1)), Now.AddHours(2)));
        Assert.Equal(("Fire safety permit", DocumentCategory.Permit, (DateOnly?)new DateOnly(2027, 2, 1)), (document.Title, document.Category, document.ValidUntil));
        Assert.Equal(1, document.CurrentVersionNumber);
    }

    [Fact]
    public void Voiding_needs_a_reason_keeps_the_versions_and_happens_once()
    {
        var (document, project) = WithLink();
        Assert.Equal("Enter the reason for voiding it.", Errors(() => document.Void(project, " ", User, Now))["reason"][0]);

        document.Void(project, "  Uploaded to the wrong project ", User, Now);

        Assert.Equal((DocumentStatus.Void, "Uploaded to the wrong project", (Guid?)User), (document.Status, document.VoidReason, document.VoidedByUserId));
        Assert.Single(document.Versions);
        Assert.Equal("This document was already voided.", Assert.Throws<DomainRuleException>(() => document.Void(project, "again", User, Now)).Message);
        Assert.Equal("This document was voided, so it can't be changed.", Assert.Throws<DomainRuleException>(() => document.Edit(project, Details("x"), Now)).Message);
    }

    // ---- Valid-until expiry (the bond rule) -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(31, DocumentExpiry.Valid)]
    [InlineData(30, DocumentExpiry.ExpiringSoon)]
    [InlineData(1, DocumentExpiry.ExpiringSoon)]
    [InlineData(0, DocumentExpiry.ExpiringSoon)]
    [InlineData(-1, DocumentExpiry.Expired)]
    public void Expiry_is_judged_on_the_company_day_with_30_days_warning(int daysLeft, DocumentExpiry expected)
    {
        Assert.Equal(expected, ProjectDocument.ExpiryFor(Today.AddDays(daysLeft), Today));
        Assert.Equal(daysLeft, ProjectDocument.DaysLeft(Today.AddDays(daysLeft), Today));
    }

    [Fact]
    public void Only_an_active_document_with_a_date_has_an_expiry()
    {
        var (undated, _) = WithLink();
        Assert.Null(undated.ExpiryOn(Today));

        var (permit, project) = WithLink(Details(category: DocumentCategory.Permit, validUntil: Today.AddDays(-3)));
        Assert.Equal(DocumentExpiry.Expired, permit.ExpiryOn(Today));
        permit.Void(project, "Replaced", User, Now);
        Assert.Null(permit.ExpiryOn(Today));
    }

    [Fact]
    public void Contract_and_insurance_are_the_priced_categories() =>
        Assert.Equal([DocumentCategory.Contract, DocumentCategory.Insurance],
            Enum.GetValues<DocumentCategory>().Where(ProjectDocument.IsPriced));

    // ---- Files ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("plan.pdf", "plan.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("C:\\Users\\ana\\Desktop\\Site photo.JPG", "Site photo.jpg")]
    [InlineData("  ..hidden  ..pdf", "hidden.pdf")]
    [InlineData("a<b>c:d\"e|f?g*h;i%j`k.pdf", "abcdefghijk.pdf")]
    [InlineData("line\r\nbreak\u0000.pdf", "linebreak.pdf")]
    [InlineData("right\u202Eto-left.pdf", "rightto-left.pdf")]
    [InlineData("many     spaces.pdf", "many spaces.pdf")]
    [InlineData(".pdf", "document.pdf")]
    [InlineData("../", "document")]
    [InlineData("Plano de cimentación.pdf", "Plano de cimentación.pdf")]
    public void A_file_name_is_cut_to_its_last_part_and_cleaned(string original, string expected) =>
        Assert.Equal(expected, DocumentFiles.CleanFileName(original));

    [Fact]
    public void A_long_file_name_is_cut_to_200_characters_keeping_its_extension()
    {
        var cleaned = DocumentFiles.CleanFileName(new string('x', 300) + ".docx");
        Assert.Equal(200, cleaned.Length);
        Assert.EndsWith("x.docx", cleaned);
    }

    [Theory]
    [InlineData("plan.pdf", "pdf")]
    [InlineData("photo.JPEG", "jpeg")]
    [InlineData("site.dwg", "dwg")]
    [InlineData("notes.txt", "txt")]
    public void The_allowed_types_are_accepted(string name, string extension) =>
        Assert.Equal(extension, DocumentFiles.CheckExtension(DocumentFiles.CleanFileName(name), DocumentFiles.AllowedExtensions));

    [Theory]
    [InlineData("page.html")]
    [InlineData("page.htm")]
    [InlineData("logo.svg")]
    [InlineData("script.js")]
    [InlineData("setup.exe")]
    [InlineData("archive.zip")]
    [InlineData("no-extension")]
    [InlineData("plan.pdf.exe")]
    public void Every_other_type_is_refused(string name) =>
        Assert.StartsWith("This type of file can't be added. Use one of: pdf, jpg, jpeg, png, webp, doc, docx, xls, xlsx, ppt, pptx, csv, txt, dwg, dxf.",
            Assert.Throws<ValidationFailedException>(() => DocumentFiles.CheckExtension(DocumentFiles.CleanFileName(name), DocumentFiles.AllowedExtensions))
                .Errors["file"][0]);

    [Fact]
    public void A_narrowed_list_refuses_what_it_leaves_out()
    {
        var message = Assert.Throws<ValidationFailedException>(() => DocumentFiles.CheckExtension("photo.png", ["pdf"])).Errors["file"][0];
        Assert.Equal("This type of file can't be added. Use one of: pdf.", message);
    }

    [Fact]
    public void Pdf_and_image_files_must_start_like_their_type()
    {
        Assert.True(DocumentFiles.StartsLike("pdf", "%PDF-1.7\n"u8));
        Assert.False(DocumentFiles.StartsLike("pdf", "<html>"u8));
        Assert.True(DocumentFiles.StartsLike("jpg", [0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.False(DocumentFiles.StartsLike("jpeg", [0x89, 0x50, 0x4E, 0x47]));
        Assert.True(DocumentFiles.StartsLike("png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.True(DocumentFiles.StartsLike("webp", "RIFF\u0010\0\0\0WEBPVP8 "u8));
        Assert.False(DocumentFiles.StartsLike("webp", "RIFF\u0010\0\0\0WAVEfmt "u8));
        // Other types have no fixed start and are not checked.
        Assert.True(DocumentFiles.StartsLike("docx", "anything"u8));
    }

    [Fact]
    public void The_content_type_comes_from_the_extension_and_drawings_are_never_shown_by_a_browser()
    {
        Assert.Equal("application/pdf", DocumentFiles.ContentTypeFor("pdf"));
        Assert.Equal("application/octet-stream", DocumentFiles.ContentTypeFor("dwg"));
        Assert.Equal("text/plain", DocumentFiles.ContentTypeFor("txt"));
        Assert.Throws<ArgumentException>(() => DocumentFiles.ContentTypeFor("html"));
        Assert.Equal(25L * 1024 * 1024, DocumentFiles.DefaultMaximumBytes);
    }
}
