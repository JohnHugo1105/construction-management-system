namespace ConstructOPX.Domain.Features.Bonds;

/// <summary>Whose bond it is: one the company gave to a client, or one a subcontractor or supplier gave to the company.</summary>
public enum BondDirection
{
    /// <summary>The company posted it: a bank or surety guarantees the company's work to the client.</summary>
    Posted = 0,

    /// <summary>The company holds it: a subcontractor or supplier gave it to guarantee their work or an advance paid to them.</summary>
    Received = 1
}

public enum BondKind
{
    Bid = 0,
    Performance = 1,
    AdvancePayment = 2,
    Warranty = 3,
    Other = 4
}

public enum BondStatus
{
    /// <summary>In force: it can be edited, extended, released, marked as called or voided.</summary>
    Active = 0,

    /// <summary>Returned or cancelled because it is no longer needed. Final.</summary>
    Released = 1,

    /// <summary>The beneficiary claimed against it. Final.</summary>
    Called = 2,

    /// <summary>Entered by mistake. It stays on record with the reason and counts in nothing.</summary>
    Void = 3
}

/// <summary>
/// How close an Active bond is to its valid-until date, judged on the company's calendar day. It is worked out every time it is read and never
/// stored, so an expired bond is never changed behind anyone's back.
/// </summary>
public enum BondExpiry
{
    Valid = 0,

    /// <summary>The bond lapses in <see cref="Bond.ExpiringSoonDays"/> days or fewer (today counts: it is still in force on its last day).</summary>
    ExpiringSoon = 1,

    /// <summary>The valid-until date is before today and nobody has released or voided the bond.</summary>
    Expired = 2
}

/// <summary>What happened to a bond, for its history.</summary>
public enum BondEventKind
{
    Recorded = 0,
    Edited = 1,
    Extended = 2,
    Released = 3,
    Called = 4,
    Voided = 5
}
