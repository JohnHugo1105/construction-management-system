using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Capabilities;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The documents API on the real SQL Server host, three ways: <see cref="Host"/> keeps files in a temporary folder (with a 1 MB limit, so the
/// size tests stay small), <see cref="FailingHost"/> has the same folder but a file storage check that reports Failing, and
/// <see cref="Factory"/> itself has no folder configured (file storage NeedsSetup). All three share one database.
/// </summary>
public sealed class DocumentsApiFixture : IAsyncLifetime
{
    public const int LimitBytes = 1024 * 1024;

    public ConstructOpxApiFactory Factory { get; } = new();

    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"constructopx-documents-{Guid.NewGuid():N}");

    public WebApplicationFactory<Program> Host { get; private set; } = null!;

    public WebApplicationFactory<Program> FailingHost { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        Host = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FileStorage:RootPath", Root);
            builder.UseSetting("FileStorage:MaxFileMegabytes", "1");
        });
        FailingHost = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FileStorage:RootPath", Root);
            builder.ConfigureTestServices(services =>
            {
                foreach (var real in services.Where(item => item.ServiceType == typeof(ICapabilityCheck) &&
                             item.ImplementationType?.Name == "FileStorageCapabilityCheck").ToArray())
                    services.Remove(real);
                services.AddScoped<ICapabilityCheck>(_ => new FakeCapabilityCheck(CapabilityKeys.FileStorage, CapabilityState.Failing,
                    "File storage can't be written to right now, so files can't be uploaded.", "Add a link to the document instead."));
            });
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await ((IAsyncLifetime)Factory).DisposeAsync();
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    /// <summary>Every file under the storage folder for one project (nothing for a project that never stored one).</summary>
    public string[] FilesOf(Guid tenantId, Guid projectId)
    {
        var folder = Path.Combine(Root, tenantId.ToString("D"), projectId.ToString("D"));
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories) : [];
    }

    public async Task<int> DocumentRowsAsync(Guid projectId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        return await db.Set<ProjectDocument>().IgnoreQueryFilters().CountAsync(item => item.ProjectId == projectId);
    }

    public async Task<string[]> StoreKeysAsync(Guid projectId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        return await (from version in db.Set<DocumentVersion>().IgnoreQueryFilters()
                      join document in db.Set<ProjectDocument>().IgnoreQueryFilters() on version.DocumentId equals document.Id
                      where document.ProjectId == projectId && version.StoreKey != null
                      select version.StoreKey!).ToArrayAsync();
    }
}

internal sealed class FakeCapabilityCheck(string key, CapabilityState state, string message, string? fallback) : ICapabilityCheck
{
    public string Key => key;

    public Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CapabilityStatus(key, "File storage", state, message, fallback));
}

/// <summary>
/// A signed-in browser stand-in for the documents routes: the session cookie and the anti-forgery header as the Angular client sends them,
/// plus multipart uploads with the fields first and the file last. It keeps every response body it reads, so a test can walk them all.
/// </summary>
public sealed class DocumentsClient : IDisposable
{
    private readonly HttpClient _http;
    private string? _csrf;

    public DocumentsClient(WebApplicationFactory<Program> host) =>
        _http = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Every JSON body read so far.</summary>
    public List<string> Bodies { get; } = [];

    public static async Task<(DocumentsClient Client, SeededUser User)> SignInAsync(DocumentsApiFixture fixture, WebApplicationFactory<Program> host,
        AccessRole roles = AccessRole.Administrator, Guid? tenantId = null)
    {
        var user = tenantId is null
            ? await TenantSeeder.CreateTenantAsync(fixture.Factory, roles)
            : await TenantSeeder.AddUserAsync(fixture.Factory, tenantId.Value, roles);
        var client = new DocumentsClient(host);
        var response = await client.SendAsync(HttpMethod.Post, "/api/identity/sign-in", JsonContent.Create(new { username = user.Username, password = user.Password }));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (client, user);
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, null);

    public Task<HttpResponseMessage> PostAsync(string url, object body) => SendAsync(HttpMethod.Post, url, JsonContent.Create(body, options: ApiClient.Json));

