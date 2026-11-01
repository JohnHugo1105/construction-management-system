using Cms.Application.Common;
using Cms.Application.Features.Purchasing;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

/// <summary>
/// Purchase orders. Reading needs purchasing.read; drafting and editing a draft needs purchasing.manage; approving, cancelling and closing
/// need purchasing.approve, so the person who commits the company's money can be someone other than the person who drafted the order.
/// </summary>
public sealed class PurchaseOrderEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var orders = api.MapFeature("/purchase-orders", "Purchase orders");

        orders.MapGet("", async ([FromQuery] Guid? projectId, [FromQuery] Guid? supplierId, [FromQuery] string? status, [FromQuery] string? search,
                [AsParameters] PageQuery page, PurchaseOrderService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(new PurchaseOrderListQuery(projectId, supplierId, status, search), page.ToRequest(), ct)))
            .RequirePermission(Permissions.PurchasingRead);

        orders.MapGet("/{id:guid}", async (Guid id, PurchaseOrderService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)))
            .RequirePermission(Permissions.PurchasingRead);

        orders.MapPost("", async (CreatePurchaseOrderRequest request, PurchaseOrderService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/purchase-orders/{created.Id}", created);
        }).RequirePermission(Permissions.PurchasingManage);

        orders.MapPut("/{id:guid}", async (Guid id, UpdatePurchaseOrderRequest request, PurchaseOrderService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct))).RequirePermission(Permissions.PurchasingManage);

        orders.MapPost("/{id:guid}/approve", async (Guid id, VersionRequest request, PurchaseOrderService service, CancellationToken ct) =>
            Results.Ok(await service.ApproveAsync(id, request, ct))).RequirePermission(Permissions.PurchasingApprove);

        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelPurchaseOrderRequest request, PurchaseOrderService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(id, request, ct))).RequirePermission(Permissions.PurchasingApprove);

        orders.MapPost("/{id:guid}/close", async (Guid id, ClosePurchaseOrderRequest request, PurchaseOrderService service, CancellationToken ct) =>
            Results.Ok(await service.CloseAsync(id, request, ct))).RequirePermission(Permissions.PurchasingApprove);

        // The lines of a project's approved orders, for the "against a purchase order" choice when recording a supplier invoice.
        api.MapFeature("/projects/{projectId:guid}/purchase-order-lines", "Purchase orders")
            .MapGet("", async (Guid projectId, PurchaseOrderService service, CancellationToken ct) => Results.Ok(await service.ListOpenLinesAsync(projectId, ct)))
            .RequirePermission(Permissions.PurchasingRead);
    }
}
