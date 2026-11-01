namespace Cms.Domain.Common;

/// <summary>Input checks shared by the domain. Messages are end-user wording and each names the field it belongs to.</summary>
public static class Guard
{
    public static string RequiredText(string? value, string field, string label, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new ValidationFailedException(field, $"Enter {label}.");
        if (text.Length > maxLength)
            throw new ValidationFailedException(field, $"{Capitalize(label)} can be at most {maxLength} characters.");
        return text;
    }

    public static string? OptionalText(string? value, string field, string label, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > maxLength)
            throw new ValidationFailedException(field, $"{Capitalize(label)} can be at most {maxLength} characters.");
        return text;
    }

    public static decimal PositiveQuantity(decimal value, string field, string label = "the quantity")
    {
        if (value <= 0m) throw new ValidationFailedException(field, $"Enter {label} greater than zero.");
        if (decimal.Round(value, 4) != value) throw new ValidationFailedException(field, $"Use at most 4 decimal places for {label}.");
        if (value > 999_999_999m) throw new ValidationFailedException(field, $"Enter {label} below 1,000,000,000.");
        return value;
    }

    public static long NonNegativeMinor(long value, string field, string label)
    {
        if (value < 0) throw new ValidationFailedException(field, $"{Capitalize(label)} can't be negative.");
        return value;
    }

    public static Guid RequiredId(Guid value, string field, string label)
    {
        if (value == Guid.Empty) throw new ValidationFailedException(field, $"Choose {label}.");
        return value;
    }

    public static string Normalize(string text) => text.Trim().ToUpperInvariant();

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
