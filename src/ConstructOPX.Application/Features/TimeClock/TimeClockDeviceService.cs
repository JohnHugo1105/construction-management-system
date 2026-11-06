using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>
/// Registering, rotating and revoking time clocks (Administration). A device's key is shown once, in the answer that created it, and only its hash
/// is kept. Every change is audited without the key.
/// </summary>
public sealed class TimeClockDeviceService(ITimeClockDeviceRepository devices, ITimeClockReader reader, IProjectRepository projects,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const string AvailableMessage = "Time clock: Available. Scans come in from your registered machines; confirm each day on the review page.";
    public const string NeedsSetupMessage =
        "Time clock: Needs setup (no device has connected yet). Register a time clock below and enter its key on the machine. Until then, record attendance on the project's Team tab.";

    public async Task<TimeClockDeviceListDto> ListAsync(CancellationToken cancellationToken)
    {
        var items = await reader.DevicesAsync(cancellationToken);
        var connected = items.Any(item => item.Status == nameof(TimeClockDeviceStatus.Active) && item.LastSeenAtUtc is not null);
        return new TimeClockDeviceListDto(items, connected
            ? new TimeClockCapabilityDto("Available", AvailableMessage)
            : new TimeClockCapabilityDto("NeedsSetup", NeedsSetupMessage));
    }

    public async Task<DeviceKeyDto> RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        // The key is shown only once, so a repeated request can't answer with it again: it says what happened instead.
        if (requestId is not null && await devices.ClientRequestUsedAsync(requestId, cancellationToken))
            throw new DomainRuleException("This time clock was already registered. To see a key again, rotate the key in the list.");

        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new NotFoundException("that project");
        var kind = ParseKind(request.Kind);
        var key = DeviceKeys.NewKey();
        var device = TimeClockDevice.Register(Guid.NewGuid(), currentUser.TenantId, request.Name, project, kind, DeviceKeys.HashOf(key)!,
            currentUser.UserId, requestId, clock.UtcNow);

        devices.Add(device);
        audit.Record("TimeClock.DeviceRegistered", nameof(TimeClockDevice), device.Id, $"Registered the time clock {device.Name} for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeviceKeyDto(await ReadAsync(device.Id, cancellationToken), key);
    }

    public async Task<DeviceKeyDto> RotateKeyAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var device = await LoadAsync(id, request.Version, cancellationToken);
        var key = DeviceKeys.NewKey();
        device.RotateKey(DeviceKeys.HashOf(key)!, clock.UtcNow);
        audit.Record("TimeClock.DeviceKeyRotated", nameof(TimeClockDevice), device.Id, $"Gave the time clock {device.Name} a new key; the old key stopped working.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeviceKeyDto(await ReadAsync(device.Id, cancellationToken), key);
    }

    public async Task<TimeClockDeviceDto> RevokeAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var device = await LoadAsync(id, request.Version, cancellationToken);
        device.Revoke(clock.UtcNow);
        audit.Record("TimeClock.DeviceRevoked", nameof(TimeClockDevice), device.Id, $"Revoked the time clock {device.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(device.Id, cancellationToken);
    }

    private async Task<TimeClockDevice> LoadAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var device = await devices.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that time clock");
        ConcurrencyGuard.Ensure(device.Version, version);
        return device;
    }

    private async Task<TimeClockDeviceDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        (await reader.DevicesAsync(cancellationToken)).FirstOrDefault(item => item.Id == id) ?? throw new NotFoundException("that time clock");

    private static TimeClockDeviceKind ParseKind(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) && Enum.TryParse<TimeClockDeviceKind>(text.Trim(), true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : (TimeClockDeviceKind)(-1);
}
