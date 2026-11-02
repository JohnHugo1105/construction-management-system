using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Documents;

internal sealed class DocumentRepository(ConstructOpxDbContext db) : IDocumentRepository
{
    public Task<ProjectDocument?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ProjectDocument>().Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<DocumentRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<ProjectDocument>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new DocumentRequestOwner(item.Id, item.ProjectId)).SingleOrDefaultAsync(cancellationToken);

    public void Add(ProjectDocument document) => db.Set<ProjectDocument>().Add(document);
}

/// <summary>
/// Reads documents straight into DTOs. A project's list is two statements (the count and the page, each document with its newest version),
/// a document two (the document and its versions), and a file's location one, however many documents and versions exist. The store key is
/// read only for a download and never put in a DTO.
/// </summary>
internal sealed class DocumentReader(ConstructOpxDbContext db) : IDocumentReader
{
    /// <summary>The newest versions one read returns.</summary>
    private const int VersionLimit = 100;

    public async Task<(IReadOnlyList<DocumentListItemDto> Items, int Total)> ListAsync(Guid projectId, DocumentCategory? category, bool includePriced,
        DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var rows =
            from document in Visible(includePriced)
            where document.ProjectId == projectId
            join version in db.Set<DocumentVersion>().AsNoTracking()
                on new { DocumentId = document.Id, Number = document.CurrentVersionNumber } equals new { version.DocumentId, version.Number }
            select new ListRow { Document = document, Current = version };
        if (category is { } only) rows = rows.Where(row => row.Document.Category == only);

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(row => row.Document.Status == DocumentStatus.Active ? 0 : 1)
            .ThenBy(row => row.Document.Category)
            .ThenBy(row => row.Document.Title)
            .ThenBy(row => row.Document.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(row => new
            {
                row.Document.Id, row.Document.ProjectId, row.Document.Title, row.Document.Description, row.Document.Category, row.Document.ValidUntil,
                row.Document.Status, row.Document.Version,
                Current = new VersionRow(row.Current.Number, row.Current.Kind, row.Current.FileName, row.Current.ContentType, row.Current.SizeBytes,
                    row.Current.Sha256, row.Current.Url, row.Current.Note, row.Current.AddedAtUtc,
                    users.Where(user => user.Id == row.Current.AddedByUserId).Select(user => user.DisplayName).FirstOrDefault())
            })
            .ToListAsync(cancellationToken);

        return (items.Select(item =>
        {
            var expiry = ProjectDocument.ExpiryFor(item.Status, item.ValidUntil, today);
            return new DocumentListItemDto(item.Id, item.ProjectId, item.Title, item.Description, item.Category.ToString(), item.ValidUntil,
                expiry?.ToString(), expiry is null ? null : ProjectDocument.DaysLeft(item.ValidUntil!.Value, today), item.Status.ToString(),
                ToDto(item.Current), item.Version);
        }).ToArray(), total);
    }

    public async Task<DocumentDto?> GetAsync(Guid id, bool includePriced, DateOnly today, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var head = await (
            from document in Visible(includePriced)
            where document.Id == id
            join project in db.Set<Project>() on document.ProjectId equals project.Id
            select new
            {
                Document = document,
                ProjectName = project.Name,
                ProjectStatus = project.Status,
                CreatedBy = users.Where(user => user.Id == document.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                VoidedBy = users.Where(user => user.Id == document.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (head is null) return null;

        var versions = await db.Set<DocumentVersion>().AsNoTracking().Where(item => item.DocumentId == id)
            .OrderByDescending(item => item.Number).Take(VersionLimit)
            .Select(item => new VersionRow(item.Number, item.Kind, item.FileName, item.ContentType, item.SizeBytes, item.Sha256, item.Url, item.Note,
                item.AddedAtUtc, users.Where(user => user.Id == item.AddedByUserId).Select(user => user.DisplayName).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var d = head.Document;
        var expiry = d.ExpiryOn(today);
        return new DocumentDto(d.Id, d.ProjectId, head.ProjectName, head.ProjectStatus == ProjectStatus.Archived, d.Title, d.Description,
            d.Category.ToString(), d.ValidUntil, expiry?.ToString(), expiry is null ? null : ProjectDocument.DaysLeft(d.ValidUntil!.Value, today),
            d.Status.ToString(), d.VoidReason, head.VoidedBy, d.VoidedAtUtc, head.CreatedBy, d.CreatedAtUtc, d.CurrentVersionNumber,
            versions.Select(ToDto).ToArray(), d.Version);
    }

    public Task<DocumentFileLocation?> FileAsync(Guid id, int versionNumber, CancellationToken cancellationToken) =>
        (from version in db.Set<DocumentVersion>().AsNoTracking()
         where version.DocumentId == id && version.Number == versionNumber
         join document in db.Set<ProjectDocument>().AsNoTracking() on version.DocumentId equals document.Id
         select new DocumentFileLocation(document.Category, version.Kind, version.StoreKey, version.FileName, version.ContentType))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>The organization's documents, without Contract and Insurance ones for a person who may not read costs.</summary>
    private IQueryable<ProjectDocument> Visible(bool includePriced)
    {
        var documents = db.Set<ProjectDocument>().AsNoTracking();
        return includePriced
            ? documents
            : documents.Where(item => item.Category != DocumentCategory.Contract && item.Category != DocumentCategory.Insurance);
    }

    private static DocumentVersionDto ToDto(VersionRow version) =>
        new(version.Number, version.Kind.ToString(), version.FileName, version.ContentType, version.SizeBytes, version.Sha256, version.Url, version.Note,
            version.AddedBy, version.AddedAtUtc);

    /// <summary>The columns of a version a DTO shows: never its place in the file store.</summary>
    private sealed record VersionRow(int Number, DocumentVersionKind Kind, string? FileName, string? ContentType, long? SizeBytes, string? Sha256,
        string? Url, string? Note, DateTimeOffset AddedAtUtc, string? AddedBy);

    /// <summary>A document with its newest version. A class with initializers (not a record) so filters and ordering still compose in SQL.</summary>
    private sealed class ListRow
    {
        public required ProjectDocument Document { get; init; }
        public required DocumentVersion Current { get; init; }
    }
}
