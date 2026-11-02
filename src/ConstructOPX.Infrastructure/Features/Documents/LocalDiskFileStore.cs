using System.Buffers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Domain.Features.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConstructOPX.Infrastructure.Features.Documents;

/// <summary>
/// The file storage settings, read from configuration section <c>FileStorage</c>:
/// <list type="bullet">
/// <item><c>RootPath</c>: the folder files are kept in (a persistent, backed-up volume). Empty means file storage is not set up.</item>
/// <item><c>MaxFileMegabytes</c>: the largest file, 1 to 1,024; default 25.</item>
/// <item><c>AllowedExtensions</c>: optional, comma-separated; narrows the built-in list (anything not on it is ignored).</item>
/// <item><c>UploadsEnabled</c>: the emergency switch, default true. False stops new uploads (the capability reports Unavailable and people
/// add links instead); files already stored still download.</item>
/// </list>
/// </summary>
public sealed record FileStorageOptions(string? RootPath, long MaximumBytes, IReadOnlyList<string> AllowedExtensions, bool UploadsEnabled = true)
{
    public const string SectionName = "FileStorage";

    /// <summary>Room for the form fields and multipart framing around the file, on top of the file size limit.</summary>
    public const long RequestOverheadBytes = 1024 * 1024;

    public static FileStorageOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var root = section["RootPath"];
        var megabytes = int.TryParse(section["MaxFileMegabytes"], out var parsed) && parsed is >= 1 and <= 1024 ? parsed : 0;
        var maximum = megabytes == 0 ? DocumentFiles.DefaultMaximumBytes : megabytes * 1024L * 1024L;
        var narrowed = (section["AllowedExtensions"] ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.TrimStart('.').ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var allowed = narrowed.Count == 0
            ? DocumentFiles.AllowedExtensions
            : DocumentFiles.AllowedExtensions.Where(narrowed.Contains).ToArray();
        var uploadsEnabled = !bool.TryParse(section["UploadsEnabled"], out var enabled) || enabled;
        return new FileStorageOptions(string.IsNullOrWhiteSpace(root) ? null : root.Trim(), maximum, allowed, uploadsEnabled);
    }

    /// <summary>The request-size limit for an upload route.</summary>
    public long RequestLimitBytes => MaximumBytes + RequestOverheadBytes;
}

/// <summary>
/// Keeps document files in a folder on the server's disk (<c>FileStorage:RootPath</c>, a mounted volume in production). A file's path is
/// built only from its key, which the server makes from two ids and a random name and which is checked against that shape before any path
/// is built, and the full path must stay inside the root: no file name a person typed ever reaches the disk. A save writes to a temporary
/// file, counting the bytes and working out the SHA-256 as they arrive, and moves it into place only when it is complete and within the
/// size limit; anything else removes the temporary file.
/// </summary>
internal sealed partial class LocalDiskFileStore(IConfiguration configuration, ILogger<LocalDiskFileStore> logger) : IFileStore
{
    private const int BufferSize = 81_920;
    private const string PartialSuffix = ".partial";

    private readonly FileStorageOptions _options = FileStorageOptions.From(configuration);

    public FileStoreLimits Limits => new(_options.MaximumBytes, _options.AllowedExtensions);

    public bool IsConfigured => _options.RootPath is not null;

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        if (_options.RootPath is null) return false;
        var probe = Path.Combine(Root(), $".probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Root());
            await File.WriteAllBytesAsync(probe, "ok"u8.ToArray(), cancellationToken);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "File storage can't be written to");
            TryDelete(probe);
            return false;
        }
    }

    public async Task<StoredFile?> SaveAsync(string key, Stream content, long maximumBytes, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        var partial = path + PartialSuffix;
        var moved = false;
        byte[]? buffer = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var leading = new byte[DocumentFiles.LeadingBytesNeeded];
            var leadingCount = 0;
            long total = 0;
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
                {
                    total += read;
                    // Stop at the first byte over the limit: the rest is never read or written.
                    if (total > maximumBytes) break;
                    if (leadingCount < leading.Length)
                    {
                        var take = Math.Min(read, leading.Length - leadingCount);
                        Array.Copy(buffer, 0, leading, leadingCount, take);
                        leadingCount += take;
                    }
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }

            if (total > maximumBytes) return null;
            File.Move(partial, path);
            moved = true;
            return new StoredFile(total, Convert.ToHexStringLower(hash.GetHashAndReset()), leading[..leadingCount]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogError(ex, "A document file couldn't be saved to file storage");
            throw new FileStoreException("The file couldn't be written to file storage.", ex);
        }
        finally
        {
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer);
            if (!moved) TryDelete(partial);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        try
        {
            if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
            return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogError(ex, "A document file couldn't be read from file storage");
            throw new FileStoreException("The file couldn't be read from file storage.", ex);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (_options.RootPath is not null && KeyShape().IsMatch(key)) TryDelete(PathFor(key));
        return Task.CompletedTask;
    }

    /// <summary>The full path of a key, refused unless the key has the server's own shape and the path stays inside the root.</summary>
    private string PathFor(string key)
    {
        if (_options.RootPath is null) throw new FileStoreException("File storage is not set up.");
        if (!KeyShape().IsMatch(key)) throw new ArgumentException("That is not a file store key.", nameof(key));
        var root = Root();
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("That key points outside file storage.", nameof(key));
        return path;
    }

    private string Root() => Path.TrimEndingDirectorySeparator(Path.GetFullPath(_options.RootPath!));

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "A temporary or unused document file couldn't be removed from file storage");
        }
    }

    /// <summary>Company id / project id / 32 hex characters: the only shape <see cref="FileStoreKeys.New"/> makes.</summary>
    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{32}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeyShape();
}
