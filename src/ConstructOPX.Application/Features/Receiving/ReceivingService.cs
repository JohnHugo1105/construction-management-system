using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Domain.Common;

namespace ConstructOPX.Application.Features.Receiving;

/// <summary>
/// The receiving screen's use cases: list the orders awaiting delivery, open one, record a delivery and void one. Recording and voiding are
/// the purchasing ones (<see cref="DeliveryRecordingService"/>: the same rules, refusals, idempotency and audit lines); this class only
/// reads the order back as quantities, so a person who may receive goods never receives a price.
/// </summary>
public sealed class ReceivingService(DeliveryRecordingService recording, IReceivingReader reader)
{
    public Task<ReceivingOrderPageDto> ListAsync(Guid? projectId, string? search, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(new ReceivingOrderFilter(projectId, string.IsNullOrWhiteSpace(search) ? null : search.Trim()), page, cancellationToken);

    public Task<ReceivingOrderDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    public async Task<ReceivingOrderDto> RecordAsync(Guid orderId, RecordDeliveryRequest request, CancellationToken cancellationToken) =>
        await ReadAsync(await recording.RecordAsync(orderId, request, cancellationToken), cancellationToken);

    public async Task<ReceivingOrderDto> VoidAsync(Guid orderId, Guid receiptId, VoidDeliveryRequest request, CancellationToken cancellationToken) =>
        await ReadAsync(await recording.VoidAsync(orderId, receiptId, request, cancellationToken), cancellationToken);

    private async Task<ReceivingOrderDto> ReadAsync(Guid orderId, CancellationToken cancellationToken) =>
        await reader.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("that purchase order");
}
