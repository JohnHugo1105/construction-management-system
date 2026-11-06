using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.ChangeOrders;

/// <summary>
/// A change to a locked budget and, optionally, to the contract price. It is drafted (header and lines editable), submitted (numbered
/// "CO-001" per project), then approved or rejected. Approving applies every line and the price change in one save and records the
/// budget effect; voiding an approved order reverses exactly that, newest first. A change order is never deleted: a draft or a
/// submitted one is discarded (voided) with a reason.
/// </summary>
public sealed class ChangeOrder : AggregateRoot
{
    public const int MaximumLines = 100;

    private readonly List<ChangeOrderLine> _lines = [];

    private ChangeOrder() { }

    public Guid ProjectId { get; private set; }

    /// <summary>"CO-001", given at submission. Null while a draft.</summary>
    public string? Number { get; private set; }

    public long? Sequence { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public ChangeOrderReason Reason { get; private set; }

    /// <summary>The signed change to the contract price, VAT included. Zero when the price doesn't change.</summary>
    public long ContractPriceDeltaMinor { get; private set; }

    public ChangeOrderStatus Status { get; private set; } = ChangeOrderStatus.Draft;

    /// <summary>The signed change to the budget: the sum of the lines' effects (an estimate until approval, then what was applied).</summary>
    public long BudgetEffectNetMinor { get; private set; }

    public long BudgetEffectVatMinor { get; private set; }
    public long BudgetEffectGrossMinor { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    /// <summary>Who approved or rejected it, and when.</summary>
    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    /// <summary>The approver's note, or the reason for rejecting.</summary>
    public string? DecisionReason { get; private set; }

    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }

    /// <summary>What it was before it was voided: Draft or Submitted (discarded), or Approved (its changes were reversed).</summary>
    public ChangeOrderStatus? VoidedFromStatus { get; private set; }

    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<ChangeOrderLine> Lines => _lines;

    /// <summary>"CO-001": numbers run per project and widen past 999.</summary>
    public static string FormatNumber(long sequence) => "CO-" + sequence.ToString("000", CultureInfo.InvariantCulture);

    public static ChangeOrder Draft(Guid id, Guid tenantId, Project project, string? title, string? description, ChangeOrderReason reason,
        long contractPriceDeltaMinor, Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        EnsureProjectTakesChangeOrders(project);
        var order = new ChangeOrder
        {
            ProjectId = project.Id,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        order.ApplyDetails(project, title, description, reason, contractPriceDeltaMinor);
        order.Initialize(id, tenantId, now);
        return order;
    }

    public void UpdateDetails(Project project, string? title, string? description, ChangeOrderReason reason, long contractPriceDeltaMinor,
        DateTimeOffset now)
    {
        EnsureDraft(project);
        ApplyDetails(project, title, description, reason, contractPriceDeltaMinor);
        Touch(now);
    }

    /// <summary>Adds a line to a draft. <paramref name="target"/> is the budget line a Revise or Omit names (null for Add).</summary>
    public ChangeOrderLine AddLine(Project project, ChangeOrderLineInput input, BudgetLine? target, int companyVatRateBps, DateTimeOffset now)
    {
        EnsureDraft(project);
        if (_lines.Count >= MaximumLines)
            throw new DomainRuleException($"A change order can have at most {MaximumLines} lines. Record another change order for the rest.");
        var line = new ChangeOrderLine(Guid.NewGuid(), TenantId, Id, _lines.Count == 0 ? 1 : _lines.Max(item => item.LineNumber) + 1);
        Fill(line, project, input, target, companyVatRateBps, null);
        _lines.Add(line);
        RecalculateEffect();
        Touch(now);
        return line;
    }

    /// <summary>Replaces one draft line. An Add line keeps the VAT rate it was first entered with.</summary>
    public void ReplaceLine(Guid lineId, Project project, ChangeOrderLineInput input, BudgetLine? target, int companyVatRateBps, DateTimeOffset now)
    {
        EnsureDraft(project);
        var line = FindLine(lineId);
        var keptRate = line.Kind == ChangeOrderLineKind.Add && input.Kind == ChangeOrderLineKind.Add ? line.VatRateBps : companyVatRateBps;
        Fill(line, project, input, target, keptRate, lineId);
        RecalculateEffect();
        Touch(now);
    }

    public void RemoveLine(Project project, Guid lineId, DateTimeOffset now)
    {
        EnsureDraft(project);
        _lines.Remove(FindLine(lineId));
        // The lines a person sees stay numbered 1, 2, 3 in the order they were added.
        var number = 1;
        foreach (var line in _lines.OrderBy(item => item.LineNumber)) line.Renumber(number++);
        RecalculateEffect();
        Touch(now);
    }

    /// <summary>Checks a draft can be submitted: something changes, and every line can still apply. Called before taking a number.</summary>
    public void EnsureCanBeSubmitted(Project project, IReadOnlyCollection<BudgetLine> targets)
    {
        EnsureDraft(project);
        if (!project.IsBudgetLocked) throw new DomainRuleException("Lock the budget before submitting a change order.");
        if (_lines.Count == 0 && ContractPriceDeltaMinor == 0)
            throw new DomainRuleException("Add at least one line, or a change to the contract price, before submitting this change order.");
        CheckTargets(targets);
        project.ContractPriceAfter(ContractPriceDeltaMinor, "contractPriceDeltaMinor");
    }

    /// <summary>Submits a draft with the next number of its project. The estimate of each Revise and Omit line is refreshed from the budget now.</summary>
    public void Submit(Project project, IReadOnlyCollection<BudgetLine> targets, long sequence, Guid userId, DateTimeOffset now)
    {
        EnsureCanBeSubmitted(project, targets);
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        var byId = targets.ToDictionary(target => target.Id);
        foreach (var line in _lines.Where(line => line.Kind != ChangeOrderLineKind.Add)) line.RefreshFrom(byId[line.BudgetLineId!.Value]);
        RecalculateEffect();
        Sequence = sequence;
        Number = FormatNumber(sequence);
        Status = ChangeOrderStatus.Submitted;
        SubmittedByUserId = userId;
        SubmittedAtUtc = now;
        Touch(now);
    }

    /// <summary>
    /// Applies the change order: Add lines become budget lines marked with this order, Revise lines change the target's quantity and unit cost
    /// (the previous values are kept on the line), Omit lines remove the target, and the contract price moves by the delta. Everything is
    /// checked before anything changes. Returns the budget lines created, for the caller to save with the rest.
    /// </summary>
    public IReadOnlyList<BudgetLine> Approve(Project project, IReadOnlyCollection<BudgetLine> targets, Guid userId, string? note, DateTimeOffset now)
    {
        EnsureProject(project);
        switch (Status)
        {
            case ChangeOrderStatus.Draft: throw new DomainRuleException("Submit this change order before approving it.");
            case ChangeOrderStatus.Approved: throw new DomainRuleException("This change order is already approved.");
            case ChangeOrderStatus.Rejected: throw new DomainRuleException("This change order was rejected. Record a new one instead.");
            case ChangeOrderStatus.Voided: throw new DomainRuleException("This change order was discarded. Record a new one instead.");
        }

        project.EnsureNotArchived();
        if (!project.IsBudgetLocked) throw new DomainRuleException("Lock the budget before approving a change order.");
        var cleanNote = Guard.OptionalText(note, "note", "the approval note", 500);
        CheckTargets(targets);
        project.ContractPriceAfter(ContractPriceDeltaMinor, "contractPriceDeltaMinor");

        // Every Add line must still be a valid line on a part of the project that takes records, and every revision must still compute.
        var byId = targets.ToDictionary(target => target.Id);
        var addValues = new Dictionary<Guid, CostLineValues>();
        foreach (var line in _lines)
        {
            try
            {
                if (line.Kind == ChangeOrderLineKind.Add)
                {
                    project.EnsureCanReceiveRecordsFor(line.SubProjectId);
                    addValues[line.Id] = line.AddValues();
                }
                else if (line.Kind == ChangeOrderLineKind.Revise)
                {
                    ChangeOrderLine.RevisedAmounts(byId[line.BudgetLineId!.Value], line.Quantity, line.UnitCostMinor);
                }
            }
            catch (Exception ex) when (ex is DomainRuleException or ValidationFailedException)
            {
                throw new DomainRuleException($"Line {line.LineNumber} ({line.Description}) can't be applied: {FirstMessage(ex)}");
            }
        }

        var created = new List<BudgetLine>();
        foreach (var line in _lines.OrderBy(item => item.LineNumber))
        {
            switch (line.Kind)
            {
                case ChangeOrderLineKind.Add:
                {
                    var budgetLine = BudgetLine.CreateFromChangeOrder(Guid.NewGuid(), TenantId, project, Id, line.SubProjectId, addValues[line.Id], userId, now);
                    line.LinkCreatedLine(budgetLine.Id);
                    created.Add(budgetLine);
                    break;
                }
                case ChangeOrderLineKind.Revise:
                {
                    var target = byId[line.BudgetLineId!.Value];
                    line.RefreshFrom(target);
                    var applied = target.ChangeQuantityAndCost(line.Quantity, line.UnitCostMinor, now);
                    if (applied != line.Resulting) throw new InvalidOperationException("The revised budget line doesn't match the change order line.");
                    break;
                }
                case ChangeOrderLineKind.Omit:
                {
                    var target = byId[line.BudgetLineId!.Value];
                    line.RefreshFrom(target);
                    target.Remove(userId, $"Change order {Number}", now);
                    break;
                }
            }
        }

        RecalculateEffect();
        project.ApplyChangeOrderDecision(ContractPriceDeltaMinor, now);
        Status = ChangeOrderStatus.Approved;
        DecidedByUserId = userId;
        DecidedAtUtc = now;
        DecisionReason = cleanNote;
        Touch(now);
        return created;
    }

    public void Reject(Project project, Guid userId, string? reason, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != ChangeOrderStatus.Submitted) throw new DomainRuleException(Status == ChangeOrderStatus.Draft
            ? "Only a submitted change order can be rejected. Discard the draft instead."
            : "Only a submitted change order can be rejected.");
        project.EnsureNotArchived();
        DecisionReason = Guard.RequiredText(reason, "reason", "the reason for rejecting this change order", 500);
        Status = ChangeOrderStatus.Rejected;
        DecidedByUserId = userId;
        DecidedAtUtc = now;
        Touch(now);
    }

    /// <summary>Discards a draft or a submitted change order. Nothing in the budget changes; the order stays on record with the reason.</summary>
    public void Discard(Project project, Guid userId, string? reason, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status is not (ChangeOrderStatus.Draft or ChangeOrderStatus.Submitted)) throw new DomainRuleException(Status == ChangeOrderStatus.Approved
            ? "An approved change order can't be discarded. Void it instead, which reverses its changes."
            : "This change order is already closed.");
        project.EnsureNotArchived();
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for discarding this change order", 500);
        VoidedFromStatus = Status;
        Status = ChangeOrderStatus.Voided;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
        Touch(now);
    }

