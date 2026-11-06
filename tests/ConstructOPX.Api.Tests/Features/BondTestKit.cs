using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Bonds;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Request builders and steps shared by the bonds API tests.</summary>
internal static class BondTestKit
{
    /// <summary>A date safely in the past in every time zone, before any date the tests release or call a bond on.</summary>
    public const string IssuedOn = "2025-01-01";

    /// <summary>Today on the company's calendar (Asia/Manila by default): the day the server judges expiry on.</summary>
    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).DateTime);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static string DaysFromToday(int days) => Date(Today().AddDays(days));

    /// <summary>Defaults: a posted performance bond of 5,000,000.00 valid for another 100 days.</summary>
    public static Dictionary<string, object?> NewBond(Guid projectId, string? direction = "Posted", string? kind = "Performance", string? issuer = "Metro Surety",
        string? number = null, long amountMinor = 5_000_000_00, string? issuedOn = IssuedOn, string? validUntil = null, string? otherParty = null,
        string? note = null, string? clientRequestId = null) => new()
    {
        ["projectId"] = projectId, ["direction"] = direction, ["kind"] = kind, ["issuerName"] = issuer,
        ["bondNumber"] = number ?? $"PB-{Guid.NewGuid():N}"[..12], ["amountMinor"] = amountMinor, ["issuedOn"] = issuedOn,
        ["validUntil"] = validUntil ?? DaysFromToday(100), ["otherPartyName"] = otherParty, ["note"] = note, ["clientRequestId"] = clientRequestId
    };

    public static async Task<BondDto> CreateAsync(ApiClient client, Guid projectId, string direction = "Posted", string kind = "Performance",
        string? validUntil = null, long amountMinor = 5_000_000_00, string? otherParty = null) =>
        await client.PostAsync<BondDto>("/api/bonds",
            NewBond(projectId, direction, kind, validUntil: validUntil, amountMinor: amountMinor,
                otherParty: direction == "Received" ? otherParty ?? "Dela Cruz Electrical" : null));

    /// <summary>The body of an edit that keeps everything as it is, except what the caller changes.</summary>
    public static Dictionary<string, object?> Edit(BondDto bond, string? kind = null, string? issuer = null, long? amountMinor = null,
        string? validUntil = null, string? note = null, int? version = null) => new()
    {
        ["kind"] = kind ?? bond.Kind, ["issuerName"] = issuer ?? bond.IssuerName, ["bondNumber"] = bond.BondNumber,
        ["amountMinor"] = amountMinor ?? bond.AmountMinor, ["issuedOn"] = Date(bond.IssuedOn), ["validUntil"] = validUntil ?? Date(bond.ValidUntil),
        ["otherPartyName"] = bond.OtherPartyName, ["note"] = note ?? bond.Note, ["version"] = version ?? bond.Version
    };

    public static Task<HttpResponseMessage> EditAsync(ApiClient client, BondDto bond, Dictionary<string, object?> body) =>
        client.PutAsync($"/api/bonds/{bond.Id}", body);

    public static Task<HttpResponseMessage> ExtendAsync(ApiClient client, BondDto bond, string validUntil, string? note = null, int? version = null) =>
        client.PostAsync($"/api/bonds/{bond.Id}/extend", new { validUntil, note, version = version ?? bond.Version });

    public static Task<HttpResponseMessage> ReleaseAsync(ApiClient client, BondDto bond, string? releasedOn = null, string? note = null, int? version = null) =>
        client.PostAsync($"/api/bonds/{bond.Id}/release", new { releasedOn = releasedOn ?? DaysFromToday(0), note, version = version ?? bond.Version });

    public static Task<HttpResponseMessage> CallAsync(ApiClient client, BondDto bond, string? calledOn = null, string? note = "Client claimed the bond",
        int? version = null) =>
        client.PostAsync($"/api/bonds/{bond.Id}/call", new { calledOn = calledOn ?? DaysFromToday(0), note, version = version ?? bond.Version });

    public static Task<HttpResponseMessage> VoidAsync(ApiClient client, BondDto bond, string? reason = "Entered by mistake", int? version = null) =>
        client.PostAsync($"/api/bonds/{bond.Id}/void", new { reason, version = version ?? bond.Version });
}
