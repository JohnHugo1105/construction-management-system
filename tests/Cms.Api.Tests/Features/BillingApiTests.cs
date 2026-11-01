using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Billing;
using Cms.Application.Features.Projects;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;
using Cms.Infrastructure.Seeding;
using Cms.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.CostingTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>Client billing end to end on SQL Server: the money, the chain of bills, payments, who may do what, and that reads stay cheap.</summary>
public sealed class BillingApiTests : IClassFixture<CmsApiFactory>
{
    // A ₱11,200,000.00 contract, VAT included.
    private const long Price = 1_120_000_000;
    private const string BillDate = "2026-09-30";

    private readonly CmsApiFactory _factory;

    public BillingApiTests(CmsApiFactory factory) => _factory = factory;

    // ---- Helpers ------------------------------------------------------------------------------------------------------------

    private static Task<ProjectDto> PricedProjectAsync(ApiClient client, long? price = Price) => CreateProjectAsync(client, contractPriceMinor: price);

    private static Task<BillingTermsDto> TermsAsync(ApiClient client, Guid projectId, int retention = 1_000, int withholding = 200, int days = 30, int version = 0,
        int? advance = null, int? vatWithholding = null, bool? government = null) =>
        client.PutAsync<BillingTermsDto>($"/api/projects/{projectId}/billing/terms", TermsBody(retention, withholding, days, version, advance, vatWithholding, government));

    private static object TermsBody(int retention = 1_000, int withholding = 200, int days = 30, int version = 0, int? advance = null, int? vatWithholding = null,
        bool? government = null) =>
        new
        {
            retentionBps = retention, creditableWithholdingBps = withholding, paymentTermsDays = days, version, advanceBps = advance,
            vatWithholdingBps = vatWithholding, isGovernmentClient = government
        };

    private static Task<ClientInvoiceDto> AdvanceDraftAsync(ApiClient client, Guid projectId, string? requestId = null) =>
        client.PostAsync<ClientInvoiceDto>($"/api/projects/{projectId}/billing/invoices",
            new { kind = "Advance", billingDate = BillDate, description = "Down payment", clientRequestId = requestId });

    private static object ProgressBody(long cumulative, string? requestId = null, string date = BillDate, string? description = "Progress billing") =>
        new { kind = "Progress", billingDate = date, description, cumulativeWorkMinor = cumulative, clientRequestId = requestId };

    private static Task<HttpResponseMessage> PostDraftAsync(ApiClient client, Guid projectId, long cumulative, string? requestId = null) =>
        client.PostAsync($"/api/projects/{projectId}/billing/invoices", ProgressBody(cumulative, requestId));

    private static Task<ClientInvoiceDto> DraftAsync(ApiClient client, Guid projectId, long cumulative, string? requestId = null) =>
        client.PostAsync<ClientInvoiceDto>($"/api/projects/{projectId}/billing/invoices", ProgressBody(cumulative, requestId));

