using ConstructOPX.Domain.Features.Costing;

namespace ConstructOPX.Domain.Features.ChangeOrders;

/// <summary>What a person types for one line of a change order. Which fields count depends on <see cref="Kind"/>.</summary>
/// <param name="BudgetLineId">The budget line a Revise or Omit line changes.</param>
public sealed record ChangeOrderLineInput(ChangeOrderLineKind Kind, Guid? BudgetLineId, Guid? SubProjectId, int? Division, string? MainScope,
    string? SubScope, string? WbsCode, string? Description, ItemType ItemType, decimal Quantity, string? Unit, long UnitCostMinor,
    VatTreatment VatTreatment, DateOnly EntryDate, string? Remarks);

/// <summary>
/// One change in a change order. The fields describe the budget line as it will be after approval (Add, Revise) or as it is being removed
/// (Omit). Revise and Omit also keep the line's previous quantity, unit cost and money, recorded again at approval, so a void puts it back
/// exactly. The effect is the signed change to the budget: Add is +amount, Revise is new minus previous, Omit is -previous.
/// </summary>
public sealed class ChangeOrderLine
{
    private ChangeOrderLine() { }

    internal ChangeOrderLine(Guid id, Guid tenantId, Guid changeOrderId, int lineNumber) =>
        (Id, TenantId, ChangeOrderId, LineNumber) = (id, tenantId, changeOrderId, lineNumber);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ChangeOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public ChangeOrderLineKind Kind { get; private set; }

    /// <summary>The budget line changed (Revise, Omit), or the one created at approval (Add).</summary>
    public Guid? BudgetLineId { get; private set; }

