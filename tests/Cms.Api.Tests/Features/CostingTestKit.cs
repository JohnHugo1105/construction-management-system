using System.Net;
using System.Text;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Api.Tests.Features;

/// <summary>Request builders and helpers shared by the Costing API tests.</summary>
internal static class CostingTestKit
{
    /// <summary>A date that is in the past in every time zone, so a cost entered with it is never "in the future".</summary>
    public const string PastDate = "2025-12-01";

    public static async Task<(ApiClient Client, SeededUser User)> SignedInAsync(CmsApiFactory factory, AccessRole roles = AccessRole.Administrator,
        Guid? tenantId = null)
    {
        var user = tenantId is null
            ? await TenantSeeder.CreateTenantAsync(factory, roles)
            : await TenantSeeder.AddUserAsync(factory, tenantId.Value, roles);
        var client = new ApiClient(factory);
        await client.SignInAsync(user.Username, user.Password);
        return (client, user);
    }

    /// <summary>Another browser session of an already seeded person (for requests that must run at the same moment).</summary>
    public static async Task<ApiClient> AnotherSessionAsync(CmsApiFactory factory, SeededUser user)
    {
        var client = new ApiClient(factory);
        await client.SignInAsync(user.Username, user.Password);
        return client;
    }

    public static async Task<ProjectDto> CreateProjectAsync(ApiClient client, string? name = null, long? contractPriceMinor = 500_000_00)
    {
        var request = new
        {
            name = name ?? TenantSeeder.Unique("Project"), client = "ACME Realty", location = "Cebu City", contractPriceMinor, startDate = "2026-01-05",
            targetEndDate = (string?)null
        };
        return await client.PostAsync<ProjectDto>("/api/projects", request);
    }

    public static async Task<ProjectDto> AddSubProjectAsync(ApiClient client, ProjectDto project, string name) =>
        await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/sub-projects", new { name, startDate = "2026-02-01", version = project.Version });

    public static async Task<ProjectDto> ArchiveAsync(ApiClient client, ProjectDto project) =>
        await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/archive", new { reason = "Finished", note = (string?)null, version = project.Version });

    /// <summary>A budget line request. Defaults: one bag of cement at 100.00, no VAT.</summary>
    public static Dictionary<string, object?> Line(string? description = "Cement", string? itemType = "Material", decimal quantity = 1m, string? unit = "bag",
        long unitCostMinor = 10_000, string? vatTreatment = "Exempt", string? wbsCode = null, int? division = null, string? entryDate = "2026-02-01",
        Guid? subProjectId = null, string? clientRequestId = null, string? remarks = null, string? mainScope = null, string? subScope = null) => new()
    {
        ["subProjectId"] = subProjectId, ["division"] = division, ["mainScope"] = mainScope, ["subScope"] = subScope, ["wbsCode"] = wbsCode,
        ["description"] = description, ["itemType"] = itemType, ["quantity"] = quantity, ["unit"] = unit, ["unitCostMinor"] = unitCostMinor,
        ["vatTreatment"] = vatTreatment, ["entryDate"] = entryDate, ["remarks"] = remarks, ["clientRequestId"] = clientRequestId
    };

    /// <summary>A cost entry request. Defaults: one bag of cement at 100.00, no VAT, from a depot on a past date.</summary>
    public static Dictionary<string, object?> Cost(string? description = "Cement", string? itemType = "Material", decimal quantity = 1m, string? unit = "bag",
        long unitCostMinor = 10_000, string? vatTreatment = "Exempt", string? wbsCode = null, int? division = null, string? entryDate = PastDate,
        Guid? subProjectId = null, string? clientRequestId = null, string? remarks = null, string? sourceDealer = "Cebu Builders Depot",
        string? invoiceNumber = null, string? mainScope = null, string? subScope = null)
    {
        var body = Line(description, itemType, quantity, unit, unitCostMinor, vatTreatment, wbsCode, division, entryDate, subProjectId, clientRequestId,
            remarks, mainScope, subScope);
        body["sourceDealer"] = sourceDealer;
        body["invoiceNumber"] = invoiceNumber ?? $"INV-{Guid.NewGuid():N}"[..14];
        return body;
    }

