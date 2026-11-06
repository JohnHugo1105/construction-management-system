using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.TimeClock;

public enum TimeClockDeviceKind
{
    /// <summary>A tablet or PC at the gate running the kiosk page, with a keyboard-style barcode or QR scanner.</summary>
    Kiosk = 0,

    /// <summary>A dedicated time clock or its gateway, pushing scans over HTTP.</summary>
    Terminal = 1
}

public enum TimeClockDeviceStatus
{
    Active = 0,
    Revoked = 1
}

/// <summary>
/// A machine allowed to send scans for one project. It proves itself with a device key: 256 random bits shown once when the device is
/// registered or its key rotated, and stored only as a SHA-256 hash, so the database never holds a key that works. Revoking is immediate and
/// final; rotating replaces the key at once (the old one stops working).
/// </summary>
public sealed class TimeClockDevice : AggregateRoot
{
    public const int MaximumNameLength = 80;
    public const int KeyHashLength = 32;

    private TimeClockDevice() { }

    public string Name { get; private set; } = "";

    /// <summary>The project the device's scans count for (where it stands).</summary>
    public Guid ProjectId { get; private set; }

    public TimeClockDeviceKind Kind { get; private set; }
    public TimeClockDeviceStatus Status { get; private set; } = TimeClockDeviceStatus.Active;

    /// <summary>SHA-256 of the device key. The key itself is never stored.</summary>
    public byte[] KeyHash { get; private set; } = [];

    /// <summary>When the device last sent scans (kept to the minute; it is not a log).</summary>
    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    public DateTimeOffset? KeyRotatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }

    public bool IsActive => Status == TimeClockDeviceStatus.Active;

    public static TimeClockDevice Register(Guid id, Guid tenantId, string? name, Project project, TimeClockDeviceKind kind, byte[] keyHash,
        Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        var errors = new ValidationErrors();
        string cleanName = "";
        try { cleanName = Guard.RequiredText(name, "name", "a name for the time clock (for example Main gate)", MaximumNameLength); }
        catch (ValidationFailedException ex) { errors.Add("name", ex.Errors["name"][0]); }
        if (!Enum.IsDefined(kind)) errors.Add("kind", "Choose whether this is a kiosk page or a time clock machine.");
        errors.ThrowIfAny();
        EnsureHash(keyHash);

        var device = new TimeClockDevice
        {
            Name = cleanName,
            ProjectId = project.Id,
            Kind = kind,
            KeyHash = keyHash,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        device.Initialize(id, tenantId, now);
        return device;
    }

    /// <summary>Replaces the key. The old key stops working the moment this is saved.</summary>
    public void RotateKey(byte[] keyHash, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainRuleException("This time clock was revoked. Register it again to give it a new key.");
        EnsureHash(keyHash);
        KeyHash = keyHash;
        KeyRotatedAtUtc = now;
        Touch(now);
    }

    /// <summary>Stops the device for good: its key is refused from the next request.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (!IsActive) throw new DomainRuleException("This time clock is already revoked.");
        Status = TimeClockDeviceStatus.Revoked;
        RevokedAtUtc = now;
        Touch(now);
    }

    private static void EnsureHash(byte[] keyHash)
    {
        if (keyHash is not { Length: KeyHashLength }) throw new ArgumentException("A device key hash is 32 bytes.", nameof(keyHash));
    }
}
