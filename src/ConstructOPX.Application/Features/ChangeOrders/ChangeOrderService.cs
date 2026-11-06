using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Billing;
using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.ChangeOrders;

/// <summary>
/// Change order use cases. A draft is written line by line, submitted (numbered per project), then approved or rejected. Approving applies
/// every line and the contract price change in one save; voiding an approved order reverses exactly that in one save, newest first.
/// </summary>
public sealed class ChangeOrderService(IChangeOrderRepository orders, IChangeOrderReader reader, IChangeOrderBudgetLineRepository targets,
    IBudgetLineRepository budgetLines, IProjectRepository projects, IOrganizationRepository organizations, INumberSequence numbers,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit, IGovernmentLimitReader governmentLimits,
    INotificationWriter notifications)
{
    public async Task<PagedResult<ChangeOrderListItemDto>> ListAsync(Guid projectId, string? status, PageRequest page, CancellationToken cancellationToken)
    {
        // The project is looked up first so another company's project answers 404, not an empty list.
        await LoadProject(projectId, cancellationToken);
        ChangeOrderStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
            filter = ParseEnum<ChangeOrderStatus>(status) ??
                throw new ValidationFailedException("status", "Choose one of: Draft, Submitted, Approved, Rejected or Voided.");
        return await reader.ListAsync(projectId, filter, page, cancellationToken);
    }

    public Task<ChangeOrderDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    public async Task<ChangeOrderDto> CreateAsync(Guid projectId, CreateChangeOrderRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await orders.FindByClientRequestIdAsync(requestId, cancellationToken) is { } original)
            return await SameProjectAsync(original, project);

        var order = ChangeOrder.Draft(Guid.NewGuid(), currentUser.TenantId, project, request.Title, request.Description, ParseReason(request.Reason),
            request.ContractPriceDeltaMinor, currentUser.UserId, requestId, clock.UtcNow);
        orders.Add(order);
        audit.Record("ChangeOrder.Drafted", nameof(ChangeOrder), order.Id, $"Started a change order \"{order.Title}\" on {project.Name}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form submitted twice at once: the database let only the first in. Answer with what the first made.
            if (await orders.FindByClientRequestIdAsync(requestId, cancellationToken) is { } first) return await SameProjectAsync(first, project);
            throw;
        }

        return await ReadAsync(order.Id, cancellationToken);

        async Task<ChangeOrderDto> SameProjectAsync(ChangeOrder found, Project expected) => found.ProjectId == expected.Id
            ? await ReadAsync(found.Id, cancellationToken)
            : throw new DomainRuleException("That request was already used for another project. Reload the page and try again.");
    }

    public async Task<ChangeOrderDto> UpdateAsync(Guid id, UpdateChangeOrderRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        order.UpdateDetails(project, request.Title, request.Description, ParseReason(request.Reason), request.ContractPriceDeltaMinor, clock.UtcNow);
        audit.Record("ChangeOrder.Updated", nameof(ChangeOrder), order.Id, $"Updated the draft change order \"{order.Title}\" on {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<ChangeOrderDto> AddLineAsync(Guid id, SaveChangeOrderLineRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var (input, target) = await InputAsync(request, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var line = order.AddLine(project, input, target, organization.VatRateBps, clock.UtcNow);
        audit.Record("ChangeOrder.LineAdded", nameof(ChangeOrder), order.Id,
            $"Added line {line.LineNumber} ({line.Kind}: \"{line.Description}\") to the draft change order \"{order.Title}\".");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<ChangeOrderDto> UpdateLineAsync(Guid id, Guid lineId, SaveChangeOrderLineRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var (input, target) = await InputAsync(request, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        order.ReplaceLine(lineId, project, input, target, organization.VatRateBps, clock.UtcNow);
        audit.Record("ChangeOrder.LineChanged", nameof(ChangeOrder), order.Id, $"Changed a line of the draft change order \"{order.Title}\".");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<ChangeOrderDto> RemoveLineAsync(Guid id, Guid lineId, VersionRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        order.RemoveLine(project, lineId, clock.UtcNow);
        audit.Record("ChangeOrder.LineRemoved", nameof(ChangeOrder), order.Id, $"Removed a line from the draft change order \"{order.Title}\".");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    /// <summary>Submits a draft. The number is taken only after every check passes, so a refused submission uses no number.</summary>
    public async Task<ChangeOrderDto> SubmitAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var lines = await targets.GetManyAsync(order.TargetIds(), cancellationToken);
        order.EnsureCanBeSubmitted(project, lines);
        var sequence = await numbers.NextAsync($"change-order:{project.Id}", cancellationToken);
        order.Submit(project, lines, sequence, currentUser.UserId, clock.UtcNow);
        audit.Record("ChangeOrder.Submitted", nameof(ChangeOrder), order.Id, $"Submitted change order {order.Number} \"{order.Title}\" on {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    /// <summary>
    /// Approves and applies the change order in one save: the budget lines it adds, revises and omits, and the contract price. The project
    /// is marked changed too, so two decisions on one project at the same moment can't both be saved.
    /// </summary>
    public async Task<ChangeOrderDto> ApproveAsync(Guid id, ApproveChangeOrderRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var lines = await targets.GetManyAsync(order.TargetIds(), cancellationToken);
        var created = order.Approve(project, lines, currentUser.UserId, request.Note, clock.UtcNow);
        foreach (var line in created) budgetLines.Add(line);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        audit.Record("ChangeOrder.Approved", nameof(ChangeOrder), order.Id,
            $"Approved change order {order.Number} on {project.Name}: budget {Signed(order.BudgetEffectGrossMinor, organization.CurrencyCode)} with VAT" +
            (order.ContractPriceDeltaMinor == 0 ? "." : $", contract price {Signed(order.ContractPriceDeltaMinor, organization.CurrencyCode)}."));
        notifications.Notify(order.CreatedByUserId, NotificationKind.DraftDecided, NotificationMessages.Decided(currentUser.DisplayName, "approved", NotificationMessages.ChangeOrder(order.Number, project.Name)), RecordKind.ChangeOrder, order.Id, order.ProjectId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<ChangeOrderDto> RejectAsync(Guid id, ChangeOrderReasonRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        order.Reject(project, currentUser.UserId, request.Reason, clock.UtcNow);
        audit.Record("ChangeOrder.Rejected", nameof(ChangeOrder), order.Id, $"Rejected change order {order.Number} on {project.Name}.");
        notifications.Notify(order.CreatedByUserId, NotificationKind.DraftDecided, NotificationMessages.Decided(currentUser.DisplayName, "rejected", NotificationMessages.ChangeOrder(order.Number, project.Name)), RecordKind.ChangeOrder, order.Id, order.ProjectId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    public async Task<ChangeOrderDto> DiscardAsync(Guid id, ChangeOrderReasonRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var wasSubmitted = order.Status == ChangeOrderStatus.Submitted;
        order.Discard(project, currentUser.UserId, request.Reason, clock.UtcNow);
        audit.Record("ChangeOrder.Discarded", nameof(ChangeOrder), order.Id, wasSubmitted
            ? $"Withdrew change order {order.Number} on {project.Name}."
            : $"Discarded the draft change order \"{order.Title}\" on {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    /// <summary>Voids an approved change order and reverses every line and the price change in one save. Newest first.</summary>
    public async Task<ChangeOrderDto> VoidAsync(Guid id, ChangeOrderReasonRequest request, CancellationToken cancellationToken)
    {
        var (order, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var later = order is { Status: ChangeOrderStatus.Approved, DecidedAtUtc: { } decided }
            ? await orders.LatestApprovedAfterAsync(project.Id, order.Id, decided, order.Sequence ?? 0, cancellationToken)
            : null;
        var touched = await targets.GetManyAsync(order.TouchedIds(), cancellationToken);
        order.Void(project, touched, later, currentUser.UserId, request.Reason, clock.UtcNow);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        audit.Record("ChangeOrder.Voided", nameof(ChangeOrder), order.Id,
            $"Voided change order {order.Number} on {project.Name} and reversed its changes: budget {Signed(-order.BudgetEffectGrossMinor, organization.CurrencyCode)} with VAT" +
            (order.ContractPriceDeltaMinor == 0 ? "." : $", contract price {Signed(-order.ContractPriceDeltaMinor, organization.CurrencyCode)}."));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(order.Id, cancellationToken);
    }

    private async Task<(ChangeOrderLineInput Input, BudgetLine? Target)> InputAsync(SaveChangeOrderLineRequest request, CancellationToken cancellationToken)
    {
        var kind = ParseEnum<ChangeOrderLineKind>(request.Kind) ?? (ChangeOrderLineKind)(-1);
        var target = kind is ChangeOrderLineKind.Revise or ChangeOrderLineKind.Omit && request.BudgetLineId is { } budgetLineId
            ? await budgetLines.GetAsync(budgetLineId, cancellationToken)
            : null;
        var input = new ChangeOrderLineInput(kind, request.BudgetLineId, request.SubProjectId, request.Division, request.MainScope, request.SubScope,
            request.WbsCode, request.Description, CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit, request.UnitCostMinor,
            CostingInput.ParseVatTreatment(request.VatTreatment), request.EntryDate, request.Remarks);
        return (input, target);
    }

    private async Task<(ChangeOrder Order, Project Project)> LoadForChangeAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that change order");
        ConcurrencyGuard.Ensure(order.Version, version);
        return (order, await LoadProject(order.ProjectId, cancellationToken));
    }

    private async Task<ChangeOrderDto> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that change order");
        return order with { GovernmentLimit = GovernmentLimitOf(order, await governmentLimits.GetAsync(order.ProjectId, cancellationToken)) };
    }

    /// <summary>
    /// A government client's limit on added work (advisory). The order's own price increase counts while it waits for approval, so the
    /// approver sees the effect before deciding; once approved it is among the approved ones, and a rejected or voided one never counts.
    /// </summary>
    internal static GovernmentLimitDto GovernmentLimitOf(ChangeOrderDto order, GovernmentLimitFigures? figures)
    {
        if (figures is not { IsGovernmentClient: true, BaselineContractPriceMinor: { } baseline }) return GovernmentLimitDto.None;
        var pending = order.Status is nameof(ChangeOrderStatus.Draft) or nameof(ChangeOrderStatus.Submitted)
            ? Math.Max(0, order.ContractPriceDeltaMinor)
            : 0;
        var limit = GovernmentContract.Variations(baseline, figures.ApprovedAdditiveMinor, pending);
        return new GovernmentLimitDto(true, limit.LimitMinor, limit.UsedMinor, limit.Exceeds);
    }

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private static ChangeOrderReason ParseReason(string? text) => ParseEnum<ChangeOrderReason>(text) ?? (ChangeOrderReason)(-1);

    private static string Signed(long minor, string currencyCode) =>
        (minor > 0 ? "+" : minor < 0 ? "-" : "") + CostingInput.MoneyText(Math.Abs(minor), currencyCode);

    /// <summary>One of the enum's names, any letter case. Numbers and lists are not names.</summary>
    private static T? ParseEnum<T>(string? text) where T : struct, Enum
    {
        var name = text?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        var match = Enum.GetNames<T>().FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : Enum.Parse<T>(match);
    }
}