    /// <summary>
    /// Reverses an approved change order: lines it added are removed, revised lines get their previous quantity and unit cost back (their money
    /// returns to the centavo), omitted lines are reinstated, and the contract price moves back. Refused while a change order approved after
    /// this one stands (<paramref name="laterApprovedNumber"/>), because that one may build on what this one changed.
    /// </summary>
    /// <param name="touched">The budget lines this order's lines point at (including the ones its Add lines created).</param>
    public void Void(Project project, IReadOnlyCollection<BudgetLine> touched, string? laterApprovedNumber, Guid userId, string? reason, DateTimeOffset now)
    {
        EnsureProject(project);
        if (Status != ChangeOrderStatus.Approved) throw new DomainRuleException(Status is ChangeOrderStatus.Draft or ChangeOrderStatus.Submitted
            ? "This change order isn't approved, so nothing needs reversing. Discard it instead."
            : "Only an approved change order can be voided.");
        project.EnsureNotArchived();
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for voiding this change order", 500);
        if (laterApprovedNumber is not null)
            throw new DomainRuleException($"Void {laterApprovedNumber} first. Change orders are voided newest first, so each one undoes exactly what it changed.");

        var byId = touched.Where(line => line.ProjectId == ProjectId).ToDictionary(line => line.Id);
        foreach (var line in _lines)
        {
            if (line.BudgetLineId is not { } id || !byId.TryGetValue(id, out var target))
                throw new InvalidOperationException("A budget line this change order changed is missing.");
            var expectedActive = line.Kind != ChangeOrderLineKind.Omit;
            if (target.IsRemoved == expectedActive || (expectedActive && target.CurrentAmounts() != line.Resulting))
                throw new DomainRuleException($"Line {line.LineNumber} ({line.Description}) changed after this change order was approved, so it can't be reversed exactly.");
        }

        project.ContractPriceAfter(-ContractPriceDeltaMinor, "contractPriceDeltaMinor");

        foreach (var line in _lines.OrderByDescending(item => item.LineNumber))
        {
            var target = byId[line.BudgetLineId!.Value];
            switch (line.Kind)
            {
                case ChangeOrderLineKind.Add:
                    target.Remove(userId, $"Change order {Number} was voided", now);
                    break;
                case ChangeOrderLineKind.Revise:
                    var restored = target.ChangeQuantityAndCost(line.PreviousQuantity!.Value, line.PreviousUnitCostMinor!.Value, now);
                    if (restored != line.Previous) throw new InvalidOperationException("The restored budget line doesn't match what it was before the change order.");
                    break;
                case ChangeOrderLineKind.Omit:
                    target.Reinstate(now);
                    break;
            }
        }

        project.ApplyChangeOrderDecision(-ContractPriceDeltaMinor, now);
        VoidReason = cleanReason;
        VoidedFromStatus = ChangeOrderStatus.Approved;
        Status = ChangeOrderStatus.Voided;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
        Touch(now);
    }

