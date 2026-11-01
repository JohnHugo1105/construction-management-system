using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Purchasing;

namespace ConstructOPX.Application.Features.Purchasing;

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface IPurchaseOrderRepository
{
    /// <summary>Loads an order with its lines, tracked for change. Null when it isn't in the organization.</summary>
    Task<PurchaseOrder?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The order an earlier request with this id created, so a repeated request returns it instead of making a second one.</summary>
    Task<PurchaseOrder?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>True when a posted (not voided) supplier invoice is matched to any line of the order.</summary>
    Task<bool> HasMatchedInvoicesAsync(Guid orderId, CancellationToken cancellationToken);

    void Add(PurchaseOrder order);
}

public interface IGoodsReceiptRepository
{
    /// <summary>Loads a delivery with its lines, tracked for change. Null when it isn't in the organization.</summary>
    Task<GoodsReceipt?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The delivery an earlier request with this id recorded, so a repeated request returns it instead of making a second one.</summary>
    Task<GoodsReceipt?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>What posted (not voided) deliveries name per order line of the order: line id to received quantity. A line nothing arrived for is absent.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> ReceivedQuantitiesAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>True when a posted delivery of this supplier already has this delivery receipt number (already normalized).</summary>
    Task<bool> NumberIsRecordedAsync(Guid supplierId, string normalizedNumber, CancellationToken cancellationToken);

    /// <summary>True when a posted delivery exists for the order.</summary>
    Task<bool> HasPostedAsync(Guid orderId, CancellationToken cancellationToken);

    void Add(GoodsReceipt receipt);
}

