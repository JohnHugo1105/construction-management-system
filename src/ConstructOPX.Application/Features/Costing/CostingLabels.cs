using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>The plain words for the choices a person sees (the screens, the reference data and the exports all use these).</summary>
public static class CostingLabels
{
    public static string ItemTypeLabel(ItemType itemType) => itemType switch
    {
        ItemType.Material => "Material",
        ItemType.Labor => "Labor",
        ItemType.Equipment => "Equipment",
        ItemType.Subcontract => "Subcontract work",
        _ => "Other"
    };

    public static string VatTreatmentLabel(VatTreatment treatment) => treatment switch
    {
        VatTreatment.Inclusive => "Unit cost includes VAT",
        VatTreatment.Exclusive => "Unit cost is before VAT",
        _ => "No VAT"
    };

    public static string VatTreatmentHint(VatTreatment treatment) => treatment switch
    {
        VatTreatment.Inclusive => "Choose this when the price on the receipt already has VAT in it. The VAT part is worked out for you.",
        VatTreatment.Exclusive => "Choose this when the price is before VAT. VAT is added on top at the company rate.",
        _ => "Choose this when no VAT is charged on the line."
    };

    public static string HealthLabel(BudgetHealth health) => health switch
    {
        BudgetHealth.OnTrack => "On track",
        BudgetHealth.NearLimit => "Near limit",
        BudgetHealth.OverBudget => "Over budget",
        BudgetHealth.Unbudgeted => "Not budgeted",
        _ => "No activity"
    };

    public static string OriginLabel(CostOrigin origin) => origin switch
    {
        CostOrigin.Payroll => "Payroll",
        CostOrigin.Inventory => "Warehouse",
        CostOrigin.Subcontract => "Subcontractor",
        _ => "Entered by a person"
    };

    public static string BasisLabel(VarianceBasis basis) => basis == VarianceBasis.ExcludingVat ? "Without VAT" : "With VAT";
}