    private static Task<ClientInvoiceDto> IssueAsync(ApiClient client, ClientInvoiceDto bill) =>
        client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}/issue", new { version = bill.Version });

    private static async Task<ClientInvoiceDto> IssuedAsync(ApiClient client, Guid projectId, long cumulative) =>
        await IssueAsync(client, await DraftAsync(client, projectId, cumulative));

    private static object PaymentBody(ClientInvoiceDto bill, long amount, string? requestId = null, string paidOn = "2026-10-15", string? method = "BankTransfer") =>
        new { amountMinor = amount, paidOn, method, reference = "BDO 1234", note = (string?)null, clientRequestId = requestId, version = bill.Version };

    private static Task<HttpResponseMessage> PostPaymentAsync(ApiClient client, ClientInvoiceDto bill, long amount, string? requestId = null) =>
        client.PostAsync($"/api/client-invoices/{bill.Id}/payments", PaymentBody(bill, amount, requestId));

    private static Task<ClientInvoiceDto> PayAsync(ApiClient client, ClientInvoiceDto bill, long amount, string? requestId = null) =>
        client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}/payments", PaymentBody(bill, amount, requestId));

    private static Task<ProjectBillingDto> BillingAsync(ApiClient client, Guid projectId) => client.GetAsync<ProjectBillingDto>($"/api/projects/{projectId}/billing");

    private static async Task<ProjectDto> CompleteAsync(ApiClient client, ProjectDto project)
    {
        var started = await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });
        return await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/complete", new { version = started.Version });
    }

    private static async Task ExpectRuleAsync(HttpResponseMessage response, string detailStart)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(response));
        Assert.StartsWith(detailStart, await DetailAsync(response));
    }

    // ---- The money ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Successive_progress_bills_are_previewed_drafted_issued_numbered_and_paid_with_exact_figures()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);

        var defaults = await BillingAsync(client, project.Id);
        Assert.Equal(new BillingTermsDto(1_000, 0, 30, 0, 0, false, true, 0), defaults.Terms);
        Assert.Equal((0L, 0L, 0L, (Guid?)null, (long?)null), (defaults.AdvanceBilledMinor, defaults.AdvanceRecoveredMinor, defaults.AdvanceToRecoverMinor,
            defaults.AdvanceBillId, defaults.AdvanceToBillMinor));
        Assert.Empty(defaults.Bills.Items);
        var terms = await TermsAsync(client, project.Id);
        Assert.Equal((1_000, 200, 30, false), (terms.RetentionBps, terms.CreditableWithholdingBps, terms.PaymentTermsDays, terms.IsDefault));

        // ₱2,240,000.00 to date: net 2,000,000.00, VAT 240,000.00, retention 224,000.00, withholding 2% of net 40,000.00.
        var preview = await client.GetAsync<BillPreviewDto>($"/api/projects/{project.Id}/billing/preview?cumulativeWorkMinor=224000000&billingDate={BillDate}");
        Assert.Equal(new BillPreviewDto(Price, 0, 224_000_000, 224_000_000, 200_000_000, 24_000_000, 1_200, 1_000, 22_400_000, 200, 4_000_000, 0, 0, 0, 197_600_000, 30,
            new DateOnly(2026, 10, 30)), preview);

        var draft = await DraftAsync(client, project.Id, 224_000_000);
        Assert.Equal(("Draft", (string?)null, false, false), (draft.Status, draft.Number, draft.IsStale, draft.IsLatestIssued));
        Assert.Equal((preview.GrossMinor, preview.RetentionMinor, preview.WithholdingMinor, preview.NetReceivableMinor),
            (draft.GrossMinor, draft.RetentionMinor, draft.WithholdingMinor, draft.NetReceivableMinor));
        Assert.Equal(0, (await BillingAsync(client, project.Id)).BilledToDateMinor);

        var first = await IssueAsync(client, draft);
        Assert.Equal(("Issued", "BILL-0001", true, 197_600_000L), (first.Status, first.Number, first.IsLatestIssued, first.OutstandingMinor));
        Assert.Equal("Test Admin", first.IssuedByName);

        // ₱5,600,000.00 to date: this bill is 3,360,000.00 (net 3,000,000.00, VAT 360,000.00, retention 336,000.00, withholding 60,000.00).
        var second = await IssuedAsync(client, project.Id, 560_000_000);
        Assert.Equal(("BILL-0002", 224_000_000L, 336_000_000L, 300_000_000L, 36_000_000L), (second.Number, second.PreviousCumulativeWorkMinor,
            second.GrossMinor, second.NetMinor, second.VatMinor));
        Assert.Equal((33_600_000L, 6_000_000L, 296_400_000L), (second.RetentionMinor, second.WithholdingMinor, second.NetReceivableMinor));
        // Issuing the second bill changed the first (it now points at its successor): it is no longer the latest, and has a new version.
        first = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{first.Id}");
        Assert.False(first.IsLatestIssued);

        var paid = await PayAsync(client, first, 100_000_000);
        Assert.Equal((100_000_000L, 97_600_000L), (paid.PaidMinor, paid.OutstandingMinor));
        var payment = Assert.Single(paid.Payments);
        Assert.Equal(("BankTransfer", "BDO 1234", new DateOnly(2026, 10, 15), false), (payment.Method, payment.Reference, payment.PaidOn, payment.IsVoided));

        // Both bills fell due in October; the date the tests run is later, so everything still to collect is overdue.
        var summary = await BillingAsync(client, project.Id);
        Assert.Equal((Price, 560_000_000L, (int?)5_000, (long?)560_000_000), (summary.ContractPriceMinor!.Value, summary.BilledToDateMinor,
            summary.PercentBilledBps, summary.RemainingToBillMinor));
        Assert.Equal((56_000_000L, 0L, 56_000_000L), (summary.RetentionWithheldMinor, summary.RetentionReleasedMinor, summary.RetentionHeldMinor));
        Assert.Equal((100_000_000L, 97_600_000L + 296_400_000L, 97_600_000L + 296_400_000L, 2),
            (summary.CollectedMinor, summary.OutstandingMinor, summary.OverdueMinor, summary.OverdueCount));
        Assert.False(summary.CanReleaseRetention);
        Assert.Equal(["BILL-0002", "BILL-0001"], summary.Bills.Items.Select(bill => bill.Number));
        Assert.True(summary.Bills.Items.All(bill => bill.IsOverdue));

        var receivables = await client.GetAsync<ReceivablesDto>("/api/billing/receivables");
        var row = Assert.Single(receivables.Rows.Items);
        Assert.Equal(new ReceivableRowDto(project.Id, project.Name, "Planning", 560_000_000, 100_000_000, 394_000_000, 394_000_000, 56_000_000), row);
        Assert.Equal((394_000_000L, 394_000_000L, 56_000_000L), (receivables.OutstandingMinor, receivables.OverdueMinor, receivables.RetentionHeldMinor));

        var overdue = await client.GetAsync<PagedResult<ClientInvoiceListItemDto>>($"/api/client-invoices?overdue=true&projectId={project.Id}");
        Assert.Equal(2, overdue.Total);
        Assert.Equal([first.Id], (await client.GetAsync<PagedResult<ClientInvoiceListItemDto>>("/api/client-invoices?search=BILL-0001")).Items.Select(bill => bill.Id));
    }

    [SqlServerFact]
    public async Task A_down_payment_is_billed_first_recovered_by_each_progress_bill_with_VAT_withheld_and_given_back_when_a_bill_is_voided()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);

        // A government client allows at most 15% down.
        var tooMuch = await client.PutAsync($"/api/projects/{project.Id}/billing/terms", TermsBody(advance: 2_000, vatWithholding: 500, government: true));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        Assert.Equal(new HashSet<string> { "advanceBps" }, await ErrorFieldsAsync(tooMuch));
        var terms = await TermsAsync(client, project.Id, advance: 1_500, vatWithholding: 500, government: true);
        Assert.Equal(new BillingTermsDto(1_000, 200, 30, 1_500, 500, true, false, terms.Version), terms);
        Assert.Equal(terms, (await BillingAsync(client, project.Id)).Terms);

        // A progress draft prepared before the down payment is billed recovers nothing yet.
        var early = await DraftAsync(client, project.Id, 224_000_000);
        Assert.Equal(0, early.AdvanceRecoveryMinor);

        // 15% of ₱11,200,000.00: the down payment the tab offers to bill, and what the bill asks for (no VAT split, nothing withheld).
        Assert.Equal(168_000_000, (await BillingAsync(client, project.Id)).AdvanceToBillMinor);
        var advance = await AdvanceDraftAsync(client, project.Id);
        Assert.Equal(("Advance", "Draft", 168_000_000L, 0L, 0L, 0L), (advance.Kind, advance.Status, advance.GrossMinor, advance.VatMinor, advance.RetentionMinor,
            advance.WithholdingMinor));
        Assert.Equal((168_000_000L, 1_500, (long?)null), (advance.NetReceivableMinor, advance.AdvanceBps, advance.CumulativeWorkMinor));
        var withDraft = await BillingAsync(client, project.Id);
        Assert.Equal(((Guid?)advance.Id, (long?)null, 0L), (withDraft.AdvanceBillId, withDraft.AdvanceToBillMinor, withDraft.AdvanceBilledMinor));
        await ExpectRuleAsync(await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "Advance", billingDate = BillDate, description = "Again" }), ClientInvoiceAdvanceExists);

        advance = await IssueAsync(client, advance);
        Assert.Equal("BILL-0001", advance.Number);

        // The early draft no longer follows from the issued bills: issuing it is refused until it is saved again.
        early = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{early.Id}");
        Assert.True(early.IsStale);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{early.Id}/issue", new { version = early.Version }),
            "Another bill was issued or voided after this draft was prepared");

        // 20% of the work recovers 336,000.00; 5% VAT withheld on the net 2,000,000.00 is 100,000.00. The preview and the bill agree.
        var preview = await client.GetAsync<BillPreviewDto>($"/api/projects/{project.Id}/billing/preview?cumulativeWorkMinor=224000000&billingDate={BillDate}");
        Assert.Equal((500, 10_000_000L, 33_600_000L, 154_000_000L), (preview.VatWithholdingBps, preview.VatWithholdingMinor, preview.AdvanceRecoveryMinor,
            preview.NetReceivableMinor));
        early = await client.PutAsync<ClientInvoiceDto>($"/api/client-invoices/{early.Id}",
            new { billingDate = BillDate, description = "Progress billing no. 1", cumulativeWorkMinor = 224_000_000L, version = early.Version });
        Assert.False(early.IsStale);
        var first = await IssueAsync(client, early);
        Assert.Equal(("BILL-0002", 33_600_000L, 500, 10_000_000L, 154_000_000L), (first.Number, first.AdvanceRecoveryMinor, first.VatWithholdingBps,
            first.VatWithholdingMinor, first.NetReceivableMinor));

        var second = await IssuedAsync(client, project.Id, 560_000_000);
        Assert.Equal((50_400_000L, 15_000_000L, 231_000_000L), (second.AdvanceRecoveryMinor, second.VatWithholdingMinor, second.NetReceivableMinor));

        var summary = await BillingAsync(client, project.Id);
        Assert.Equal((168_000_000L, 84_000_000L, 84_000_000L, (long?)null), (summary.AdvanceBilledMinor, summary.AdvanceRecoveredMinor,
            summary.AdvanceToRecoverMinor, summary.AdvanceToBillMinor));
        // Billed to date is work; the down payment is a bill like any other, owed until it is paid.
        Assert.Equal((560_000_000L, 168_000_000L + 154_000_000L + 231_000_000L), (summary.BilledToDateMinor, summary.OutstandingMinor));
        var row = Assert.Single((await client.GetAsync<ReceivablesDto>("/api/billing/receivables")).Rows.Items, item => item.ProjectId == project.Id);
        Assert.Equal(summary.OutstandingMinor, row.OutstandingMinor);

        advance = await PayAsync(client, await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{advance.Id}"), 168_000_000);
        Assert.Equal(0, advance.OutstandingMinor);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{advance.Id}/void", new { reason = "Wrong", version = advance.Version }),
            "Only the latest bill of a project can be voided. Void BILL-0003 first.");

        // Voiding the latest progress bill gives back the recovery it took; billing it again recovers the same.
        await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{second.Id}/void", new { reason = "Wrong period", version = second.Version });
        summary = await BillingAsync(client, project.Id);
        Assert.Equal((33_600_000L, 134_400_000L, 154_000_000L), (summary.AdvanceRecoveredMinor, summary.AdvanceToRecoverMinor, summary.OutstandingMinor));
        Assert.Equal(50_400_000, (await IssuedAsync(client, project.Id, 560_000_000)).AdvanceRecoveryMinor);
    }

    private const string ClientInvoiceAdvanceExists = "This project already has a down payment bill. Open it instead of preparing another.";

    [SqlServerFact]
    public async Task A_down_payment_is_refused_after_the_first_progress_bill_or_without_a_down_payment_in_the_terms()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);

        await ExpectRuleAsync(await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "Advance", billingDate = BillDate, description = "Down payment" }), "This project's billing terms have no down payment.");

        // A private client may take more than 15%.
        var terms = await TermsAsync(client, project.Id, advance: 3_000);
        Assert.Equal((3_000, 0, false), (terms.AdvanceBps, terms.VatWithholdingBps, terms.IsGovernmentClient));
        // A draft prepared now can't be issued once a progress bill is issued first.
        var advance = await AdvanceDraftAsync(client, project.Id);
        await IssuedAsync(client, project.Id, 112_000_000);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{advance.Id}/issue", new { version = advance.Version }),
            "Bill the down payment before the first progress bill.");

        // Discarded, the tab no longer offers a down payment: progress was billed first.
        await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{advance.Id}/discard", new { reason = "Progress billed first", version = advance.Version });
        var summary = await BillingAsync(client, project.Id);
        Assert.Equal(((Guid?)null, (long?)null, 0L), (summary.AdvanceBillId, summary.AdvanceToBillMinor, summary.AdvanceBilledMinor));
        await ExpectRuleAsync(await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "Advance", billingDate = BillDate, description = "Down payment" }), "Bill the down payment before the first progress bill.");
    }

    [SqlServerFact]
    public async Task Changing_the_terms_or_the_VAT_rate_never_changes_a_bill_already_made()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var terms = await TermsAsync(client, project.Id);
        var bill = await IssuedAsync(client, project.Id, 224_000_000);

        await TermsAsync(client, project.Id, retention: 500, withholding: 0, days: 60, version: terms.Version);
        await ChangeOrganizationAsync(client, vatRateBps: 1_000);

        var again = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}");
        Assert.Equal((1_200, 1_000, 200, 30, 197_600_000L), (again.VatRateBps, again.RetentionBps, again.WithholdingBps, again.PaymentTermsDays, again.NetReceivableMinor));

        // The next bill uses what is in force now: 10% VAT, 5% retention, no withholding, 60 days.
        var next = await DraftAsync(client, project.Id, 334_000_000);
        Assert.Equal((110_000_000L, 100_000_000L, 10_000_000L, 5_500_000L, 0L, 104_500_000L), (next.GrossMinor, next.NetMinor, next.VatMinor, next.RetentionMinor,
            next.WithholdingMinor, next.NetReceivableMinor));
        Assert.Equal(new DateOnly(2026, 11, 29), next.DueDate);
    }

    [SqlServerFact]
    public async Task Retention_is_released_by_a_bill_once_the_project_is_completed()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        await IssuedAsync(client, project.Id, 224_000_000);
        await IssuedAsync(client, project.Id, Price);

        var early = await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "RetentionRelease", billingDate = BillDate, description = "Release of retention", amountMinor = 1_000L });
        await ExpectRuleAsync(early, "Retention is released once the project is completed.");

        await CompleteAsync(client, project);
        var summary = await BillingAsync(client, project.Id);
        Assert.Equal((112_000_000L, true, (int?)10_000, (long?)0), (summary.RetentionHeldMinor, summary.CanReleaseRetention, summary.PercentBilledBps,
            summary.RemainingToBillMinor));

        var tooMuch = await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "RetentionRelease", billingDate = BillDate, description = "Release of retention", amountMinor = 112_000_001L });
        Assert.Equal("That is more than the retention still held on this project.", await ErrorMessageAsync(tooMuch, "amountMinor"));

        var release = await client.PostAsync<ClientInvoiceDto>($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "RetentionRelease", billingDate = BillDate, description = "Release of retention", amountMinor = 112_000_000L });
        var issued = await IssueAsync(client, release);
        Assert.Equal(("RetentionRelease", "BILL-0003", 112_000_000L, 0L, 0L, 0L, 112_000_000L), (issued.Kind, issued.Number, issued.GrossMinor, issued.VatMinor,
            issued.RetentionMinor, issued.WithholdingMinor, issued.NetReceivableMinor));

        var after = await BillingAsync(client, project.Id);
        Assert.Equal((112_000_000L, 112_000_000L, 0L, false), (after.RetentionWithheldMinor, after.RetentionReleasedMinor, after.RetentionHeldMinor,
            after.CanReleaseRetention));
        Assert.Equal(Price, after.BilledToDateMinor);
    }

    // ---- The chain ----------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task A_draft_made_stale_by_another_bill_is_refused_at_issue_until_it_is_saved_again()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var early = await DraftAsync(client, project.Id, 300_000_000);
        await IssuedAsync(client, project.Id, 224_000_000);

        var opened = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{early.Id}");
        Assert.True(opened.IsStale);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{early.Id}/issue", new { version = opened.Version }),
            "Another bill was issued or voided after this draft was prepared");

        var saved = await client.PutAsync<ClientInvoiceDto>($"/api/client-invoices/{early.Id}",
            new { billingDate = BillDate, description = "Progress billing no. 2", cumulativeWorkMinor = 300_000_000L, version = opened.Version });
        Assert.Equal((false, 224_000_000L, 76_000_000L), (saved.IsStale, saved.PreviousCumulativeWorkMinor, saved.GrossMinor));
        Assert.Equal("BILL-0002", (await IssueAsync(client, saved)).Number);
    }

    [SqlServerFact]
    public async Task Two_bills_issued_at_the_same_moment_can_not_both_follow_the_same_bill()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        using var other = await AnotherSessionAsync(_factory, user);
        var project = await PricedProjectAsync(client);

        // Round 1: two first bills (nothing to follow, the database's index decides). Round 2: two bills that follow the same one.
        for (var round = 1; round <= 2; round++)
        {
            var a = await DraftAsync(client, project.Id, (await BillingAsync(client, project.Id)).BilledToDateMinor + 10_000_000);
            var b = await DraftAsync(client, project.Id, (await BillingAsync(client, project.Id)).BilledToDateMinor + 20_000_000);
            var responses = await Task.WhenAll(
                client.PostAsync($"/api/client-invoices/{a.Id}/issue", new { version = a.Version }),
                other.PostAsync($"/api/client-invoices/{b.Id}/issue", new { version = b.Version }));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(response => response.StatusCode).Order());
            var summary = await BillingAsync(client, project.Id);
            var issued = summary.Bills.Items.Where(bill => bill.Status == "Issued").ToArray();
            Assert.Equal(round, issued.Length);
            // The chain still adds up: billed to date is the latest issued bill's work to date, and each bill bills its own difference.
            Assert.Equal(summary.BilledToDateMinor, issued.Sum(bill => bill.GrossMinor));
            foreach (var bill in summary.Bills.Items.Where(bill => bill.Status == "Draft"))
                await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}/discard", new { reason = "Lost the race", version = bill.Version });
        }
    }

    [SqlServerFact]
    public async Task Only_the_latest_bill_without_payments_can_be_voided_and_a_draft_is_discarded_instead()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var first = await IssuedAsync(client, project.Id, 224_000_000);
        var second = await IssuedAsync(client, project.Id, 560_000_000);

        // Issuing the second bill changed the first (it now points at its successor), so read its version again.
        var firstNow = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{first.Id}");
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{first.Id}/void", new { reason = "Wrong", version = firstNow.Version }),
            "Only the latest bill of a project can be voided. Void BILL-0002 first.");

        var paid = await PayAsync(client, second, 1_000_00);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{second.Id}/void", new { reason = "Wrong", version = paid.Version }),
            "This bill has payments recorded against it.");
        var unpaid = await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{second.Id}/payments/{paid.Payments[0].Id}/void",
            new { reason = "Entered on the wrong bill", version = paid.Version });
        Assert.Equal((0L, 302_400_000L, true), (unpaid.PaidMinor, unpaid.OutstandingMinor, unpaid.Payments[0].IsVoided));

        var voided = await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{second.Id}/void", new { reason = "Billed the wrong period", version = unpaid.Version });
        Assert.Equal(("Void", "Billed the wrong period", 0L), (voided.Status, voided.VoidReason, voided.OutstandingMinor));
        var summary = await BillingAsync(client, project.Id);
        Assert.Equal((224_000_000L, 22_400_000L), (summary.BilledToDateMinor, summary.RetentionHeldMinor));
        Assert.True((await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{first.Id}")).IsLatestIssued);

        var draft = await DraftAsync(client, project.Id, 400_000_000);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{draft.Id}/void", new { reason = "x", version = draft.Version }),
            "A draft isn't voided: discard it instead.");
        var discarded = await client.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{draft.Id}/discard", new { reason = "Duplicate", version = draft.Version });
        Assert.Equal(("Void", (string?)null), (discarded.Status, discarded.Number));
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{first.Id}/discard",
                new { reason = "x", version = (await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{first.Id}")).Version }),
            "An issued bill can't be discarded. Void it instead.");
    }

    // ---- Refusals -----------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Every_wrong_field_is_reported_together_and_billing_stays_within_the_contract_price()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);

        var bad = await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "Progress", billingDate = "2999-01-01", description = " ", cumulativeWorkMinor = 0L });
        Assert.Equal(new HashSet<string> { "billingDate", "description", "cumulativeWorkMinor" }, await ErrorFieldsAsync(bad));

        var over = await PostDraftAsync(client, project.Id, Price + 1);
        Assert.Equal("Work completed to date can't be more than the contract price.", await ErrorMessageAsync(over, "cumulativeWorkMinor"));
        var overPreview = await client.GetAsync($"/api/projects/{project.Id}/billing/preview?cumulativeWorkMinor={Price + 1}");
        Assert.Equal("Work completed to date can't be more than the contract price.", await ErrorMessageAsync(overPreview, "cumulativeWorkMinor"));
        Assert.Contains("kind", await ErrorFieldsAsync(await client.PostAsync($"/api/projects/{project.Id}/billing/invoices",
            new { kind = "Deposit", billingDate = BillDate, description = "x", cumulativeWorkMinor = 1L })));

        var badTerms = await client.PutAsync($"/api/projects/{project.Id}/billing/terms",
            new { retentionBps = 3_001, creditableWithholdingBps = 1_001, paymentTermsDays = 181, version = 0, advanceBps = 5_001, vatWithholdingBps = 1_201 });
        Assert.Equal(new HashSet<string> { "retentionBps", "creditableWithholdingBps", "paymentTermsDays", "advanceBps", "vatWithholdingBps" },
            await ErrorFieldsAsync(badTerms));

        var unpriced = await PricedProjectAsync(client, price: null);
        await ExpectRuleAsync(await PostDraftAsync(client, unpriced.Id, 100), "This project has no contract price yet.");

        await IssuedAsync(client, project.Id, Price);
        await ExpectRuleAsync(await PostDraftAsync(client, project.Id, Price), "The whole contract price has already been billed.");
    }

    [SqlServerFact]
    public async Task A_payment_can_not_exceed_what_is_still_to_collect_or_be_recorded_on_a_draft()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        await TermsAsync(client, project.Id);
        var draft = await DraftAsync(client, project.Id, 224_000_000);
        await ExpectRuleAsync(await PostPaymentAsync(client, draft, 1), "Issue this bill before recording a payment against it.");

        var bill = await IssueAsync(client, draft);
        var over = await PostPaymentAsync(client, bill, 197_600_001);
        Assert.Equal("That is more than what is still to collect on this bill.", await ErrorMessageAsync(over, "amountMinor"));

        var bad = await client.PostAsync($"/api/client-invoices/{bill.Id}/payments",
            new { amountMinor = 0L, paidOn = "2999-01-01", method = "Barter", version = bill.Version });
        Assert.Equal(new HashSet<string> { "amountMinor", "paidOn", "method" }, await ErrorFieldsAsync(bad));

        var full = await PayAsync(client, bill, 197_600_000);
        Assert.Equal(0, full.OutstandingMinor);
        await ExpectRuleAsync(await PostPaymentAsync(client, full, 1), "This bill is fully paid.");
    }

    [SqlServerFact]
    public async Task An_archived_project_is_read_only_for_billing_but_stays_readable()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var bill = await IssuedAsync(client, project.Id, 224_000_000);
        var draft = await DraftAsync(client, project.Id, 300_000_000);
        await ArchiveAsync(client, await client.GetAsync<ProjectDto>($"/api/projects/{project.Id}"));

        const string archived = "This project is archived. Restore it before making changes.";
        await ExpectRuleAsync(await PostDraftAsync(client, project.Id, 400_000_000), archived);
        await ExpectRuleAsync(await PostPaymentAsync(client, bill, 1), archived);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{bill.Id}/void", new { reason = "x", version = bill.Version }), archived);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{draft.Id}/issue", new { version = draft.Version }), archived);
        await ExpectRuleAsync(await client.PostAsync($"/api/client-invoices/{draft.Id}/discard", new { reason = "x", version = draft.Version }), archived);
        await ExpectRuleAsync(await client.PutAsync($"/api/projects/{project.Id}/billing/terms",
            new { retentionBps = 500, creditableWithholdingBps = 0, paymentTermsDays = 30, version = 0 }), archived);

        var summary = await BillingAsync(client, project.Id);
        Assert.Equal(("Archived", 224_000_000L), (summary.ProjectStatus, summary.BilledToDateMinor));
    }

    [SqlServerFact]
    public async Task A_stale_version_is_a_conflict_and_nothing_is_overwritten()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var draft = await DraftAsync(client, project.Id, 224_000_000);
        var changed = await client.PutAsync<ClientInvoiceDto>($"/api/client-invoices/{draft.Id}",
            new { billingDate = BillDate, description = "Changed", cumulativeWorkMinor = 230_000_000L, version = draft.Version });

        async Task ExpectConflictAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("concurrency_conflict", await RuleCodeAsync(response));
        }

        await ExpectConflictAsync(await client.PutAsync($"/api/client-invoices/{draft.Id}",
            new { billingDate = BillDate, description = "Mine", cumulativeWorkMinor = 240_000_000L, version = draft.Version }));
        await ExpectConflictAsync(await client.PostAsync($"/api/client-invoices/{draft.Id}/issue", new { version = draft.Version }));
        var bill = await IssueAsync(client, changed);
        await PayAsync(client, bill, 1_000_00);
        await ExpectConflictAsync(await PostPaymentAsync(client, bill, 1_000_00));

        var terms = await TermsAsync(client, project.Id);
        await TermsAsync(client, project.Id, retention: 500, version: terms.Version);
        await ExpectConflictAsync(await client.PutAsync($"/api/projects/{project.Id}/billing/terms",
            new { retentionBps = 700, creditableWithholdingBps = 0, paymentTermsDays = 30, version = terms.Version }));
        Assert.Equal(230_000_000, (await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{draft.Id}")).CumulativeWorkMinor);
    }

    [SqlServerFact]
    public async Task A_repeated_request_id_returns_the_original_bill_or_payment_and_is_refused_for_something_else()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var otherProject = await PricedProjectAsync(client);
        var requestId = Guid.NewGuid().ToString();

        var first = await DraftAsync(client, project.Id, 224_000_000, requestId);
        var repeat = await DraftAsync(client, project.Id, 224_000_000, requestId);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(1, (await BillingAsync(client, project.Id)).Bills.Total);
        await ExpectRuleAsync(await PostDraftAsync(client, otherProject.Id, 224_000_000, requestId), "That request id was already used for a different bill.");

        var bill = await IssueAsync(client, first);
        var paymentId = Guid.NewGuid().ToString();
        var paid = await PayAsync(client, bill, 1_000_00, paymentId);
        // The retry carries the version the form had before the first save went through; it still gets the original answer.
        var again = await PayAsync(client, bill, 1_000_00, paymentId);
        Assert.Equal((1, 1_000_00L), (again.Payments.Count, again.PaidMinor));
        Assert.Equal(paid.Version, again.Version);

        var otherBill = await IssuedAsync(client, otherProject.Id, 100_000_000);
        await ExpectRuleAsync(await PostPaymentAsync(client, otherBill, 1_000_00, paymentId), "That request id was already used for a payment on a different bill.");
    }

    // ---- Who may do what ----------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Each_role_reaches_only_the_billing_steps_its_permissions_allow()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await PricedProjectAsync(admin);
        var issued = await IssuedAsync(admin, project.Id, 224_000_000);
        var paid = await PayAsync(admin, issued, 1_000_00);
        var draft = await DraftAsync(admin, project.Id, 300_000_000);

        // Viewer and cost engineer read only. Warehouse keeper and HR officer see nothing of billing.
        foreach (var role in new[] { AccessRole.Viewer, AccessRole.CostEngineer })
        {
            var (reader, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _r = reader;
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/projects/{project.Id}/billing")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/client-invoices/{issued.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/billing/receivables")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await PostDraftAsync(reader, project.Id, 400_000_000)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/projects/{project.Id}/billing/preview?cumulativeWorkMinor=1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsync($"/api/projects/{project.Id}/billing/terms",
                new { retentionBps = 500, creditableWithholdingBps = 0, paymentTermsDays = 30, version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await PostPaymentAsync(reader, paid, 1)).StatusCode);
        }

        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (outsider, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _o = outsider;
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/projects/{project.Id}/billing")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/client-invoices/{issued.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/client-invoices")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/billing/receivables")).StatusCode);
        }

        // A project manager prepares bills and records payments but neither issues nor voids.
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, adminUser.TenantId);
        using var _m = manager;
        Assert.Equal(HttpStatusCode.Created, (await PostDraftAsync(manager, project.Id, 400_000_000)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostPaymentAsync(manager, paid, 1_000_00)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/client-invoices/{draft.Id}/issue", new { version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/client-invoices/{issued.Id}/void", new { reason = "x", version = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/client-invoices/{issued.Id}/payments/{paid.Payments[0].Id}/void",
            new { reason = "x", version = 9 })).StatusCode);

        // An accountant does every step.
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, adminUser.TenantId);
        using var _ac = accountant;
        Assert.Equal("BILL-0002", (await IssueAsync(accountant, draft)).Number);
    }

    [SqlServerFact]
    public async Task Another_company_can_not_read_or_change_any_of_it()
    {
        var (owner, _) = await SignedInAsync(_factory);
        using var _o = owner;
        var project = await PricedProjectAsync(owner);
        var bill = await IssuedAsync(owner, project.Id, 224_000_000);
        var paid = await PayAsync(owner, bill, 1_000_00);
        var draft = await DraftAsync(owner, project.Id, 300_000_000);

        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var notFound = new List<HttpResponseMessage>
        {
            await stranger.GetAsync($"/api/projects/{project.Id}/billing"),
            await stranger.GetAsync($"/api/projects/{project.Id}/billing/preview?cumulativeWorkMinor=1"),
            await stranger.PutAsync($"/api/projects/{project.Id}/billing/terms", new { retentionBps = 0, creditableWithholdingBps = 0, paymentTermsDays = 0, version = 0 }),
            await PostDraftAsync(stranger, project.Id, 400_000_000),
            await stranger.GetAsync($"/api/client-invoices/{bill.Id}"),
            await stranger.PutAsync($"/api/client-invoices/{draft.Id}", new { billingDate = BillDate, description = "x", cumulativeWorkMinor = 1L, version = draft.Version }),
            await stranger.PostAsync($"/api/client-invoices/{draft.Id}/issue", new { version = draft.Version }),
            await stranger.PostAsync($"/api/client-invoices/{draft.Id}/discard", new { reason = "x", version = draft.Version }),
            await stranger.PostAsync($"/api/client-invoices/{bill.Id}/void", new { reason = "x", version = paid.Version }),
            await PostPaymentAsync(stranger, paid, 1),
            await stranger.PostAsync($"/api/client-invoices/{bill.Id}/payments/{paid.Payments[0].Id}/void", new { reason = "x", version = paid.Version })
        };
        Assert.All(notFound, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Empty((await stranger.GetAsync<PagedResult<ClientInvoiceListItemDto>>("/api/client-invoices")).Items);
        Assert.Empty((await stranger.GetAsync<ReceivablesDto>("/api/billing/receivables")).Rows.Items);

        // And the owner's figures are untouched.
        Assert.Equal((1_000_00L, 2), ((await BillingAsync(owner, project.Id)).CollectedMinor, (await BillingAsync(owner, project.Id)).Bills.Total));
    }

    // ---- Reads stay cheap ---------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_bills_list_the_project_billing_and_the_receivables_cost_the_same_statements_however_many_bills_exist()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await PricedProjectAsync(client);
        var other = await PricedProjectAsync(client);
        // A down payment first, so its figures are part of what is measured.
        await TermsAsync(client, project.Id, advance: 1_000);
        await IssueAsync(client, await AdvanceDraftAsync(client, project.Id));
        var bill = await IssuedAsync(client, project.Id, 10_000_000);
        await PayAsync(client, bill, 1_000_00);

        var urls = new[] { "/api/client-invoices", $"/api/projects/{project.Id}/billing", "/api/billing/receivables", $"/api/client-invoices/{bill.Id}" };
        var small = new List<int>();
        foreach (var url in urls) small.Add(await StatementsAsync(_factory, async () => await client.GetAsync(url)));

        for (var index = 2; index <= 12; index++)
        {
            foreach (var target in new[] { project.Id, other.Id })
            {
                var next = await IssuedAsync(client, target, (await BillingAsync(client, target)).BilledToDateMinor + 10_000_000);
                var withPayment = await PayAsync(client, next, 1_000_00);
                await PayAsync(client, withPayment, 2_000_00);
            }
            await DraftAsync(client, project.Id, (await BillingAsync(client, project.Id)).BilledToDateMinor + 5_000_000);
        }
        var big = new List<int>();
        foreach (var url in urls) big.Add(await StatementsAsync(_factory, async () => await client.GetAsync(url)));

        Assert.Equal(small, big);
        // Each request reads the company's time zone for "today", then: list, count + page; project billing, one statement for the
        // project, its terms, the down payment and every figure, then the bills' count + page; receivables, count + page + totals; a bill,
        // itself + its payments.
        Assert.Equal([3, 4, 4, 3], big);
    }

    [SqlServerFact]
    public async Task The_demo_bills_Riverside_with_one_paid_bill_one_overdue_bill_and_a_draft_and_Harbor_with_a_down_payment()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client, "Riverside Residences", 48_500_000_00);
        var harborProject = await CreateProjectAsync(client, "Harbor Warehouse", 12_000_000_00);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var organization = await db.Set<Organization>().IgnoreQueryFilters().SingleAsync(item => item.Id == user.TenantId);
            var riverside = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == project.Id);
            var harborEntity = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == harborProject.Id);
            var today = organization.TodayLocal(DateTimeOffset.UtcNow);
            await BillingDemo.SeedAsync(db, organization, riverside, harborEntity, user.UserId, user.UserId, today, DateTimeOffset.UtcNow);
        }

        var summary = await BillingAsync(client, project.Id);
        Assert.Equal((1_940_000_000L, (int?)4_000, 194_000_000L), (summary.BilledToDateMinor, summary.PercentBilledBps, summary.RetentionHeldMinor));
        Assert.Equal(["Draft", "Issued", "Issued"], summary.Bills.Items.Select(bill => bill.Status));
        Assert.Equal([null, "BILL-0002", "BILL-0001"], summary.Bills.Items.Select(bill => bill.Number));
        var second = summary.Bills.Items[1];
        Assert.True(second.IsOverdue);
        Assert.Equal(second.NetReceivableMinor - 5_000_000_00, second.OutstandingMinor);
        Assert.Equal(0, summary.Bills.Items[2].OutstandingMinor);
        Assert.Equal((summary.OutstandingMinor, 1), (summary.OverdueMinor, summary.OverdueCount));

        // Harbor Warehouse: a 20% down payment (BILL-0003), paid, and a draft first progress bill that recovers its share of it.
        var harbor = await BillingAsync(client, harborProject.Id);
        Assert.Equal((2_400_000_00L, 0L, 2_400_000_00L), (harbor.AdvanceBilledMinor, harbor.AdvanceRecoveredMinor, harbor.AdvanceToRecoverMinor));
        var harborDraft = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{harbor.Bills.Items.Single(bill => bill.Status == "Draft").Id}");
        Assert.Equal(240_000_00L, harborDraft.AdvanceRecoveryMinor);
        Assert.Equal("BILL-0003", harbor.Bills.Items.Single(bill => bill.Status == "Issued").Number);

        // The next bill an accountant issues continues the demo's numbering.
        var draft = await client.GetAsync<ClientInvoiceDto>($"/api/client-invoices/{summary.Bills.Items[0].Id}");
        Assert.False(draft.IsStale);
        Assert.Equal("BILL-0004", (await IssueAsync(client, draft)).Number);
    }
}
