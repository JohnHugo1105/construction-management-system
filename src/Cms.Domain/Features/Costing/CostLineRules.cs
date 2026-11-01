using Cms.Domain.Common;

namespace Cms.Domain.Features.Costing;

internal sealed record CostLineValues(
    CostClassification Classification,
    string Description,
    ItemType ItemType,
    decimal Quantity,
    string Unit,
    long UnitCostMinor,
    VatTreatment VatTreatment,
    int VatRateBps,
    LineAmounts Amounts,
    DateOnly EntryDate,
    string? Remarks);

/// <summary>
/// The checks a budget line and a cost entry share. Every problem is collected and reported together so a person fixes the whole
/// form in one go. The desktop app only listed empty fields and then let the person save anyway ("not recommended").
/// </summary>
internal static class CostLineRules
{
    private static readonly DateOnly EarliestDate = new(2000, 1, 1);

    public static CostLineValues Validate(int? division, string? mainScope, string? subScope, string? wbsCode, string? description,
        ItemType itemType, decimal quantity, string? unit, long unitCostMinor, VatTreatment vatTreatment, int vatRateBps,
        DateOnly entryDate, string? remarks, DateOnly? latestAllowedDate)
    {
        var errors = new ValidationErrors();
        var classification = CostClassification.Unclassified;
        var cleanDescription = "";
        var cleanUnit = "";
        string? cleanRemarks = null;

        Try(errors, () => classification = CostClassification.Create(division, mainScope, subScope, wbsCode));
        Try(errors, () => cleanDescription = Guard.RequiredText(description, "description", "a description of the item", 200));
        Try(errors, () => cleanUnit = UnitOfMeasure.Normalize(unit));
        Try(errors, () => cleanRemarks = Guard.OptionalText(remarks, "remarks", "the remarks", 500));
        Try(errors, () => Guard.PositiveQuantity(quantity, "quantity"));
        if (!Enum.IsDefined(itemType)) errors.Add("itemType", "Choose what this line pays for.");
        if (unitCostMinor < 0) errors.Add("unitCostMinor", "The unit cost can't be negative.");
        if (!Enum.IsDefined(vatTreatment)) errors.Add("vatTreatment", "Choose whether the unit cost includes VAT.");
        if (vatRateBps is < 0 or > LineAmounts.MaximumVatRateBps) errors.Add("vatRateBps", "Enter a VAT rate between 0% and 100%.");
        if (entryDate < EarliestDate) errors.Add("entryDate", "Choose a valid date.");
        else if (latestAllowedDate is { } latest && entryDate > latest) errors.Add("entryDate", "The date can't be in the future.");
        errors.ThrowIfAny();

        LineAmounts amounts;
        try { amounts = LineAmounts.Calculate(quantity, unitCostMinor, vatTreatment, vatRateBps); }
        catch (OverflowException) { throw new ValidationFailedException("unitCostMinor", "That amount is too large. Check the quantity and unit cost."); }

        return new CostLineValues(classification, cleanDescription, itemType, quantity, cleanUnit, unitCostMinor, vatTreatment,
            vatRateBps, amounts, entryDate, cleanRemarks);
    }

    private static void Try(ValidationErrors errors, Action check)
    {
        try { check(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
        }
    }
}
