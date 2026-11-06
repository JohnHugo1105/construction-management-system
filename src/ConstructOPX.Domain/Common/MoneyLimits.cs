namespace ConstructOPX.Domain.Common;

/// <summary>
/// The largest amounts the system accepts. Money is stored as <c>long</c> minor units, and reports add up tens of thousands of lines;
/// bounding every input keeps any realistic total far inside the range of a <c>long</c> (a bound of 10^13 times 10^5 lines is still
/// under 10^19), so a typing mistake is refused with a message instead of overflowing a sum later.
/// </summary>
public static class MoneyLimits
{
    /// <summary>Largest amount on one line, one contract price or one deduction: 10^13 centavos, which is ₱100,000,000,000.00.</summary>
    public const long MaximumAmountMinor = 10_000_000_000_000L;

    /// <summary>Largest pay rate, per day or per hour: ₱10,000,000.00.</summary>
    public const long MaximumRateMinor = 1_000_000_000L;

    public static void CheckAmount(ValidationErrors errors, long valueMinor, string field, string label)
    {
        if (valueMinor > MaximumAmountMinor) errors.Add(field, $"{Capitalize(label)} can be at most ₱100,000,000,000.00.");
    }

    public static long Amount(long valueMinor, string field, string label)
    {
        if (valueMinor > MaximumAmountMinor)
            throw new ValidationFailedException(field, $"{Capitalize(label)} can be at most ₱100,000,000,000.00.");
        return valueMinor;
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
