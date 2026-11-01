using System.Globalization;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;

namespace Cms.Application.Features.Costing;

/// <summary>Checks and conversions for what a person typed, shared by the budget line and cost entry use cases.</summary>
internal static class CostingInput
{
    /// <summary>
    /// The item type named in a request. A missing or unknown name becomes a value outside the enum, so the domain reports the
    /// "Choose what this line pays for" message together with every other problem instead of the binder failing on its own.
    /// </summary>
    public static ItemType ParseItemType(string? text) => TryParse<ItemType>(text) ?? (ItemType)(-1);

    public static VatTreatment ParseVatTreatment(string? text) => TryParse<VatTreatment>(text) ?? (VatTreatment)(-1);

    /// <summary>The request id without surrounding spaces, or null when none was sent.</summary>
    public static string? RequestId(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static BudgetLineFilter ToFilter(BudgetLineListQuery query) =>
        new(query.SubProjectId, query.Division, FilterItemType(query.ItemType), CleanSearch(query.Search), query.IncludeRemoved);

    public static CostEntryFilter ToFilter(CostEntryListQuery query)
    {
        CostOrigin? origin = null;
        if (!string.IsNullOrWhiteSpace(query.Origin))
            origin = TryParse<CostOrigin>(query.Origin) ??
                throw new ValidationFailedException("origin", "Choose where the cost came from: Manual, Payroll or Inventory.");
        if (query.From is { } from && query.To is { } to && to < from)
            throw new ValidationFailedException("to", "The end date can't be before the start date.");

        return new CostEntryFilter(query.SubProjectId, query.Division, FilterItemType(query.ItemType), origin, query.From, query.To,
            CleanSearch(query.Search), query.IncludeVoided);
    }

    /// <summary>The same wording the screens use for dates in messages: 3 Dec 2026.</summary>
    public static string DateText(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A money amount in words for audit lines, with the currency code: PHP 1,250.50.</summary>
    public static string MoneyText(long minor, string currencyCode) =>
        $"{currencyCode} {(minor / 100m).ToString("N2", CultureInfo.InvariantCulture)}";

    private static ItemType? FilterItemType(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return TryParse<ItemType>(text) ??
            throw new ValidationFailedException("itemType", "Choose one of: Material, Labor, Equipment, Subcontract or Other.");
    }

    private static string? CleanSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    /// <summary>
    /// Matches one of the enum's names (any letter case). A number such as "1" or a list such as "Material,Labor" is not a name, so it is
    /// rejected rather than read as a value.
    /// </summary>
    private static T? TryParse<T>(string? text) where T : struct, Enum
    {
        var name = text?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        var match = Enum.GetNames<T>().FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : Enum.Parse<T>(match);
    }
}
