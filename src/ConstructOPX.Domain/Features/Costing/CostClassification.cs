using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Costing;

/// <summary>
/// Where a line sits in the work breakdown: division, main scope, sub-scope and work-breakdown code. In the desktop app these four
/// were loose text, kept consistent only by code in a form, so reports could not rely on them. Here a standard code decides its
/// own division and names, and a custom code must say which division it belongs to.
/// </summary>
public sealed record CostClassification(int? Division, string? MainScope, string? SubScope, string? WbsCode)
{
    public static CostClassification Unclassified { get; } = new(null, null, null, null);

    public bool IsUnclassified => Division is null && WbsCode is null;

    public static CostClassification Create(int? division, string? mainScope, string? subScope, string? wbsCode)
    {
        var code = Guard.OptionalText(wbsCode, "wbsCode", "the work-breakdown code", 20);
        var main = Guard.OptionalText(mainScope, "mainScope", "the main scope", 120);
        var sub = Guard.OptionalText(subScope, "subScope", "the sub-scope", 160);

        if (division is { } d && (d < WbsCatalog.FirstDivision || d > WbsCatalog.LastDivision))
            throw new ValidationFailedException("division", $"Choose a division from {WbsCatalog.FirstDivision} to {WbsCatalog.LastDivision}.");

        if (code is null && division is null && main is null && sub is null) return Unclassified;

        if (code is null)
            throw new ValidationFailedException("wbsCode", "Enter the work-breakdown code, or clear the division and scopes to leave this line unclassified.");

        var standard = WbsCatalog.FindByCode(code);
        if (standard is not null)
        {
            if (division is { } given && given != standard.Division)
                throw new ValidationFailedException("division",
                    $"Code {code} belongs to Division {standard.Division} ({standard.MainScope}), not Division {given}.");
            return new CostClassification(standard.Division, standard.MainScope, standard.SubScope, standard.Code);
        }

        if (division is null)
            throw new ValidationFailedException("division", $"Code {code} isn't a standard code. Choose the division it belongs to.");
        if (sub is null)
            throw new ValidationFailedException("subScope", $"Code {code} isn't a standard code. Enter its sub-scope name.");
        return new CostClassification(division, main ?? WbsCatalog.MainScopeOf(division.Value), sub, code);
    }
}
