using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;

namespace ConstructOPX.Application.Features.Purchasing;

/// <summary>
/// Delivery use cases for people who may read purchase orders: record a delivery against an approved order, or void one entered by mistake,
/// and answer with the order as the purchasing screens show it. The domain work is shared with the quantity-only receiving screen
/// (<see cref="DeliveryRecordingService"/>); only the shape of the answer differs.
/// </summary>
public sealed class GoodsReceiptService(DeliveryRecordingService recording, IPurchaseOrderReader reader)
{
    public async Task<PurchaseOrderDto> RecordAsync(Guid orderId, RecordDeliveryRequest request, CancellationToken cancellationToken) =>
        await ReadAsync(await recording.RecordAsync(orderId, request, cancellationToken), cancellationToken);

    public async Task<PurchaseOrderDto> VoidAsync(Guid orderId, Guid receiptId, VoidDeliveryRequest request, CancellationToken cancellationToken) =>
        await ReadAsync(await recording.VoidAsync(orderId, receiptId, request, cancellationToken), cancellationToken);

    private async Task<PurchaseOrderDto> ReadAsync(Guid orderId, CancellationToken cancellationToken) =>
        await reader.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("that purchase order");
}

/// <summary>
/// The domain work of a delivery, shared by every screen that records one: a person on site records what a supplier actually brought against
/// an approved purchase order, and voids a delivery that was entered by mistake. A delivery changes no money (committed cost still falls only
/// when an invoice is matched), so it needs no approval; it is audited like the other purchasing steps, without any typed reason in the
/// summary. Each step is one save. Each answers with the id of the order it changed, so the caller reads it back in its own shape.
/// </summary>
public sealed class DeliveryRecordingService(IGoodsReceiptRepository receipts, IPurchaseOrderRepository orders, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<Guid> RecordAsync(Guid orderId, RecordDeliveryRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await receipts.FindByClientRequestIdAsync(requestId, cancellationToken) is { } already)
            return EnsureSameRequest(already, orderId);

        try
        {
            return await RecordCoreAsync(orderId, request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException or ValidationFailedException)
        {
            // The same form submitted twice at once: the first was saved between this one's look-up and its save, so the second collided with it
            // (on the order's version or the request id's index) or found the quantity already received. Answer it with the delivery the first
            // made; without one, the refusal is real.
            if (await receipts.FindByClientRequestIdAsync(requestId, cancellationToken) is { } first)
                return EnsureSameRequest(first, orderId);
            throw;
        }
    }

    public async Task<Guid> VoidAsync(Guid orderId, Guid receiptId, VoidDeliveryRequest request, CancellationToken cancellationToken)
    {
        // The order is looked up first and the delivery must belong to it, so another company's id, or a delivery of another order, answers 404.
        var order = await orders.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("that purchase order");
        var receipt = await receipts.GetAsync(receiptId, cancellationToken);
        if (receipt is null || receipt.PurchaseOrderId != order.Id) throw new NotFoundException("that delivery");
        ConcurrencyGuard.Ensure(receipt.Version, request.Version);
        var project = await LoadProjectAsync(order.ProjectId, cancellationToken);

        receipt.Void(project, request.Reason, currentUser.UserId, clock.UtcNow);

        // The reason stays on the delivery: a typed reason may mention people or amounts and the activity log is read by more people than the order.
        audit.Record("GoodsReceipt.Voided", nameof(GoodsReceipt), receipt.Id, $"Voided a delivery against purchase order {order.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id;
    }

    private async Task<Guid> RecordCoreAsync(Guid orderId, RecordDeliveryRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("that purchase order");
        var project = await LoadProjectAsync(order.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;

        var alreadyReceived = await receipts.ReceivedQuantitiesAsync(order.Id, cancellationToken);
        var normalized = string.IsNullOrWhiteSpace(request.DeliveryReceiptNumber) ? null : GoodsReceipt.NormalizeNumber(request.DeliveryReceiptNumber.Trim());
        var numberRecorded = !string.IsNullOrEmpty(normalized) && await receipts.NumberIsRecordedAsync(order.SupplierId, normalized, cancellationToken);

        var receipt = GoodsReceipt.Record(Guid.NewGuid(), currentUser.TenantId, order, project, request.ReceivedDate, request.DeliveryReceiptNumber, request.Note,
            (request.Lines ?? []).Select(line => new GoodsReceiptLineInput(line.PurchaseOrderLineId, line.Quantity)).ToArray(), alreadyReceived, numberRecorded,
            currentUser.UserId, requestId, organization.TodayLocal(now), now);

        receipts.Add(receipt);
        audit.Record("GoodsReceipt.Recorded", nameof(GoodsReceipt), receipt.Id, $"Recorded a delivery against purchase order {order.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id;
    }

    /// <summary>A repeated request id answers with the original delivery only when it was for the same order.</summary>
    private static Guid EnsureSameRequest(GoodsReceipt original, Guid orderId) =>
        original.PurchaseOrderId == orderId
            ? orderId
            : throw new DomainRuleException("That request was already used for another delivery. Reload the page and try again.");

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
