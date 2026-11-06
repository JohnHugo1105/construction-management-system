using ConstructOPX.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests;

/// <summary>
/// Reads the real routing table (no database needed) and fails when an endpoint is reachable without a named permission. The
/// "signed in" fallback alone would let a new endpoint skip role checks without anyone noticing.
/// </summary>
public sealed class EndpointSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Endpoints that any signed-in person may call, or that must work before sign-in. Everything else needs a permission.
    private static readonly string[] OpenToSignedInPeople = ["/api/identity/", "/api/organization"];

    // The one route no person uses: time clock machines post scans with a device key (DeviceKeyAuthentication), so it names the device policy instead of
    // a permission. Only this route and this method, with exactly that policy, are allowed; any other endpoint naming the device policy fails below.
    private static readonly (string Route, string Method, string Policy) MachineRoute =
        ("/api/timeclock/punches", "POST", ConstructOPX.Api.Security.DeviceKeyAuthentication.Policy);

    private readonly WebApplicationFactory<Program> _factory;

    public EndpointSecurityTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Only the routing table is read, so no database is needed. "Testing" keeps the Development-only start-up migration off.
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:ConstructOpx", "Server=unused;Database=unused;Integrated Security=true");
            builder.UseSetting("Database:MigrateOnStartup", "false");
        });

    [Fact]
    public void Every_api_endpoint_outside_identity_names_a_known_permission()
    {
        var endpoints = ApiEndpoints().ToArray();
        Assert.NotEmpty(endpoints);

        var offenders = endpoints
            .Where(endpoint => !OpenToSignedInPeople.Any(prefix => endpoint.Route.StartsWith(prefix, StringComparison.Ordinal)) ||
                (endpoint.Route == "/api/organization" && endpoint.Methods.Any(method => method != "GET")))
            .Where(endpoint => !IsMachineRoute(endpoint))
            .Where(endpoint => endpoint.Policies.Count == 0 || endpoint.Policies.Any(policy => !Permissions.All.Contains(policy)))
            .Select(endpoint => $"{string.Join(",", endpoint.Methods)} {endpoint.Route}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Only_the_machine_route_uses_the_device_policy_and_it_uses_nothing_else()
    {
        var endpoints = ApiEndpoints().ToArray();

        var machine = Assert.Single(endpoints, endpoint => endpoint.Route == MachineRoute.Route && endpoint.Methods.Contains(MachineRoute.Method));
        Assert.Equal([MachineRoute.Method], machine.Methods);
        Assert.Equal([MachineRoute.Policy], machine.Policies);
        Assert.False(machine.AllowsAnonymous);
        Assert.DoesNotContain(endpoints, endpoint => endpoint != machine && endpoint.Policies.Contains(MachineRoute.Policy));
        Assert.DoesNotContain(MachineRoute.Policy, Permissions.All);
    }

    private static bool IsMachineRoute(EndpointInfo endpoint) =>
        endpoint.Route == MachineRoute.Route && endpoint.Methods.SequenceEqual([MachineRoute.Method]) && endpoint.Policies.SequenceEqual([MachineRoute.Policy]);

    [Fact]
    public void Nothing_outside_identity_allows_anonymous_access()
    {
        var offenders = ApiEndpoints()
            .Where(endpoint => endpoint.AllowsAnonymous && !endpoint.Route.StartsWith("/api/identity/", StringComparison.Ordinal))
            .Select(endpoint => $"{string.Join(",", endpoint.Methods)} {endpoint.Route}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_permission_in_the_matrix_is_used_by_some_endpoint_or_service()
    {
        // A permission nobody enforces is a rule that exists only on paper. Each is checked by at least one endpoint.
        var used = ApiEndpoints().SelectMany(endpoint => endpoint.Policies).ToHashSet();
        var unused = Permissions.All.Where(permission => !used.Contains(permission)).Order().ToArray();

        Assert.True(unused.Length == 0, $"Unenforced permissions: {string.Join(", ", unused)}");
    }

    [Fact]
    public void The_needs_action_routes_are_read_only_and_need_actions_read()
    {
        var routes = ApiEndpoints().Where(endpoint => endpoint.Route.StartsWith("/api/needs-action", StringComparison.Ordinal)).ToArray();

        Assert.Equal(["/api/needs-action", "/api/needs-action/count"], routes.Select(endpoint => endpoint.Route).Order());
        Assert.All(routes, endpoint =>
        {
            Assert.Equal(["GET"], endpoint.Methods);
            Assert.Equal([Permissions.ActionsRead], endpoint.Policies);
        });
    }

    private IEnumerable<EndpointInfo> ApiEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } text && text.StartsWith("/api/", StringComparison.Ordinal))
            .Select(endpoint => new EndpointInfo(
                endpoint.RoutePattern.RawText!.TrimEnd('/'),
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ToArray() ?? [],
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).Where(policy => policy is not null).Cast<string>().ToArray(),
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null));

    private sealed record EndpointInfo(string Route, string[] Methods, IReadOnlyList<string> Policies, bool AllowsAnonymous);
}
