using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Identity;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace Cms.Api.Tests.Features;

public sealed class FirstTimeSetupApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public FirstTimeSetupApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task First_time_setup_creates_the_company_and_administrator_once()
    {
        using var client = new ApiClient(_factory);

        var before = await client.GetAsync<JsonElement>("/api/identity/setup-status");
        Assert.True(before.GetProperty("needsSetup").GetBoolean());

        var response = await client.PostAsync("/api/identity/bootstrap",
            new { companyName = "GSG Builders", username = "owner", displayName = "The Owner", password = TenantSeeder.Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await client.ReadAsync<SessionDto>(response);
        Assert.Equal("GSG Builders", session.OrganizationName);
        Assert.Contains("Administrator", session.Roles);
        Assert.Contains("users.manage", session.Permissions);

        Assert.False((await client.GetAsync<JsonElement>("/api/identity/setup-status")).GetProperty("needsSetup").GetBoolean());
        var again = await client.PostAsync("/api/identity/bootstrap",
            new { companyName = "Another", username = "intruder", displayName = "Intruder", password = TenantSeeder.Password });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already set up", (await ApiClient.ProblemAsync(again)).GetProperty("detail").GetString());
    }

}

/// <summary>Its own class, so it gets its own empty database and the outcome is exactly one thing.</summary>
public sealed class FirstTimeSetupPolicyApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public FirstTimeSetupPolicyApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task First_time_setup_enforces_the_password_policy_and_creates_nothing_when_it_fails()
    {
        using var client = new ApiClient(_factory);

        var response = await client.PostAsync("/api/identity/bootstrap",
            new { companyName = "X Builders", username = "weak", displayName = "Weak", password = "short" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(response)).GetProperty("errors").TryGetProperty("password", out _));
        Assert.True((await client.GetAsync<JsonElement>("/api/identity/setup-status")).GetProperty("needsSetup").GetBoolean());
    }
}

