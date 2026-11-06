namespace ConstructOPX.Domain.Features.TimeClock;

/// <summary>
/// The code on a worker's badge: 12 characters from an alphabet without look-alikes (no 0 or O, no 1, I or L), random and unique within the
/// company. It identifies the worker to a time clock and nothing else: it is never derived from the employee number or anything about pay.
/// </summary>
public static class PunchCode
{
    public const int Length = 12;

    /// <summary>Digits 2 to 9 and the letters A to Z without I, L and O: 31 characters, so a code has about 59 bits of randomness.</summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>The longest text a scan or typed code may be before it is refused unread.</summary>
    public const int MaximumInputLength = 64;

    /// <summary>
    /// A new code from <paramref name="nextIndex"/>, which returns a uniformly random number from 0 up to (not including) the value it is given.
    /// The caller supplies a cryptographic source.
    /// </summary>
    public static string Generate(Func<int, int> nextIndex)
    {
        var characters = new char[Length];
        for (var index = 0; index < Length; index++) characters[index] = Alphabet[nextIndex(Alphabet.Length)];
        return new string(characters);
    }

    /// <summary>
    /// The code as stored, from what a scanner or a person typed: upper case, without spaces or dashes (a badge prints it in groups of four). Null
    /// when the text cannot be a code at all (wrong length or a character outside the alphabet), so it matches nothing.
    /// </summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputLength) return null;
        Span<char> buffer = stackalloc char[Length];
        var count = 0;
        foreach (var raw in text)
        {
            if (raw is ' ' or '-' or '\t' or '\r' or '\n') continue;
            var character = char.ToUpperInvariant(raw);
            if (count == Length || !Alphabet.Contains(character)) return null;
            buffer[count++] = character;
        }

        return count == Length ? new string(buffer) : null;
    }

    /// <summary>"ABCD-EFGH-JKMN": how the code is printed under the barcode for a person to read and type.</summary>
    public static string Grouped(string code) =>
        code.Length == Length ? $"{code[..4]}-{code[4..8]}-{code[8..]}" : code;
}
