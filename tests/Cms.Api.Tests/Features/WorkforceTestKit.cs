using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Api.Tests.Features;

/// <summary>
/// Shared set-up for the workforce, attendance and payroll API tests. Dates are fixed days in October 2026 (all in the past for the
/// test host's clock) so the figures worked out in the tests never depend on when they run.
/// </summary>
internal static class WorkforceTestKit
{
    public const string HireDate = "2026-01-05";

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

    public static Task<ProjectDto> NewProjectAsync(ApiClient client, string name = "Tower A") =>
        client.PostAsync<ProjectDto>("/api/projects",
            new { name, client = "ACME Realty", location = "Cebu City", contractPriceMinor = (long?)null, startDate = "2026-01-05", targetEndDate = (string?)null });

    public static Task<WorkerDetailDto> HireAsync(ApiClient client, string number, string first = "Juan", string last = "Dela Cruz", string designation = "Mason",
        long rateMinor = 80_000, string unit = "Daily", string hired = HireDate, string? clientRequestId = null) =>
        client.PostAsync<WorkerDetailDto>("/api/workers",
            new { employeeNumber = number, firstName = first, middleName = (string?)null, lastName = last, dateHired = hired, designation, rateMinor, rateUnit = unit, clientRequestId });

    public static Task<WorkerDetailDto> AssignAsync(ApiClient client, WorkerDetailDto worker, ProjectDto project, string start = "2026-01-06") =>
        client.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/assign", new { projectId = project.Id, subProjectId = (Guid?)null, startDate = start, version = worker.Version });

    public static Task<AttendanceEntryDto> RecordAsync(ApiClient client, Guid workerId, Guid projectId, string date, decimal regular, decimal overtime = 0m,
        Guid? subProjectId = null, string? clientRequestId = null) =>
        client.PostAsync<AttendanceEntryDto>("/api/attendance",
            new { workerId, projectId, subProjectId, workDate = date, regularHours = regular, overtimeHours = overtime, remarks = (string?)null, clientRequestId });

    public static Task<PayrollRunDto> CreateRunAsync(ApiClient client, string start, string end, string? clientRequestId = null) =>
        client.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = start, periodEnd = end, clientRequestId });

    /// <summary>The cost entries a payroll run posted, read straight from the database (every tenant's rows are filtered out of a context nobody is signed in to).</summary>
    public static async Task<List<CostEntry>> PostedCostsAsync(CmsApiFactory factory, Guid tenantId, Guid runId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        return await db.Set<CostEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.Origin == CostOrigin.Payroll && entry.OriginReferenceId == runId)
            .OrderBy(entry => entry.ProjectId).ToListAsync();
    }

    /// <summary>Every audit line a tenant has, for checking that none of them carries a pay figure.</summary>
    public static async Task<List<string>> AuditSummariesAsync(CmsApiFactory factory, Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        return await db.Set<Cms.Domain.Features.Audit.AuditEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.TenantId == tenantId).Select(entry => entry.Summary).ToListAsync();
    }

    // 1,050.00 / 1050.00 / 105000 / any figure with a decimal point and two places. Counts of workers and the year are not amounts.
    private static readonly Regex MoneyLooking = new(@"\d+\.\d{2}\b|\d{1,3}(,\d{3})+|\b\d{5,}\b", RegexOptions.Compiled);

    public static void AssertNoPayFigures(IEnumerable<string> lines) =>
        Assert.All(lines, line => Assert.False(MoneyLooking.IsMatch(line), $"This line looks like it carries a pay figure: {line}"));

    /// <summary>Walks a response and fails if any money field (named ...Minor) or the rate unit holds a value.</summary>
    public static void AssertNoPayFields(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.EndsWith("Minor", StringComparison.Ordinal) || property.Name is "currentRateUnit" or "rateUnit")
                        Assert.True(property.Value.ValueKind == JsonValueKind.Null, $"{property.Name} must be null without payroll.read but was {property.Value}.");
                    AssertNoPayFields(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) AssertNoPayFields(item);
                break;
        }
    }

    public static async Task<JsonElement> RawAsync(ApiClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} returned {(int)response.StatusCode}");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
