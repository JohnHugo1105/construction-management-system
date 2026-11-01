namespace ConstructOPX.Domain.Features.Subcontracts;

public enum SubcontractStatus
{
    /// <summary>Being prepared: every detail can still change. Nothing is committed yet.</summary>
    Draft = 0,

    /// <summary>Awarded: the work is committed, claims can be certified and paid.</summary>
    Active = 1,

    /// <summary>The work is finished: no more claims; retention can be released.</summary>
    Completed = 2,

    /// <summary>Called off before any work was certified.</summary>
    Cancelled = 3
}

public enum SubcontractClaimStatus
{
    /// <summary>Entered, not yet checked: it can be edited or discarded and has no effect on cost.</summary>
    Draft = 0,

    /// <summary>Certified: the work is posted as project cost and the net payable is owed.</summary>
    Certified = 1,

    /// <summary>Discarded as a draft, or voided after certification (its cost voided with it).</summary>
    Voided = 2
}

public enum SubcontractPaymentKind
{
    /// <summary>A payment against a certified claim's net payable.</summary>
    Claim = 0,

    /// <summary>Retention held on certified claims, paid out once the subcontract is completed.</summary>
    RetentionRelease = 1
}

public enum SubcontractPaymentMethod
{
    BankTransfer = 0,
    Cheque = 1,
    Cash = 2,
    Other = 3
}

public enum SubcontractPaymentStatus
{
    Recorded = 0,
    Voided = 1
}