    public static Dictionary<string, object?> With(this Dictionary<string, object?> body, string key, object? value) =>
        new(body) { [key] = value };

    public static async Task<OrganizationDto> ChangeOrganizationAsync(ApiClient admin, int? vatRateBps = null, string? varianceBasis = null,
        string? timeZoneId = null)
    {
        var organization = await admin.GetAsync<OrganizationDto>("/api/organization");
        return await admin.PutAsync<OrganizationDto>("/api/organization", new
        {
            name = organization.Name, currencyCode = organization.CurrencyCode, vatRateBps = vatRateBps ?? organization.VatRateBps,
            timeZoneId = timeZoneId ?? organization.TimeZoneId, hoursPerDay = organization.HoursPerDay,
            overtimeMultiplierBps = organization.OvertimeMultiplierBps, varianceBasis = varianceBasis ?? organization.VarianceBasis,
            version = organization.Version
        });
    }

    /// <summary>The field names a 422 response complains about.</summary>
    public static async Task<HashSet<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ApiClient.ProblemAsync(response);
        return problem.GetProperty("errors").EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
    }

    public static async Task<string> ErrorMessageAsync(HttpResponseMessage response, string field)
    {
        var problem = await ApiClient.ProblemAsync(response);
        return problem.GetProperty("errors").GetProperty(field)[0].GetString()!;
    }

    public static async Task<string> RuleCodeAsync(HttpResponseMessage response) =>
        (await ApiClient.ProblemAsync(response)).GetProperty("code").GetString()!;

    /// <summary>Saves a cost made by the system (payroll or a stock transfer) straight in the database, the way those features post them.</summary>
    public static async Task<Guid> SeedSystemCostAsync(CmsApiFactory factory, SeededUser user, Guid projectId, CostOrigin origin, long grossMinor,
        string description = "Payroll 1-15 Nov")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var project = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == projectId);
        var entry = CostEntry.RecordSystem(Guid.NewGuid(), user.TenantId, project, null, origin, Guid.NewGuid(), description, ItemType.Labor, 1m, "l.s",
            grossMinor, VatTreatment.Exempt, 0, new DateOnly(2025, 12, 2), user.UserId, DateTimeOffset.UtcNow);
        db.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    /// <summary>
    /// How many SQL statements one run of the action costs. It runs three times and the smallest count wins, so a sign-in check that
    /// happens to miss its 15-second cache in one run does not make the number look bigger than the read path itself.
    /// </summary>
    public static async Task<int> StatementsAsync(CmsApiFactory factory, Func<Task> action)
    {
        var counts = new List<int>();
        for (var run = 0; run < 3; run++) counts.Add(await factory.Sql.MeasureAsync(action));
        return counts.Min();
    }

    // ---- CSV ----------------------------------------------------------------------------------------------------------------

    public sealed record Download(HttpResponseMessage Response, byte[] Bytes, List<string[]> Rows)
    {
        public string[] Header => Rows[0];

        public string Cell(int row, string column) => Rows[row][Array.IndexOf(Header, column)];
    }

    public static async Task<Download> DownloadCsvAsync(ApiClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "The file should start with a UTF-8 byte-order mark.");
        return new Download(response, bytes, ParseCsv(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)));
    }

    public static string FileNameOf(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "";

    /// <summary>A small RFC 4180 reader: quoted cells, doubled quotes and line breaks inside quotes.</summary>
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character != '"') cell.Append(character);
                else if (index + 1 < text.Length && text[index + 1] == '"') { cell.Append('"'); index++; }
                else quoted = false;
            }
            else if (character == '"') quoted = true;
            else if (character == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (character == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else if (character != '\r') cell.Append(character);
        }

        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
