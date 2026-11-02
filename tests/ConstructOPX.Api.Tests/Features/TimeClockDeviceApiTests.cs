using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.TimeClockTestKit;
using static ConstructOPX.Api.Tests.Features.WorkforceTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The machine route's security: a dedicated device scheme, keys stored as hashes and refused alike, no cookie on the machine route and no device key
/// anywhere else, a per-device rate limit, and one company per device.
/// </summary>
public sealed class TimeClockDeviceApiTests(ConstructOpxApiFactory factory) : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory = factory;

    [SqlServerFact]
    public async Task A_registered_device_gets_its_key_once_and_its_scans_are_accepted_without_a_cookie_or_anti_forgery_token()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);

        var registered = await RegisterAsync(admin, project.Id);
        Assert.Equal(43, registered.Key.Length);
        Assert.Equal(("Main gate", "Kiosk", "Active", project.Name), (registered.Device.Name, registered.Device.Kind, registered.Device.Status,
            registered.Device.ProjectName));

        // The list never shows the key again, and the capability says no device has connected yet.
        var before = await admin.GetAsync<TimeClockDeviceListDto>("/api/timeclock/devices");
        Assert.Equal("NeedsSetup", before.Capability.Status);
        var listJson = await RawAsync(admin, "/api/timeclock/devices");
        Assert.DoesNotContain(registered.Key, listJson.GetRawText(), StringComparison.Ordinal);

        var result = await PushAsync(_factory, registered.Key, Scan("k-1", crew[0].Code, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(PunchOutcome.Accepted, Assert.Single(result.Results).Outcome);

        var after = await admin.GetAsync<TimeClockDeviceListDto>("/api/timeclock/devices");
        Assert.Equal("Available", after.Capability.Status);
        Assert.NotNull(Assert.Single(after.Items).LastSeenAtUtc);
    }

    [SqlServerFact]
    public async Task Unknown_revoked_rotated_out_and_malformed_keys_all_get_the_same_401_with_no_detail()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var first = await RegisterAsync(admin, project.Id, "Gate 1");
        var second = await RegisterAsync(admin, project.Id, "Gate 2");
        var body = new { punches = new[] { Scan("x-1", crew[0].Code, DateTimeOffset.UtcNow.AddMinutes(-1)) } };

        // Rotating gives a new key; the old one stops working at once.
        var rotated = await admin.PostAsync<DeviceKeyDto>($"/api/timeclock/devices/{first.Device.Id}/rotate-key", new { version = first.Device.Version });
        Assert.NotEqual(first.Key, rotated.Key);
        Assert.Equal(HttpStatusCode.OK, (await PushRawAsync(_factory, rotated.Key, body)).StatusCode);

        // Revoking is immediate.
        Assert.Equal(HttpStatusCode.OK, (await PushRawAsync(_factory, second.Key, new { punches = new[] { Scan("y-1", crew[0].Code, DateTimeOffset.UtcNow.AddMinutes(-3)) } })).StatusCode);
        var revoked = await admin.PostAsync<TimeClockDeviceDto>($"/api/timeclock/devices/{second.Device.Id}/revoke", new { version = second.Device.Version });
        Assert.Equal("Revoked", revoked.Status);

        var answers = new List<string>();
        foreach (var key in new[] { first.Key, second.Key, DeviceKeys.NewKey(), "not-a-key", "", new string('A', 500) })
        {
            var response = await PushRawAsync(_factory, key, body);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Key '{key[..Math.Min(key.Length, 12)]}' got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            answers.Add(await response.Content.ReadAsStringAsync());
        }

        // No key at all is refused the same way.
        var none = await PushRawAsync(_factory, null, body);
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        answers.Add(await none.Content.ReadAsStringAsync());

        var shapes = answers.Select(text =>
        {
            var problem = JsonDocument.Parse(text).RootElement;
            return (problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString(), problem.GetProperty("code").GetString());
        }).Distinct().ToArray();
        Assert.Equal([("Sign in required", "Sign in to continue.", "unauthenticated")], shapes);
        Assert.All(answers, text => Assert.DoesNotContain("revok", text, StringComparison.OrdinalIgnoreCase));

        // A revoked device can't be rotated back to life.
        var stale = await admin.PostAsync($"/api/timeclock/devices/{second.Device.Id}/rotate-key", new { version = revoked.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [SqlServerFact]
    public async Task The_machine_route_is_unreachable_with_a_persons_cookie()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (_, crew) = await CrewAsync(admin, 1);
        var body = new { punches = new[] { Scan("c-1", crew[0].Code, DateTimeOffset.UtcNow.AddMinutes(-1)) } };

        // Through the signed-in client (cookie and anti-forgery token): still 401, because the route only knows device keys.
        var withSession = await admin.PostAsync("/api/timeclock/punches", body);
        Assert.Equal(HttpStatusCode.Unauthorized, withSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PushRawAsync(_factory, null, body, admin.SessionCookie)).StatusCode);
    }

    [SqlServerFact]
    public async Task A_device_key_opens_no_other_route()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var (project, crew) = await CrewAsync(admin, 1);
        var key = (await RegisterAsync(admin, project.Id)).Key;

        var sample = new (HttpMethod Method, string Url)[]
        {
            (HttpMethod.Get, "/api/organization"),
            (HttpMethod.Get, "/api/identity/session"),
            (HttpMethod.Get, "/api/workers"),
            (HttpMethod.Get, $"/api/workers/{crew[0].Worker.Id}"),
            (HttpMethod.Get, "/api/projects"),
            (HttpMethod.Get, $"/api/timeclock/days?projectId={project.Id}"),
            (HttpMethod.Get, $"/api/timeclock/summary?projectId={project.Id}"),
            (HttpMethod.Get, "/api/timeclock/devices"),
            (HttpMethod.Get, $"/api/timeclock/badges?workerIds={crew[0].Worker.Id}"),
            (HttpMethod.Post, $"/api/workers/{crew[0].Worker.Id}/punch-code"),
            (HttpMethod.Post, "/api/timeclock/punches/manual"),
            (HttpMethod.Post, "/api/timeclock/days/confirm"),
            (HttpMethod.Post, "/api/attendance"),
            (HttpMethod.Get, "/api/payroll-runs"),
            (HttpMethod.Get, "/api/needs-action"),
            (HttpMethod.Get, "/api/users")
        };
        foreach (var (method, url) in sample)
        {
            var status = await WithDeviceKeyOnlyAsync(_factory, key, method, url);
            Assert.True(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{method} {url} answered {(int)status} to a device key");
        }
    }

    [SqlServerFact]
    public async Task Each_device_may_send_120_requests_a_minute_and_gets_429_over_it()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var busy = (await RegisterAsync(admin, project.Id, "Busy gate")).Key;
        var quiet = (await RegisterAsync(admin, project.Id, "Quiet gate")).Key;

        // An empty batch is refused (422) but still counts against the limit.
        for (var request = 1; request <= 120; request++)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PushRawAsync(_factory, busy, new { punches = Array.Empty<object>() })).StatusCode);

        var over = await PushRawAsync(_factory, busy, new { punches = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        Assert.Equal("rate_limited", (await ApiClient.ProblemAsync(over)).GetProperty("code").GetString());
        // Another device has its own budget.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PushRawAsync(_factory, quiet, new { punches = Array.Empty<object>() })).StatusCode);
    }

    [SqlServerFact]
    public async Task A_device_belongs_to_one_company_and_never_reaches_anothers_workers_or_devices()
    {
        var (companyA, _) = await SignedInAsync(_factory);
        using var _a = companyA;
        var (companyB, userB) = await SignedInAsync(_factory);
        using var _b = companyB;
        var (projectA, _) = await CrewAsync(companyA, 1);
        var (projectB, crewB) = await CrewAsync(companyB, 1, "Harbor");
        var deviceA = await RegisterAsync(companyA, projectA.Id);
        var deviceB = await RegisterAsync(companyB, projectB.Id);

        // Company A's machine scanning company B's badge: the code is unknown in A, answered like any inactive badge, and nothing is stored in B.
        var result = await PushAsync(_factory, deviceA.Key, Scan("b-1", crewB[0].Code, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(("Rejected", "BadgeNotActive"), (result.Results[0].Outcome, result.Results[0].ReasonCode));
        Assert.Equal(0, StoredPunches(_factory, userB.TenantId));

        // Company A can't see, rotate or revoke company B's device, or register one on B's project.
        Assert.DoesNotContain((await companyA.GetAsync<TimeClockDeviceListDto>("/api/timeclock/devices")).Items, item => item.Id == deviceB.Device.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await companyA.PostAsync($"/api/timeclock/devices/{deviceB.Device.Id}/rotate-key", new { version = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await companyA.PostAsync($"/api/timeclock/devices/{deviceB.Device.Id}/revoke", new { version = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await companyA.PostAsync("/api/timeclock/devices", new { name = "Sneaky", projectId = projectB.Id, kind = "Kiosk" })).StatusCode);
    }

    [SqlServerFact]
    public async Task Only_administrators_manage_devices_and_a_stale_version_is_a_conflict()
    {
        var (admin, owner) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var device = await RegisterAsync(admin, project.Id);

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.ProjectManager })
        {
            var (other, _) = await SignedInAsync(_factory, role, owner.TenantId);
            using var _o = other;
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/timeclock/devices")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync("/api/timeclock/devices", new { name = "Gate", projectId = project.Id, kind = "Kiosk" })).StatusCode);
        }

        await admin.PostAsync<DeviceKeyDto>($"/api/timeclock/devices/{device.Device.Id}/rotate-key", new { version = device.Device.Version });
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsync($"/api/timeclock/devices/{device.Device.Id}/revoke", new { version = device.Device.Version })).StatusCode);

        var invalid = await admin.PostAsync("/api/timeclock/devices", new { name = "", projectId = project.Id, kind = "Phone" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await ApiClient.ProblemAsync(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _) && errors.TryGetProperty("kind", out _));
    }
}
