using System.Text;
using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Documents;

/// <summary>
/// Which files a project document may hold and how a file's name is made safe. Only these types are accepted; anything a browser could run
/// or render as a page (html, svg, js, exe and every other type) is refused. The server decides a file's content type from its extension,
/// never from what the browser says, and for pdf and the image types it also checks that the file really starts like one.
/// </summary>
public static class DocumentFiles
{
    public const int MaximumFileNameLength = 200;

    /// <summary>The default largest file, 25 MB. The deployment can set a different size.</summary>
    public const long DefaultMaximumBytes = 25L * 1024 * 1024;

    /// <summary>The bytes a type check needs from the start of a file.</summary>
    public const int LeadingBytesNeeded = 12;

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pdf"] = "application/pdf",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["webp"] = "image/webp",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["csv"] = "text/csv",
        ["txt"] = "text/plain",
        // Drawings are opened in a CAD program, never by a browser.
        ["dwg"] = "application/octet-stream",
        ["dxf"] = "application/octet-stream"
    };

    /// <summary>Every extension that may ever be accepted, in the order people read them. A deployment can narrow this list, never widen it.</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions =
        ["pdf", "jpg", "jpeg", "png", "webp", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "csv", "txt", "dwg", "dxf"];

    /// <summary>Characters Windows refuses in a name, plus the ones that split or escape a header value.</summary>
    private static readonly char[] Unsafe = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', ';', '%', '`'];

    /// <summary>The lower-case extension of a file name without the dot ("pdf"), or an empty string.</summary>
    public static string ExtensionOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot < 0 || dot == fileName.Length - 1 ? "" : fileName[(dot + 1)..].ToLowerInvariant();
    }

    public static bool IsSafeExtension(string extension) => ContentTypes.ContainsKey(extension);

    /// <summary>The content type the server stores and sends for an allowed extension.</summary>
    public static string ContentTypeFor(string extension) =>
        ContentTypes.TryGetValue(extension, out var type) ? type : throw new ArgumentException("That type of file is not allowed.", nameof(extension));

    /// <summary>
    /// The name a file is shown and downloaded under: only the last part of what the browser sent (no folders), without control characters or
    /// characters that mean something to a file system, a shell or a header, with runs of spaces made one, and at most 200 characters with the
    /// extension kept. A name with nothing left becomes "document.ext". The name never reaches a path on the server.
    /// </summary>
    public static string CleanFileName(string? original)
    {
        var name = original ?? "";
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0) name = name[(lastSeparator + 1)..];

        var builder = new StringBuilder(name.Length);
        var lastWasSpace = false;
        foreach (var character in name)
        {
            if (char.IsControl(character) || char.IsSurrogate(character) || Array.IndexOf(Unsafe, character) >= 0 ||
                char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.OtherNotAssigned)
                continue;
            var space = char.IsWhiteSpace(character);
            if (space && lastWasSpace) continue;
            builder.Append(space ? ' ' : character);
            lastWasSpace = space;
        }

        var cleaned = builder.ToString().Trim(' ');
        var extension = ExtensionOf(cleaned);
        var stem = extension.Length == 0 ? cleaned : cleaned[..^(extension.Length + 1)].Trim(' ', '.');
        if (stem.Length == 0) stem = "document";
        var suffix = extension.Length == 0 ? "" : "." + extension;
        if (suffix.Length >= MaximumFileNameLength) suffix = "";
        if (stem.Length + suffix.Length > MaximumFileNameLength) stem = stem[..(MaximumFileNameLength - suffix.Length)].TrimEnd(' ', '.');
        return stem + suffix;
    }

    /// <summary>
    /// The extension of a file a person chose, checked: allowed by the built-in list and by the deployment's list. Throws a validation error on
    /// the <c>file</c> field naming the types that are allowed.
    /// </summary>
    public static string CheckExtension(string cleanFileName, IReadOnlyCollection<string> allowedHere)
    {
        var extension = ExtensionOf(cleanFileName);
        if (!IsSafeExtension(extension) || !allowedHere.Contains(extension))
            throw new ValidationFailedException("file",
                $"This type of file can't be added. Use one of: {string.Join(", ", AllowedExtensions.Where(allowedHere.Contains))}.");
        return extension;
    }

    /// <summary>True when the first bytes match the type for pdf and the image types; other types have no fixed start and always pass.</summary>
    public static bool StartsLike(string extension, ReadOnlySpan<byte> leading) => extension switch
    {
        "pdf" => leading.StartsWith("%PDF-"u8),
        "jpg" or "jpeg" => leading.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "png" => leading.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "webp" => leading.Length >= 12 && leading.StartsWith("RIFF"u8) && leading[8..12].SequenceEqual("WEBP"u8),
        _ => true
    };

    /// <summary>"PDF", "JPEG image": the type named for a person in a refusal.</summary>
    public static string TypeWords(string extension) => extension switch
    {
        "pdf" => "PDF",
        "jpg" or "jpeg" => "JPEG image",
        "png" => "PNG image",
        "webp" => "WebP image",
        _ => extension.ToUpperInvariant() + " file"
    };
}

/// <summary>The rule for a document kept somewhere else: a full <c>https://</c> address, nothing else.</summary>
public static class DocumentLinks
{
    public const int MaximumLength = 500;

    /// <summary>The trimmed link, or a validation error on <paramref name="field"/>. Only https is accepted, and an address with a password in it is refused.</summary>
    public static string Clean(string? url, string field = "url")
    {
        var text = url?.Trim();
        if (string.IsNullOrEmpty(text)) throw new ValidationFailedException(field, "Enter the link to the document.");
        if (text.Length > MaximumLength) throw new ValidationFailedException(field, $"The link can be at most {MaximumLength} characters.");
        if (text.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            !Uri.TryCreate(text, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(uri.Host))
            throw new ValidationFailedException(field, "Enter a full link that starts with https://, for example https://drive.example.com/plans.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ValidationFailedException(field, "Use a link without a user name or password in it.");
        return text;
    }
}
