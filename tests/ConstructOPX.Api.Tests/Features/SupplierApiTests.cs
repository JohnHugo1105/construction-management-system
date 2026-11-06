using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Suppliers;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

public sealed class SupplierApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public SupplierApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private static object Firm(string name, bool supplier = true, bool subcontractor = false, string? email = "sales@example.com") => new
    {
        name, isSupplier = supplier, isSubcontractor = subcontractor, tin = "123-456-789-000", contactPerson = "Ana Reyes", phone = "0917 000 0000", email,
        address = "Cebu City", notes = (string?)null, version = 0
    };

    [SqlServerFact]
    public async Task A_firm_is_added_listed_found_by_kind_changed_archived_and_restored()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var steel = await client.PostAsync<SupplierDto>("/api/suppliers", Firm("Visayas Steel Corp."));
        var crew = await client.PostAsync<SupplierDto>("/api/suppliers", Firm("Dela Cruz Masonry", supplier: false, subcontractor: true));
        var both = await client.PostAsync<SupplierDto>("/api/suppliers", Firm("Cebu Formworks", subcontractor: true));

        var all = await client.GetAsync<PagedResult<SupplierListItemDto>>("/api/suppliers");
        Assert.Equal(["Cebu Formworks", "Dela Cruz Masonry", "Visayas Steel Corp."], all.Items.Select(item => item.Name));
        var onlySubs = await client.GetAsync<PagedResult<SupplierListItemDto>>("/api/suppliers?kind=Subcontractor");
        Assert.Equal(["Cebu Formworks", "Dela Cruz Masonry"], onlySubs.Items.Select(item => item.Name));
        Assert.Equal(["Cebu Formworks", "Visayas Steel Corp."], (await client.GetAsync<List<SupplierLookupDto>>("/api/suppliers/lookup?kind=Supplier")).Select(item => item.Name));
        Assert.Equal([crew.Id], (await client.GetAsync<PagedResult<SupplierListItemDto>>("/api/suppliers?search=masonry")).Items.Select(item => item.Id));
        Assert.True(both.IsSupplier && both.IsSubcontractor);

        var changed = await client.PutAsync<SupplierDto>($"/api/suppliers/{steel.Id}", new
        {
            name = "Visayas Steel Corporation", isSupplier = true, isSubcontractor = false, tin = "123-456-789-000", contactPerson = "Ben Tan", phone = (string?)null,
            email = (string?)null, address = (string?)null, notes = "Net 30", version = steel.Version
        });
        Assert.Equal("Ben Tan", changed.ContactPerson);
        Assert.True(changed.Version > steel.Version);

        var archived = await client.PostAsync<SupplierDto>($"/api/suppliers/{steel.Id}/archive", new { version = changed.Version });
        Assert.True(archived.IsArchived);
        Assert.DoesNotContain(await client.GetAsync<List<SupplierLookupDto>>("/api/suppliers/lookup"), item => item.Id == steel.Id);
        Assert.Contains((await client.GetAsync<PagedResult<SupplierListItemDto>>("/api/suppliers?includeArchived=true")).Items, item => item.Id == steel.Id && item.IsArchived);
        Assert.False((await client.PostAsync<SupplierDto>($"/api/suppliers/{steel.Id}/restore", new { version = archived.Version })).IsArchived);
    }

    [SqlServerFact]
    public async Task A_name_is_used_once_whatever_its_case_or_spacing_and_every_problem_is_reported_together()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        await client.PostAsync<SupplierDto>("/api/suppliers", Firm("Visayas Steel Corp."));

        var duplicate = await client.PostAsync("/api/suppliers", Firm("  visayas STEEL corp. "));
        Assert.Contains("name", await ErrorFieldsAsync(duplicate));

        var bad = await client.PostAsync("/api/suppliers", Firm(" ", supplier: false, subcontractor: false, email: "nope"));
        Assert.Equal(new HashSet<string> { "name", "kind", "email" }, await ErrorFieldsAsync(bad));
    }

    [SqlServerFact]
    public async Task A_stale_edit_is_a_conflict()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var firm = await client.PostAsync<SupplierDto>("/api/suppliers", Firm("Visayas Steel Corp."));
        await client.PostAsync<SupplierDto>($"/api/suppliers/{firm.Id}/archive", new { version = firm.Version });

        var stale = await client.PostAsync($"/api/suppliers/{firm.Id}/restore", new { version = firm.Version });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
    }

    [SqlServerFact]
    public async Task Roles_and_companies_are_kept_apart()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var firm = await admin.PostAsync<SupplierDto>("/api/suppliers", Firm("Visayas Steel Corp."));

        // A warehouse keeper reads firm names (to name a supplier on stock) but changes nothing; a viewer reads only.
        var (store, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _s = store;
        Assert.Equal(HttpStatusCode.OK, (await store.GetAsync("/api/suppliers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await store.PostAsync("/api/suppliers", Firm("Another"))).StatusCode);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, user.TenantId);
        using var _v = viewer;
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/suppliers/{firm.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/suppliers/{firm.Id}/archive", new { version = firm.Version })).StatusCode);

        // Another company never sees the firm, even by its id.
        var (other, _) = await SignedInAsync(_factory);
        using var _o = other;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/suppliers/{firm.Id}")).StatusCode);
        Assert.Empty((await other.GetAsync<PagedResult<SupplierListItemDto>>("/api/suppliers")).Items);
        // And the same name is free there.
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsync("/api/suppliers", Firm("Visayas Steel Corp."))).StatusCode);
    }
}
