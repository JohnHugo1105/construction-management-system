using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Costing;

/// <summary>
/// The money on one budget or cost line, in minor units (centavos). The three figures always satisfy
/// <c>NetMinor + VatMinor == GrossMinor</c> exactly, whatever the rounding.
/// </summary>
/// <remarks>
/// The legacy system multiplied and divided <c>double</c> values in text boxes and displayed each figure rounded on its own,
/// so net and VAT could disagree with the total by a centavo and per-project sums drifted. Here the quantity-times-cost
/// amount is rounded once (half away from zero), then the other two figures are derived so they cannot disagree.
/// </remarks>
public readonly record struct LineAmounts(long NetMinor, long VatMinor, long GrossMinor)
{
    public const int MaximumVatRateBps = 10_000;

    public static LineAmounts Calculate(decimal quantity, long unitCostMinor, VatTreatment treatment, int vatRateBps)
    {
        if (quantity <= 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (unitCostMinor < 0) throw new ArgumentOutOfRangeException(nameof(unitCostMinor), "Unit cost can't be negative.");
        if (vatRateBps is < 0 or > MaximumVatRateBps) throw new ArgumentOutOfRangeException(nameof(vatRateBps));
        if (!Enum.IsDefined(treatment)) throw new ArgumentOutOfRangeException(nameof(treatment));

        var baseMinor = RoundToMinor(quantity * unitCostMinor);
        return FromBase(baseMinor, treatment, vatRateBps);
    }

    /// <summary>Splits an amount that is already a whole number of minor units.</summary>
    public static LineAmounts FromBase(long baseMinor, VatTreatment treatment, int vatRateBps)
    {
        if (baseMinor < 0) throw new ArgumentOutOfRangeException(nameof(baseMinor));
        switch (treatment)
        {
            case VatTreatment.Inclusive:
            {
                var net = RoundToMinor(baseMinor * 10_000m / (10_000 + vatRateBps));
                return new LineAmounts(net, baseMinor - net, baseMinor);
            }
            case VatTreatment.Exclusive:
            {
                var vat = RoundToMinor(baseMinor * (decimal)vatRateBps / 10_000m);
                return new LineAmounts(baseMinor, vat, checked(baseMinor + vat));
            }
            case VatTreatment.Exempt:
                return new LineAmounts(baseMinor, 0, baseMinor);
            default:
                throw new ArgumentOutOfRangeException(nameof(treatment));
        }
    }

    public static LineAmounts operator +(LineAmounts left, LineAmounts right) =>
        new(checked(left.NetMinor + right.NetMinor), checked(left.VatMinor + right.VatMinor), checked(left.GrossMinor + right.GrossMinor));

    public static LineAmounts Zero => new(0, 0, 0);

    internal static long RoundToMinor(decimal value) =>
        checked((long)decimal.Round(value, 0, MidpointRounding.AwayFromZero));
}
