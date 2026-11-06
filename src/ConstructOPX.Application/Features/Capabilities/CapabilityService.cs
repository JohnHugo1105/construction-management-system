namespace ConstructOPX.Application.Features.Capabilities;

/// <summary>
/// Reads the capability checks: every one for the System readiness page, or one for a feature that depends on it. A capability with no
/// registered check is reported as needing setup, never as available.
/// </summary>
public sealed class CapabilityService(IEnumerable<ICapabilityCheck> checks)
{
    public async Task<CapabilitiesDto> ListAsync(CancellationToken cancellationToken)
    {
        var all = checks.ToArray();
        var items = new List<CapabilityDto>(all.Length);
        // Known capabilities in their fixed order, then any other check by key.
        foreach (var check in all.OrderBy(check => Order(check.Key)).ThenBy(check => check.Key, StringComparer.Ordinal))
        {
            var status = await check.CheckAsync(cancellationToken);
            items.Add(new CapabilityDto(status.Key, status.Name, status.State.ToString(), status.Message, status.Fallback));
        }
        return new CapabilitiesDto(items);
    }

    public async Task<CapabilityStatus> GetAsync(string key, CancellationToken cancellationToken)
    {
        var check = checks.FirstOrDefault(item => item.Key == key);
        return check is null
            ? new CapabilityStatus(key, key, CapabilityState.NeedsSetup, "This is not set up.", null)
            : await check.CheckAsync(cancellationToken);
    }

    private static int Order(string key)
    {
        var index = CapabilityKeys.InOrder.ToList().IndexOf(key);
        return index < 0 ? int.MaxValue : index;
    }
}