public sealed class IdentityApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public IdentityApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Writes_without_the_anti_forgery_token_are_refused()
    {
        var seeded = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(_factory);

        var response = await client.SendAsync(HttpMethod.Post, "/api/identity/sign-in", new { username = seeded.Username, password = seeded.Password }, withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", (await ApiClient.ProblemAsync(response)).GetProperty("code").GetString());
    }

    [SqlServerFact]
    public async Task Signing_in_returns_the_session_and_unknown_user_and_wrong_password_look_the_same()
    {
        var seeded = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(_factory);

        var wrongPassword = await client.PostAsync("/api/identity/sign-in", new { username = seeded.Username, password = "not the password 1" });
        var unknownUser = await client.PostAsync("/api/identity/sign-in", new { username = "nobody-" + Guid.NewGuid().ToString("N"), password = "not the password 1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal((await ApiClient.ProblemAsync(wrongPassword)).GetProperty("detail").GetString(),
            (await ApiClient.ProblemAsync(unknownUser)).GetProperty("detail").GetString());

        await client.SignInAsync(seeded.Username, seeded.Password);
        var session = await client.GetAsync<SessionDto>("/api/identity/session");
        Assert.Equal(seeded.Username, session.Username);
        Assert.Equal(seeded.TenantId, session.TenantId);
        Assert.Equal("PHP", session.CurrencyCode);
    }

    [SqlServerFact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        var seeded = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(_factory);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/identity/sign-in", new { username = seeded.Username, password = "wrong password 1" })).StatusCode);

        var locked = await client.PostAsync("/api/identity/sign-in", new { username = seeded.Username, password = seeded.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("locked", (await ApiClient.ProblemAsync(locked)).GetProperty("detail").GetString());
    }

    [SqlServerFact]
    public async Task A_signed_out_caller_is_refused_with_401()
    {
        using var client = new ApiClient(_factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/session")).StatusCode);
    }

    [SqlServerFact]
    public async Task Signing_out_ends_the_session()
    {
        var seeded = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(_factory);
        await client.SignInAsync(seeded.Username, seeded.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/projects")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/identity/sign-out")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
    }

    [SqlServerFact]
    public async Task A_temporary_password_must_be_changed_before_anything_else_can_be_done()
    {
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        var temp = await TenantSeeder.AddUserAsync(_factory, admin.TenantId, AccessRole.ProjectManager, mustChangePassword: true);
        using var client = new ApiClient(_factory);
        await client.SignInAsync(temp.Username, temp.Password);

        var blocked = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password_change_required", (await ApiClient.ProblemAsync(blocked)).GetProperty("code").GetString());

        var weak = await client.PostAsync("/api/identity/change-password", new { currentPassword = temp.Password, newPassword = "short" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);

        var changed = await client.PostAsync<SessionDto>("/api/identity/change-password", new { currentPassword = temp.Password, newPassword = "A brand-new passphrase 42" });
        Assert.False(changed.MustChangePassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/projects")).StatusCode);
    }

    [SqlServerFact]
    public async Task Health_endpoints_report_liveness_readiness_and_version()
    {
        using var client = new ApiClient(_factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var version = await client.GetAsync<JsonElement>("/health/version");
        Assert.Equal("cms-api", version.GetProperty("service").GetString());
    }
}

public sealed class UserAdministrationApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public UserAdministrationApiTests(CmsApiFactory factory) => _factory = factory;

    private async Task<(ApiClient Client, SeededUser Admin)> SignedInAdminAsync()
    {
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        var client = new ApiClient(_factory);
        await client.SignInAsync(admin.Username, admin.Password);
        return (client, admin);
    }

    [SqlServerFact]
    public async Task An_administrator_creates_a_user_who_signs_in_with_a_temporary_password()
    {
        var (admin, _) = await SignedInAdminAsync();
        using var _admin = admin;
        var username = TenantSeeder.Unique("cost");

        var created = await admin.PostAsync<UserDto>("/api/users", new
        {
            username, displayName = "Carla Cost", email = "carla@example.com", password = "Temporary-pass-123", roles = new[] { "CostEngineer", "Accountant" }
        });

        Assert.True(created.MustChangePassword);
        Assert.Equal(["Accountant", "CostEngineer"], created.Roles.Order().ToArray());
        using var carla = new ApiClient(_factory);
        await carla.SignInAsync(username, "Temporary-pass-123");
        Assert.Equal(HttpStatusCode.Forbidden, (await carla.GetAsync("/api/projects")).StatusCode);
    }

    [SqlServerFact]
    public async Task Usernames_are_unique_and_passwords_must_meet_the_policy()
    {
        var (admin, existing) = await SignedInAdminAsync();
        using var _admin = admin;

        var duplicate = await admin.PostAsync("/api/users", new { username = existing.Username, displayName = "Dup", password = "Temporary-pass-123", roles = new[] { "Viewer" } });
        var weak = await admin.PostAsync("/api/users", new { username = TenantSeeder.Unique("weak"), displayName = "Weak", password = "short", roles = new[] { "Viewer" } });
        var noRole = await admin.PostAsync("/api/users", new { username = TenantSeeder.Unique("norole"), displayName = "No Role", password = "Temporary-pass-123", roles = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.True((await ApiClient.ProblemAsync(duplicate)).GetProperty("errors").TryGetProperty("username", out _));
        Assert.True((await ApiClient.ProblemAsync(weak)).GetProperty("errors").TryGetProperty("password", out _));
        Assert.True((await ApiClient.ProblemAsync(noRole)).GetProperty("errors").TryGetProperty("roles", out _));
    }

    [SqlServerFact]
    public async Task The_last_administrator_and_your_own_account_are_protected()
    {
        var (admin, self) = await SignedInAdminAsync();
        using var _admin = admin;
        var me = await admin.GetAsync<UserDto>($"/api/users/{self.UserId}");

        var deactivateSelf = await admin.PostAsync($"/api/users/{self.UserId}/deactivate");
        var demoteSelf = await admin.PutAsync($"/api/users/{self.UserId}", new { displayName = me.DisplayName, email = (string?)null, roles = new[] { "Viewer" }, version = me.Version });

        Assert.Equal(HttpStatusCode.Conflict, deactivateSelf.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, demoteSelf.StatusCode);
    }

    [SqlServerFact]
    public async Task Turning_off_an_account_ends_its_session_at_once()
    {
        var (admin, adminUser) = await SignedInAdminAsync();
        using var _admin = admin;
        var target = await TenantSeeder.AddUserAsync(_factory, adminUser.TenantId, AccessRole.Viewer);
        using var viewer = new ApiClient(_factory);
        await viewer.SignInAsync(target.Username, target.Password);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/projects")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{target.UserId}/deactivate")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/projects")).StatusCode);
        using var again = new ApiClient(_factory);
        var refused = await again.PostAsync("/api/identity/sign-in", new { username = target.Username, password = target.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [SqlServerFact]
    public async Task A_stale_edit_of_a_user_is_refused()
    {
        var (admin, adminUser) = await SignedInAdminAsync();
        using var _admin = admin;
        var target = await TenantSeeder.AddUserAsync(_factory, adminUser.TenantId, AccessRole.Viewer);
        var user = await admin.GetAsync<UserDto>($"/api/users/{target.UserId}");
        await admin.PutAsync<UserDto>($"/api/users/{target.UserId}", new { displayName = "First edit", email = (string?)null, roles = new[] { "Viewer" }, version = user.Version });

        var stale = await admin.PutAsync($"/api/users/{target.UserId}", new { displayName = "Second edit", email = (string?)null, roles = new[] { "Viewer" }, version = user.Version });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", (await ApiClient.ProblemAsync(stale)).GetProperty("code").GetString());
    }

    [SqlServerFact]
    public async Task Only_administrators_manage_users_and_settings()
    {
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        var manager = await TenantSeeder.AddUserAsync(_factory, admin.TenantId, AccessRole.ProjectManager);
        using var client = new ApiClient(_factory);
        await client.SignInAsync(manager.Username, manager.Password);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/organization")).StatusCode);
    }
}

public sealed class ApiFailureShapeTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public ApiFailureShapeTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Refusals_without_a_body_are_still_problem_details_the_web_app_can_read()
    {
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        var viewer = await TenantSeeder.AddUserAsync(_factory, admin.TenantId, AccessRole.Viewer);
        using var signedOut = new ApiClient(_factory);
        using var signedIn = new ApiClient(_factory);
        await signedIn.SignInAsync(viewer.Username, viewer.Password);

        var unauthenticated = await signedOut.GetAsync("/api/projects");
        var forbidden = await signedIn.GetAsync("/api/users");
        var unknownRoute = await signedIn.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("application/problem+json", unauthenticated.Content.Headers.ContentType?.MediaType);
        Assert.Equal("unauthenticated", (await ApiClient.ProblemAsync(unauthenticated)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("forbidden", (await ApiClient.ProblemAsync(forbidden)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await ApiClient.ProblemAsync(unknownRoute)).GetProperty("traceId").GetString()));
    }

    [SqlServerFact]
    public async Task The_activity_log_hides_routine_sign_ins_unless_asked_but_always_shows_failed_ones()
    {
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(_factory);
        using var stranger = new ApiClient(_factory);
        await stranger.PostAsync("/api/identity/sign-in", new { username = admin.Username, password = "wrong password 123" });
        await client.SignInAsync(admin.Username, admin.Password);

        var quiet = await client.GetAsync<Cms.Application.Common.PagedResult<Cms.Application.Features.Audit.AuditEntryDto>>("/api/audit");
        var withSignIns = await client.GetAsync<Cms.Application.Common.PagedResult<Cms.Application.Features.Audit.AuditEntryDto>>("/api/audit?includeSignIns=true");

        Assert.DoesNotContain(quiet.Items, entry => entry.Action == "User.SignedIn");
        Assert.Contains(quiet.Items, entry => entry.Action == "User.SignInFailed");
        Assert.Contains(withSignIns.Items, entry => entry.Action == "User.SignedIn");
    }
}

public sealed class FirstTimeSetupSwitchApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public FirstTimeSetupSwitchApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task With_the_switch_off_first_time_setup_is_neither_offered_nor_accepted_even_on_an_empty_system()
    {
        await using var switchedOff = _factory.WithWebHostBuilder(builder => builder.UseSetting("Bootstrap:Enabled", "false"));
        using var client = new ApiClient(switchedOff);

        var status = await client.GetAsync<JsonElement>("/api/identity/setup-status");
        var attempt = await client.PostAsync("/api/identity/bootstrap",
            new { companyName = "Hijack Ltd", username = "intruder", displayName = "Intruder", password = TenantSeeder.Password });

        Assert.False(status.GetProperty("needsSetup").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
    }
}
