using System.Buffers.Text;
using System.Security.Cryptography;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>
/// Device keys: 256 random bits written as 43 URL-safe base64 characters. Only the SHA-256 hash of the key's bytes is stored. Hashing a key that
/// was guessed or mistyped gives a hash no device has, so it is refused like any other.
/// </summary>
public static class DeviceKeys
{
    public const int KeyBytes = 32;
    public const string HeaderName = "X-Device-Key";

    /// <summary>The longest header value that is even read; anything longer is refused unread.</summary>
    private const int MaximumKeyText = 64;

    public static string NewKey() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(KeyBytes));

    /// <summary>The stored hash of a key, or null when the text can't be a key (wrong length or not base64url).</summary>
    public static byte[]? HashOf(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaximumKeyText) return null;
        var text = key.Trim();
        // Only the URL-safe base64 alphabet is read; the decoder throws on anything else, and a refusal must never be an error.
        if (!text.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) return null;
        Span<byte> bytes = stackalloc byte[MaximumKeyText];
        int written;
        try
        {
            if (!Base64Url.TryDecodeFromChars(text, bytes, out written)) return null;
        }
        catch (FormatException)
        {
            // A length base64url can't have (one character past a multiple of four).
            return null;
        }
        return written == KeyBytes ? SHA256.HashData(bytes[..written]) : null;
    }

    /// <summary>Compares two hashes in constant time.</summary>
    public static bool Matches(byte[] expected, byte[] actual) =>
        expected.Length == TimeClockDevice.KeyHashLength && CryptographicOperations.FixedTimeEquals(expected, actual);
}

/// <summary>New badge codes from a cryptographic random source.</summary>
public static class PunchCodes
{
    public static string New() => PunchCode.Generate(RandomNumberGenerator.GetInt32);
}
