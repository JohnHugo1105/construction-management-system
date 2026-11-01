namespace Cms.Domain.Features.ChangeOrders;

public enum ChangeOrderStatus
{
    /// <summary>Being written: header and lines can change.</summary>
    Draft = 0,

    /// <summary>Numbered and waiting for a decision. Its effect is "pending exposure".</summary>
    Submitted = 1,

    /// <summary>Applied to the budget and the contract price.</summary>
    Approved = 2,

    /// <summary>Turned down with a reason. Final.</summary>
    Rejected = 3,

    /// <summary>Discarded before a decision, or an approved one whose changes were reversed. Final.</summary>
    Voided = 4
}

public enum ChangeOrderReason
{
    ClientRequest = 0,
    DesignChange = 1,
    SiteCondition = 2,
    ErrorOrOmission = 3,
    Other = 4
}

public enum ChangeOrderLineKind
{
    /// <summary>A new budget line.</summary>
    Add = 0,

    /// <summary>An existing active budget line gets a new quantity and unit cost; everything else about it stays.</summary>
    Revise = 1,

    /// <summary>An existing active budget line is removed.</summary>
    Omit = 2
}
