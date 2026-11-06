namespace ConstructOPX.Domain.Features.Documents;

/// <summary>What a project document is. Contract and Insurance reveal prices, so only people who may read costs see them.</summary>
public enum DocumentCategory
{
    Contract = 0,
    Drawing = 1,
    Permit = 2,
    Insurance = 3,
    Certificate = 4,
    Photo = 5,
    Report = 6,
    Other = 7
}

public enum DocumentStatus
{
    /// <summary>In use: new versions can be added and its details changed.</summary>
    Active = 0,

    /// <summary>Added by mistake or no longer relevant. It stays on record with the reason, and its versions stay downloadable.</summary>
    Void = 1
}

/// <summary>A version is either a stored file or a link to where the document is kept.</summary>
public enum DocumentVersionKind
{
    File = 0,
    Link = 1
}

/// <summary>
/// How close an Active document with a valid-until date is to that date, judged on the company's calendar day. Worked out every time it is
/// read and never stored (the same rule as bonds).
/// </summary>
public enum DocumentExpiry
{
    Valid = 0,

    /// <summary><see cref="ProjectDocument.ExpiringSoonDays"/> days or fewer left; it is still valid on its last day.</summary>
    ExpiringSoon = 1,

    /// <summary>The valid-until date is before today.</summary>
    Expired = 2
}
