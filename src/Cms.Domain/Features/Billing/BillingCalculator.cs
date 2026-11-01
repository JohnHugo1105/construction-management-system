using Cms.Domain.Features.Costing;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// The money on one bill to the client, in minor units. Always <c>NetMinor + VatMinor == GrossMinor</c> and
/// <c>NetReceivableMinor == GrossMinor - RetentionMinor - WithholdingMinor</c>, to the centavo.
/// </summary>
public readonly record struct BillAmounts(long GrossMinor, long NetMinor, long VatMinor, long RetentionMinor, long WithholdingMinor, long NetReceivableMinor);

/// <summary>
/// The billing arithmetic, in one place. Each figure is rounded once, half away from zero, from exact inputs (multiply first, divide
/// last), and the receivable is the difference of rounded figures, so a bill's parts always add up.
/// </summary>
public static class BillingCalculator
{
    private const int Whole = 10_000;

    /// <summary>
    /// A progress bill of <paramref name="grossMinor"/> (VAT included, like the contract price). VAT is split out of it at the bill's
    /// rate; retention is a share of the gross; creditable withholding is a share of the amount before VAT, which the client keeps and
    /// covers with a certificate. What the client is asked to pay now is what is left.
    /// </summary>
    public static BillAmounts Progress(long grossMinor, int vatRateBps, int retentionBps, int withholdingBps)
    {
        if (grossMinor < 0) throw new ArgumentOutOfRangeException(nameof(grossMinor));
        if (vatRateBps is < 0 or > LineAmounts.MaximumVatRateBps) throw new ArgumentOutOfRangeException(nameof(vatRateBps));
        if (retentionBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(retentionBps));
        if (withholdingBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(withholdingBps));

        var split = LineAmounts.FromBase(grossMinor, VatTreatment.Inclusive, vatRateBps);
        var retention = LineAmounts.RoundToMinor(grossMinor * (decimal)retentionBps / Whole);
        var withholding = LineAmounts.RoundToMinor(split.NetMinor * (decimal)withholdingBps / Whole);
        return new BillAmounts(grossMinor, split.NetMinor, split.VatMinor, retention, withholding, grossMinor - retention - withholding);
    }

    /// <summary>
    /// A retention release: the client pays back retention held on earlier bills. The VAT was billed with the work, so none is added,
    /// and nothing is retained or withheld again.
    /// </summary>
    public static BillAmounts RetentionRelease(long amountMinor)
    {
        if (amountMinor < 0) throw new ArgumentOutOfRangeException(nameof(amountMinor));
        return new BillAmounts(amountMinor, amountMinor, 0, 0, 0, amountMinor);
    }

    /// <summary><paramref name="part"/> as a share of <paramref name="whole"/> in basis points (rounded once); null when there is no whole.</summary>
    public static int? ShareBps(long part, long whole) =>
        whole <= 0 ? null : (int)LineAmounts.RoundToMinor(part * (decimal)Whole / whole);
}
