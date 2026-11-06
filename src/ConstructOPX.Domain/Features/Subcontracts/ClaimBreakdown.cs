using System.Globalization;
using ConstructOPX.Domain.Features.Costing;

namespace ConstructOPX.Domain.Features.Subcontracts;

/// <summary>
/// What one progress claim is worth and what is owed on it, in minor units. The subcontractor states the work done to date (a base amount on
/// the contract's VAT treatment); this claim is the increase over the work already certified. Each figure is rounded once, half away from
/// zero, and the rest are derived, so <c>Net + Vat == Gross</c> and <c>Gross - Retention - Withholding == NetPayable</c> always hold.
/// </summary>
/// <param name="ThisClaimBaseMinor">Cumulative minus previous: the base amount of the work this claim adds.</param>
/// <param name="RetentionMinor"><c>round(gross x retention rate)</c>: held back until the subcontract is completed. It is still project cost.</param>
/// <param name="WithholdingMinor"><c>round(net x withholding rate)</c>: tax the company withholds and remits for the subcontractor.</param>
/// <param name="NetPayableMinor"><c>gross - retention - withholding</c>: what is paid against this claim.</param>
public readonly record struct ClaimBreakdown(long CumulativeWorkMinor, long PreviousCumulativeMinor, long ThisClaimBaseMinor, long NetMinor,
    long VatMinor, long GrossMinor, long RetentionMinor, long WithholdingMinor, long NetPayableMinor)
{
    public static ClaimBreakdown Calculate(long cumulativeWorkMinor, long previousCumulativeMinor, VatTreatment vatTreatment, int vatRateBps,
        int retentionBps, int withholdingBps)
    {
        if (previousCumulativeMinor < 0) throw new ArgumentOutOfRangeException(nameof(previousCumulativeMinor));
        if (cumulativeWorkMinor <= previousCumulativeMinor) throw new ArgumentOutOfRangeException(nameof(cumulativeWorkMinor), "A claim must add work.");
        if (retentionBps is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(retentionBps));
        if (withholdingBps is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(withholdingBps));

        var thisClaim = checked(cumulativeWorkMinor - previousCumulativeMinor);
        var amounts = LineAmounts.FromBase(thisClaim, vatTreatment, vatRateBps);
        var retention = Share(amounts.GrossMinor, retentionBps);
        var withholding = Share(amounts.NetMinor, withholdingBps);
        return new ClaimBreakdown(cumulativeWorkMinor, previousCumulativeMinor, thisClaim, amounts.NetMinor, amounts.VatMinor, amounts.GrossMinor,
            retention, withholding, amounts.GrossMinor - retention - withholding);
    }

    /// <summary>A rate in basis points of an amount, multiplied first and rounded once, half away from zero.</summary>
    public static long Share(long amountMinor, int rateBps) => LineAmounts.RoundToMinor(amountMinor * (decimal)rateBps / 10_000m);

    /// <summary>"₱1,250,000.00", for messages.</summary>
    internal static string Peso(long minor) => "₱" + (minor / 100m).ToString("N2", CultureInfo.InvariantCulture);
}