    /// <summary>The budget lines this order's Revise and Omit lines point at (what to load before submitting or approving).</summary>
    public IReadOnlyList<Guid> TargetIds() => _lines.Where(line => line.Kind != ChangeOrderLineKind.Add).Select(line => line.BudgetLineId!.Value).ToArray();

    /// <summary>Every budget line this order touched, including the ones its Add lines created (what to load before voiding).</summary>
    public IReadOnlyList<Guid> TouchedIds() => _lines.Where(line => line.BudgetLineId is not null).Select(line => line.BudgetLineId!.Value).ToArray();

    internal static void EnsureProjectTakesChangeOrders(Project project)
    {
        project.EnsureNotArchived();
        if (!project.IsBudgetLocked)
            throw new DomainRuleException("Lock the budget before recording change orders. Until then, change the budget lines directly.");
    }

    private void ApplyDetails(Project project, string? title, string? description, ChangeOrderReason reason, long contractPriceDeltaMinor)
    {
        var errors = new ValidationErrors();
        string? cleanTitle = null;
        string? cleanDescription = null;
        Collect(errors, () => cleanTitle = Guard.RequiredText(title, "title", "a title for the change", 120));
        Collect(errors, () => cleanDescription = Guard.OptionalText(description, "description", "the description", 1_000));
        if (!Enum.IsDefined(reason)) errors.Add("reason", "Choose why the change is needed.");
        Collect(errors, () => project.ContractPriceAfter(contractPriceDeltaMinor, "contractPriceDeltaMinor"));
        errors.ThrowIfAny();

        Title = cleanTitle!;
        Description = cleanDescription;
        Reason = reason;
        ContractPriceDeltaMinor = contractPriceDeltaMinor;
    }