    public Guid? SubProjectId { get; private set; }
    public int? Division { get; private set; }
    public string? MainScope { get; private set; }
    public string? SubScope { get; private set; }
    public string? WbsCode { get; private set; }
    public string Description { get; private set; } = "";
    public ItemType ItemType { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";
    public long UnitCostMinor { get; private set; }
    public VatTreatment VatTreatment { get; private set; }
    public int VatRateBps { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string? Remarks { get; private set; }
    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }

    public decimal? PreviousQuantity { get; private set; }
    public long? PreviousUnitCostMinor { get; private set; }
    public long? PreviousNetMinor { get; private set; }
    public long? PreviousVatMinor { get; private set; }
    public long? PreviousGrossMinor { get; private set; }

    public long EffectNetMinor { get; private set; }
    public long EffectVatMinor { get; private set; }
    public long EffectGrossMinor { get; private set; }

    public LineAmounts Effect => new(EffectNetMinor, EffectVatMinor, EffectGrossMinor);

    internal LineAmounts Resulting => new(NetMinor, VatMinor, GrossMinor);

    internal LineAmounts? Previous => PreviousNetMinor is { } net ? new LineAmounts(net, PreviousVatMinor!.Value, PreviousGrossMinor!.Value) : null;

    /// <summary>An Add line: the new budget line's own fields, checked by the same rules as a budget line.</summary>
    internal void SetAdd(Guid? subProjectId, CostLineValues values)
    {
        Kind = ChangeOrderLineKind.Add;
        BudgetLineId = null;
        SubProjectId = subProjectId;
        CopyValues(values.Classification.Division, values.Classification.MainScope, values.Classification.SubScope, values.Classification.WbsCode,
            values.Description, values.ItemType, values.Quantity, values.Unit, values.UnitCostMinor, values.VatTreatment, values.VatRateBps,
            values.EntryDate, values.Remarks);
        SetResulting(values.Amounts);
        PreviousQuantity = null;
        PreviousUnitCostMinor = null;
        SetPrevious(null);
        SetEffect(values.Amounts);
    }

    /// <summary>A Revise line against the target's current values. <paramref name="revised"/> is the line with the new quantity and unit cost.</summary>
    internal void SetRevise(BudgetLine target, decimal quantity, long unitCostMinor, LineAmounts revised)
    {
        Kind = ChangeOrderLineKind.Revise;
        BudgetLineId = target.Id;
        CopyTarget(target);
        Quantity = quantity;
        UnitCostMinor = unitCostMinor;
        SetResulting(revised);
        RecordPrevious(target);
        SetEffect(Subtract(revised, target.CurrentAmounts()));
    }

    /// <summary>An Omit line: the target as it stands, and an effect of minus its money.</summary>
    internal void SetOmit(BudgetLine target)
    {
        Kind = ChangeOrderLineKind.Omit;
        BudgetLineId = target.Id;
        CopyTarget(target);
        SetResulting(target.CurrentAmounts());
        RecordPrevious(target);
        SetEffect(Subtract(LineAmounts.Zero, target.CurrentAmounts()));
    }

    /// <summary>Refreshes a Revise or Omit line from its target as it is now (at submission and at approval).</summary>
    internal void RefreshFrom(BudgetLine target)
    {
        if (Kind == ChangeOrderLineKind.Omit) SetOmit(target);
        else if (Kind == ChangeOrderLineKind.Revise) SetRevise(target, Quantity, UnitCostMinor, RevisedAmounts(target, Quantity, UnitCostMinor));
    }

    internal void LinkCreatedLine(Guid budgetLineId) => BudgetLineId = budgetLineId;

    internal void Renumber(int lineNumber) => LineNumber = lineNumber;

    /// <summary>The Add line's fields as the budget line rules see them, re-checked at approval.</summary>
    internal CostLineValues AddValues() => CostLineRules.Validate(Division, MainScope, SubScope, WbsCode, Description, ItemType, Quantity, Unit,
        UnitCostMinor, VatTreatment, VatRateBps, EntryDate, Remarks, latestAllowedDate: null);

    /// <summary>The money the target would have with a new quantity and unit cost, by the target's own VAT treatment and rate.</summary>
    internal static LineAmounts RevisedAmounts(BudgetLine target, decimal quantity, long unitCostMinor) =>
        CostLineRules.Validate(target.Division, target.MainScope, target.SubScope, target.WbsCode, target.Description, target.ItemType, quantity,
            target.Unit, unitCostMinor, target.VatTreatment, target.VatRateBps, target.EntryDate, target.Remarks, latestAllowedDate: null).Amounts;

    private void CopyTarget(BudgetLine target)
    {
        SubProjectId = target.SubProjectId;
        CopyValues(target.Division, target.MainScope, target.SubScope, target.WbsCode, target.Description, target.ItemType, target.Quantity, target.Unit,
            target.UnitCostMinor, target.VatTreatment, target.VatRateBps, target.EntryDate, target.Remarks);
    }

    private void CopyValues(int? division, string? mainScope, string? subScope, string? wbsCode, string description, ItemType itemType, decimal quantity,
        string unit, long unitCostMinor, VatTreatment vatTreatment, int vatRateBps, DateOnly entryDate, string? remarks)
    {
        Division = division;
        MainScope = mainScope;
        SubScope = subScope;
        WbsCode = wbsCode;
        Description = description;
        ItemType = itemType;
        Quantity = quantity;
        Unit = unit;
        UnitCostMinor = unitCostMinor;
        VatTreatment = vatTreatment;
        VatRateBps = vatRateBps;
        EntryDate = entryDate;
        Remarks = remarks;
    }

    private void RecordPrevious(BudgetLine target)
    {
        PreviousQuantity = target.Quantity;
        PreviousUnitCostMinor = target.UnitCostMinor;
        SetPrevious(target.CurrentAmounts());
    }

    private void SetPrevious(LineAmounts? amounts)
    {
        PreviousNetMinor = amounts?.NetMinor;
        PreviousVatMinor = amounts?.VatMinor;
        PreviousGrossMinor = amounts?.GrossMinor;
    }

    private void SetResulting(LineAmounts amounts) => (NetMinor, VatMinor, GrossMinor) = (amounts.NetMinor, amounts.VatMinor, amounts.GrossMinor);

    private void SetEffect(LineAmounts amounts) =>
        (EffectNetMinor, EffectVatMinor, EffectGrossMinor) = (amounts.NetMinor, amounts.VatMinor, amounts.GrossMinor);

    // LineAmounts refuses negative parts only in its factories; a signed difference is built directly.
    private static LineAmounts Subtract(LineAmounts left, LineAmounts right) =>
        new(checked(left.NetMinor - right.NetMinor), checked(left.VatMinor - right.VatMinor), checked(left.GrossMinor - right.GrossMinor));
}
