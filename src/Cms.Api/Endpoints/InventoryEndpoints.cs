using Cms.Application.Common;
using Cms.Application.Features.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

public sealed class InventoryEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/inventory/items", "Inventory");

        group.MapGet("", async ([FromQuery] string? search, [FromQuery] string? category, [FromQuery] string? status, [FromQuery] Guid? projectId,
                [FromQuery] bool? inWarehouse, [AsParameters] PageQuery page, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.ListAsync(new InventoryListRequest(search, category, status, projectId, inWarehouse), page.ToRequest(), ct)))
            .RequirePermission(Permissions.InventoryRead);

        group.MapGet("/{id:guid}", async (Guid id, InventoryService inventory, CancellationToken ct) => Results.Ok(await inventory.GetAsync(id, ct)))
            .RequirePermission(Permissions.InventoryRead);

        group.MapGet("/{id:guid}/movements", async (Guid id, [AsParameters] PageQuery page, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.ListMovementsAsync(id, page.ToRequest(), ct))).RequirePermission(Permissions.InventoryRead);

        group.MapPost("", async (RegisterItemRequest request, InventoryService inventory, CancellationToken ct) =>
        {
            var created = await inventory.RegisterAsync(request, ct);
            return Results.Created($"/api/inventory/items/{created.Id}", created);
        }).RequirePermission(Permissions.InventoryManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateItemRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.UpdateAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/receive", async (Guid id, ReceiveStockRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.ReceiveAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/transfer", async (Guid id, TransferStockRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.TransferAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/adjust", async (Guid id, AdjustStockRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.AdjustAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/write-off", async (Guid id, WriteOffStockRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.WriteOffAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/movements/{movementId:guid}/reverse", async (Guid id, Guid movementId, ReverseMovementRequest request,
                InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.ReverseAsync(id, movementId, request, ct))).RequirePermission(Permissions.InventoryManage);

        group.MapPost("/{id:guid}/retire", async (Guid id, RetireItemRequest request, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.RetireAsync(id, request, ct))).RequirePermission(Permissions.InventoryManage);

        // What is on one project now, for the project's own page.
        var onProject = api.MapFeature("/projects/{projectId:guid}/inventory", "Inventory");
        onProject.MapGet("", async (Guid projectId, [AsParameters] PageQuery page, InventoryService inventory, CancellationToken ct) =>
            Results.Ok(await inventory.ListForProjectAsync(projectId, page.ToRequest(), ct))).RequirePermission(Permissions.InventoryRead);
    }
}