/// <summary>Reads orders straight into DTOs in SQL. Never hydrates whole aggregates to build a list.</summary>
public interface IPurchaseOrderReader
{
    Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>One order with its lines, what has been invoiced against each, and the invoices matched to it. Null when it isn't in the organization.</summary>
    Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The lines of a project's approved orders, for choosing what an invoice is against.</summary>
    Task<IReadOnlyList<OpenPurchaseOrderLineDto>> ListOpenLinesAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>Finds one order line and the order it belongs to. Cost entries use it to match an invoice to an order.</summary>
public interface IPurchaseOrderLineLookup
{
    Task<PurchaseOrderLineRef?> FindAsync(Guid lineId, CancellationToken cancellationToken);
}

public sealed record PurchaseOrderLineRef(Guid LineId, Guid OrderId, string OrderNumber, Guid ProjectId, PurchaseOrderStatus OrderStatus, string Description);

// ---- Queries ----------------------------------------------------------------------------------------------------------------

public sealed record PurchaseOrderListQuery(Guid? ProjectId, Guid? SupplierId, string? Status, string? Search);

public sealed record PurchaseOrderFilter(Guid? ProjectId, Guid? SupplierId, PurchaseOrderStatus? Status, string? Search);

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

/// <param name="DeliveryState">NotReceived, PartlyReceived or FullyReceived for an approved order; null for every other status (nothing is expected of a draft, a closed or a cancelled order).</param>
public sealed record PurchaseOrderListItemDto(Guid Id, string Number, Guid SupplierId, string SupplierName, Guid ProjectId, string ProjectName,
    DateOnly OrderDate, DateOnly? ExpectedDate, string Status, int LineCount, long NetMinor, long VatMinor, long GrossMinor, string? DeliveryState);

/// <param name="InvoicedNetMinor">The posted supplier invoices matched to this line, before VAT.</param>
/// <param name="RemainingGrossMinor">What has not been invoiced yet: the line amount minus the invoices, never below zero.</param>
/// <param name="OverByGrossMinor">How much the invoices matched to this line exceed it (zero unless they do).</param>
/// <param name="ReceivedQuantity">What posted deliveries say arrived for this line.</param>
/// <param name="ToReceiveQuantity">Ordered minus received, never below zero: what a new delivery may still name.</param>
/// <param name="ReceivedValueNetMinor">The line's unit cost times the received quantity, rounded once, before VAT.</param>
/// <param name="ReceivedValueGrossMinor">The same with VAT, on the line's own VAT basis and rate.</param>
/// <param name="InvoicedAheadOfDelivery">Advisory: more was invoiced to this line (with VAT) than the value of what arrived. The invoice is still valid.</param>
public sealed record PurchaseOrderLineDto(Guid Id, int LineNumber, int? Division, string? MainScope, string? SubScope, string? WbsCode, string Description,
    string ItemType, decimal Quantity, string Unit, long UnitCostMinor, string VatTreatment, int VatRateBps, long NetMinor, long VatMinor, long GrossMinor,
    long InvoicedNetMinor, long InvoicedGrossMinor, long RemainingNetMinor, long RemainingGrossMinor, long OverByGrossMinor, decimal ReceivedQuantity,
    decimal ToReceiveQuantity, long ReceivedValueNetMinor, long ReceivedValueGrossMinor, bool InvoicedAheadOfDelivery);

public sealed record GoodsReceiptLineDto(Guid Id, Guid PurchaseOrderLineId, int LineNumber, string Description, string Unit, decimal Quantity);

/// <param name="Status">Posted or Void.</param>
/// <param name="CanVoid">A posted delivery of a project that is not archived.</param>
public sealed record GoodsReceiptDto(Guid Id, DateOnly ReceivedDate, string? DeliveryReceiptNumber, string? Note, string Status, string RecordedByName,
    DateTimeOffset RecordedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc, string? VoidReason, bool CanVoid, IReadOnlyList<GoodsReceiptLineDto> Lines,
    int Version);

public sealed record MatchedInvoiceDto(Guid CostEntryId, Guid PurchaseOrderLineId, int LineNumber, DateOnly EntryDate, string SourceDealer, string? InvoiceNumber,
    string Description, long NetMinor, long GrossMinor);

/// <param name="CommittedNetMinor">What an approved order has not been invoiced for yet (zero for any other status): the amount in the project's forecast.</param>
/// <param name="Invoices">The newest invoices matched to the order, at most 100; <paramref name="InvoiceCount"/> is the true number.</param>
/// <param name="CanEdit">A draft of a project that can still receive records.</param>
/// <param name="CanCancel">A draft, or an approved order nothing has been invoiced against or delivered for, of a project that is not archived.</param>
/// <param name="DeliveryState">NotReceived, PartlyReceived or FullyReceived, from the lines and the posted deliveries.</param>
/// <param name="Receipts">The newest deliveries (voided ones too), at most 100; <paramref name="ReceiptCount"/> is the true number.</param>
/// <param name="CanReceive">An approved order of a project that is not archived with something still to receive.</param>
public sealed record PurchaseOrderDto(Guid Id, string Number, string Status, Guid SupplierId, string SupplierName, Guid ProjectId, string ProjectName,
    bool ProjectArchived, Guid? SubProjectId, string? SubProjectName, DateOnly OrderDate, DateOnly? ExpectedDate, string? SupplierReference, string? Notes,
    long NetMinor, long VatMinor, long GrossMinor, long InvoicedNetMinor, long InvoicedGrossMinor, long CommittedNetMinor, long CommittedGrossMinor,
    IReadOnlyList<PurchaseOrderLineDto> Lines, IReadOnlyList<MatchedInvoiceDto> Invoices, int InvoiceCount, string CreatedByName, DateTimeOffset CreatedAtUtc,
    string? ApprovedByName, DateTimeOffset? ApprovedAtUtc, string? ClosedByName, DateTimeOffset? ClosedAtUtc, string? CloseReason, string? CancelledByName,
    DateTimeOffset? CancelledAtUtc, string? CancelReason, bool CanEdit, bool CanApprove, bool CanCancel, bool CanClose, string DeliveryState,
    IReadOnlyList<GoodsReceiptDto> Receipts, int ReceiptCount, bool CanReceive, int Version);

/// <summary>A line of an approved order a supplier invoice can be matched to.</summary>
public sealed record OpenPurchaseOrderLineDto(Guid LineId, Guid OrderId, string OrderNumber, int LineNumber, string SupplierName, string Description,
    long GrossMinor, long RemainingNetMinor, long RemainingGrossMinor);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// One line as typed. <c>ItemType</c> and <c>VatTreatment</c> are names (for example "Material", "Inclusive"); the company's VAT rate at the moment of
/// entry is kept on the line, so it is not a field.
/// </summary>
public sealed record PurchaseOrderLineRequest(int? Division, string? MainScope, string? SubScope, string? WbsCode, string? Description, string? ItemType,
    decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment);

public sealed record CreatePurchaseOrderRequest(Guid ProjectId, Guid? SubProjectId, Guid SupplierId, DateOnly OrderDate, DateOnly? ExpectedDate,
    string? SupplierReference, string? Notes, IReadOnlyList<PurchaseOrderLineRequest>? Lines, string? ClientRequestId);

public sealed record UpdatePurchaseOrderRequest(Guid ProjectId, Guid? SubProjectId, Guid SupplierId, DateOnly OrderDate, DateOnly? ExpectedDate,
    string? SupplierReference, string? Notes, IReadOnlyList<PurchaseOrderLineRequest>? Lines, int Version);

public sealed record CancelPurchaseOrderRequest(string? Reason, int Version);

public sealed record ClosePurchaseOrderRequest(string? Reason, int Version);

/// <param name="Lines">The lines that arrived: an order line and a quantity greater than zero, no line twice. Leave out a line nothing arrived for.</param>
public sealed record RecordDeliveryRequest(DateOnly ReceivedDate, string? DeliveryReceiptNumber, string? Note, IReadOnlyList<DeliveryLineRequest>? Lines,
    string? ClientRequestId);

public sealed record DeliveryLineRequest(Guid PurchaseOrderLineId, decimal Quantity);

public sealed record VoidDeliveryRequest(string? Reason, int Version);
