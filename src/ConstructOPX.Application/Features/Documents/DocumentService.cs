using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Capabilities;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Documents;

/// <summary>
/// Project documents: add a document with its first version (a file or a link), add newer versions, change its details, void it, list and
/// read them, and download a version's file. Contract and Insurance documents reveal prices, so a person who may not read costs never sees
/// them: they are left out of lists and are "not found" by id and by download. A file is stored before its record is written; when anything
/// after that fails, the file is removed, so a failed upload leaves no file and no row. Uploading needs file storage to be Available (the
/// capability check); a link always works.
/// </summary>
public sealed class DocumentService(IDocumentRepository documents, IDocumentReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, IFileStore files, CapabilityService capabilities, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    public async Task<DocumentListDto> ListAsync(Guid projectId, string? category, PageRequest page, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, cancellationToken);
        DocumentCategory? filter = string.IsNullOrWhiteSpace(category)
            ? null
            : Parse(category, (DocumentCategory)(-1)) is var parsed && Enum.IsDefined(parsed)
                ? parsed
                : throw new ValidationFailedException("category", "Choose Contract, Drawing, Permit, Insurance, Certificate, Photo, Report or Other.");

        var uploads = await UploadsAsync(cancellationToken);
        // Asking for a category the person may not see gives the same empty answer as a category with nothing in it.
        if (filter is { } only && ProjectDocument.IsPriced(only) && !CanSeePriced)
            return new DocumentListDto([], 0, page.SafePage, page.SafePageSize, uploads);

        var (items, total) = await reader.ListAsync(projectId, filter, CanSeePriced, await TodayAsync(cancellationToken), page, cancellationToken);
        return new DocumentListDto(items, total, page.SafePage, page.SafePageSize, uploads);
    }

    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken cancellationToken) => await ReadAsync(id, cancellationToken);

    /// <summary>A version's file, for the authenticated download route. A link version, a hidden category or another company's id is "not found".</summary>
    public async Task<DocumentDownload> DownloadAsync(Guid id, int versionNumber, CancellationToken cancellationToken)
    {
        var location = await reader.FileAsync(id, versionNumber, cancellationToken);
        if (location is null || (ProjectDocument.IsPriced(location.Category) && !CanSeePriced)) throw new NotFoundException("that document");
        if (location.Kind != DocumentVersionKind.File || location.StoreKey is null || location.FileName is null || location.ContentType is null)
            throw new NotFoundException("a file for that version");

        Stream? content;
        try
        {
            content = await files.OpenReadAsync(location.StoreKey, cancellationToken);
        }
        catch (FileStoreException)
        {
            throw new DomainRuleException("This file can't be opened right now because file storage isn't working. Try again later, or ask your administrator.");
        }
        return content is null
            ? throw new NotFoundException("that file. It is missing from file storage; ask your administrator to restore it from the backup")
            : new DocumentDownload(content, location.ContentType, location.FileName);
    }

    // ---- New documents ----------------------------------------------------------------------------------------------------------

    public async Task<DocumentDto> CreateLinkAsync(Guid projectId, CreateDocumentLinkRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await documents.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, projectId, cancellationToken);

        var project = await LoadProjectAsync(projectId, cancellationToken);
        var details = new DocumentDetails(request.Title, request.Description, Parse(request.Category, (DocumentCategory)(-1)), request.ValidUntil);
        EnsureMayHandle(details.Category);
        var document = ProjectDocument.Create(Guid.NewGuid(), currentUser.TenantId, project, details, null, request.Url, request.Note, currentUser.UserId,
            requestId, clock.UtcNow);
        return await SaveNewAsync(document, project, requestId, null, cancellationToken);
    }

    /// <summary>
    /// A new document whose first version is an uploaded file. Everything that can be checked before the file arrives is checked first (the
    /// project, the details, the note, the type of file and whether file storage works), so a refused upload stores nothing.
    /// </summary>
    public async Task<DocumentDto> CreateFileAsync(Guid projectId, DocumentUploadFields fields, string? fileName, Stream content,
        CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(fields.ClientRequestId);
        if (requestId is not null && await documents.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, projectId, cancellationToken);

        var project = await LoadProjectAsync(projectId, cancellationToken);
        project.EnsureNotArchived();
        var errors = new ValidationErrors();
        var validUntil = Collect(errors, () => ParseDate(fields.ValidUntil, "validUntil"));
        var details = new DocumentDetails(fields.Title, fields.Description, Parse(fields.Category, (DocumentCategory)(-1)), validUntil);
        Collect(errors, () => { ProjectDocument.CheckNew(details, fields.Note); return true; });
        var (cleanName, extension) = CheckFile(errors, fileName);
        errors.ThrowIfAny();
        EnsureMayHandle(details.Category);
        await EnsureUploadsAvailableAsync(cancellationToken);

        var stored = await StoreAsync(project, cleanName, extension, content, cancellationToken);
        try
        {
            var document = ProjectDocument.Create(Guid.NewGuid(), currentUser.TenantId, project, details, stored, null, fields.Note, currentUser.UserId,
                requestId, clock.UtcNow);
            return await SaveNewAsync(document, project, requestId, stored.StoreKey, cancellationToken);
        }
        catch
        {
            await files.DeleteAsync(stored.StoreKey, CancellationToken.None);
            throw;
        }
    }

    // ---- Changes ----------------------------------------------------------------------------------------------------------------

    public async Task<DocumentDto> AddLinkVersionAsync(Guid id, AddDocumentLinkVersionRequest request, CancellationToken cancellationToken)
    {
        var (document, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var version = document.AddVersion(project, null, request.Url, request.Note, currentUser.UserId, clock.UtcNow, request.ValidUntil);
        RecordVersion(document, project, version.Number);
        return await SaveAndReadAsync(document, cancellationToken);
    }

    /// <summary>
    /// A newer version that is an uploaded file, with a renewed valid-until date when one is sent. The version the person opened, the note,
    /// the date and the type of file are checked before the file arrives.
    /// </summary>
    public async Task<DocumentDto> AddFileVersionAsync(Guid id, DocumentUploadFields fields, string? fileName, Stream content,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(fields.Version, NumberStyles.None, CultureInfo.InvariantCulture, out var openedVersion))
            throw new ValidationFailedException("version", "Reload the page and try again.");
        var (document, project) = await LoadForChangeAsync(id, openedVersion, cancellationToken);
        project.EnsureNotArchived();
        if (document.Status != DocumentStatus.Active) throw new DomainRuleException("This document was voided, so it can't be given a new version.");

        var errors = new ValidationErrors();
        var validUntil = Collect(errors, () => ParseDate(fields.ValidUntil, "validUntil"));
        Collect(errors, () => { document.CheckNewVersion(fields.Note, validUntil); return true; });
        var (cleanName, extension) = CheckFile(errors, fileName);
        errors.ThrowIfAny();
        await EnsureUploadsAvailableAsync(cancellationToken);

        var stored = await StoreAsync(project, cleanName, extension, content, cancellationToken);
        try
        {
            var version = document.AddVersion(project, stored, null, fields.Note, currentUser.UserId, clock.UtcNow, validUntil);
            RecordVersion(document, project, version.Number);
            // The file is already stored: finish the save even if the client has gone, so a dropped connection cannot leave a row whose file was deleted.
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            await files.DeleteAsync(stored.StoreKey, CancellationToken.None);
            throw;
        }
        return await ReadAsync(document.Id, cancellationToken);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var (document, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var details = new DocumentDetails(request.Title, request.Description, Parse(request.Category, (DocumentCategory)(-1)), request.ValidUntil);
        if (Enum.IsDefined(details.Category)) EnsureMayHandle(details.Category);
        if (!document.Edit(project, details, clock.UtcNow)) return await ReadAsync(document.Id, cancellationToken);

        audit.Record("Document.Edited", nameof(ProjectDocument), document.Id,
            $"Changed the details of document \"{document.Title}\" ({document.Category}) on {project.Name}.");
        return await SaveAndReadAsync(document, cancellationToken);
    }

    public async Task<DocumentDto> VoidAsync(Guid id, VoidDocumentRequest request, CancellationToken cancellationToken)
    {
        var (document, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        document.Void(project, request.Reason, currentUser.UserId, clock.UtcNow);
        // The reason is free text and stays on the record, not in the audit line.
        audit.Record("Document.Voided", nameof(ProjectDocument), document.Id,
            $"Voided document \"{document.Title}\" ({document.Category}) on {project.Name}.");
        return await SaveAndReadAsync(document, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    /// <summary>Contract and Insurance documents reveal prices: only people who may read costs see them.</summary>
    private bool CanSeePriced => Permissions.IsAllowed(currentUser.Roles, Permissions.CostsRead);

    private void EnsureMayHandle(DocumentCategory category)
    {
        if (ProjectDocument.IsPriced(category) && !CanSeePriced)
            throw new ValidationFailedException("category", "Only people who can see costs can add contracts and insurance documents. Choose another kind.");
    }

    private async Task<DocumentUploadsDto> UploadsAsync(CancellationToken cancellationToken)
    {
        var status = await capabilities.GetAsync(CapabilityKeys.FileStorage, cancellationToken);
        var limits = files.Limits;
        var available = status.State == CapabilityState.Available;
        return new DocumentUploadsDto(available, status.State.ToString(), available ? status.Message : UploadRefusal(status), limits.MaximumBytes,
            limits.AllowedExtensions);
    }

    private async Task EnsureUploadsAvailableAsync(CancellationToken cancellationToken)
    {
        var status = await capabilities.GetAsync(CapabilityKeys.FileStorage, cancellationToken);
        if (status.State != CapabilityState.Available) throw new DomainRuleException(UploadRefusal(status));
    }

    private static string UploadRefusal(CapabilityStatus status) => status.Fallback is null ? status.Message : $"{status.Message} {status.Fallback}";

    /// <summary>The cleaned file name and its checked extension; the problems go into <paramref name="errors"/>.</summary>
    private (string Name, string Extension) CheckFile(ValidationErrors errors, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            errors.Add("file", "Choose a file to upload.");
            return ("", "");
        }
        var cleanName = DocumentFiles.CleanFileName(fileName);
        var extension = Collect(errors, () => DocumentFiles.CheckExtension(cleanName, files.Limits.AllowedExtensions));
        return (cleanName, extension ?? "");
    }

    /// <summary>
    /// Streams the file into the store under a new key. The store stops at the size limit and leaves nothing; a file whose first bytes don't
    /// match its type is removed again. Only a file that passed every check comes back.
    /// </summary>
    private async Task<StoredDocumentFile> StoreAsync(Project project, string cleanName, string extension, Stream content,
        CancellationToken cancellationToken)
    {
        var key = FileStoreKeys.New(currentUser.TenantId, project.Id);
        var limit = files.Limits.MaximumBytes;
        StoredFile? stored;
        try
        {
            stored = await files.SaveAsync(key, content, limit, cancellationToken);
        }
        catch (FileStoreException)
        {
            throw new DomainRuleException("The file couldn't be saved because file storage isn't working. Add a link to the document instead, or try again later.");
        }

        if (stored is null)
            throw new ValidationFailedException("file",
                $"The file is larger than {SizeWords(limit)}. Make it smaller, or add a link to where it is kept.");
        if (stored.SizeBytes == 0 || !DocumentFiles.StartsLike(extension, stored.LeadingBytes))
        {
            await files.DeleteAsync(key, CancellationToken.None);
            throw new ValidationFailedException("file", stored.SizeBytes == 0
                ? "The file is empty. Choose the file again."
                : $"This file isn't a real {DocumentFiles.TypeWords(extension)}. Check that you chose the right file.");
        }
        return new StoredDocumentFile(cleanName, DocumentFiles.ContentTypeFor(extension), stored.SizeBytes, stored.Sha256, key);
    }

    private async Task<DocumentDto> SaveNewAsync(ProjectDocument document, Project project, string? requestId, string? storeKey,
        CancellationToken cancellationToken)
    {
        documents.Add(document);
        // The title and category only: the file name, the link and the note never go into the audit line.
        audit.Record("Document.Added", nameof(ProjectDocument), document.Id,
            $"Added document \"{document.Title}\" ({document.Category}) to {project.Name}.");
        try
        {
            // A stored file is already on disk: finish the save even if the client has gone, rather than leave a file with no row (or a row whose
            // file the cleanup removed). A link has no file, so it keeps the client's token.
            await unitOfWork.SaveChangesAsync(storeKey is not null ? CancellationToken.None : cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made, and keep no second file.
            if (await documents.FindByClientRequestAsync(requestId, cancellationToken) is { } first)
            {
                if (storeKey is not null) await files.DeleteAsync(storeKey, CancellationToken.None);
                return await SameCreateAsync(first, project.Id, cancellationToken);
            }
            throw;
        }
        return await ReadAsync(document.Id, cancellationToken);
    }

    private void RecordVersion(ProjectDocument document, Project project, int number) =>
        audit.Record("Document.VersionAdded", nameof(ProjectDocument), document.Id,
            $"Added version {number} of document \"{document.Title}\" ({document.Category}) on {project.Name}.");

    private async Task<DocumentDto> SaveAndReadAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(document.Id, cancellationToken);
    }

    private async Task<DocumentDto> SameCreateAsync(DocumentRequestOwner owner, Guid projectId, CancellationToken cancellationToken) =>
        owner.ProjectId == projectId
            ? await ReadAsync(owner.DocumentId, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different document. Reload the page and try again.");

    /// <summary>Loads the document and its project for a change. A document the person may not see is "not found"; the version must be the one they opened.</summary>
    private async Task<(ProjectDocument Document, Project Project)> LoadForChangeAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken);
        if (document is null || (ProjectDocument.IsPriced(document.Category) && !CanSeePriced)) throw new NotFoundException("that document");
        ConcurrencyGuard.Ensure(document.Version, version);
        return (document, await LoadProjectAsync(document.ProjectId, cancellationToken));
    }

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task<DocumentDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, CanSeePriced, await TodayAsync(cancellationToken), cancellationToken) ?? throw new NotFoundException("that document");

    /// <summary>The company's calendar day: expiry is judged on it, never on the UTC day.</summary>
    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    /// <summary>"25 MB", "1.5 MB".</summary>
    public static string SizeWords(long bytes)
    {
        var megabytes = bytes / (1024m * 1024m);
        return $"{decimal.Round(megabytes, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture)} MB";
    }

    private static DateOnly? ParseDate(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ValidationFailedException(field, "Choose a valid date.");
    }

    /// <summary>A word from the request as the enum, or <paramref name="invalid"/> (which the domain reports as "choose one"). Numbers are not words.</summary>
    private static T Parse<T>(string? text, T invalid) where T : struct, Enum =>
        !string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) && Enum.TryParse<T>(text.Trim(), ignoreCase: true, out var value) &&
        Enum.IsDefined(value)
            ? value
            : invalid;

    private static T? Collect<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}
