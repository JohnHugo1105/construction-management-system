using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Domain.Features.Costing;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>
/// The choices the budget line and cost entry forms offer: the standard work breakdown, the unit suggestions, what a line can pay for and
/// how VAT applies, in plain words, plus the company's current VAT rate. It never changes while the program runs, so the web client can
/// load it once per session.
/// </summary>
public sealed class CostingReferenceService(IOrganizationRepository organizations)
{
    private static readonly IReadOnlyList<WbsDivisionDto> Divisions = WbsCatalog.Divisions
        .Select(division => new WbsDivisionDto(division.Number, division.MainScope,
            WbsCatalog.ForDivision(division.Number).Select(entry => new WbsScopeDto(entry.Code, entry.SubScope)).ToArray()))
        .ToArray();

    private static readonly IReadOnlyList<ChoiceDto> ItemTypes = Enum.GetValues<ItemType>()
        .Select(itemType => new ChoiceDto(itemType.ToString(), CostingLabels.ItemTypeLabel(itemType))).ToArray();

    private static readonly IReadOnlyList<VatChoiceDto> VatTreatments = Enum.GetValues<VatTreatment>()
        .Select(treatment => new VatChoiceDto(treatment.ToString(), CostingLabels.VatTreatmentLabel(treatment), CostingLabels.VatTreatmentHint(treatment)))
        .ToArray();

    public async Task<CostingReferenceDto> GetAsync(CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        return new CostingReferenceDto(Divisions, UnitOfMeasure.Suggestions, ItemTypes, VatTreatments, organization.VatRateBps);
    }
}
