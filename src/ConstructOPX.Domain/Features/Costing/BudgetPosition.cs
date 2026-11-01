namespace ConstructOPX.Domain.Features.Costing;

public enum BudgetHealth
{
    /// <summary>Nothing budgeted and nothing spent.</summary>
    NoActivity = 0,

    /// <summary>Less than 90% of the budget is used.</summary>
    OnTrack = 1,

    /// <summary>90% to 100% of the budget is used.</summary>
    NearLimit = 2,

    /// <summary>More than the budget is spent.</summary>
    OverBudget = 3,

    /// <summary>Money is spent where no budget exists (for example a work-breakdown code nobody budgeted).</summary>
    Unbudgeted = 4
}

/// <summary>
/// Budget against actual for any slice (a project, a sub-project, a division, a code). The retired desktop app never computed
/// remaining budget or a percentage: its variance report was a binary file with no formulas in code, and the old legacy join matched
/// budget to actual by typed item description. Here the definitions are explicit and tested:
/// <c>Remaining = Budget - Actual</c> (negative means over budget), <c>Used = Actual / Budget</c>.
/// </summary>
public readonly record struct BudgetPosition(long BudgetMinor, long ActualMinor)
{
    /// <summary>The share of the budget used from which a line is "near its limit".</summary>
    public const int NearLimitBps = 9_000;

    public long RemainingMinor => BudgetMinor - ActualMinor;

    /// <summary>
    /// Actual as a share of budget in basis points (10,000 is 100%); null when there is no budget to compare with. A share too large for
    /// an <c>int</c> (a tiny budget against a large cost) is shown as the largest value rather than failing the whole report.
    /// </summary>
    public int? UsedBps => BudgetMinor <= 0
        ? null
        : (int)Math.Min(decimal.Round((decimal)ActualMinor * 10_000m / BudgetMinor, 0, MidpointRounding.AwayFromZero), int.MaxValue);

    public BudgetHealth Health
    {
        get
        {
            if (BudgetMinor <= 0) return ActualMinor > 0 ? BudgetHealth.Unbudgeted : BudgetHealth.NoActivity;
            if (ActualMinor > BudgetMinor) return BudgetHealth.OverBudget;
            // Compare exact amounts, not the rounded basis points: 89.999% used is still on track.
            return (decimal)ActualMinor * 10_000m >= (decimal)BudgetMinor * NearLimitBps ? BudgetHealth.NearLimit : BudgetHealth.OnTrack;
        }
    }

    public static BudgetPosition operator +(BudgetPosition left, BudgetPosition right) =>
        new(checked(left.BudgetMinor + right.BudgetMinor), checked(left.ActualMinor + right.ActualMinor));
}
