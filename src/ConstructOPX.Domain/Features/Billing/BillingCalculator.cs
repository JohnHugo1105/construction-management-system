using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;

namespace ConstructOPX.Domain.Features.Billing;

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
    /// What a progress bill recovers of the down payment, in proportion to the work against the project's contract price as it is now:
    /// the recovery to date is <c>round(down payment x work to date / contract price)</c> (multiplied first, rounded once), never more
    /// than the down payment, so it is exactly the down payment when the work reaches the price, whatever change orders did to the price
    /// since the down payment was billed. The bill recovers that less what earlier issued progress bills recovered, never below zero.
    /// No issued down payment: nothing.
    /// </summary>
    /// <param name="advanceMinor">The issued down payment (0 when none).</param>
    /// <param name="recoveredBeforeMinor">Recovered on the issued progress bills before this one.</param>
    public static long AdvanceRecovery(long cumulativeWorkMinor, long contractPriceMinor, long advanceMinor, long recoveredBeforeMinor)
    {
        if (cumulativeWorkMinor < 0) throw new ArgumentOutOfRangeException(nameof(cumulativeWorkMinor));
        if (contractPriceMinor < 0) throw new ArgumentOutOfRangeException(nameof(contractPriceMinor));
        if (recoveredBeforeMinor < 0) throw new ArgumentOutOfRangeException(nameof(recoveredBeforeMinor));
        if (advanceMinor <= 0) return 0;

        // Work at or past the price is the whole down payment (this is the cap, and it keeps a price of zero from dividing).
        var toDate = cumulativeWorkMinor >= contractPriceMinor
            ? advanceMinor
            : Math.Min(advanceMinor, LineAmounts.RoundToMinor(advanceMinor * (decimal)cumulativeWorkMinor / contractPriceMinor));
        return Math.Max(0, toDate - recoveredBeforeMinor);
    }

    /// <summary><see cref="AdvanceRecovery(long, long, long, long)"/> against what the project's issued bills add up to now.</summary>
    public static long AdvanceRecovery(long cumulativeWorkMinor, long contractPriceMinor, BillingLedger ledger) =>
        AdvanceRecovery(cumulativeWorkMinor, contractPriceMinor, ledger.AdvanceMinor, ledger.AdvanceRecoveredMinor);

    /// <summary><paramref name="part"/> as a share of <paramref name="whole"/> in basis points (rounded once); null when there is no whole.</summary>
    public static int? ShareBps(long part, long whole) =>
        whole <= 0 ? null : (int)LineAmounts.RoundToMinor(part * (decimal)Whole / whole);
}
