using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Purchasing;
using Cms.Application.Features.Suppliers;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.CostingTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>Request builders and helpers shared by the Purchasing API tests.</summary>
internal static class PurchasingTestKit
{
    public static async Task<SupplierDto> CreateFirmAsync(ApiClient client, string? name = null, bool supplier = true, bool subcontractor = false) =>
        await client.PostAsync<SupplierDto>("/api/suppliers", new
        {
            name = name ?? TenantSeeder.Unique("Firm"), isSupplier = supplier, isSubcontractor = subcontractor, tin = (string?)null, contactPerson = "Ana Reyes",
            phone = (string?)null, email = (string?)null, address = (string?)null, notes = (string?)null, version = 0
        });

    /// <summary>One order line. Defaults: 10 pieces of rebar at 500.00, no VAT, so net equals gross (5,000.00).</summary>
    public static Dictionary<string, object?> OrderLine(string? description = "Rebar 16 mm", decimal quantity = 10m, long unitCostMinor = 50_000,
        string? vatTreatment = "Exempt", string? itemType = "Material", string? unit = "pc", string? wbsCode = "3200", int? division = 3) => new()
    {
        ["division"] = division, ["mainScope"] = null, ["subScope"] = null, ["wbsCode"] = wbsCode, ["description"] = description, ["itemType"] = itemType,
        ["quantity"] = quantity, ["unit"] = unit, ["unitCostMinor"] = unitCostMinor, ["vatTreatment"] = vatTreatment
    };

    public static Dictionary<string, object?> OrderBody(Guid projectId, Guid supplierId, IEnumerable<Dictionary<string, object?>>? lines = null,
        string? orderDate = PastDate, string? expectedDate = null, string? supplierReference = null, string? notes = null, Guid? subProjectId = null,
        string? clientRequestId = null) => new()
    {
        ["projectId"] = projectId, ["subProjectId"] = subProjectId, ["supplierId"] = supplierId, ["orderDate"] = orderDate, ["expectedDate"] = expectedDate,
        ["supplierReference"] = supplierReference, ["notes"] = notes, ["lines"] = (lines ?? [OrderLine()]).ToArray(), ["clientRequestId"] = clientRequestId
    };

    public static Dictionary<string, object?> UpdateBody(PurchaseOrderDto order, IEnumerable<Dictionary<string, object?>>? lines = null, string? notes = null,
        Guid? supplierId = null, Guid? projectId = null, string? orderDate = PastDate) => new()
    {
        ["projectId"] = projectId ?? order.ProjectId, ["subProjectId"] = order.SubProjectId, ["supplierId"] = supplierId ?? order.SupplierId,
        ["orderDate"] = orderDate, ["expectedDate"] = null, ["supplierReference"] = order.SupplierReference, ["notes"] = notes,
        ["lines"] = (lines ?? [OrderLine()]).ToArray(), ["version"] = order.Version
    };

    public static Task<PurchaseOrderDto> DraftAsync(ApiClient client, ProjectDto project, SupplierDto firm, params Dictionary<string, object?>[] lines) =>
        client.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id, lines.Length == 0 ? null : lines));

    public static async Task<PurchaseOrderDto> ApprovedAsync(ApiClient client, ProjectDto project, SupplierDto firm, params Dictionary<string, object?>[] lines) =>
        await ApproveAsync(client, await DraftAsync(client, project, firm, lines));

    public static Task<PurchaseOrderDto> ApproveAsync(ApiClient client, PurchaseOrderDto order) =>
        client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/approve", new { version = order.Version });

    public static Task<PurchaseOrderDto> GetOrderAsync(ApiClient client, Guid id) => client.GetAsync<PurchaseOrderDto>($"/api/purchase-orders/{id}");

    /// <summary>A supplier invoice matched to an order line (one unit at the given amount, no VAT unless told).</summary>
    public static Task<CostEntryDto> InvoiceAsync(ApiClient client, Guid projectId, Guid? lineId, long amountMinor, string vat = "Exempt", string dealer = "Visayas Steel Corp.") =>
        client.PostAsync<CostEntryDto>($"/api/projects/{projectId}/cost-entries",
            Cost("Rebar delivery", "Material", unitCostMinor: amountMinor, vatTreatment: vat, sourceDealer: dealer).With("purchaseOrderLineId", lineId));

    /// <summary>How many commitment sources the host registers: each adds one grouped statement to the dashboard and the project summary.</summary>
    public static int CommitmentSourceCount(this CmsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetServices<ICommitmentSource>().Count();
    }

    public static HttpResponseMessage Ok(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {response.Content.ReadAsStringAsync().Result}");
        return response;
    }

    public static async Task<DashboardProjectRowDto> DashboardRowAsync(ApiClient client, Guid projectId) =>
        (await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Single(row => row.Id == projectId);

    public static Task<CostSummaryDto> SummaryAsync(ApiClient client, Guid projectId) => client.GetAsync<CostSummaryDto>($"/api/projects/{projectId}/cost-summary");

    /// <summary>A budget line of the given amount (no VAT), so a project has something to forecast against.</summary>
    public static Task<BudgetLineDto> BudgetAsync(ApiClient client, Guid projectId, long amountMinor) =>
        client.PostAsync<BudgetLineDto>($"/api/projects/{projectId}/budget-lines", Line("Budget", "Material", unitCostMinor: amountMinor, wbsCode: "3200"));
}
