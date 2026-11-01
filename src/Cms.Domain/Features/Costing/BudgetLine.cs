using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Costing;

public enum BudgetLineStatus
{
    Active = 0,
    Removed = 1
}

/// <summary>
/// One planned cost of a project (the desktop app's "bill of materials"). The money is computed once from quantity, unit cost and
/// the VAT treatment, stored in minor units, and never re-derived by the client. A line is removed with a reason, never deleted.
/// </summary>
public sealed class BudgetLine : AggregateRoot
{
    private BudgetLine() { }

    public Guid ProjectId { get; private set; }
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

    /// <summary>The VAT rate in force when the line was entered. A later change of the company rate doesn't alter it.</summary>
    public int VatRateBps { get; private set; }

    public long NetMinor { get; private set; }
    public long VatMinor { get; private set; }
    public long GrossMinor { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string? Remarks { get; private set; }
    public BudgetLineStatus Status { get; private set; } = BudgetLineStatus.Active;
    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public string? RemovedReason { get; private set; }

    public bool IsRemoved => Status == BudgetLineStatus.Removed;

    public static BudgetLine Create(Guid id, Guid tenantId, Project projectContext, Guid? subProjectId, int? division,
        string? mainScope, string? subScope, string? wbsCode, string? description, ItemType itemType, decimal quantity, string? unit,
        long unitCostMinor, VatTreatment vatTreatment, int vatRateBps, DateOnly entryDate, string? remarks, Guid createdByUserId,
        string? clientRequestId, DateTimeOffset now)
    {
        projectContext.EnsureCanReceiveRecordsFor(subProjectId);
        var values = CostLineRules.Validate(division, mainScope, subScope, wbsCode, description, itemType, quantity, unit,
            unitCostMinor, vatTreatment, vatRateBps, entryDate, remarks, latestAllowedDate: null);

        var line = new BudgetLine
        {
            ProjectId = projectContext.Id,
            SubProjectId = subProjectId,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        line.Initialize(id, tenantId, now);
        line.Apply(values);
        return line;
    }

    public void Revise(Project projectContext, Guid? subProjectId, int? division, string? mainScope, string? subScope, string? wbsCode,
        string? description, ItemType itemType, decimal quantity, string? unit, long unitCostMinor, VatTreatment vatTreatment,
        int vatRateBps, DateOnly entryDate, string? remarks, DateTimeOffset now)
    {
        if (projectContext.Id != ProjectId) throw new ArgumentException("The project doesn't match this line.", nameof(projectContext));
        if (IsRemoved) throw new DomainRuleException("This budget line was removed. Add a new line instead.");
        projectContext.EnsureCanReceiveRecordsFor(subProjectId);
        var values = CostLineRules.Validate(division, mainScope, subScope, wbsCode, description, itemType, quantity, unit,
            unitCostMinor, vatTreatment, vatRateBps, entryDate, remarks, latestAllowedDate: null);
        SubProjectId = subProjectId;
        Apply(values);
        Touch(now);
    }

    public void Remove(Guid userId, string? reason, DateTimeOffset now)
    {
        if (IsRemoved) throw new DomainRuleException("This budget line was already removed.");
        RemovedReason = Guard.RequiredText(reason, "reason", "the reason for removing this line", 300);
        Status = BudgetLineStatus.Removed;
        RemovedAtUtc = now;
        RemovedByUserId = userId;
        Touch(now);
    }

    private void Apply(CostLineValues values)
    {
        Division = values.Classification.Division;
        MainScope = values.Classification.MainScope;
        SubScope = values.Classification.SubScope;
        WbsCode = values.Classification.WbsCode;
        Description = values.Description;
        ItemType = values.ItemType;
        Quantity = values.Quantity;
        Unit = values.Unit;
        UnitCostMinor = values.UnitCostMinor;
        VatTreatment = values.VatTreatment;
        VatRateBps = values.VatRateBps;
        NetMinor = values.Amounts.NetMinor;
        VatMinor = values.Amounts.VatMinor;
        GrossMinor = values.Amounts.GrossMinor;
        EntryDate = values.EntryDate;
        Remarks = values.Remarks;
    }
}
