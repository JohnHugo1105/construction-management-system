using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Suppliers;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;

namespace Cms.Application.Features.Purchasing;

/// <summary>
/// Purchase order use cases. A cost engineer drafts an order to a supplier for a project, someone who may commit the company's money
/// approves it (its lines are then locked), and what it has not been invoiced for counts as committed cost in the project's forecast.
/// An order is cancelled before anything is invoiced against it, or closed when the supplier delivered less. Each step is one save with
/// its audit line, so the order and its history change together or not at all. Orders are never deleted.
/// </summary>
public sealed class PurchaseOrderService(IPurchaseOrderRepository orders, IPurchaseOrderReader reader, ISupplierRepository suppliers, IProjectRepository projects,
    IOrganizationRepository organizations, INumberSequence numbers, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const string NumberSequenceKey = "purchase-order";

    public Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        PurchaseOrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
            status = Enum.GetNames<PurchaseOrderStatus>().FirstOrDefault(name => string.Equals(name, query.Status.Trim(), StringComparison.OrdinalIgnoreCase)) is { } name
                ? Enum.Parse<PurchaseOrderStatus>(name)
                : throw new ValidationFailedException("status", "Choose Draft, Approved, Closed or Cancelled.");
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return reader.ListAsync(new PurchaseOrderFilter(query.ProjectId, query.SupplierId, status, search), page, cancellationToken);
    }

    public Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    /// <summary>The lines of the project's approved orders, for the "against a purchase order" choice when recording an invoice.</summary>
    public async Task<IReadOnlyList<OpenPurchaseOrderLineDto>> ListOpenLinesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // The project is looked up first so a project of another company answers 404, not an empty list.
        _ = await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        return await reader.ListOpenLinesAsync(projectId, cancellationToken);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await orders.FindByClientRequestIdAsync(requestId, cancellationToken) is { } already)
            return await EnsureSameRequest(already, request, cancellationToken);

        try
        {
            return await RaiseAsync(request, requestId, cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form submitted twice at once: the first was saved between this one's look-up and its save, and the database refused the
            // second. Answer it with the order the first made; without one the refusal is a real one and is reported as it is.
            if (await orders.FindByClientRequestIdAsync(requestId, cancellationToken) is { } first)
                return await EnsureSameRequest(first, request, cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(order.Version, request.Version);
        var supplier = await LoadSupplierAsync(request.SupplierId, cancellationToken);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);

        order.ReviseDraft(supplier, project, request.SubProjectId, request.OrderDate, request.ExpectedDate, request.SupplierReference, request.Notes,
            ToInputs(request.Lines), organization.VatRateBps, organization.TodayLocal(clock.UtcNow), clock.UtcNow);

        audit.Record("PurchaseOrder.Updated", nameof(PurchaseOrder), order.Id, $"Updated draft purchase order {order.Number} to {supplier.Name} for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(order.Version, request.Version);
        var supplier = await LoadSupplierAsync(order.SupplierId, cancellationToken);
        var project = await LoadProjectAsync(order.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);

        order.Approve(supplier, project, currentUser.UserId, clock.UtcNow);

        audit.Record("PurchaseOrder.Approved", nameof(PurchaseOrder), order.Id,
            $"Approved purchase order {order.Number} to {supplier.Name} for {project.Name}, {CostingInput.MoneyText(order.GrossMinor, organization.CurrencyCode)} with VAT.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid id, CancelPurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(order.Version, request.Version);
        var project = await LoadProjectAsync(order.ProjectId, cancellationToken);
        var hasInvoices = await orders.HasMatchedInvoicesAsync(order.Id, cancellationToken);

        order.Cancel(project, request.Reason, hasInvoices, currentUser.UserId, clock.UtcNow);

        audit.Record("PurchaseOrder.Cancelled", nameof(PurchaseOrder), order.Id, $"Cancelled purchase order {order.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> CloseAsync(Guid id, ClosePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(order.Version, request.Version);
        var project = await LoadProjectAsync(order.ProjectId, cancellationToken);

        order.Close(project, request.Reason, currentUser.UserId, clock.UtcNow);

        // The reason stays on the order: a typed reason may mention amounts or people and the activity log is read by more people than the order.
        audit.Record("PurchaseOrder.Closed", nameof(PurchaseOrder), order.Id, $"Closed purchase order {order.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    private async Task<PurchaseOrderDto> RaiseAsync(CreatePurchaseOrderRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var supplier = await LoadSupplierAsync(request.SupplierId, cancellationToken);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;

        var order = PurchaseOrder.CreateDraft(Guid.NewGuid(), currentUser.TenantId, supplier, project, request.SubProjectId, request.OrderDate,
            request.ExpectedDate, request.SupplierReference, request.Notes, ToInputs(request.Lines), organization.VatRateBps, currentUser.UserId, requestId,
            organization.TodayLocal(now), now);
        // The number is taken only now that the order is known to be valid, so a form with mistakes never uses one up.
        order.AssignNumber(await numbers.NextAsync(NumberSequenceKey, cancellationToken));

        orders.Add(order);
        audit.Record("PurchaseOrder.Created", nameof(PurchaseOrder), order.Id,
            $"Drafted purchase order {order.Number} to {supplier.Name} for {project.Name}, {CostingInput.MoneyText(order.GrossMinor, organization.CurrencyCode)} with VAT.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    /// <summary>A repeated request id answers with the original order only when it was for the same project and supplier.</summary>
    private async Task<PurchaseOrderDto> EnsureSameRequest(PurchaseOrder original, CreatePurchaseOrderRequest request, CancellationToken cancellationToken) =>
        original.ProjectId == request.ProjectId && original.SupplierId == request.SupplierId
            ? await ReadAsync(original.Id, cancellationToken)
            : throw new DomainRuleException("That request was already used for another order. Reload the page and try again.");

    private static IReadOnlyList<PurchaseOrderLineInput> ToInputs(IReadOnlyList<PurchaseOrderLineRequest>? lines) =>
        (lines ?? []).Select(line => new PurchaseOrderLineInput(line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description,
            CostingInput.ParseItemType(line.ItemType), line.Quantity, line.Unit, line.UnitCostMinor, CostingInput.ParseVatTreatment(line.VatTreatment))).ToArray();

    private async Task<PurchaseOrderDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that purchase order");

    private async Task<PurchaseOrder> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await orders.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that purchase order");

    private async Task<Supplier> LoadSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ValidationFailedException("supplierId", "Choose the supplier this order is for.");
        return await suppliers.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that firm");
    }

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ValidationFailedException("projectId", "Choose the project this order is for.");
        return await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
    }
}
