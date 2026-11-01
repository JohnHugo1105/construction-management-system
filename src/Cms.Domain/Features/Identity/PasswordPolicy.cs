namespace Cms.Domain.Features.Identity;

/// <summary>
/// The retired desktop app capped passwords at 15 characters, had no minimum, and stored them as plain text. This policy
/// asks for length (not tricks): at least 10 characters, a letter and a digit, and not the username.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;

    public static IReadOnlyList<string> Check(string? password, string? username)
    {
        var problems = new List<string>();
        var value = password ?? "";
        if (value.Length < MinimumLength) problems.Add($"Use at least {MinimumLength} characters.");
        if (value.Length > MaximumLength) problems.Add($"Use at most {MaximumLength} characters.");
        if (!value.Any(char.IsLetter)) problems.Add("Include at least one letter.");
        if (!value.Any(char.IsDigit)) problems.Add("Include at least one number.");
        if (!string.IsNullOrWhiteSpace(username) && value.Contains(username.Trim(), StringComparison.OrdinalIgnoreCase))
            problems.Add("Don't include your username.");
        return problems;
    }
}
