namespace ConstructOPX.Application.Features.Documents;

/// <summary>
/// Where document files are kept. A file is saved under a key the server makes (<see cref="FileStoreKeys.New"/>), never under a name a person
/// typed, so no file name ever reaches a path. The adapter streams: it counts the bytes and works out the SHA-256 while it writes, stops as
/// soon as the size limit is passed, and leaves nothing behind when a save does not finish.
/// </summary>
public interface IFileStore
{
    /// <summary>The size and type limits the deployment set (defaults: 25 MB and the full list of allowed types).</summary>
    FileStoreLimits Limits { get; }

    /// <summary>True when a place to keep files is configured. Whether it can be written to is the capability check's question.</summary>
    bool IsConfigured { get; }

    /// <summary>Writes and removes a small test file. Never throws: a failure is reported as false.</summary>
    Task<bool> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Streams <paramref name="content"/> into the store under <paramref name="key"/>. Returns null, and leaves no file, when the content is
    /// larger than <paramref name="maximumBytes"/>. Any other failure throws <see cref="FileStoreException"/> and leaves no file.
    /// </summary>
    Task<StoredFile?> SaveAsync(string key, Stream content, long maximumBytes, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading, or null when it is not there. Throws <see cref="FileStoreException"/> when the store can't be read.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes a stored file if it is there (used to undo a save whose record was not written). Never throws.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <param name="MaximumBytes">The largest file accepted.</param>
/// <param name="AllowedExtensions">Lower-case, without the dot; always a subset of the built-in list.</param>
public sealed record FileStoreLimits(long MaximumBytes, IReadOnlyList<string> AllowedExtensions);

/// <summary>What the store measured while it wrote the file.</summary>
/// <param name="Sha256">Lower-case hex.</param>
/// <param name="LeadingBytes">The first bytes (up to 12), for the type check.</param>
public sealed record StoredFile(long SizeBytes, string Sha256, byte[] LeadingBytes);

/// <summary>The file store could not be written to or read from. The message is for the log, never for a person.</summary>
public sealed class FileStoreException(string message, Exception? inner = null) : Exception(message, inner);

public static class FileStoreKeys
{
    /// <summary>"&lt;tenantId&gt;/&lt;projectId&gt;/&lt;random&gt;": the company, the project and a random name. Nothing a person typed.</summary>
    public static string New(Guid tenantId, Guid projectId) => $"{tenantId:D}/{projectId:D}/{Guid.NewGuid():N}";
}
