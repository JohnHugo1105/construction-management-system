using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Features.Costing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.CostingTestKit;

namespace Cms.Api.Tests.Features;

public sealed class CostingReferenceApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public CostingReferenceApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task The_reference_data_lists_the_work_breakdown_units_choices_and_the_company_vat_rate_in_plain_words()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var response = await client.GetAsync("/api/costing/reference");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Cacheable data, but the standard private/no-store header stays on.
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var reference = await client.ReadAsync<CostingReferenceDto>(response);

        Assert.Equal(Enumerable.Range(1, 16), reference.Divisions.Select(division => division.Number));
        var concrete = reference.Divisions.Single(division => division.Number == 3);
        Assert.Equal("Concrete", concrete.MainScope);
        Assert.Contains(new WbsScopeDto("3300", "Cast-In-Place Concrete"), concrete.Scopes);
        Assert.All(reference.Divisions, division => Assert.NotEmpty(division.Scopes));
        Assert.Equal(WbsCatalog.Entries.Count, reference.Divisions.Sum(division => division.Scopes.Count));

        Assert.Contains("pc", reference.Units);
        Assert.Contains("m³", reference.Units);
        Assert.Equal(UnitOfMeasure.Suggestions, reference.Units);

        Assert.Equal(["Material", "Labor", "Equipment", "Subcontract", "Other"], reference.ItemTypes.Select(item => item.Value));
        Assert.Equal(["Material", "Labor", "Equipment", "Subcontract work", "Other"], reference.ItemTypes.Select(item => item.Label));

        Assert.Equal(["Inclusive", "Exclusive", "Exempt"], reference.VatTreatments.Select(item => item.Value));
        Assert.Equal(["Unit cost includes VAT", "Unit cost is before VAT", "No VAT"], reference.VatTreatments.Select(item => item.Label));
        Assert.All(reference.VatTreatments, item => Assert.False(string.IsNullOrWhiteSpace(item.Hint)));
        Assert.Equal(1_200, reference.VatRateBps);

        // The values it offers are the values the line endpoints accept.
        var project = await CreateProjectAsync(client);
        foreach (var itemType in reference.ItemTypes)
        foreach (var vat in reference.VatTreatments)
            Assert.Equal(HttpStatusCode.Created,
                (await client.PostAsync($"/api/projects/{project.Id}/budget-lines", Line(itemType: itemType.Value, vatTreatment: vat.Value))).StatusCode);

        // The company's rate follows a settings change.
        await ChangeOrganizationAsync(client, vatRateBps: 1_000);
        Assert.Equal(1_000, (await client.GetAsync<CostingReferenceDto>("/api/costing/reference")).VatRateBps);
    }

    [SqlServerFact]
    public async Task Only_people_who_can_read_costs_may_load_the_reference_data_and_each_company_sees_its_own_rate()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/costing/reference")).StatusCode);
        }

        foreach (var role in new[] { AccessRole.Viewer, AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _r = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/costing/reference")).StatusCode);
        }

        using var anonymous = new ApiClient(_factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/costing/reference")).StatusCode);

        await ChangeOrganizationAsync(admin, vatRateBps: 500);
        var (other, _) = await SignedInAsync(_factory);
        using var _o = other;
        Assert.Equal(1_200, (await other.GetAsync<CostingReferenceDto>("/api/costing/reference")).VatRateBps);
        Assert.Equal(500, (await admin.GetAsync<CostingReferenceDto>("/api/costing/reference")).VatRateBps);
    }
}
