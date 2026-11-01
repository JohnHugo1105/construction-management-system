using Cms.Domain.Features.Costing;

namespace Cms.Domain.Features.Progress;

/// <summary>
/// How much of the planned time has passed on the as-of date, kept as a fraction of days so the planned value is rounded once, from the
/// exact share, and never from a share that was already rounded for display.
/// </summary>
public readonly record struct PlannedProgress(int ElapsedDays, int TotalDays)
{
    /// <summary>
    /// The linear share of time from the start date to the target end date that has passed on <paramref name="asOf"/>: nothing before the
    /// start, all of it on or after the end. Null when the project has no target end date, so there is no schedule to measure against.
    /// </summary>
    public static PlannedProgress? From(DateOnly start, DateOnly? targetEnd, DateOnly asOf)
    {
        if (targetEnd is not { } end) return null;
        var total = end.DayNumber - start.DayNumber;
        if (total <= 0) return asOf >= end ? new PlannedProgress(1, 1) : new PlannedProgress(0, 1);
        var elapsed = Math.Clamp(asOf.DayNumber - start.DayNumber, 0, total);
        return new PlannedProgress(elapsed, total);
    }

    /// <summary>The share in basis points, rounded half away from zero, for display.</summary>
    public int Bps => (int)decimal.Round(ElapsedDays * 10_000m / TotalDays, 0, MidpointRounding.AwayFromZero);
}

/// <summary>Everything earned value says about one project on one date. Money is in minor units, indices in basis points (10,000 is 1.00).</summary>
/// <param name="PlannedMinor">Null when the project has no target end date.</param>
/// <param name="ScheduleVarianceMinor">Null without a planned value.</param>
/// <param name="CostIndexBps">Earned value per unit spent; null when nothing is spent.</param>
/// <param name="ScheduleIndexBps">Earned value per unit planned; null without a planned value, or when none is planned yet.</param>
/// <param name="PercentCompleteBps">Earned value as a share of the budget; null when there is no budget.</param>
/// <param name="PlannedPercentBps">The share of the planned time that has passed; null without a target end date.</param>
public sealed record EarnedValueFigures(long BudgetAtCompletionMinor, long EarnedMinor, long ActualMinor, long? PlannedMinor, long CostVarianceMinor,
    long? ScheduleVarianceMinor, int? CostIndexBps, int? ScheduleIndexBps, long EstimateAtCompletionMinor, long EstimateToCompleteMinor,
    long VarianceAtCompletionMinor, int? PercentCompleteBps, int? PlannedPercentBps);

/// <summary>
/// The earned-value formulas, in one place. The retired desktop app had no progress at all, so cost was only ever compared with the budget
/// ("how much have we spent") and never with the work done ("what did the money buy"). All amounts are whole minor units; every division is
/// done last and rounded once, half away from zero, so a figure never depends on an earlier rounded figure.
/// <list type="bullet">
/// <item>EV of a line = budget x percent complete.</item>
/// <item>PV = BAC x the share of the planned time that has passed.</item>
/// <item>CV = EV - AC. SV = EV - PV. CPI = EV / AC. SPI = EV / PV.</item>
/// <item>EAC = BAC while nothing is earned or spent, otherwise BAC x AC / EV (the cost index method: the rest of the work costs what the work so far did).</item>
/// <item>ETC = the rest of EAC (never negative). VAC = BAC - EAC.</item>
/// </list>
/// </summary>
public static class EarnedValueCalculator
{
    /// <summary>
    /// The largest forecast shown: 10^18 centavos. A project that has spent a lot and earned almost nothing would otherwise forecast a figure
    /// beyond what a whole number of centavos can hold, so the forecast is held at this ceiling instead of failing the report.
    /// </summary>
    public const long MaximumForecastMinor = 1_000_000_000_000_000_000L;

    /// <summary>The value of the work done on one line: its budget times its percent complete, rounded once to a whole centavo.</summary>
    public static long LineEarnedValue(long budgetMinor, int percentCompleteBps)
    {
        if (budgetMinor < 0) throw new ArgumentOutOfRangeException(nameof(budgetMinor));
        if (percentCompleteBps is < 0 or > ProgressEntry.FullBps) throw new ArgumentOutOfRangeException(nameof(percentCompleteBps));
        return LineAmounts.RoundToMinor(budgetMinor * (decimal)percentCompleteBps / ProgressEntry.FullBps);
    }

    public static EarnedValueFigures Calculate(long budgetAtCompletionMinor, long earnedMinor, long actualMinor, PlannedProgress? planned)
    {
        if (budgetAtCompletionMinor < 0) throw new ArgumentOutOfRangeException(nameof(budgetAtCompletionMinor));
        if (earnedMinor < 0 || earnedMinor > budgetAtCompletionMinor) throw new ArgumentOutOfRangeException(nameof(earnedMinor));
        if (actualMinor < 0) throw new ArgumentOutOfRangeException(nameof(actualMinor));

        long? plannedMinor = planned is { } share
            ? MultiplyDivide(budgetAtCompletionMinor, share.ElapsedDays, share.TotalDays)
            : null;

        var estimateAtCompletion = earnedMinor == 0 || actualMinor == 0
            ? budgetAtCompletionMinor
            : MultiplyDivide(budgetAtCompletionMinor, actualMinor, earnedMinor);

        return new EarnedValueFigures(
            budgetAtCompletionMinor, earnedMinor, actualMinor, plannedMinor,
            CostVarianceMinor: earnedMinor - actualMinor,
            ScheduleVarianceMinor: plannedMinor is { } pv ? earnedMinor - pv : null,
            CostIndexBps: actualMinor == 0 ? null : Index(earnedMinor, actualMinor),
            ScheduleIndexBps: plannedMinor is > 0 ? Index(earnedMinor, plannedMinor.Value) : null,
            EstimateAtCompletionMinor: estimateAtCompletion,
            EstimateToCompleteMinor: Math.Max(0, estimateAtCompletion - actualMinor),
            VarianceAtCompletionMinor: budgetAtCompletionMinor - estimateAtCompletion,
            PercentCompleteBps: budgetAtCompletionMinor == 0 ? null : Index(earnedMinor, budgetAtCompletionMinor),
            PlannedPercentBps: planned?.Bps);
    }

    /// <summary>
    /// What the company expects to keep: the contract price (with VAT) minus the forecast cost, both with VAT. Null when there is no price.
    /// Callers pass the forecast worked out on VAT-inclusive amounts, whatever basis the budget is compared on.
    /// </summary>
    public static long? ForecastMarginMinor(long? contractPriceMinor, long estimateAtCompletionGrossMinor) =>
        contractPriceMinor is { } price ? price - estimateAtCompletionGrossMinor : null;

    /// <summary>A ratio of two amounts in basis points, rounded half away from zero and held to the largest <c>int</c>.</summary>
    private static int Index(long numerator, long denominator) =>
        (int)Math.Min(decimal.Round(numerator * 10_000m / denominator, 0, MidpointRounding.AwayFromZero), int.MaxValue);

    /// <summary>
    /// <c>round(a x b / c)</c> half away from zero for non-negative values. The product of two amounts can exceed a <c>decimal</c> but never an
    /// <see cref="Int128"/>, and the result is held to the forecast ceiling.
    /// </summary>
    private static long MultiplyDivide(long a, long b, long c)
    {
        var product = (Int128)a * b;
        var (quotient, remainder) = Int128.DivRem(product, c);
        if (remainder * 2 >= c) quotient++;
        return quotient > MaximumForecastMinor ? MaximumForecastMinor : (long)quotient;
    }
}
