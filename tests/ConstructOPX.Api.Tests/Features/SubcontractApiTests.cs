using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.SubcontractTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>The subcontract workflow on the real API and SQL Server: figures to the centavo, the cost it posts, and every refusal.</summary>
public sealed class SubcontractApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public SubcontractApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_subcontract_runs_from_draft_to_retention_released_with_exact_figures()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await SubcontractorAsync(client, "Dela Cruz Electrical");

        var draft = await CreateAsync(client, project.Id, firm);
        Assert.Equal(("SC-0001", "Draft", "Dela Cruz Electrical", 1_200), (draft.Number, draft.Status, draft.SubcontractorName, draft.VatRateBps));
        Assert.Equal((1_000_000_00L, 1_000_000_00L, 120_000_00L, 1_120_000_00L),
            (draft.RevisedSumMinor, draft.RevisedNetMinor, draft.RevisedVatMinor, draft.RevisedGrossMinor));

        var active = await AwardAsync(client, draft);
        Assert.Equal("Active", active.Status);
        Assert.Equal("Test Admin", active.AwardedByName);

        // A variation adds 200,000.00 of work.
        active = await client.PostAsync<SubcontractDto>($"/api/subcontracts/{active.Id}/variations",
            new { description = "Lobby lighting", amountMinor = 200_000_00, variationDate = ClaimDate, version = active.Version });
        Assert.Equal((200_000_00L, 1_200_000_00L, 1_344_000_00L), (active.VariationsMinor, active.RevisedSumMinor, active.RevisedGrossMinor));

        // Claim 1: 250,000.00 of work before VAT.
        var preview = await client.GetAsync<ClaimPreviewDto>($"/api/subcontracts/{active.Id}/claims/preview?cumulativeWorkMinor=25000000");
        Assert.Equal(new ClaimPreviewDto(250_000_00, 0, 250_000_00, 250_000_00, 30_000_00, 280_000_00, 28_000_00, 5_000_00, 247_000_00), preview);
        var one = await CertifiedAsync(client, active, 250_000_00);
        var claim1 = ClaimNumbered(one, 1);
        Assert.Equal((preview.NetMinor, preview.VatMinor, preview.GrossMinor, preview.RetentionMinor, preview.WithholdingMinor, preview.NetPayableMinor),
            (claim1.NetMinor, claim1.VatMinor, claim1.GrossMinor, claim1.RetentionMinor, claim1.WithholdingMinor, claim1.NetPayableMinor));

        // Claim 2: 600,000.00 to date, so 350,000.00 more.
        var two = await CertifiedAsync(client, one, 600_000_00);
        var claim2 = ClaimNumbered(two, 2);
        Assert.Equal((350_000_00L, 392_000_00L, 39_200_00L, 7_000_00L, 345_800_00L),
            (claim2.ThisClaimBaseMinor, claim2.GrossMinor, claim2.RetentionMinor, claim2.WithholdingMinor, claim2.NetPayableMinor));
        Assert.Equal((600_000_00L, 600_000_00L, 5_000, 67_200_00L, 592_800_00L, 592_800_00L),
            (two.CertifiedWorkMinor, two.RemainingWorkMinor, two.PercentCertifiedBps!.Value, two.RetentionHeldMinor, two.NetPayableMinor, two.OutstandingMinor));

        // Pay claim 1 in full and part of claim 2.
        var paid = await client.ReadAsync<SubcontractDto>(await PayAsync(client, two, claim1.Id, 247_000_00));
        paid = await client.ReadAsync<SubcontractDto>(await PayAsync(client, paid, claim2.Id, 100_000_00));
        Assert.Equal((347_000_00L, 245_800_00L), (paid.PaidOnClaimsMinor, paid.OutstandingMinor));
        Assert.Equal((0L, 245_800_00L), (ClaimNumbered(paid, 1).OutstandingMinor, ClaimNumbered(paid, 2).OutstandingMinor));

        // Complete, then release part of the retention.
        var completed = await client.PostAsync<SubcontractDto>($"/api/subcontracts/{paid.Id}/complete", new { version = paid.Version });
        Assert.Equal("Completed", completed.Status);
        var released = await client.PostAsync<SubcontractDto>($"/api/subcontracts/{completed.Id}/retention-releases", Payment(60_000_00, completed.Version));
        Assert.Equal((67_200_00L, 60_000_00L, 7_200_00L), (released.RetentionHeldMinor, released.RetentionReleasedMinor, released.RetentionBalanceMinor));
        Assert.Contains(released.Payments, payment => payment is { Kind: "RetentionRelease", AmountMinor: 60_000_00, ClaimNumber: null });

        var row = (await client.GetAsync<PagedResult<SubcontractListItemDto>>($"/api/subcontracts?projectId={project.Id}")).Items.Single();
        Assert.Equal(("Completed", 1_200_000_00L, 600_000_00L, 5_000, 7_200_00L, 407_000_00L, 245_800_00L),
            (row.Status, row.RevisedSumMinor, row.CertifiedWorkMinor, row.PercentCertifiedBps!.Value, row.RetentionHeldMinor, row.PaidMinor, row.OutstandingMinor));
    }

    [SqlServerFact]
    public async Task Certifying_posts_the_claim_as_project_cost_and_voiding_the_claim_voids_that_cost()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        // VAT included, so the posted net is split out of the claim: 250,000.00 gives net 223,214.29 and VAT 26,785.71.
        var active = await AwardedAsync(client, vat: "Inclusive");
        var certified = await CertifiedAsync(client, active, 250_000_00);
        var claim = ClaimNumbered(certified, 1);

        var cost = await client.GetAsync<CostEntryDto>($"/api/cost-entries/{claim.CostEntryId}");
        Assert.Equal(("Subcontract", claim.Id, "Subcontract", 1m, "l.s", 250_000_00L, "Inclusive", 1_200),
            (cost.Origin, cost.OriginReferenceId!.Value, cost.ItemType, cost.Quantity, cost.Unit, cost.UnitCostMinor, cost.VatTreatment, cost.VatRateBps));
        Assert.Equal((223_214_29L, 26_785_71L, 250_000_00L), (cost.NetMinor, cost.VatMinor, cost.GrossMinor));
        Assert.Equal((claim.NetMinor, claim.VatMinor, claim.GrossMinor), (cost.NetMinor, cost.VatMinor, cost.GrossMinor));
        Assert.Equal((certified.SubcontractorName, new DateOnly(2025, 12, 1), "SC-0001 claim 1: Electrical works", false),
            (cost.SourceDealer, cost.EntryDate, cost.Description, cost.CanEdit));

        // The cost is corrected through the claim, not at the entry.
        var atEntry = await client.PostAsync($"/api/cost-entries/{cost.Id}/void", new { reason = "Wrong", version = cost.Version });
        Assert.Equal(HttpStatusCode.Conflict, atEntry.StatusCode);
        Assert.Contains("Void the certificate instead", await DetailAsync(atEntry));

        var voided = await client.ReadAsync<SubcontractDto>(await VoidClaimAsync(client, certified, claim.Id));
        Assert.Equal(("Voided", 0L, 0L), (voided.Claims.Single().Status, voided.CertifiedWorkMinor, voided.NetPayableMinor));
        var after = await client.GetAsync<CostEntryDto>($"/api/cost-entries/{claim.CostEntryId}");
        Assert.Equal(("Voided", "Measured wrongly"), (after.Status, after.VoidReason));
        Assert.Equal(0, (await client.GetAsync<CostSummaryDto>($"/api/projects/{certified.ProjectId}/cost-summary")).Position.ActualMinor);

        // A new claim numbers on: claim numbers are never reused.
        var again = await CertifiedAsync(client, voided, 100_000_00);
        Assert.Equal(2, again.Claims.Single(item => item.Status == "Certified").ClaimNumber);
    }

    [SqlServerFact]
    public async Task A_certified_claim_is_counted_under_the_subcontracts_work_item_not_as_unclassified()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CostingTestKit.CreateProjectAsync(client);
        var firm = await SubcontractorAsync(client);

        // An unknown code is refused with its field, and a draft with a standard code keeps it.
        var bad = NewSubcontract(project.Id, firm);
        bad["wbsCode"] = "9999-nope";
        Assert.Contains("wbsCode", await ErrorFieldsAsync(await client.PostAsync("/api/subcontracts", bad)));
        var good = NewSubcontract(project.Id, firm);
        good["wbsCode"] = "3300";
        var draft = await client.PostAsync<SubcontractDto>("/api/subcontracts", good);
        Assert.Equal(("3300", 3), (draft.WbsCode, draft.Division));
        Assert.False(string.IsNullOrWhiteSpace(draft.WorkItem));

        var awarded = await AwardAsync(client, draft);
        var certified = await CertifiedAsync(client, awarded, 250_000_00);
        var cost = await client.GetAsync<CostEntryDto>($"/api/cost-entries/{ClaimNumbered(certified, 1).CostEntryId}");
        Assert.Equal((3, "3300"), (cost.Division, cost.WbsCode));

        // The cost summary counts it in Division 3, not in "Unclassified", so it is compared with the budget for that work.
        var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");
        Assert.Equal(3, Assert.Single(summary.ByDivision).Division);

        // Once awarded the work item is fixed: it decides where every claim lands.
        var change = await client.PutAsync($"/api/subcontracts/{certified.Id}", new
        {
            subcontractorId = firm, subProjectId = (Guid?)null, title = "Electrical works", notes = (string?)null, contractSumMinor = 1_000_000_00,
            vatTreatment = "Exclusive", retentionBps = 1_000, withholdingBps = 200, version = certified.Version, wbsCode = "3400"
        });
        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
    }

    [SqlServerFact]
    public async Task Once_all_the_work_is_certified_nothing_is_left_committed_even_when_each_claim_rounds_its_own_vat()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        // 2,000,008 centavos before 12% VAT: the whole sum is 2,240,008.96 -> VAT 240,001 (rounded once) but two claims of 1,000,004 each give
        // VAT 120,000 + 120,000, a centavo less than the whole. That centavo is not a commitment.
        var awarded = await AwardedAsync(client, sumMinor: 2_000_008, vat: "Exclusive");
        var first = await CertifiedAsync(client, awarded, 1_000_004);
        var done = await CertifiedAsync(client, first, 2_000_008);

        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");

        Assert.Null(dashboard.Projects.Single(item => item.Id == done.ProjectId).Commitments);
        Assert.Null((await client.GetAsync<CostSummaryDto>($"/api/projects/{done.ProjectId}/cost-summary")).Commitments);
    }

    [SqlServerFact]
    public async Task A_subcontract_with_no_work_item_still_posts_unclassified_cost()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var certified = await CertifiedAsync(client, await AwardedAsync(client), 100_000_00);

        var cost = await client.GetAsync<CostEntryDto>($"/api/cost-entries/{ClaimNumbered(certified, 1).CostEntryId}");

        Assert.Null(cost.Division);
        Assert.Null(cost.WbsCode);
    }

    [SqlServerFact]
    public async Task The_dashboard_commitment_shrinks_as_claims_are_certified_and_ends_at_completion()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client, contractPriceMinor: 2_000_000_00);
        var firm = await SubcontractorAsync(client);
        var draft = await CreateAsync(client, project.Id, firm);

        async Task<(long? Committed, long Actual)> PositionAsync()
        {
            var row = (await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Single(item => item.Id == project.Id);
            var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");
            Assert.Equal(row.Commitments?.CommittedMinor, summary.Commitments?.CommittedMinor);
            return (row.Commitments?.CommittedMinor, row.ActualMinor);
        }

        // A draft commits nothing.
        Assert.Equal((null, 0L), await PositionAsync());

        var active = await AwardAsync(client, draft);
        Assert.Equal((1_120_000_00L, 0L), await PositionAsync());

        var one = await CertifiedAsync(client, active, 250_000_00);
        Assert.Equal((840_000_00L, 280_000_00L), await PositionAsync());

        // Committed plus actual is still the revised sum with VAT: nothing is counted twice.
        var two = await CertifiedAsync(client, one, 600_000_00);
        Assert.Equal((448_000_00L, 672_000_00L), await PositionAsync());

        await client.PostAsync<SubcontractDto>($"/api/subcontracts/{two.Id}/complete", new { version = two.Version });
        Assert.Equal((null, 672_000_00L), await PositionAsync());
    }

    [SqlServerFact]
    public async Task Claims_are_refused_over_the_sum_on_a_stale_chain_out_of_order_and_with_payments()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var active = await AwardedAsync(client);

        // Over the revised sum, and nothing added: field errors.
        var over = await DraftClaimAsync(client, active, 1_000_000_01);
        Assert.Equal("Work done to date can't be more than the revised contract sum of ₱1,000,000.00. Record a variation first if the work grew.",
            await ErrorMessageAsync(over, "cumulativeWorkMinor"));
        var one = await CertifiedAsync(client, active, 250_000_00);
        Assert.Equal(new HashSet<string> { "cumulativeWorkMinor" }, await ErrorFieldsAsync(await DraftClaimAsync(client, one, 250_000_00)));

        // A stale chain: claim 2 is drafted on claim 1, then claim 1 is voided.
        var drafted = await DraftAsync(client, one, 600_000_00);
        var voidedFirst = await client.ReadAsync<SubcontractDto>(await VoidClaimAsync(client, drafted, ClaimNumbered(drafted, 1).Id));
        var stale = OpenDraft(voidedFirst);
        Assert.True(stale.IsStale);
        var refused = await CertifyAsync(client, voidedFirst, stale.Id);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(refused));
        Assert.Contains("Open the claim and save it again", await DetailAsync(refused));

        // Saving it again brings it up to date; then it certifies as the whole 600,000.00.
        var resaved = await client.PutAsync<SubcontractDto>($"/api/subcontracts/{voidedFirst.Id}/claims/{stale.Id}", Claim(600_000_00, voidedFirst.Version));
        Assert.False(OpenDraft(resaved).IsStale);
        var certified = await client.ReadAsync<SubcontractDto>(await CertifyAsync(client, resaved, stale.Id));
        Assert.Equal(600_000_00, ClaimNumbered(certified, 2).ThisClaimBaseMinor);

        // Claim 3, then try to void claim 2 (not the latest), and claim 3 with a payment on it.
        var three = await CertifiedAsync(client, certified, 700_000_00);
        var notLatest = await VoidClaimAsync(client, three, ClaimNumbered(three, 2).Id);
        Assert.Equal("Only the latest certified claim can be voided. Void claim 3 first.", await DetailAsync(notLatest));
        var paid = await client.ReadAsync<SubcontractDto>(await PayAsync(client, three, ClaimNumbered(three, 3).Id, 10_000_00));
        var withPayment = await VoidClaimAsync(client, paid, ClaimNumbered(paid, 3).Id);
        Assert.Equal(HttpStatusCode.Conflict, withPayment.StatusCode);
        Assert.Contains("Payments are recorded against this claim", await DetailAsync(withPayment));

        // Overpaying claim 3 (net payable 98,800.00, 10,000.00 already paid) is a field error.
        var overpay = await PayAsync(client, paid, ClaimNumbered(paid, 3).Id, 88_800_01);
        Assert.Equal("That is more than the ₱88,800.00 still owed on this claim.", await ErrorMessageAsync(overpay, "amountMinor"));

        // Retention can't be released before completion.
        var early = await client.PostAsync($"/api/subcontracts/{paid.Id}/retention-releases", Payment(1_00, paid.Version));
        Assert.Contains("Complete it first", await DetailAsync(early));
    }

    [SqlServerFact]
    public async Task A_stale_version_and_an_archived_project_are_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var active = await AwardedAsync(client, project.Id);
        var drafted = await DraftAsync(client, active, 100_000_00);

        // Certifying with the version from before the draft was added.
        var stale = await CertifyAsync(client, active, OpenDraft(drafted).Id);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));

        var certified = await client.ReadAsync<SubcontractDto>(await CertifyAsync(client, drafted, OpenDraft(drafted).Id));
        var current = await client.GetAsync<ConstructOPX.Application.Features.Projects.ProjectDto>($"/api/projects/{project.Id}");
        await ArchiveAsync(client, current);

        var read = await client.GetAsync<SubcontractDto>($"/api/subcontracts/{certified.Id}");
        Assert.True(read.ProjectArchived);
        foreach (var refused in new[]
                 {
                     await VoidClaimAsync(client, read, ClaimNumbered(read, 1).Id),
                     await PayAsync(client, read, ClaimNumbered(read, 1).Id, 1_00),
                     await DraftClaimAsync(client, read, 200_000_00),
                     await client.PostAsync($"/api/subcontracts/{read.Id}/complete", new { version = read.Version })
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Contains("archived", await DetailAsync(refused));
        }
    }

    [SqlServerFact]
    public async Task Every_problem_with_a_new_subcontract_is_reported_at_once_and_a_supplier_only_firm_is_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await SubcontractorAsync(client);

        var bad = await client.PostAsync("/api/subcontracts",
            NewSubcontract(project.Id, firm, sumMinor: 0, vat: "Sometimes", retentionBps: 3_001, withholdingBps: -1, title: " "));
        Assert.Equal(new HashSet<string> { "contractSumMinor", "vatTreatment", "retentionBps", "withholdingBps", "title" }, await ErrorFieldsAsync(bad));

        var supplierOnly = await SubcontractorAsync(client, subcontractor: false);
        var wrongKind = await client.PostAsync("/api/subcontracts", NewSubcontract(project.Id, supplierOnly));
        Assert.Equal(HttpStatusCode.Conflict, wrongKind.StatusCode);
        Assert.Contains("isn't set up as a subcontractor", await DetailAsync(wrongKind));

        // Defaults: 10% retention and no withholding when not given.
        var defaults = await client.PostAsync<SubcontractDto>("/api/subcontracts", NewSubcontract(project.Id, firm, retentionBps: null, withholdingBps: null));
        Assert.Equal((1_000, 0), (defaults.RetentionBps, defaults.WithholdingBps));

        // Unknown list filter.
        Assert.Contains("status", await ErrorFieldsAsync(await client.GetAsync("/api/subcontracts?status=Paid")));
    }

    [SqlServerFact]
    public async Task Repeated_requests_return_the_first_result_and_a_reused_request_id_is_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await SubcontractorAsync(client);
        var requestId = Guid.NewGuid().ToString();

        var first = await client.PostAsync<SubcontractDto>("/api/subcontracts", NewSubcontract(project.Id, firm, clientRequestId: requestId));
        var second = await client.PostAsync<SubcontractDto>("/api/subcontracts", NewSubcontract(project.Id, firm, clientRequestId: requestId));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, (await client.GetAsync<PagedResult<SubcontractListItemDto>>($"/api/subcontracts?projectId={project.Id}")).Total);
        var otherProject = await CreateProjectAsync(client);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync("/api/subcontracts", NewSubcontract(otherProject.Id, firm, clientRequestId: requestId))).StatusCode);

        var active = await AwardAsync(client, first);
        var claimRequest = Guid.NewGuid().ToString();
        var drafted = await client.ReadAsync<SubcontractDto>(await DraftClaimAsync(client, active, 100_000_00, claimRequest));
        // The repeat carries the version the form was opened with, which is now stale: it still answers with the first draft.
        var repeated = await client.ReadAsync<SubcontractDto>(await DraftClaimAsync(client, active, 100_000_00, claimRequest));
        Assert.Single(repeated.Claims);
        Assert.Equal(drafted.Claims.Single().Id, repeated.Claims.Single().Id);

        var certified = await client.ReadAsync<SubcontractDto>(await CertifyAsync(client, drafted, OpenDraft(drafted).Id));
        var claimId = ClaimNumbered(certified, 1).Id;
        var paymentRequest = Guid.NewGuid().ToString();
        await client.ReadAsync<SubcontractDto>(await PayAsync(client, certified, claimId, 5_000_00, paymentRequest));
        var paidTwice = await client.ReadAsync<SubcontractDto>(await PayAsync(client, certified, claimId, 5_000_00, paymentRequest));
        Assert.Single(paidTwice.Payments);
        Assert.Equal(5_000_00, paidTwice.PaidOnClaimsMinor);

        // A payment request id used again for a draft claim is something else entirely.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/subcontracts/{paidTwice.Id}/retention-releases",
            Payment(1_00, paidTwice.Version, paymentRequest))).StatusCode);
    }

    [SqlServerFact]
    public async Task Cancelling_and_variations_follow_the_state()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await SubcontractorAsync(client);

        var draft = await CreateAsync(client, project.Id, firm);
        var edited = await client.PutAsync<SubcontractDto>($"/api/subcontracts/{draft.Id}", new
        {
            subcontractorId = firm, subProjectId = (Guid?)null, title = "Electrical works, revised", notes = "Phase 1", contractSumMinor = 900_000_00,
            vatTreatment = "Exempt", retentionBps = 500, withholdingBps = 100, version = draft.Version
        });
        Assert.Equal(("Electrical works, revised", 900_000_00L, 900_000_00L, 500), (edited.Title, edited.RevisedSumMinor, edited.RevisedGrossMinor, edited.RetentionBps));

        var cancelled = await client.PostAsync<SubcontractDto>($"/api/subcontracts/{edited.Id}/cancel", new { reason = "Scope moved in-house", version = edited.Version });
        Assert.Equal(("Cancelled", "Scope moved in-house"), (cancelled.Status, cancelled.CancelReason));

        var active = await AwardedAsync(client, project.Id);
        var certified = await CertifiedAsync(client, active, 100_000_00);
        var cancel = await client.PostAsync($"/api/subcontracts/{certified.Id}/cancel", new { reason = "No", version = certified.Version });
        Assert.Contains("can't be cancelled", await DetailAsync(cancel));
        var belowCertified = await client.PostAsync($"/api/subcontracts/{certified.Id}/variations",
            new { description = "Omit", amountMinor = -900_000_01, variationDate = ClaimDate, version = certified.Version });
        Assert.Equal("The revised contract sum can't be less than the ₱100,000.00 of work already certified.",
            await ErrorMessageAsync(belowCertified, "amountMinor"));
    }
}
