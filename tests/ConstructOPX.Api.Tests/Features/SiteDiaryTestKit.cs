using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.SiteDiary;
using ConstructOPX.Application.Features.Suppliers;
using ConstructOPX.Application.Features.Workforce;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Request builders and steps shared by the site diary API tests.</summary>
internal static class SiteDiaryTestKit
{
    /// <summary>A fixed day in the past for the "filled in" tests: attendance and deliveries are recorded on it.</summary>
    public const string SiteDay = "2026-10-14";

    /// <summary>Today on the company's calendar (Asia/Manila by default): the day the server judges "not in the future" and the author window on.</summary>
    public static DateOnly Today() => BondTestKit.Today();

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static string DaysFromToday(int days) => Date(Today().AddDays(days));

    public static string Route(Guid projectId, string? date = null, string? tail = null) =>
        $"/api/projects/{projectId}/diary" + (date is null ? "" : $"/{date}") + (tail is null ? "" : $"/{tail}");

    /// <summary>Defaults: a fair day, two lines of work done, six subcontractor workers, for today.</summary>
    public static Dictionary<string, object?> NewEntry(string? date = null, string? weather = "Fair", string? weatherNote = null,
        string? workDone = "Poured the second-floor slab.\nTied rebar for the stair.", string? issues = null, string? visitors = null, string? safety = null,
        int subcontractors = 6, string? clientRequestId = null) => new()
    {
        ["reportDate"] = date ?? DaysFromToday(0), ["weather"] = weather, ["weatherNote"] = weatherNote, ["workDone"] = workDone, ["issuesAndDelays"] = issues,
        ["visitorsAndInspections"] = visitors, ["safetyNotes"] = safety, ["subcontractorWorkers"] = subcontractors, ["clientRequestId"] = clientRequestId
    };

    public static async Task<DiaryEntryDto> CreateAsync(ApiClient client, Guid projectId, string? date = null, string? workDone = null, string? weather = "Fair") =>
        await client.PostAsync<DiaryEntryDto>(Route(projectId), NewEntry(date, weather, workDone: workDone ?? "Poured the second-floor slab.\nTied rebar for the stair."));

    /// <summary>The body of a correction that keeps everything as it is, except what the caller changes.</summary>
    public static Dictionary<string, object?> Change(DiaryEntryDto entry, string? weather = null, string? weatherNote = null, string? workDone = null,
        string? issues = null, string? visitors = null, string? safety = null, int? subcontractors = null, int? version = null) => new()
    {
        ["weather"] = weather ?? entry.Weather, ["weatherNote"] = weatherNote ?? entry.WeatherNote, ["workDone"] = workDone ?? entry.WorkDone,
        ["issuesAndDelays"] = issues ?? entry.IssuesAndDelays, ["visitorsAndInspections"] = visitors ?? entry.VisitorsAndInspections,
        ["safetyNotes"] = safety ?? entry.SafetyNotes, ["subcontractorWorkers"] = subcontractors ?? entry.SubcontractorWorkers, ["version"] = version ?? entry.Version
    };

    public static Task<HttpResponseMessage> CorrectAsync(ApiClient client, DiaryEntryDto entry, Dictionary<string, object?> body) =>
        client.PutAsync(Route(entry.ProjectId, Date(entry.ReportDate)), body);

    public static Task<HttpResponseMessage> VoidAsync(ApiClient client, DiaryEntryDto entry, string? reason = "Wrote it on the wrong day", int? version = null) =>
        client.PostAsync(Route(entry.ProjectId, Date(entry.ReportDate), "void"), new { reason, version = version ?? entry.Version });

    /// <summary>A worker hired, posted to the project and recorded present on the day.</summary>
    public static async Task<WorkerDetailDto> WorkerOnSiteAsync(ApiClient client, ProjectDto project, string number, string first, string last, string day = SiteDay)
    {
        var worker = await WorkforceTestKit.AssignAsync(client, await WorkforceTestKit.HireAsync(client, number, first, last), project);
        await WorkforceTestKit.RecordAsync(client, worker.Id, project.Id, day, 8m);
        return worker;
    }

    /// <summary>An approved order with the given lines (description, unit, quantity) and the quantities received on a day, one delivery.</summary>
    public static async Task<PurchaseOrderDto> DeliveredAsync(ApiClient client, ProjectDto project, SupplierDto firm, string day,
        string number, params (string Description, string Unit, decimal Ordered, decimal Received)[] lines)
    {
        var order = await ApprovedAsync(client, project, firm, lines.Select(line => OrderLine(line.Description, line.Ordered, unit: line.Unit)).ToArray());
        var received = lines.Select((line, index) => (index + 1, line.Received)).Where(line => line.Received > 0m).ToArray();
        var body = DeliveryBody(order, received, number, day);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/purchase-orders/{order.Id}/receipts", body)).StatusCode);
        return await GetOrderAsync(client, order.Id);
    }

    /// <summary>Every property name in a JSON document, at every depth.</summary>
    public static void CollectNames(JsonElement element, ISet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    CollectNames(property.Value, names);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectNames(item, names);
                break;
        }
    }

    public static async Task<JsonElement> RawAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
