using Cms.Domain.Features.Costing;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// Where the variation orders added to a government project stand against the limit: the limit (a share of the original contract
/// price), what the added work comes to, and whether it passes the limit.
/// </summary>
public readonly record struct VariationLimit(long LimitMinor, long UsedMinor, bool Exceeds);

/// <summary>
/// The rules a government client brings, in one place. The down payment is capped (enforced on the billing terms). Added variation
/// orders over a share of the original contract price need the agency's approval: the system only warns, because that approval is the
/// agency's act, not the system's.
/// </summary>
public static class GovernmentContract
{
    private const int Whole = 10_000;

    /// <summary>The largest down payment a government project allows: 15% of the contract price.</summary>
    public const int MaximumAdvanceBps = 1_500;

    /// <summary>Added variation orders past this share of the original contract price are flagged: 10%.</summary>
    public const int VariationLimitBps = 1_000;

    /// <summary>
    /// Added work against the limit. <paramref name="approvedAdditiveMinor"/> is the sum of the price increases of the approved change
    /// orders; <paramref name="pendingAdditiveMinor"/> the increase of a change order still waiting for approval (0 otherwise), so a person
    /// sees the effect before approving it.
    /// </summary>
    public static VariationLimit Variations(long baselineContractPriceMinor, long approvedAdditiveMinor, long pendingAdditiveMinor)
    {
        if (baselineContractPriceMinor < 0) throw new ArgumentOutOfRangeException(nameof(baselineContractPriceMinor));
        if (approvedAdditiveMinor < 0) throw new ArgumentOutOfRangeException(nameof(approvedAdditiveMinor));
        if (pendingAdditiveMinor < 0) throw new ArgumentOutOfRangeException(nameof(pendingAdditiveMinor));

        var limit = LineAmounts.RoundToMinor(baselineContractPriceMinor * (decimal)VariationLimitBps / Whole);
        var used = approvedAdditiveMinor + pendingAdditiveMinor;
        return new VariationLimit(limit, used, used > limit);
    }
}
