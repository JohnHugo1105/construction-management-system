using ConstructOPX.Application.Common;

namespace ConstructOPX.Application.Features.Receiving;

// The quantity-only view of deliveries, for the people who receive goods on site (the warehouse keeper). Nothing here carries an amount of
// money, a rate or a total, so these types are the whole of what that screen can ever show: see docs/business-rules.md, "Receiving". A test
// walks the property names of every response to keep it that way.

// ---- Ports ------------------------------------------------------------------------------------------------------------------

/// <summary>Reads orders awaiting delivery straight into quantity-only DTOs in SQL. Never selects a price, a total or an invoice.</summary>
public interface IReceivingReader
{
    Task<ReceivingOrderPageDto> ListAsync(ReceivingOrderFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>How many orders await delivery (the list's <c>OrderCount</c>) in one statement, without reading a row.</summary>
    Task<int> CountAsync(ReceivingOrderFilter filter, CancellationToken cancellationToken);

    /// <summary>One order with its lines and deliveries, quantities only. Null when it isn't in the organization.</summary>
    Task<ReceivingOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}

// ---- Queries ----------------------------------------------------------------------------------------------------------------

public sealed record ReceivingOrderFilter(Guid? ProjectId, string? Search);

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

/// <param name="DeliveryState">NotReceived or PartlyReceived: an order with nothing left to receive is not listed.</param>
public sealed record ReceivingOrderListItemDto(Guid Id, string Number, string SupplierName, string ProjectName, string? SubProjectName, DateOnly? ExpectedDate,
    string DeliveryState, int Version);

/// <summary>A page of the orders awaiting delivery. <paramref name="OrderCount"/> is how many orders wait in all, across every page.</summary>
public sealed record ReceivingOrderPageDto(IReadOnlyList<ReceivingOrderListItemDto> Items, int OrderCount, int Page, int PageSize);

/// <param name="OrderedQuantity">What the order asks for, in the line's own unit.</param>
/// <param name="ReceivedQuantity">What posted (not voided) deliveries say arrived.</param>
/// <param name="RemainingQuantity">Ordered minus received, never below zero: what a new delivery may still name.</param>
public sealed record ReceivingLineDto(Guid Id, int LineNumber, string Description, string Unit, decimal OrderedQuantity, decimal ReceivedQuantity,
    decimal RemainingQuantity);

public sealed record ReceivingDeliveryLineDto(Guid Id, Guid OrderLineId, int LineNumber, string Description, string Unit, decimal Quantity);

/// <param name="Status">Posted or Void.</param>
/// <param name="CanVoid">A posted delivery of a project that is not archived.</param>
/// <remarks>The reason typed when a delivery was voided is not part of this view: a reason can mention people or amounts.</remarks>
public sealed record ReceivingDeliveryDto(Guid Id, DateOnly ReceivedDate, string? DeliveryReceiptNumber, string? Note, string Status, string RecordedByName,
    DateTimeOffset RecordedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc, bool CanVoid, IReadOnlyList<ReceivingDeliveryLineDto> Lines, int Version);

/// <param name="Status">The order's own status (Draft, Approved, Closed or Cancelled); only an approved order takes a delivery.</param>
/// <param name="DeliveryState">NotReceived, PartlyReceived or FullyReceived, from the lines and the posted deliveries.</param>
/// <param name="CanRecord">An approved order of a project that is not archived with something still to receive.</param>
/// <param name="Deliveries">The newest deliveries (voided ones too), at most 100; <paramref name="DeliveryCount"/> is the true number.</param>
public sealed record ReceivingOrderDto(Guid Id, string Number, string Status, string SupplierName, string ProjectName, string? SubProjectName,
    DateOnly? ExpectedDate, string? SupplierReference, string DeliveryState, bool CanRecord, IReadOnlyList<ReceivingLineDto> Lines,
    IReadOnlyList<ReceivingDeliveryDto> Deliveries, int DeliveryCount, int Version);
