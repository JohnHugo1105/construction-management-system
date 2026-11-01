using Cms.Domain.Common;

namespace Cms.Domain.Features.Costing;

/// <summary>
/// Units are free text with suggestions, as before, but near-duplicates the desktop app kept side by side (pc and pcs, bag and
/// bags, set and sets, sht and sheet) are folded into one spelling so quantities group and report cleanly.
/// </summary>
public static class UnitOfMeasure
{
    public const int MaximumLength = 20;

    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "l.s", "m", "m²", "m³", "l.m.", "kg", "pc", "bag", "sheet", "set", "roll", "lot", "no.", "gal", "liter", "tube", "pail",
        "can", "unit", "yard", "bd.ft."
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pcs"] = "pc", ["piece"] = "pc", ["pieces"] = "pc",
        ["bags"] = "bag",
        ["sets"] = "set",
        ["sht"] = "sheet", ["sheets"] = "sheet",
        ["liters"] = "liter", ["litre"] = "liter", ["litres"] = "liter", ["l"] = "liter",
        ["m2"] = "m²", ["sqm"] = "m²", ["sq.m"] = "m²", ["sq.m."] = "m²",
        ["m3"] = "m³", ["cu.m"] = "m³", ["cu.m."] = "m³", ["cum"] = "m³",
        ["ls"] = "l.s", ["lump sum"] = "l.s", ["lumpsum"] = "l.s", ["l.s."] = "l.s",
        ["lm"] = "l.m.", ["l.m"] = "l.m.",
        ["units"] = "unit",
        ["lots"] = "lot"
    };

    public static string Normalize(string? unit, string field = "unit")
    {
        var text = Guard.RequiredText(unit, field, "the unit (for example pc, bag, m³)", MaximumLength);
        return Aliases.TryGetValue(text, out var canonical) ? canonical : text;
    }
}