    private void Fill(ChangeOrderLine line, Project project, ChangeOrderLineInput input, BudgetLine? target, int vatRateBps, Guid? replacingLineId)
    {
        switch (input.Kind)
        {
            case ChangeOrderLineKind.Add:
                project.EnsureCanReceiveRecordsFor(input.SubProjectId);
                line.SetAdd(input.SubProjectId, CostLineRules.Validate(input.Division, input.MainScope, input.SubScope, input.WbsCode, input.Description,
                    input.ItemType, input.Quantity, input.Unit, input.UnitCostMinor, input.VatTreatment, vatRateBps, input.EntryDate, input.Remarks,
                    latestAllowedDate: null));
                break;
            case ChangeOrderLineKind.Revise:
            case ChangeOrderLineKind.Omit:
            {
                var budgetLine = CheckTarget(input.BudgetLineId, target, replacingLineId);
                if (input.Kind == ChangeOrderLineKind.Omit)
                {
                    line.SetOmit(budgetLine);
                    break;
                }

                var errors = new ValidationErrors();
                Collect(errors, () => Guard.PositiveQuantity(input.Quantity, "quantity"));
                if (input.UnitCostMinor < 0) errors.Add("unitCostMinor", "The unit cost can't be negative.");
                MoneyLimits.CheckAmount(errors, input.UnitCostMinor, "unitCostMinor", "the unit cost");
                errors.ThrowIfAny();
                if (input.Quantity == budgetLine.Quantity && input.UnitCostMinor == budgetLine.UnitCostMinor)
                    throw new ValidationFailedException("quantity", "Change the quantity or the unit cost: both are the same as the budget line now.");
                line.SetRevise(budgetLine, input.Quantity, input.UnitCostMinor,
                    ChangeOrderLine.RevisedAmounts(budgetLine, input.Quantity, input.UnitCostMinor));
                break;
            }
            default:
                throw new ValidationFailedException("kind", "Choose whether the line adds, revises or omits a budget line.");
        }
    }

