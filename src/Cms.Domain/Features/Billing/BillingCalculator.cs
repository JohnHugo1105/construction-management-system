using Cms.Domain.Common;
using Cms.Domain.Features.Costing;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// The money on one bill to the client, in minor units. Always <c>NetMinor + VatMinor == GrossMinor</c> and
/// <c>NetReceivableMinor == GrossMinor - RetentionMinor - WithholdingMinor - VatWithholdingMinor - AdvanceRecoveryMinor</c>, to the centavo.
/// </summary>
public readonly record struct BillAmounts(long GrossMinor, long NetMinor, long VatMinor, long RetentionMinor, long WithholdingMinor, long NetReceivableMinor,
    long VatWithholdingMinor = 0, long AdvanceRecoveryMinor = 0);

/// <summary>
/// The billing arithmetic, in one place. Each figure is rounded once, half away from zero, from exact inputs (multiply first, divide
/// last), and the receivable is the difference of rounded figures, so a bill's parts always add up.
/// </summary>
public static class BillingCalculator
{
    private const int Whole = 10_000;

    public const string DeductionsExceedBillMessage =
        "The deductions are more than this bill. Check the down payment, retention and withholding rates.";

    /// <summary>
    /// A progress bill of <paramref name="grossMinor"/> (VAT included, like the contract price). VAT is split out of it at the bill's
    /// rate; retention is a share of the gross; creditable withholding and VAT withholding are shares of the amount before VAT, which the
    /// client keeps and covers with certificates; the down payment recovery is worked out by <see cref="AdvanceRecovery"/>. All of them
    /// are taken from the bill as it is, before the recovery. What the client is asked to pay now is what is left, and a bill whose
    /// deductions are more than the bill is refused.
    /// </summary>
    public static BillAmounts Progress(long grossMinor, int vatRateBps, int retentionBps, int withholdingBps, int vatWithholdingBps = 0,
        long advanceRecoveryMinor = 0)
    {
        if (grossMinor < 0) throw new ArgumentOutOfRangeException(nameof(grossMinor));
        if (vatRateBps is < 0 or > LineAmounts.MaximumVatRateBps) throw new ArgumentOutOfRangeException(nameof(vatRateBps));
        if (retentionBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(retentionBps));
        if (withholdingBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(withholdingBps));
        if (vatWithholdingBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(vatWithholdingBps));
        if (advanceRecoveryMinor < 0) throw new ArgumentOutOfRangeException(nameof(advanceRecoveryMinor));

        var split = LineAmounts.FromBase(grossMinor, VatTreatment.Inclusive, vatRateBps);
        var retention = LineAmounts.RoundToMinor(grossMinor * (decimal)retentionBps / Whole);
        var withholding = LineAmounts.RoundToMinor(split.NetMinor * (decimal)withholdingBps / Whole);
        var vatWithholding = LineAmounts.RoundToMinor(split.NetMinor * (decimal)vatWithholdingBps / Whole);
        var receivable = grossMinor - retention - withholding - vatWithholding - advanceRecoveryMinor;
        if (receivable < 0) throw new DomainRuleException(DeductionsExceedBillMessage);
        return new BillAmounts(grossMinor, split.NetMinor, split.VatMinor, retention, withholding, receivable, vatWithholding, advanceRecoveryMinor);
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

    /// <summary>
    /// The down payment: <c>round(contract price x rate / 10,000)</c>. It is a receivable only: no VAT is split out (the VAT is billed on
    /// the progress bills at the full value of the work, so it is never counted twice), and nothing is retained or withheld from it.
    /// </summary>
    /// <remarks>The VAT treatment of a down payment is the accountant's to confirm; this is the one place to change it.</remarks>
    public static BillAmounts Advance(long contractPriceMinor, int advanceBps)
    {
        if (contractPriceMinor < 0) throw new ArgumentOutOfRangeException(nameof(contractPriceMinor));
        if (advanceBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(advanceBps));
        var amount = LineAmounts.RoundToMinor(contractPriceMinor * (decimal)advanceBps / Whole);
        return new BillAmounts(amount, amount, 0, 0, 0, amount);
    }

    /// <summary>
    /// What a progress bill recovers of the down payment, worked out on the work to date so the recovery is exact when the work reaches
    /// the contract price: <c>min(down payment, round(work to date x rate / 10,000)) - recovered on earlier bills</c>, never below zero.
    /// When the work to date reaches the contract price (the last bill), everything still to recover is recovered, so a contract price
    /// changed since the down payment was billed still leaves nothing behind. No issued down payment: nothing.
    /// </summary>
    /// <param name="advanceMinor">The issued down payment (0 when none).</param>
    /// <param name="advanceBps">The rate the down payment was billed at, kept on its bill.</param>
    /// <param name="recoveredBeforeMinor">Recovered on the issued progress bills before this one.</param>
    public static long AdvanceRecovery(long cumulativeWorkMinor, long contractPriceMinor, long advanceMinor, int advanceBps, long recoveredBeforeMinor)
    {
        if (cumulativeWorkMinor < 0) throw new ArgumentOutOfRangeException(nameof(cumulativeWorkMinor));
        if (advanceBps is < 0 or > Whole) throw new ArgumentOutOfRangeException(nameof(advanceBps));
        if (advanceMinor <= 0) return 0;

        var target = cumulativeWorkMinor >= contractPriceMinor
            ? advanceMinor
            : Math.Min(advanceMinor, LineAmounts.RoundToMinor(cumulativeWorkMinor * (decimal)advanceBps / Whole));
        return Math.Max(0, target - recoveredBeforeMinor);
    }

    /// <summary><see cref="AdvanceRecovery(long, long, long, int, long)"/> against what the project's issued bills add up to now.</summary>
    public static long AdvanceRecovery(long cumulativeWorkMinor, long contractPriceMinor, BillingLedger ledger) =>
        AdvanceRecovery(cumulativeWorkMinor, contractPriceMinor, ledger.AdvanceMinor, ledger.AdvanceBps, ledger.AdvanceRecoveredMinor);

    /// <summary><paramref name="part"/> as a share of <paramref name="whole"/> in basis points (rounded once); null when there is no whole.</summary>
    public static int? ShareBps(long part, long whole) =>
        whole <= 0 ? null : (int)LineAmounts.RoundToMinor(part * (decimal)Whole / whole);
}
