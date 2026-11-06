using ConstructOPX.Application.Features.Capabilities;
using ConstructOPX.Application.Features.Documents;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Infrastructure.Features.Documents;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ConstructOPX.Infrastructure.Features.Capabilities;

/// <summary>
/// File storage: Available when a folder is configured and a test file can be written there, NeedsSetup when no folder is configured, Failing
/// when it can't be written to, and Unavailable when an administrator switched uploads off (<c>FileStorage:UploadsEnabled</c> false, the
/// emergency switch; files already stored still download). The write test is cached for 30 seconds so pages that ask often don't touch the
/// disk each time.
/// </summary>
internal sealed class FileStorageCapabilityCheck(IFileStore store, IConfiguration configuration, IMemoryCache cache) : ICapabilityCheck
{
    private static readonly TimeSpan ProbeLifetime = TimeSpan.FromSeconds(30);
    private const string Name = "File storage";
    private const string Fallback = "Add a link to the document instead.";

    public string Key => CapabilityKeys.FileStorage;

    public async Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
            return new CapabilityStatus(Key, Name, CapabilityState.NeedsSetup,
                "File storage is not set up yet, so files can't be uploaded. Your administrator sets the storage folder for the server.", Fallback);

        if (!FileStorageOptions.From(configuration).UploadsEnabled)
            return new CapabilityStatus(Key, Name, CapabilityState.Unavailable,
                "Uploading files is switched off for now. Files already added can still be downloaded.", Fallback);

        var writable = await cache.GetOrCreateAsync($"capability:{Key}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProbeLifetime;
            return await store.ProbeAsync(cancellationToken);
        });
        return writable
            ? new CapabilityStatus(Key, Name, CapabilityState.Available, "Files are kept on the server and offered only as downloads.", null)
            : new CapabilityStatus(Key, Name, CapabilityState.Failing,
                "File storage can't be written to right now, so files can't be uploaded. Your administrator needs to check the storage folder.",
                Fallback);
    }
}

/// <summary>Virus scanning is not built: files are stored and offered only as downloads, marked as not scanned.</summary>
internal sealed class VirusScanningCapabilityCheck : ICapabilityCheck
{
    public string Key => CapabilityKeys.VirusScanning;

    public Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CapabilityStatus(Key, "Virus scanning", CapabilityState.NeedsSetup,
            "Files are stored and offered only as downloads; they are not scanned for viruses.",
            "Open files only from people you know, and keep virus protection on the computers that open them."));
}

/// <summary>Email delivery is not built: notifications appear in the app only.</summary>
internal sealed class EmailDeliveryCapabilityCheck : ICapabilityCheck
{
    public string Key => CapabilityKeys.EmailDelivery;

    public Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CapabilityStatus(Key, "Email delivery", CapabilityState.NeedsSetup,
            "Notifications appear in the app; email is not set up.", "People see their notifications under the bell in the app."));
}

/// <summary>
/// Time clock: Available once a registered, active time clock has sent a scan, NeedsSetup before that (nobody registered one, or a registered
/// one has not connected yet). Attendance recorded by hand works either way. One statement, filtered to the company.
/// </summary>
internal sealed class TimeClockCapabilityCheck(ConstructOpxDbContext db) : ICapabilityCheck
{
    private const string Name = "Time clock";

    public string Key => CapabilityKeys.TimeClock;

    public async Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var connected = await db.Set<TimeClockDevice>().AsNoTracking()
            .AnyAsync(device => device.Status == TimeClockDeviceStatus.Active && device.LastSeenAtUtc != null, cancellationToken);
        return connected
            ? new CapabilityStatus(Key, Name, CapabilityState.Available, "Workers can scan their badge to time in and out, and a supervisor confirms each day.", null)
            : new CapabilityStatus(Key, Name, CapabilityState.NeedsSetup,
                "No time clock has connected yet, so badge scans can't come in. Register one under Administration, Time clocks, then enter its key on the machine.",
                "Record attendance by hand on the project's Team tab.");
    }
}
