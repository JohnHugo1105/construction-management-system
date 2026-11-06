using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Documents;

/// <summary>What a person types about a document. The category and the valid-until date can change while the document is Active.</summary>
public sealed record DocumentDetails(string? Title, string? Description, DocumentCategory Category, DateOnly? ValidUntil);

/// <summary>A file that was stored, as the file store reported it. The store key is the file's place in the store and never leaves the server.</summary>
/// <param name="FileName">The cleaned name (see <see cref="DocumentFiles.CleanFileName"/>).</param>
/// <param name="Sha256">The lower-case hex SHA-256 of the bytes, worked out while they were stored.</param>
public sealed record StoredDocumentFile(string FileName, string ContentType, long SizeBytes, string Sha256, string StoreKey);

/// <summary>
/// A drawing, permit, contract, certificate, photo or other document kept on a project. It has one or more versions, each a stored file or an
/// <c>https://</c> link; a new version never replaces the old ones, which stay downloadable. A permit, an insurance policy or a certificate may
/// carry the date it is valid until; it is "expiring soon" 30 days before and "expired" after, worked out on read and never stored (the same
/// rule as bonds). A document is voided with a reason, never deleted. Every change needs the project not to be archived.
/// </summary>
public sealed class ProjectDocument : AggregateRoot
{
    /// <summary>An Active document with this many days or fewer before its valid-until date is "expiring soon". The bond rule's figure.</summary>
    public const int ExpiringSoonDays = 30;

    public const int MaximumTitleLength = 120;
    public const int MaximumDescriptionLength = 300;
    public const int MaximumNoteLength = 300;
    public const int MaximumReasonLength = 300;
    public const int MaximumStoreKeyLength = 120;

    /// <summary>The categories that carry a valid-until date.</summary>
    public static readonly IReadOnlyList<DocumentCategory> DatedCategories = [DocumentCategory.Permit, DocumentCategory.Insurance, DocumentCategory.Certificate];

