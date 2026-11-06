using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// Shared set-up for the time clock API tests. Scans must be within the last 7 days of the server's clock, so they are placed on recent days in the
/// company's time zone (Manila, UTC+8 all year) rather than on fixed dates.
/// </summary>
internal static class TimeClockTestKit
{
    public static readonly TimeSpan Manila = TimeSpan.FromHours(8);

    /// <summary>Today's date in Manila.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Manila).DateTime);

    /// <summary>A Manila wall-clock time on the day <paramref name="daysAgo"/> before today.</summary>
    public static DateTimeOffset At(int daysAgo, int hour, int minute, int second = 0) =>
        new(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, minute, second)), Manila);

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>A project, and workers hired and posted to it, each with a badge code.</summary>
    public static async Task<(ProjectDto Project, IReadOnlyList<(WorkerDetailDto Worker, string Code)> Crew)> CrewAsync(ApiClient admin, int size,
        string projectName = "Tower A", bool post = true, string numberPrefix = "W")
    {
        var project = await WorkforceTestKit.NewProjectAsync(admin, projectName);
        var crew = new List<(WorkerDetailDto, string)>(size);
        for (var index = 0; index < size; index++)
        {
            var worker = await WorkforceTestKit.HireAsync(admin, $"{numberPrefix}-{index + 1:000}", $"Worker{index + 1:00}", $"Crew{index + 1:00}");
            if (post) worker = await WorkforceTestKit.AssignAsync(admin, worker, project);
            var issued = await admin.PostAsync<PunchCodeIssuedDto>($"/api/workers/{worker.Id}/punch-code", new { version = worker.Version });
            crew.Add((worker with { Version = issued.Version }, issued.Code));
        }
        return (project, crew);
    }

    public static Task<DeviceKeyDto> RegisterAsync(ApiClient admin, Guid projectId, string name = "Main gate", string kind = "Kiosk") =>
        admin.PostAsync<DeviceKeyDto>("/api/timeclock/devices", new { name, projectId, kind });

    public static object Scan(string id, string code, DateTimeOffset at, string? direction = null) =>
        new { clientPunchId = id, workerCode = code, punchedAt = at, direction };

    /// <summary>Sends scans the way a machine does: the device key in its header, no cookie, no anti-forgery token.</summary>
    public static async Task<HttpResponseMessage> PushRawAsync(ConstructOpxApiFactory factory, string? key, object body, string? cookie = null)
    {
        using var http = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/timeclock/punches") { Content = JsonContent.Create(body, options: ApiClient.Json) };
        if (key is not null) request.Headers.Add("X-Device-Key", key);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return await http.SendAsync(request);
    }

    public static async Task<PushPunchesResultDto> PushAsync(ConstructOpxApiFactory factory, string key, params object[] scans)
    {
        var response = await PushRawAsync(factory, key, new { punches = scans });
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Push failed with {(int)response.StatusCode}: {text}");
        return JsonSerializer.Deserialize<PushPunchesResultDto>(text, ApiClient.Json)!;
    }

    /// <summary>Sends a request to any route carrying only a device key: what a stolen kiosk key could try.</summary>
    public static async Task<HttpStatusCode> WithDeviceKeyOnlyAsync(ConstructOpxApiFactory factory, string key, HttpMethod method, string url)
    {
        using var http = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var request = new HttpRequestMessage(method, url);
        if (method != HttpMethod.Get) request.Content = JsonContent.Create(new { }, options: ApiClient.Json);
        request.Headers.Add("X-Device-Key", key);
        return (await http.SendAsync(request)).StatusCode;
    }

    public static Task<TimeClockDayListDto> DayAsync(ApiClient client, Guid projectId, DateOnly date) =>
        client.GetAsync<TimeClockDayListDto>($"/api/timeclock/days?projectId={projectId}&date={Iso(date)}");

    public static Task<ConfirmTimeClockResultDto> ConfirmAsync(ApiClient client, Guid projectId, DateOnly date, bool allClean,
        params (Guid WorkerId, bool ApproveOvertime)[] rows) =>
        client.PostAsync<ConfirmTimeClockResultDto>("/api/timeclock/days/confirm", new
        {
            projectId,
            workDate = Iso(date),
            allClean,
            rows = rows.Select(row => new { workerId = row.WorkerId, approveOvertime = row.ApproveOvertime }).ToArray()
        });

    /// <summary>Confirms rows the way the review page does: each row carries the stamp of the scans the person read.</summary>
    public static Task<ConfirmTimeClockResultDto> ConfirmSeenAsync(ApiClient client, Guid projectId, DateOnly date,
        params (Guid WorkerId, bool ApproveOvertime, string ScanStamp)[] rows) =>
        client.PostAsync<ConfirmTimeClockResultDto>("/api/timeclock/days/confirm", new
        {
            projectId,
            workDate = Iso(date),
            allClean = false,
            rows = rows.Select(row => new { workerId = row.WorkerId, approveOvertime = row.ApproveOvertime, scanStamp = row.ScanStamp }).ToArray()
        });

    /// <summary>How many scans a tenant has stored, read straight from the database.</summary>
    public static int StoredPunches(ConstructOpxApiFactory factory, Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.Set<Domain.Features.TimeClock.TimePunch>())
            .Count(punch => punch.TenantId == tenantId);
    }

    private static readonly string[] ForbiddenNameParts =
        ["minor", "rate", "pay", "salary", "wage", "amount", "money", "employeenumber", "photo", "image", "biometric", "finger", "face", "latitude",
         "longitude", "location", "gps", "code", "key", "hash"];

    /// <summary>
    /// Walks a response and fails if any property is named for pay, money, the employee number, a photo, a biometric, a place or a code or key. A
    /// rejected scan's <c>reasonCode</c> is a fixed word, not a badge code, and is allowed by name.
    /// </summary>
    public static void AssertNothingPrivate(JsonElement element, params string[] allowed)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name.ToLowerInvariant();
                    if (!allowed.Contains(property.Name))
                        Assert.False(ForbiddenNameParts.Any(name.Contains), $"The response carries a property it must not: {property.Name}");
                    AssertNothingPrivate(property.Value, allowed);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) AssertNothingPrivate(item, allowed);
                break;
        }
    }
}
