using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Receiving;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The quantity-only receiving screen: orders awaiting delivery, an order with its lines and deliveries, recording a delivery and voiding one.
/// Every route needs receiving.record, which the warehouse keeper holds without any purchasing permission, because nothing here carries a
/// price. The rules are the purchasing ones (the same request bodies, refusals and idempotency); only the answer is quantities alone.
/// </summary>
public sealed class ReceivingEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var orders = api.MapFeature("/receiving/orders", "Receiving");

        orders.MapGet("", async ([FromQuery] Guid? projectId, [FromQuery] string? search, [AsParameters] PageQuery page, ReceivingService service,
                CancellationToken ct) => Results.Ok(await service.ListAsync(projectId, search, page.ToRequest(), ct)))
            .RequirePermission(Permissions.ReceivingRecord);

        orders.MapGet("/{id:guid}", async (Guid id, ReceivingService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)))
            .RequirePermission(Permissions.ReceivingRecord);

        orders.MapPost("/{id:guid}/deliveries", async (Guid id, RecordDeliveryRequest request, ReceivingService service, CancellationToken ct) =>
        {
            var order = await service.RecordAsync(id, request, ct);
            return Results.Created($"/api/receiving/orders/{id}", order);
        }).RequirePermission(Permissions.ReceivingRecord);

        orders.MapPost("/{id:guid}/deliveries/{receiptId:guid}/void", async (Guid id, Guid receiptId, VoidDeliveryRequest request, ReceivingService service,
                CancellationToken ct) => Results.Ok(await service.VoidAsync(id, receiptId, request, ct))).RequirePermission(Permissions.ReceivingRecord);
    }
}
