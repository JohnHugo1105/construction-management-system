using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Subcontracts;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;
using Cms.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.CostingTestKit;
using static Cms.Api.Tests.Features.SubcontractTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>Who may do what, companies kept apart, and the read paths' statement counts staying fixed as rows grow.</summary>
public sealed class SubcontractAccessApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public SubcontractAccessApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Drafters_draft_approvers_commit_money_and_others_only_read_or_nothing()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await SubcontractorAsync(admin);
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, user.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, user.TenantId);
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, user.TenantId);
        var (store, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _m = manager;
        using var _ac = accountant;
        using var _v = viewer;
        using var _h = hr;
        using var _s = store;

        // A project manager drafts; an accountant may not draft.
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync("/api/subcontracts", NewSubcontract(project.Id, firm))).StatusCode);
        var draft = await manager.PostAsync<SubcontractDto>("/api/subcontracts", NewSubcontract(project.Id, firm));
        var draftVariation = await manager.PostAsync<SubcontractDto>($"/api/subcontracts/{draft.Id}/variations",
            new { description = "More", amountMinor = 1_00, variationDate = ClaimDate, version = draft.Version });

        // Only an approver awards.
        Assert.Equal(HttpStatusCode.Forbidden, (await AwardRequest(manager, draftVariation)).StatusCode);
        var active = await AwardAsync(accountant, draftVariation);

        // Once awarded, a variation and a cancellation need an approver (the service says 403).
        var variation = new { description = "More", amountMinor = 1_00, variationDate = ClaimDate, version = active.Version };
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/subcontracts/{active.Id}/variations", variation)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await manager.PostAsync($"/api/subcontracts/{active.Id}/cancel", new { reason = "No", version = active.Version })).StatusCode);

        // The manager drafts the claim; only the accountant certifies and records payments.
        var drafted = await DraftAsync(manager, active, 100_000_00);
        var claimId = OpenDraft(drafted).Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await CertifyAsync(manager, drafted, claimId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DraftClaimAsync(accountant, drafted, 200_000_00)).StatusCode);
        var certified = await accountant.ReadAsync<SubcontractDto>(await CertifyAsync(accountant, drafted, claimId));
        Assert.Equal(HttpStatusCode.Forbidden, (await PayAsync(manager, certified, claimId, 1_00)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await VoidClaimAsync(manager, certified, claimId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PayAsync(accountant, certified, claimId, 1_00)).StatusCode);

        // A viewer reads but changes nothing; HR and the warehouse keeper don't see subcontracts at all.
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/subcontracts/{active.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/subcontracts", NewSubcontract(project.Id, firm))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/subcontracts/{active.Id}/claims/preview?cumulativeWorkMinor=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/subcontracts/{active.Id}/variations", variation)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/subcontracts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await store.GetAsync($"/api/subcontracts/{active.Id}")).StatusCode);
    }

    private static Task<HttpResponseMessage> AwardRequest(ApiClient client, SubcontractDto subcontract) =>
        client.PostAsync($"/api/subcontracts/{subcontract.Id}/award", new { version = subcontract.Version });

    [SqlServerFact]
    public async Task Another_company_never_reaches_a_subcontract_its_claims_or_its_firms()
    {
        var (owner, _) = await SignedInAsync(_factory);
        using var _o = owner;
        var project = await CreateProjectAsync(owner);
        var firm = await SubcontractorAsync(owner);
        var certified = await CertifiedAsync(owner, await AwardAsync(owner, await CreateAsync(owner, project.Id, firm)), 100_000_00);
        var claimId = ClaimNumbered(certified, 1).Id;

        var (other, _) = await SignedInAsync(_factory);
        using var _x = other;
        var theirProject = await CreateProjectAsync(other);
        var theirFirm = await SubcontractorAsync(other);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/subcontracts/{certified.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/subcontracts?projectId={project.Id}")).StatusCode);
        Assert.Empty((await other.GetAsync<PagedResult<SubcontractListItemDto>>("/api/subcontracts")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await AwardRequest(other, certified)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await VoidClaimAsync(other, certified, claimId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PayAsync(other, certified, claimId, 1_00)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync("/api/subcontracts", NewSubcontract(theirProject.Id, firm))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync("/api/subcontracts", NewSubcontract(project.Id, theirFirm))).StatusCode);

        // A claim id from another subcontract of the same company is not found on this one.
        var second = await AwardedAsync(owner, project.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await VoidClaimAsync(owner, second, claimId)).StatusCode);

        // Their dashboard has no commitment from our subcontract.
        Assert.Null((await other.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Single().Commitments);
    }

    [SqlServerFact]
    public async Task The_demo_data_is_valid_its_costs_equal_its_certified_claims_and_the_sequences_continue()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var created = await CreateProjectAsync(client, "Riverside Residences");
        await AddSubProjectAsync(client, created, "Tower A");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var organization = await db.Set<Organization>().IgnoreQueryFilters().SingleAsync(item => item.Id == user.TenantId);
            var project = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == created.Id);
            var now = DateTimeOffset.UtcNow;
            await SubcontractDemo.SeedAsync(db, organization, project, user.UserId, user.UserId, organization.TodayLocal(now), now);
            // A second run adds nothing.
            await SubcontractDemo.SeedAsync(db, organization, project, user.UserId, user.UserId, organization.TodayLocal(now), now);
        }

        var rows = (await client.GetAsync<PagedResult<SubcontractListItemDto>>($"/api/subcontracts?projectId={created.Id}")).Items;
        Assert.Equal(["SC-0001 Active", "SC-0002 Draft"], rows.Select(row => $"{row.Number} {row.Status}").Order());
        var electrical = await client.GetAsync<SubcontractDto>($"/api/subcontracts/{rows.Single(row => row.Number == "SC-0001").Id}");
        // 2,750,000.00 + 150,000.00 before VAT; claims of 900,000.00 and 750,000.00; claim 1 paid in full.
        Assert.Equal((2_900_000_00L, 1_650_000_00L, 1_848_000_00L, 184_800_00L, 741_000_00L, "Tower A"),
            (electrical.RevisedSumMinor, electrical.CertifiedWorkMinor, electrical.CertifiedGrossMinor, electrical.RetentionHeldMinor,
                electrical.OutstandingMinor, electrical.SubProjectName));
        Assert.Equal(1_848_000_00, (await client.GetAsync<CostSummaryDto>($"/api/projects/{created.Id}/cost-summary")).Position.ActualMinor);

        // The sequences continue after the demo's numbers.
        var next = await CreateAsync(client, created.Id, await SubcontractorAsync(client));
        Assert.Equal("SC-0003", next.Number);
        var third = await CertifiedAsync(client, electrical, 2_000_000_00);
        Assert.Contains(third.Claims, claim => claim.ClaimNumber == 3 && claim.ThisClaimBaseMinor == 350_000_00);
    }

    [SqlServerFact]
    public async Task The_list_the_page_and_the_commitments_cost_a_fixed_number_of_statements()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var first = await CertifiedAsync(client, await AwardedAsync(client, project.Id), 100_000_00);

        var listUrl = $"/api/subcontracts?projectId={project.Id}";
        var getUrl = $"/api/subcontracts/{first.Id}";
        async Task<(int List, int Get, int Dashboard)> MeasureAsync() =>
            (await StatementsAsync(_factory, () => client.GetAsync<PagedResult<SubcontractListItemDto>>(listUrl)),
             await StatementsAsync(_factory, () => client.GetAsync<SubcontractDto>(getUrl)),
             await StatementsAsync(_factory, () => client.GetAsync<DashboardDto>("/api/reports/dashboard")));

        var small = await MeasureAsync();

        // Ten more subcontracts, and more claims and payments on the first.
        for (var index = 0; index < 10; index++) await AwardedAsync(client, project.Id);
        var grown = first;
        for (var step = 2; step <= 6; step++)
        {
            grown = await CertifiedAsync(client, grown, step * 100_000_00);
            grown = await client.ReadAsync<SubcontractDto>(await PayAsync(client, grown, ClaimNumbered(grown, step).Id, 1_000_00));
        }
        Assert.Equal(11, (await client.GetAsync<PagedResult<SubcontractListItemDto>>(listUrl)).Total);
        Assert.Equal(6, (await client.GetAsync<SubcontractDto>(getUrl)).Claims.Count);

        var large = await MeasureAsync();
        Assert.Equal(small, large);
        // List: the project check (project and sub-projects), count and page. Page: subcontract, claims, payments, variations.
        Assert.True(large.List <= 4, $"The list took {large.List} statements.");
        Assert.True(large.Get <= 4, $"The page took {large.Get} statements.");
    }
}
