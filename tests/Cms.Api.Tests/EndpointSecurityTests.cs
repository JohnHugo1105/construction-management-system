using Cms.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Api.Tests;

/// <summary>
/// Reads the real routing table (no database needed) and fails when an endpoint is reachable without a named permission. The
/// "signed in" fallback alone would let a new endpoint skip role checks without anyone noticing.
/// </summary>
public sealed class EndpointSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Endpoints that any signed-in person may call, or that must work before sign-in. Everything else needs a permission.
    private static readonly string[] OpenToSignedInPeople = ["/api/identity/", "/api/organization"];

    private readonly WebApplicationFactory<Program> _factory;

    public EndpointSecurityTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Only the routing table is read, so no database is needed. "Testing" keeps the Development-only start-up migration off.
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Cms", "Server=unused;Database=unused;Integrated Security=true");
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
            .Where(endpoint => endpoint.Policies.Count == 0 || endpoint.Policies.Any(policy => !Permissions.All.Contains(policy)))
            .Select(endpoint => $"{string.Join(",", endpoint.Methods)} {endpoint.Route}")
            .ToArray();

        Assert.Empty(offenders);
    }

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

        // Features add their endpoints over time; this list shrinks to empty and must stay empty.
        Assert.True(unused.Length == 0 || ExpectedUnusedWhileBuilding.IsSupersetOf(unused), $"Unenforced permissions: {string.Join(", ", unused)}");
    }

    // Permissions whose feature's endpoints are added in a later slice. Remove entries as the endpoints land; the final test run has none.
    private static readonly HashSet<string> ExpectedUnusedWhileBuilding = new(StringComparer.Ordinal)
    {
        "purchasing.read", "purchasing.manage", "purchasing.approve",
        "progress.read", "progress.write",
        "changeorders.read", "changeorders.manage", "changeorders.approve",
        "billing.read", "billing.manage", "billing.approve",
        "subcontracts.read", "subcontracts.manage", "subcontracts.approve",
        "statutory.manage"
    };

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