    /// <summary>The categories that reveal prices: only people who may read costs see them.</summary>
    public static readonly IReadOnlyList<DocumentCategory> PricedCategories = [DocumentCategory.Contract, DocumentCategory.Insurance];

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);
    private static readonly DateOnly LatestDate = new(2100, 12, 31);

    private readonly List<DocumentVersion> _versions = [];

    private ProjectDocument() { }

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public DocumentCategory Category { get; private set; }

    /// <summary>The last day the document is valid, for a permit, an insurance policy or a certificate.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Active;

    /// <summary>The newest version's number; the first version is 1.</summary>
    public int CurrentVersionNumber { get; private set; }

    public string? VoidReason { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<DocumentVersion> Versions => _versions;

    private DocumentDetails Current => new(Title, Description, Category, ValidUntil);

    // ---- Expiry -----------------------------------------------------------------------------------------------------------------

    /// <summary>Whole days from <paramref name="today"/> to the valid-until date: zero on the last day, negative once it has passed.</summary>
    public static int DaysLeft(DateOnly validUntil, DateOnly today) => validUntil.DayNumber - today.DayNumber;

    /// <summary>Valid, ExpiringSoon (30 days or fewer left, the last day included) or Expired (the date has passed).</summary>
    public static DocumentExpiry ExpiryFor(DateOnly validUntil, DateOnly today)
    {
        var daysLeft = DaysLeft(validUntil, today);
        return daysLeft < 0 ? DocumentExpiry.Expired : daysLeft <= ExpiringSoonDays ? DocumentExpiry.ExpiringSoon : DocumentExpiry.Valid;
    }

    /// <summary>The expiry state of an Active document with a valid-until date; null otherwise.</summary>
    public static DocumentExpiry? ExpiryFor(DocumentStatus status, DateOnly? validUntil, DateOnly today) =>
        status == DocumentStatus.Active && validUntil is { } date ? ExpiryFor(date, today) : null;

    public DocumentExpiry? ExpiryOn(DateOnly today) => ExpiryFor(Status, ValidUntil, today);

    public static bool IsPriced(DocumentCategory category) => PricedCategories.Contains(category);

    // ---- Checks a caller runs before storing a file -----------------------------------------------------------------------------

    /// <summary>
    /// Checks the details and the note of a new document, all problems together, so an upload is refused before its file is stored. The same
    /// checks run again when the document is created.
    /// </summary>
    public static void CheckNew(DocumentDetails details, string? note)
    {
        var errors = new ValidationErrors();
        CheckDetails(details, errors);
        Field(errors, () => Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        errors.ThrowIfAny();
    }

    /// <summary>
    /// Checks the note of a new version, and the new valid-until date when one is given, all problems together, before its file is stored.
    /// The same checks run again when the version is added.
    /// </summary>
    public void CheckNewVersion(string? note, DateOnly? validUntil)
    {
        var errors = new ValidationErrors();
        Field(errors, () => Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        if (validUntil is { } date) CheckValidUntil(Category, date, errors);
        errors.ThrowIfAny();
    }

    // ---- Lifecycle --------------------------------------------------------------------------------------------------------------

    /// <summary>A new document with its first version: a stored file (<paramref name="file"/>) or a link (<paramref name="url"/>), never both.</summary>
    public static ProjectDocument Create(Guid id, Guid tenantId, Project project, DocumentDetails details, StoredDocumentFile? file, string? url,
        string? note, Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        var document = new ProjectDocument { ProjectId = project.Id, CreatedByUserId = createdByUserId };
        var errors = new ValidationErrors();
        document.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));
        var clean = CheckDetails(details, errors);
        var version = NewVersion(errors, file, url, note);
        errors.ThrowIfAny();

        document.Apply(clean!);
        document.Initialize(id, tenantId, now);
        document.Append(version!, createdByUserId, now);
        return document;
    }

    /// <summary>
    /// Adds a newer version (a file or a link). The older versions stay as they are. A renewed permit, insurance policy or certificate can
    /// carry its new valid-until date (<paramref name="validUntil"/>), saved with the version in one change; null keeps the date as it is.
    /// </summary>
    public DocumentVersion AddVersion(Project project, StoredDocumentFile? file, string? url, string? note, Guid userId, DateTimeOffset now,
        DateOnly? validUntil = null)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("given a new version");
        var errors = new ValidationErrors();
        var version = NewVersion(errors, file, url, note);
        if (validUntil is { } date) CheckValidUntil(Category, date, errors);
        errors.ThrowIfAny();
        Append(version!, userId, now);
        if (validUntil is not null) ValidUntil = validUntil;
        Touch(now);
        return _versions[^1];
    }

    /// <summary>Changes the title, description, category and valid-until date. Returns false, and changes nothing, when they are the same.</summary>
    public bool Edit(Project project, DocumentDetails details, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("changed");
        var errors = new ValidationErrors();
        var clean = CheckDetails(details, errors);
        errors.ThrowIfAny();
        if (clean == Current) return false;
        Apply(clean!);
        Touch(now);
        return true;
    }

    /// <summary>Marks the document as no longer in use. It stays on record with the reason; its versions can still be downloaded.</summary>
    public void Void(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        if (Status == DocumentStatus.Void) throw new DomainRuleException("This document was already voided.");
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for voiding it", MaximumReasonLength);
        Status = DocumentStatus.Void;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
        Touch(now);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private static DocumentDetails? CheckDetails(DocumentDetails details, ValidationErrors errors)
    {
        var title = Field(errors, () => Guard.RequiredText(details.Title, "title", "the document's title", MaximumTitleLength));
        var description = Field(errors, () => Guard.OptionalText(details.Description, "description", "the description", MaximumDescriptionLength));
        var categoryOk = Enum.IsDefined(details.Category);
        if (!categoryOk)
            errors.Add("category", "Choose what kind of document it is: contract, drawing, permit, insurance, certificate, photo, report or other.");

        if (details.ValidUntil is { } validUntil)
        {
            if (validUntil < EarliestDate || validUntil > LatestDate) errors.Add("validUntil", "Choose a valid date.");
            else if (categoryOk) CheckValidUntil(details.Category, validUntil, errors);
        }

        return errors.Any ? null : new DocumentDetails(title, description, details.Category, details.ValidUntil);
    }

    private static void CheckValidUntil(DocumentCategory category, DateOnly validUntil, ValidationErrors errors)
    {
        if (validUntil < EarliestDate || validUntil > LatestDate) errors.Add("validUntil", "Choose a valid date.");
        else if (!DatedCategories.Contains(category))
            errors.Add("validUntil", "Only a permit, an insurance policy or a certificate has a valid-until date. Leave it empty for this kind of document.");
    }

    private static PendingVersion? NewVersion(ValidationErrors errors, StoredDocumentFile? file, string? url, string? note)
    {
        var cleanNote = Field(errors, () => Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        if (file is not null && url is not null) throw new ArgumentException("A version is a file or a link, not both.");
        if (file is null)
        {
            var link = Field(errors, () => DocumentLinks.Clean(url));
            return errors.Any ? null : new PendingVersion(DocumentVersionKind.Link, null, link, cleanNote);
        }

        if (file.SizeBytes <= 0) errors.Add("file", "The file is empty. Choose the file again.");
        if (file.FileName.Length is 0 or > DocumentFiles.MaximumFileNameLength || !DocumentFiles.IsSafeExtension(DocumentFiles.ExtensionOf(file.FileName)))
            throw new ArgumentException("The file name must be cleaned and checked before the file is stored.", nameof(file));
        if (file.Sha256.Length != 64 || string.IsNullOrWhiteSpace(file.StoreKey) || file.StoreKey.Length > MaximumStoreKeyLength)
            throw new ArgumentException("A stored file needs its hash and its place in the store.", nameof(file));
        return errors.Any ? null : new PendingVersion(DocumentVersionKind.File, file, null, cleanNote);
    }

    private void Apply(DocumentDetails details)
    {
        Title = details.Title!;
        Description = details.Description;
        Category = details.Category;
        ValidUntil = details.ValidUntil;
    }

    private void Append(PendingVersion version, Guid userId, DateTimeOffset now)
    {
        CurrentVersionNumber++;
        _versions.Add(new DocumentVersion(Guid.NewGuid(), TenantId, Id, CurrentVersionNumber, version.Kind, version.File, version.Url, version.Note,
            userId, now));
    }

    private void EnsureActive(string doing)
    {
        if (Status != DocumentStatus.Active) throw new DomainRuleException($"This document was voided, so it can't be {doing}.");
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this document.", nameof(project));
    }

    /// <summary>"3 Dec 2026", for messages.</summary>
    public static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }

    private sealed record PendingVersion(DocumentVersionKind Kind, StoredDocumentFile? File, string? Url, string? Note);
}

/// <summary>
/// One version of a document: a stored file (its cleaned name, type, size, SHA-256 and its place in the store) or an <c>https://</c> link, with
/// an optional note, who added it and when. Append-only: a version is never changed or removed.
/// </summary>
public sealed class DocumentVersion
{
    private DocumentVersion() { }

    internal DocumentVersion(Guid id, Guid tenantId, Guid documentId, int number, DocumentVersionKind kind, StoredDocumentFile? file, string? url,
        string? note, Guid addedByUserId, DateTimeOffset addedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        DocumentId = documentId;
        Number = number;
        Kind = kind;
        FileName = file?.FileName;
        ContentType = file?.ContentType;
        SizeBytes = file?.SizeBytes;
        Sha256 = file?.Sha256;
        StoreKey = file?.StoreKey;
        Url = url;
        Note = note;
        AddedByUserId = addedByUserId;
        AddedAtUtc = addedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Number { get; private set; }
    public DocumentVersionKind Kind { get; private set; }
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public string? Sha256 { get; private set; }

    /// <summary>Where the file is kept in the file store. Never sent to a browser.</summary>
    public string? StoreKey { get; private set; }

    public string? Url { get; private set; }
    public string? Note { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
}