    public Task<HttpResponseMessage> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, JsonContent.Create(body, options: ApiClient.Json));

    /// <summary>A multipart upload: the text fields, then the file part named "file" (left out when <paramref name="fileName"/> is null).</summary>
    public Task<HttpResponseMessage> UploadAsync(string url, IReadOnlyDictionary<string, string?> fields, string? fileName, byte[] bytes,
        bool withCsrf = true)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
            if (value is not null) form.Add(new StringContent(value, Encoding.UTF8), name);
        if (fileName is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            // Written by hand, without the client's own checks, so a test controls exactly what name arrives (folders, quotes, odd characters,
            // non-ASCII as filename* the way browsers send it).
            var disposition = $"form-data; name=\"file\"; filename={Quote(fileName)}";
            if (fileName.Any(character => character > 127)) disposition += $"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            file.Headers.TryAddWithoutValidation("Content-Disposition", disposition);
            form.Add(file);
        }
        return SendAsync(HttpMethod.Post, url, form, withCsrf);
    }

    public async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {text}");
        Bodies.Add(text);
        return JsonSerializer.Deserialize<T>(text, ApiClient.Json)!;
    }

    public async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await GetAsync(url));

    public async Task<ProjectDto> CreateProjectAsync() =>
        await ReadAsync<ProjectDto>(await PostAsync("/api/projects", new
        {
            name = TenantSeeder.Unique("Project"), client = "ACME Realty", location = "Cebu City", contractPriceMinor = 500_000_00L,
            startDate = "2026-01-05", targetEndDate = (string?)null
        }));

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, bool withCsrf = true)
    {
        var write = method != HttpMethod.Get;
        if (write && withCsrf && _csrf is null) await SendAsync(HttpMethod.Get, "/api/identity/csrf", null);
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (write && withCsrf && _csrf is not null) request.Headers.Add("X-XSRF-TOKEN", _csrf);
        var response = await _http.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            foreach (var cookie in cookies.Where(item => item.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)))
                _csrf = Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..].Split(';')[0]);
        return response;
    }

    public void Dispose() => _http.Dispose();

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

internal static class DocumentTestKit
{
    public static byte[] Pdf(int size = 4096)
    {
        var bytes = new byte[size];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        for (var index = 9; index < size; index++) bytes[index] = (byte)('a' + index % 26);
        return bytes;
    }

    public static byte[] Png(int size = 512)
    {
        var bytes = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    public static Dictionary<string, string?> Fields(string title = "Ground floor plan", string category = "Drawing", string? validUntil = null,
        string? note = null, string? clientRequestId = null, string? description = null) => new()
    {
        ["title"] = title, ["category"] = category, ["validUntil"] = validUntil, ["note"] = note, ["clientRequestId"] = clientRequestId,
        ["description"] = description
    };

    public static object Link(string title = "Site photos folder", string category = "Photo", string url = "https://files.example.com/photos",
        string? validUntil = null, string? clientRequestId = null, string? note = null) =>
        new { title, category, url, validUntil, clientRequestId, note, description = (string?)null };

    public static Task<HttpResponseMessage> UploadAsync(DocumentsClient client, Guid projectId, string fileName, byte[] bytes,
        Dictionary<string, string?>? fields = null) =>
        client.UploadAsync($"/api/projects/{projectId}/documents", fields ?? Fields(), fileName, bytes);

    public static async Task<DocumentDto> UploadedAsync(DocumentsClient client, Guid projectId, string fileName, byte[] bytes,
        Dictionary<string, string?>? fields = null)
    {
        var response = await UploadAsync(client, projectId, fileName, bytes, fields);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadAsync<DocumentDto>(response);
    }

    public static async Task<DocumentDto> LinkedAsync(DocumentsClient client, Guid projectId, string category = "Photo", string title = "Site photos folder",
        string? validUntil = null)
    {
        var response = await client.PostAsync($"/api/projects/{projectId}/documents", Link(title, category, validUntil: validUntil));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadAsync<DocumentDto>(response);
    }

    public static string FileUrl(Guid documentId, int number = 1) => $"/api/documents/{documentId}/versions/{number}/file";

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) => await ApiClient.ProblemAsync(response);

    /// <summary>Every property name in a JSON document, at any depth.</summary>
    public static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => []
    };

    /// <summary>Every string value in a JSON document, at any depth.</summary>
    public static IEnumerable<string> StringValues(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => StringValues(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(StringValues),
        JsonValueKind.String => [element.GetString()!],
        _ => []
    };
}
