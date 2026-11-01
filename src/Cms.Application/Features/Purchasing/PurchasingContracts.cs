using Cms.Application.Common;
using Cms.Domain.Features.Purchasing;

namespace Cms.Application.Features.Purchasing;

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

public sealed record PurchaseOrderListItemDto(Guid Id, string Number, Guid SupplierId, string SupplierName, Guid ProjectId, string ProjectName,
    DateOnly OrderDate, DateOnly? ExpectedDate, string Status, int LineCount, long NetMinor, long VatMinor, long GrossMinor);

/// <param name="InvoicedNetMinor">The posted supplier invoices matched to this line, before VAT.</param>
/// <param name="RemainingGrossMinor">What has not been invoiced yet: the line amount minus the invoices, never below zero.</param>
/// <param name="OverByGrossMinor">How much the invoices matched to this line exceed it (zero unless they do).</param>
public sealed record PurchaseOrderLineDto(Guid Id, int LineNumber, int? Division, string? MainScope, string? SubScope, string? WbsCode, string Description,
    string ItemType, decimal Quantity, string Unit, long UnitCostMinor, string VatTreatment, int VatRateBps, long NetMinor, long VatMinor, long GrossMinor,
    long InvoicedNetMinor, long InvoicedGrossMinor, long RemainingNetMinor, long RemainingGrossMinor, long OverByGrossMinor);

public sealed record MatchedInvoiceDto(Guid CostEntryId, Guid PurchaseOrderLineId, int LineNumber, DateOnly EntryDate, string SourceDealer, string? InvoiceNumber,
    string Description, long NetMinor, long GrossMinor);

/// <param name="CommittedNetMinor">What an approved order has not been invoiced for yet (zero for any other status): the amount in the project's forecast.</param>
/// <param name="Invoices">The newest invoices matched to the order, at most 100; <paramref name="InvoiceCount"/> is the true number.</param>
/// <param name="CanEdit">A draft of a project that can still receive records.</param>
/// <param name="CanCancel">A draft, or an approved order nothing has been invoiced against, of a project that is not archived.</param>
public sealed record PurchaseOrderDto(Guid Id, string Number, string Status, Guid SupplierId, string SupplierName, Guid ProjectId, string ProjectName,
    bool ProjectArchived, Guid? SubProjectId, string? SubProjectName, DateOnly OrderDate, DateOnly? ExpectedDate, string? SupplierReference, string? Notes,
    long NetMinor, long VatMinor, long GrossMinor, long InvoicedNetMinor, long InvoicedGrossMinor, long CommittedNetMinor, long CommittedGrossMinor,
    IReadOnlyList<PurchaseOrderLineDto> Lines, IReadOnlyList<MatchedInvoiceDto> Invoices, int InvoiceCount, string CreatedByName, DateTimeOffset CreatedAtUtc,
    string? ApprovedByName, DateTimeOffset? ApprovedAtUtc, string? ClosedByName, DateTimeOffset? ClosedAtUtc, string? CloseReason, string? CancelledByName,
    DateTimeOffset? CancelledAtUtc, string? CancelReason, bool CanEdit, bool CanApprove, bool CanCancel, bool CanClose, int Version);

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
