using System.Text;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Infrastructure.Features.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// Project documents: drawings, permits, contracts, certificates, photos and reports, each with versions that are uploaded files or https
/// links. Reading and downloading need documents.read (and costs.read for Contract and Insurance documents, which the service enforces);
/// adding, replacing, changing and voiding need documents.manage. A new document or version is sent as JSON when it is a link, or as
/// multipart form data (the fields first, the file last) when it is a file; the upload routes have their own request-size limit. Files are
/// only ever sent back as downloads through the authenticated file route.
/// </summary>
public sealed class DocumentEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var options = FileStorageOptions.From(((IEndpointRouteBuilder)api).ServiceProvider.GetRequiredService<IConfiguration>());
        var uploadLimit = new RequestSizeLimitAttribute(options.RequestLimitBytes);

        var project = api.MapFeature("/projects/{projectId:guid}/documents", "Documents");
        project.MapGet("", async (Guid projectId, [FromQuery] string? category, [AsParameters] PageQuery page, DocumentService service,
                CancellationToken ct) => Results.Ok(await service.ListAsync(projectId, category, page.ToRequest(), ct)))
            .RequirePermission(Permissions.DocumentsRead);

        project.MapPost("", async (Guid projectId, HttpRequest request, DocumentService service, CancellationToken ct) =>
        {
            var created = UploadRequests.IsJson(request)
                ? await service.CreateLinkAsync(projectId, await UploadRequests.JsonAsync<CreateDocumentLinkRequest>(request, ct), ct)
                : await UploadRequests.MultipartAsync(request, (fields, fileName, content) =>
                    service.CreateFileAsync(projectId, fields, fileName, content, ct), ct);
            return Results.Created($"/api/documents/{created.Id}", created);
        }).RequirePermission(Permissions.DocumentsManage).WithMetadata(uploadLimit);

        var documents = api.MapFeature("/documents", "Documents");
        documents.MapGet("/{id:guid}", async (Guid id, DocumentService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)))
            .RequirePermission(Permissions.DocumentsRead);

        // The only way a stored file leaves the server: as an attachment, never shown inline, never cached.
        documents.MapGet("/{id:guid}/versions/{number:int}/file", async (Guid id, int number, DocumentService service, HttpContext context,
            CancellationToken ct) =>
        {
            var download = await service.DownloadAsync(id, number, ct);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(download.Content, download.ContentType, download.FileName);
        }).RequirePermission(Permissions.DocumentsRead);

        documents.MapPost("/{id:guid}/versions", async (Guid id, HttpRequest request, DocumentService service, CancellationToken ct) =>
            Results.Ok(UploadRequests.IsJson(request)
                ? await service.AddLinkVersionAsync(id, await UploadRequests.JsonAsync<AddDocumentLinkVersionRequest>(request, ct), ct)
                : await UploadRequests.MultipartAsync(request, (fields, fileName, content) =>
                    service.AddFileVersionAsync(id, fields, fileName, content, ct), ct)))
            .RequirePermission(Permissions.DocumentsManage).WithMetadata(uploadLimit);

        documents.MapPut("/{id:guid}", async (Guid id, UpdateDocumentRequest request, DocumentService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct))).RequirePermission(Permissions.DocumentsManage);

        documents.MapPost("/{id:guid}/void", async (Guid id, VoidDocumentRequest request, DocumentService service, CancellationToken ct) =>
            Results.Ok(await service.VoidAsync(id, request, ct))).RequirePermission(Permissions.DocumentsManage);
    }
}

/// <summary>
/// Reads an upload request without buffering it: JSON for a link, or multipart form data whose text fields come first and whose file part
/// comes last. The file part is handed over as a stream, so the file store can enforce the size limit as the bytes arrive. Text fields are
/// limited in number and length; a field sent after the file is ignored.
/// </summary>
internal static class UploadRequests
{
    private const int MaximumFields = 16;
    private const int MaximumFieldLength = 2_000;
    private const int MaximumBoundaryLength = 70;

    public static bool IsJson(HttpRequest request) => request.HasJsonContentType();

    public static async Task<T> JsonAsync<T>(HttpRequest request, CancellationToken cancellationToken) where T : class =>
        await request.ReadFromJsonAsync<T>(cancellationToken) ?? throw new BadHttpRequestException("The request had no body.");

    public static async Task<T> MultipartAsync<T>(HttpRequest request, Func<DocumentUploadFields, string?, Stream, Task<T>> onFile,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadMultipartAsync(request, onFile, cancellationToken);
        }
        catch (InvalidDataException)
        {
            // A malformed body (bad framing, headers over the limits). Whatever was stored has already been removed by the file store.
            throw new BadHttpRequestException("The upload couldn't be read.");
        }
    }

    private static async Task<T> ReadMultipartAsync<T>(HttpRequest request, Func<DocumentUploadFields, string?, Stream, Task<T>> onFile,
        CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new BadHttpRequestException("Send a link as JSON or a file as multipart form data.");
        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaximumBoundaryLength)
            throw new BadHttpRequestException("The upload has no valid boundary.");

        var reader = new MultipartReader(boundary, request.Body) { HeadersCountLimit = 8, HeadersLengthLimit = 8 * 1024 };
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            // A text field is "form-data" without a file name; a file part is "form-data" with one. Anything else is skipped.
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ||
                !(disposition.IsFormDisposition() || disposition.IsFileDisposition())) continue;
            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? "";
            if (disposition.IsFileDisposition())
            {
                if (!name.Equals("file", StringComparison.OrdinalIgnoreCase)) continue;
                // filename* (RFC 5987, for names outside ASCII) wins over filename when both are sent.
                var fileName = !StringSegment.IsNullOrEmpty(disposition.FileNameStar)
                    ? disposition.FileNameStar.Value
                    : HeaderUtilities.UnescapeAsQuotedString(disposition.FileName).Value;
                return await onFile(Fields(fields), fileName, section.Body);
            }

            if (fields.Count >= MaximumFields) throw new BadHttpRequestException("The upload has too many fields.");
            fields[name] = await ReadFieldAsync(section.Body, cancellationToken);
        }

        // No file part: the service answers "Choose a file to upload" with the other problems.
        return await onFile(Fields(fields), null, Stream.Null);
    }

    private static async Task<string> ReadFieldAsync(Stream body, CancellationToken cancellationToken)
    {
        using var text = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var buffer = new char[MaximumFieldLength + 1];
        var read = await text.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        if (read > MaximumFieldLength) throw new BadHttpRequestException("A field in the upload is too long.");
        return new string(buffer, 0, read);
    }

    private static DocumentUploadFields Fields(IReadOnlyDictionary<string, string> fields)
    {
        string? Get(string name) => fields.TryGetValue(name, out var value) ? value : null;
        return new DocumentUploadFields(Get("title"), Get("description"), Get("category"), Get("validUntil"), Get("note"), Get("clientRequestId"),
            Get("version"));
    }
}