    private BudgetLine CheckTarget(Guid? budgetLineId, BudgetLine? target, Guid? replacingLineId)
    {
        if (budgetLineId is null || budgetLineId == Guid.Empty) throw new ValidationFailedException("budgetLineId", "Choose the budget line to change.");
        if (target is null || target.Id != budgetLineId || target.ProjectId != ProjectId)
            throw new ValidationFailedException("budgetLineId", "That budget line doesn't belong to this project.");
        if (target.IsRemoved) throw new ValidationFailedException("budgetLineId", "That budget line was removed. Choose an active one.");
        if (_lines.Any(other => other.Id != replacingLineId && other.Kind != ChangeOrderLineKind.Add && other.BudgetLineId == target.Id))
            throw new ValidationFailedException("budgetLineId", "This change order already changes that budget line. Edit that line instead.");
        return target;
    }

    private void CheckTargets(IReadOnlyCollection<BudgetLine> targets)
    {
        var byId = targets.ToDictionary(target => target.Id);
        foreach (var line in _lines.Where(line => line.Kind != ChangeOrderLineKind.Add))
        {
            if (!byId.TryGetValue(line.BudgetLineId!.Value, out var target) || target.ProjectId != ProjectId)
                throw new DomainRuleException($"Line {line.LineNumber} ({line.Description}) points at a budget line that is no longer there.");
            if (target.IsRemoved)
                throw new DomainRuleException($"Line {line.LineNumber} ({line.Description}): that budget line was removed since. Edit or remove this line first.");
        }
    }

    private void RecalculateEffect()
    {
        long net = 0, vat = 0, gross = 0;
        foreach (var line in _lines)
        {
            net = checked(net + line.EffectNetMinor);
            vat = checked(vat + line.EffectVatMinor);
            gross = checked(gross + line.EffectGrossMinor);
        }

        (BudgetEffectNetMinor, BudgetEffectVatMinor, BudgetEffectGrossMinor) = (net, vat, gross);
    }

    private void EnsureDraft(Project project)
    {
        EnsureProject(project);
        if (Status != ChangeOrderStatus.Draft)
            throw new DomainRuleException("Only a draft change order can be changed. Discard this one and record a new one if it needs to change.");
        project.EnsureNotArchived();
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this change order.", nameof(project));
    }

    private ChangeOrderLine FindLine(Guid lineId) =>
        _lines.FirstOrDefault(line => line.Id == lineId) ?? throw new NotFoundException("that line of the change order");

    private static void Collect(ValidationErrors errors, Action check)
    {
        try { check(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
        }
    }

    private static string FirstMessage(Exception ex) => ex is ValidationFailedException validation
        ? validation.Errors.Values.SelectMany(messages => messages).FirstOrDefault() ?? ex.Message
        : ex.Message;
}
