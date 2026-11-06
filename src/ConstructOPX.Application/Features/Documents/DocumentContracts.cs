using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Documents;

namespace ConstructOPX.Application.Features.Documents;

public interface IDocumentRepository
{
    /// <summary>Loads a document with its versions, tracked for change. Null when it isn't in the organization.</summary>
    Task<ProjectDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The document (and its project) an earlier create with this request id made.</summary>
    Task<DocumentRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(ProjectDocument document);
}

public sealed record DocumentRequestOwner(Guid DocumentId, Guid ProjectId);

/// <summary>Where one version's file is, for a download. Read only after the caller is known to be allowed to see the document.</summary>
public sealed record DocumentFileLocation(DocumentCategory Category, DocumentVersionKind Kind, string? StoreKey, string? FileName, string? ContentType);

/// <summary>
/// Reads documents straight into DTOs. <c>includePriced</c> false leaves out Contract and Insurance documents entirely (the caller may not read
/// costs). The expiry state is judged on <c>today</c>, the company's calendar day. Every read is a fixed number of statements however many
/// documents and versions exist.
/// </summary>
public interface IDocumentReader
{
    Task<(IReadOnlyList<DocumentListItemDto> Items, int Total)> ListAsync(Guid projectId, DocumentCategory? category, bool includePriced,
        DateOnly today, PageRequest page, CancellationToken cancellationToken);

    Task<DocumentDto?> GetAsync(Guid id, bool includePriced, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Where one version's file is, or null when the document or the version isn't in the organization.</summary>
    Task<DocumentFileLocation?> FileAsync(Guid id, int versionNumber, CancellationToken cancellationToken);
}

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

/// <summary>
/// A project's documents, Active first (by category, then title), then the voided ones. <paramref name="Uploads"/> says whether files can be
/// uploaded now and why not, so the page can offer a link instead.
/// </summary>
public sealed record DocumentListDto(IReadOnlyList<DocumentListItemDto> Items, int Total, int Page, int PageSize, DocumentUploadsDto Uploads);

/// <summary>Whether a file can be uploaded now. When it can't, <paramref name="Message"/> says why in plain words; a link always works.</summary>
/// <param name="State">Available, NeedsSetup, Unavailable or Failing.</param>
/// <param name="MaximumBytes">The largest file accepted.</param>
public sealed record DocumentUploadsDto(bool Available, string State, string Message, long MaximumBytes, IReadOnlyList<string> AllowedExtensions);

/// <param name="Expiry">Valid, ExpiringSoon or Expired, for an Active document with a valid-until date; otherwise null.</param>
/// <param name="DaysLeft">Days to the valid-until date (negative once past), with <paramref name="Expiry"/>.</param>
/// <param name="Current">The newest version.</param>
public sealed record DocumentListItemDto(Guid Id, Guid ProjectId, string Title, string? Description, string Category, DateOnly? ValidUntil, string? Expiry,
    int? DaysLeft, string Status, DocumentVersionDto Current, int Version);

/// <summary>One version. A file version has a name, type, size and SHA-256; a link version has its address. Where a file is stored is never sent.</summary>
public sealed record DocumentVersionDto(int Number, string Kind, string? FileName, string? ContentType, long? SizeBytes, string? Sha256, string? Url,
    string? Note, string? AddedByName, DateTimeOffset AddedAtUtc);

/// <param name="ProjectArchived">The project is archived: the document can be read and downloaded but not changed.</param>
/// <param name="Versions">Newest first, at most the latest 100.</param>
public sealed record DocumentDto(Guid Id, Guid ProjectId, string ProjectName, bool ProjectArchived, string Title, string? Description, string Category,
    DateOnly? ValidUntil, string? Expiry, int? DaysLeft, string Status, string? VoidReason, string? VoidedByName, DateTimeOffset? VoidedAtUtc,
    string? CreatedByName, DateTimeOffset CreatedAtUtc, int CurrentVersionNumber, IReadOnlyList<DocumentVersionDto> Versions, int Version);

/// <summary>A file to send to the browser as a download.</summary>
public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <summary>A new document whose first version is a link (sent as JSON).</summary>
/// <param name="Category">Contract, Drawing, Permit, Insurance, Certificate, Photo, Report or Other.</param>
/// <param name="ValidUntil">Only for a permit, an insurance policy or a certificate.</param>
/// <param name="Url">An https:// address.</param>
public sealed record CreateDocumentLinkRequest(string? Title, string? Description, string? Category, DateOnly? ValidUntil, string? Url, string? Note,
    string? ClientRequestId);

/// <summary>The form fields that come with an uploaded file (multipart), before the file itself.</summary>
public sealed record DocumentUploadFields(string? Title, string? Description, string? Category, string? ValidUntil, string? Note, string? ClientRequestId,
    string? Version);

/// <summary>A newer version that is a link (sent as JSON).</summary>
/// <param name="ValidUntil">A renewed permit's, policy's or certificate's new valid-until date, saved with the version; null keeps the date.</param>
public sealed record AddDocumentLinkVersionRequest(string? Url, string? Note, DateOnly? ValidUntil, int Version);

public sealed record UpdateDocumentRequest(string? Title, string? Description, string? Category, DateOnly? ValidUntil, int Version);

public sealed record VoidDocumentRequest(string? Reason, int Version);
