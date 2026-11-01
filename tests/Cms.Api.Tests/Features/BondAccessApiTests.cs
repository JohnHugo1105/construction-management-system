using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Bonds;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.BondTestKit;
using static Cms.Api.Tests.Features.CostingTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>Who may do what with bonds, companies kept apart, and the read paths' statement counts staying fixed as rows grow.</summary>
public sealed class BondAccessApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public BondAccessApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Managers_manage_readers_only_read_and_everyone_else_is_refused()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var bond = await CreateAsync(admin, project.Id);
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, user.TenantId);
        var (costEngineer, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, user.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, user.TenantId);
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, user.TenantId);
        var (store, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _m = manager;
        using var _ac = accountant;
        using var _ce = costEngineer;
        using var _v = viewer;
        using var _h = hr;
        using var _s = store;

        // A project manager and an accountant manage bonds.
        var byManager = await manager.PostAsync<BondDto>("/api/bonds", NewBond(project.Id));
        var extended = await accountant.ReadAsync<BondDto>(await ExtendAsync(accountant, byManager, DaysFromToday(200)));
        Assert.Equal(DaysFromToday(200), Date(extended.ValidUntil));

        // A cost engineer and a viewer read the register but change nothing.
        foreach (var reader in new[] { costEngineer, viewer })
        {
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/bonds")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/bonds/totals")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/bonds/{bond.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/projects/{project.Id}/bonds")).StatusCode);
            foreach (var refused in new[]
                     {
                         await reader.PostAsync("/api/bonds", NewBond(project.Id)),
                         await EditAsync(reader, bond, Edit(bond, note: "x")),
                         await ExtendAsync(reader, bond, DaysFromToday(300)),
                         await ReleaseAsync(reader, bond),
                         await CallAsync(reader, bond),
                         await VoidAsync(reader, bond)
                     })
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // An HR officer and a warehouse keeper may not even read them.
        foreach (var outsider in new[] { hr, store })
            foreach (var url in new[] { "/api/bonds", "/api/bonds/totals", $"/api/bonds/{bond.Id}", $"/api/projects/{project.Id}/bonds" })
                Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(url)).StatusCode);

        Assert.Equal("Active", (await admin.GetAsync<BondDto>($"/api/bonds/{bond.Id}")).Status);
    }

    [SqlServerFact]
    public async Task Another_company_cannot_see_or_change_a_bond_or_use_its_project()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var project = await CreateProjectAsync(mine);
        var bond = await CreateAsync(mine, project.Id);
        var theirProject = await CreateProjectAsync(theirs);
        var theirBond = await CreateAsync(theirs, theirProject.Id);

        // Another company's bond is "not found" for every read and every step, including a guessed id.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/bonds/{bond.Id}")).StatusCode);
        foreach (var refused in new[]
                 {
                     await EditAsync(theirs, bond, Edit(bond, note: "x")),
                     await ExtendAsync(theirs, bond, DaysFromToday(300)),
                     await ReleaseAsync(theirs, bond),
                     await CallAsync(theirs, bond),
                     await VoidAsync(theirs, bond)
                 })
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        // Another company's project cannot be listed, filtered by or given a bond.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/bonds")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/bonds?projectId={project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/bonds", NewBond(project.Id))).StatusCode);

        // Registers and totals hold only the company's own bonds.
        Assert.Equal(theirBond.Id, Assert.Single((await theirs.GetAsync<PagedResult<BondListItemDto>>("/api/bonds")).Items).Id);
        var totals = await theirs.GetAsync<BondTotalsDto>("/api/bonds/totals");
        Assert.Equal((1, 5_000_000_00L), (totals.Posted.ActiveCount, totals.Posted.ActiveAmountMinor));

        // Nothing of the other company's changed.
        var untouched = await mine.GetAsync<BondDto>($"/api/bonds/{bond.Id}");
        Assert.Equal((bond.Version, "Active"), (untouched.Version, untouched.Status));
    }

    [SqlServerFact]
    public async Task Every_read_costs_a_fixed_number_of_statements_however_many_bonds_exist()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var first = await CreateAsync(client, project.Id);

        async Task<(int List, int Filtered, int Get, int ProjectList, int Totals)> MeasureAsync(Guid bondId) => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/bonds?pageSize=100")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync($"/api/bonds?pageSize=100&projectId={project.Id}&direction=Posted&kind=Performance&status=Active&expiry=Valid")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/bonds/{bondId}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/bonds")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/bonds/totals")).StatusCode)));

        var small = await MeasureAsync(first.Id);

        // Thirty more bonds across two projects and directions, and a bond with many steps in its history.
        var second = await CreateProjectAsync(client);
        for (var index = 0; index < 15; index++)
        {
            await CreateAsync(client, project.Id, index % 2 == 0 ? "Posted" : "Received", "Warranty", DaysFromToday(20 + index));
            await CreateAsync(client, second.Id, "Posted", "Bid", DaysFromToday(-index));
        }
        var busy = first;
        for (var step = 1; step <= 12; step++)
            busy = await client.ReadAsync<BondDto>(await ExtendAsync(client, busy, DaysFromToday(100 + step)));

        var large = await MeasureAsync(busy.Id);

        Assert.Equal(small, large);
        // The organization (for today's date and currency) plus: the count and the page; the same with a project check; the bond and its
        // history; the project (with its sub-projects) and the rows; one grouped statement.
        Assert.Equal((3, 4, 3, 3, 2), large);
        Assert.Equal(13, busy.History.Count);
    }
}
