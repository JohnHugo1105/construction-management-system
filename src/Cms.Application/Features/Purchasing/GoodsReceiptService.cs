using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;

namespace Cms.Application.Features.Purchasing;

/// <summary>
/// Delivery use cases: a person on site records what a supplier actually brought against an approved purchase order, and voids a delivery
/// that was entered by mistake. A delivery changes no money (committed cost still falls only when an invoice is matched), so it needs no
/// approval; it is audited like the other purchasing steps, without any typed reason in the summary. Each step is one save.
/// </summary>
public sealed class GoodsReceiptService(IGoodsReceiptRepository receipts, IPurchaseOrderRepository orders, IPurchaseOrderReader reader,
    IProjectRepository projects, IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<PurchaseOrderDto> RecordAsync(Guid orderId, RecordDeliveryRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await receipts.FindByClientRequestIdAsync(requestId, cancellationToken) is { } already)
            return await EnsureSameRequest(already, orderId, cancellationToken);

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
                return await EnsureSameRequest(first, orderId, cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseOrderDto> VoidAsync(Guid orderId, Guid receiptId, VoidDeliveryRequest request, CancellationToken cancellationToken)
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
        return await ReadAsync(order.Id, cancellationToken);
    }

    private async Task<PurchaseOrderDto> RecordCoreAsync(Guid orderId, RecordDeliveryRequest request, string? requestId, CancellationToken cancellationToken)
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
        return await ReadAsync(order.Id, cancellationToken);
    }

    /// <summary>A repeated request id answers with the original delivery only when it was for the same order.</summary>
    private async Task<PurchaseOrderDto> EnsureSameRequest(GoodsReceipt original, Guid orderId, CancellationToken cancellationToken) =>
        original.PurchaseOrderId == orderId
            ? await ReadAsync(orderId, cancellationToken)
            : throw new DomainRuleException("That request was already used for another delivery. Reload the page and try again.");

    private async Task<PurchaseOrderDto> ReadAsync(Guid orderId, CancellationToken cancellationToken) =>
        await reader.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("that purchase order");

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
