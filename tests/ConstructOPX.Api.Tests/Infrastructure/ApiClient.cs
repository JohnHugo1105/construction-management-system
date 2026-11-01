using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ConstructOPX.Api.Tests.Infrastructure;

/// <summary>
/// A browser stand-in: it keeps the session cookies and echoes the anti-forgery token the API hands out, exactly as the Angular
/// client does, so tests exercise the same security path a real person would.
/// </summary>
public sealed class ApiClient : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _http;
    private readonly WebApplicationFactory<Program> _factory;
    private string? _csrf;

    public ApiClient(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
    }

    /// <summary>The session cookie as last issued (<c>constructopx.session=...</c>): what someone who copied it would hold.</summary>
    public string? SessionCookie { get; private set; }

    /// <summary>Sends a GET carrying only the given cookie, the way a replayed, copied session cookie would arrive.</summary>
    public async Task<HttpStatusCode> ReplayAsync(string url, string cookie)
    {
        using var replayer = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", cookie);
        return (await replayer.SendAsync(request)).StatusCode;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool withCsrf = true)
    {
        if (withCsrf && _csrf is null && !HttpMethods.IsGet(method.Method)) await GetAsync("/api/identity/csrf");
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (withCsrf && _csrf is not null && !HttpMethods.IsGet(method.Method)) request.Headers.Add("X-XSRF-TOKEN", _csrf);
        var response = await _http.SendAsync(request);
        CaptureCsrf(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);
    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body ?? new { });
    public Task<HttpResponseMessage> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);

    public async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    public async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await GetAsync(url));
    public async Task<T> PostAsync<T>(string url, object? body = null) => await ReadAsync<T>(await PostAsync(url, body));
    public async Task<T> PutAsync<T>(string url, object body) => await ReadAsync<T>(await PutAsync(url, body));

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    public async Task SignInAsync(string username, string password)
    {
        var response = await PostAsync("/api/identity/sign-in", new { username, password });
        Assert.True(response.IsSuccessStatusCode, $"Sign-in failed: {await response.Content.ReadAsStringAsync()}");
    }

    private void CaptureCsrf(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return;
        foreach (var cookie in cookies)
        {
            const string sessionPrefix = "constructopx.session=";
            if (cookie.StartsWith(sessionPrefix, StringComparison.Ordinal))
            {
                var value = cookie.Split(';')[0];
                if (value.Length > sessionPrefix.Length) SessionCookie = value;
            }
            const string prefix = "XSRF-TOKEN=";
            if (!cookie.StartsWith(prefix, StringComparison.Ordinal)) continue;
            _csrf = Uri.UnescapeDataString(cookie[prefix.Length..].Split(';')[0]);
        }
    }

    public void Dispose() => _http.Dispose();

    private static class HttpMethods
    {
        public static bool IsGet(string method) => method.Equals("GET", StringComparison.OrdinalIgnoreCase);
    }
}
