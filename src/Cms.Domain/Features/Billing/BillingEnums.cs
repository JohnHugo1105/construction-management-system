namespace Cms.Domain.Features.Billing;

/// <summary>What a bill to the client is for.</summary>
public enum ClientInvoiceKind
{
    /// <summary>Work done: states the value of all work billed to date and bills the difference from the previous bill.</summary>
    Progress = 0,

    /// <summary>Gives back retention held on earlier progress bills, once the project is completed.</summary>
    RetentionRelease = 1
}

public enum ClientInvoiceStatus
{
    /// <summary>Being prepared. It has no number and nothing counts yet.</summary>
    Draft = 0,

    /// <summary>Issued to the client: numbered, locked, and counted in what the client owes.</summary>
    Issued = 1,

    /// <summary>Voided (an issued bill) or discarded (a draft), with a reason. It stays on record and never counts.</summary>
    Void = 2
}

/// <summary>How the client paid.</summary>
public enum ClientPaymentMethod
{
    BankTransfer = 0,
    Cheque = 1,
    Cash = 2,
    Other = 3
}
