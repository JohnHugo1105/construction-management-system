using System.Security.Cryptography;
using ConstructOPX.Application.Common;
using Microsoft.Extensions.Options;

namespace ConstructOPX.Infrastructure.Security;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>PBKDF2-SHA256 iterations. 600,000 is the current OWASP minimum; tests lower it for speed.</summary>
    public int Iterations { get; set; } = 600_000;
}

/// <summary>
/// PBKDF2-SHA256 with a random 16-byte salt per password, stored as <c>PBKDF2-SHA256$iterations$salt$hash</c> so the cost can be raised
/// later without invalidating old hashes. Comparison is constant-time.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int MinimumIterations = 1_000;

    // Verified against when the account doesn't exist, so an unknown username costs the same as a wrong password.
    private readonly Lazy<string> _dummy = new(() => Create("this-account-does-not-exist", options.Value.Iterations));

    public string Hash(string password) => Create(password, options.Value.Iterations);

    public bool Verify(string? passwordHash, string password)
    {
        var known = passwordHash is not null;
        var parts = (passwordHash ?? _dummy.Value).Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out var iterations) || iterations < MinimumIterations)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected) && known;
    }

    private static string Create(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Math.Max(iterations, MinimumIterations), HashAlgorithmName.SHA256, HashBytes);
        return $"{Algorithm}${Math.Max(iterations, MinimumIterations)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
