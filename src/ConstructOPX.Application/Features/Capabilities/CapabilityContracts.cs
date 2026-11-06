namespace ConstructOPX.Application.Features.Capabilities;

// A capability is something a feature depends on that may not be set up yet (file storage, virus scanning, email). One check per capability
// decides whether it can be used now, says so in plain words and names the manual path that still works. A feature never hides behind a
// switch: it asks its capability and falls back. See docs/development/coordination-spec.md section 4.

/// <summary>Whether a capability can be used now.</summary>
public enum CapabilityState
{
    /// <summary>Set up and working.</summary>
    Available = 0,

    /// <summary>Not set up yet: someone has to configure it. The fallback works meanwhile.</summary>
    NeedsSetup = 1,

    /// <summary>Set up but switched off on purpose.</summary>
    Unavailable = 2,

    /// <summary>Set up but not working right now (the last check failed).</summary>
    Failing = 3
}

/// <summary>
/// One capability's state. Implementations are classes named <c>*CapabilityCheck</c> in Infrastructure and are registered automatically. A
/// check must be cheap (a probe that touches something outside the process is cached briefly) and must never throw: a failure is reported as
/// <see cref="CapabilityState.Failing"/>.
/// </summary>
public interface ICapabilityCheck
{
    /// <summary>The capability's key, for example "file-storage" (see <see cref="CapabilityKeys"/>).</summary>
    string Key { get; }

    Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken);
}

public static class CapabilityKeys
{
    public const string FileStorage = "file-storage";
    public const string VirusScanning = "virus-scanning";
    public const string EmailDelivery = "email-delivery";
    public const string TimeClock = "time-clock";

    /// <summary>The order the readiness page lists them in.</summary>
    public static readonly IReadOnlyList<string> InOrder = [FileStorage, VirusScanning, EmailDelivery, TimeClock];
}

/// <summary>A capability's state in plain words: what it is, whether it works, what that means and what still works meanwhile.</summary>
/// <param name="Name">"File storage".</param>
/// <param name="Message">What the state means for people, in one or two sentences. Never a path, a vendor name or an error code.</param>
/// <param name="Fallback">What people can do meanwhile, or null when the capability is Available.</param>
public sealed record CapabilityStatus(string Key, string Name, CapabilityState State, string Message, string? Fallback);

/// <summary>The readiness page: every capability in a fixed order.</summary>
public sealed record CapabilitiesDto(IReadOnlyList<CapabilityDto> Items);

public sealed record CapabilityDto(string Key, string Name, string State, string Message, string? Fallback);
