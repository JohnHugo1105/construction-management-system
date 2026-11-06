using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// Delivery receipts on purchase orders, end to end on the real host: recording and voiding, the derived received quantity and value, the
/// order's delivery state, every refusal, who may do what, that another company never reaches a delivery, and that two people receiving
/// the last quantity at the same moment record it once. Money is in centavos: 50,000 is 500.00.
/// </summary>
public sealed class GoodsReceiptApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public GoodsReceiptApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    /// <summary>10 pc of rebar at 500.00 with 12% VAT included (gross 5,000.00) and 3 pc of tie wire at 99.99 with VAT added (gross 335.97).</summary>
    private static Dictionary<string, object?>[] TwoLines() => [OrderLine("Rebar 16 mm", 10m, 50_000, "Inclusive"), OrderLine("Tie wire", 3m, 9_999, "Exclusive", wbsCode: "3100")];

    [SqlServerFact]
    public async Task A_delivery_is_recorded_partly_then_in_full_voided_and_never_touches_committed_cost()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 10_000_000);
        var order = await ApprovedAsync(client, project, firm, TwoLines());

        Assert.Equal(("NotReceived", 0, true), (order.DeliveryState, order.ReceiptCount, order.CanReceive));
        Assert.Empty(order.Receipts);
        Assert.Equal([(0m, 10m), (0m, 3m)], order.Lines.Select(line => (line.ReceivedQuantity, line.ToReceiveQuantity)));
        var commitments = (await DashboardRowAsync(client, project.Id)).Commitments;
        Assert.Equal(533_597, commitments!.CommittedMinor);

        // 4 of 10 pc of rebar and 1.5 of 3 pc of tie wire arrive.
        var partly = await ReceiveAsync(client, order, "DR-0012", (1, 4m), (2, 1.5m));

        Assert.Equal(("PartlyReceived", 1, true), (partly.DeliveryState, partly.ReceiptCount, partly.CanReceive));
        var receipt = Assert.Single(partly.Receipts);
        Assert.Equal(("Posted", "DR-0012", "Test Admin", true), (receipt.Status, receipt.DeliveryReceiptNumber, receipt.RecordedByName, receipt.CanVoid));
        Assert.Equal([(1, "Rebar 16 mm", 4m), (2, "Tie wire", 1.5m)], receipt.Lines.Select(line => (line.LineNumber, line.Description, line.Quantity)));
        // 4 x 500.00 included: net 1,785.71, gross 2,000.00. 1.5 x 99.99 added: 149.99 (149.985 rounded once), VAT 18.00 (17.9988 rounded), gross 167.99.
        Assert.Equal((4m, 6m, 178_571L, 200_000L), (partly.Lines[0].ReceivedQuantity, partly.Lines[0].ToReceiveQuantity, partly.Lines[0].ReceivedValueNetMinor, partly.Lines[0].ReceivedValueGrossMinor));
        Assert.Equal((1.5m, 1.5m, 14_999L, 16_799L), (partly.Lines[1].ReceivedQuantity, partly.Lines[1].ToReceiveQuantity, partly.Lines[1].ReceivedValueNetMinor, partly.Lines[1].ReceivedValueGrossMinor));
        Assert.All(partly.Lines, line => Assert.False(line.InvoicedAheadOfDelivery));
        // A delivery moves no money: what is committed is exactly what it was, here, on the dashboard and on the order's page.
        Assert.Equal((order.CommittedNetMinor, order.CommittedGrossMinor), (partly.CommittedNetMinor, partly.CommittedGrossMinor));
        Assert.Equal(commitments, (await DashboardRowAsync(client, project.Id)).Commitments);
        Assert.Equal(commitments, (await SummaryAsync(client, project.Id)).Commitments);
        Assert.True(partly.Version > order.Version);
        Assert.Equal("PartlyReceived", Assert.Single((await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items).DeliveryState);

        // The rest arrives under another delivery receipt: the order is received in full and the quantity left is zero.
        var full = await ReceiveAsync(client, partly, "DR-0013", (1, 6m), (2, 1.5m));
        Assert.Equal(("FullyReceived", 2, false), (full.DeliveryState, full.ReceiptCount, full.CanReceive));
        Assert.Equal(["DR-0013", "DR-0012"], full.Receipts.Select(item => item.DeliveryReceiptNumber));
        Assert.Equal([0m, 0m], full.Lines.Select(line => line.ToReceiveQuantity));
        // Receiving the whole of a line is worth exactly the line.
        Assert.Equal([(446_429L, 500_000L), (29_997L, 33_597L)], full.Lines.Select(line => (line.ReceivedValueNetMinor, line.ReceivedValueGrossMinor)));
        Assert.Equal("FullyReceived", Assert.Single((await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items).DeliveryState);
        Assert.Equal(commitments, (await DashboardRowAsync(client, project.Id)).Commitments);

        // The second delivery was a mistake: voiding it keeps it on record with its reason and takes its quantities back.
        var voided = await VoidDeliveryAsync(client, order.Id, full.Receipts[0], "Counted the wrong truck");
        Assert.Equal(("PartlyReceived", 2, true), (voided.DeliveryState, voided.ReceiptCount, voided.CanReceive));
        var shown = voided.Receipts[0];
        Assert.Equal(("Void", "Counted the wrong truck", "Test Admin", false), (shown.Status, shown.VoidReason, shown.VoidedByName, shown.CanVoid));
        Assert.NotNull(shown.VoidedAtUtc);
        Assert.Equal(2, shown.Lines.Count);
        Assert.Equal([(4m, 6m), (1.5m, 1.5m)], voided.Lines.Select(line => (line.ReceivedQuantity, line.ToReceiveQuantity)));
        Assert.Equal(commitments, (await DashboardRowAsync(client, project.Id)).Commitments);

        // The voided delivery frees its number and its quantities: the same delivery can be entered again.
        var again = await ReceiveAsync(client, voided, "DR-0013", (1, 6m), (2, 1.5m));
        Assert.Equal(("FullyReceived", 3), (again.DeliveryState, again.ReceiptCount));
    }

    [SqlServerFact]
    public async Task Only_an_approved_order_shows_a_delivery_state_in_the_list_and_a_draft_or_closed_order_takes_no_delivery()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);

        var draft = await DraftAsync(client, project, firm);
        Assert.Equal(("NotReceived", false), (draft.DeliveryState, draft.CanReceive));
        var refusedDraft = await TryReceiveAsync(client, draft, "DR-1", (1, 1m));
        Assert.Equal(HttpStatusCode.Conflict, refusedDraft.StatusCode);
        Assert.Contains("is not approved yet", await DetailAsync(refusedDraft));

        var toClose = await ApprovedAsync(client, project, firm);
        var closed = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toClose.Id}/close", new { reason = "Done", version = toClose.Version });
        var refusedClosed = await TryReceiveAsync(client, closed, "DR-2", (1, 1m));
        Assert.Equal(HttpStatusCode.Conflict, refusedClosed.StatusCode);
        Assert.Equal("This order is closed.", await DetailAsync(refusedClosed));

        var toCancel = await ApprovedAsync(client, project, firm);
        var cancelled = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toCancel.Id}/cancel", new { reason = "Re-tender", version = toCancel.Version });
        var refusedCancelled = await TryReceiveAsync(client, cancelled, "DR-3", (1, 1m));
        Assert.Equal(HttpStatusCode.Conflict, refusedCancelled.StatusCode);
        Assert.Equal("This order was cancelled.", await DetailAsync(refusedCancelled));

        var approved = await ApprovedAsync(client, project, firm);
        var states = (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items.ToDictionary(item => item.Id, item => item.DeliveryState);
        Assert.Equal((null, "NotReceived", null, null), (states[draft.Id], states[approved.Id], states[closed.Id], states[cancelled.Id]));
        Assert.Equal(0, (await GetOrderAsync(client, closed.Id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task Every_refusal_says_what_is_wrong_and_over_receipt_never_gets_in()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm, TwoLines());
        var other = await ApprovedAsync(client, project, firm);

        // Over-receipt is refused by the line, with what is left, and nothing is saved.
        var over = await TryReceiveAsync(client, order, "DR-1", (1, 10.0001m));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(over));
        Assert.Equal("Line 1 (Rebar 16 mm): only 10 pc are left to receive. Enter 10 or less.", await ErrorMessageAsync(over, "lines"));
        Assert.Equal(0, (await GetOrderAsync(client, order.Id)).ReceiptCount);

        // Exactly the quantity ordered is fine, and then there is nothing left for that line.
        var partly = await ReceiveAsync(client, order, "DR-2", (1, 10m));
        var again = await TryReceiveAsync(client, partly, "DR-3", (1, 0.5m));
        Assert.Equal("Line 1 (Rebar 16 mm) has all been received already.", await ErrorMessageAsync(again, "lines"));

        // Every problem together, each on its field.
        var bad = await client.PostAsync($"/api/purchase-orders/{order.Id}/receipts", new Dictionary<string, object?>
        {
            ["receivedDate"] = "2999-01-01", ["deliveryReceiptNumber"] = new string('d', 61), ["note"] = new string('n', 301),
            ["lines"] = new object[] { new { purchaseOrderLineId = order.Lines[1].Id, quantity = 0m }, new { purchaseOrderLineId = other.Lines[0].Id, quantity = 1m } }
        });
        Assert.Equal(new HashSet<string> { "receivedDate", "deliveryReceiptNumber", "note", "lines" }, await ErrorFieldsAsync(bad));
        var lineMessages = (await ApiClient.ProblemAsync(bad)).GetProperty("errors").GetProperty("lines").EnumerateArray().Select(message => message.GetString()!).ToArray();
        Assert.Equal(2, lineMessages.Length);
        Assert.Contains(lineMessages, message => message.StartsWith("Line 2 (Tie wire): Enter the quantity received greater than zero", StringComparison.Ordinal));
        Assert.Contains(lineMessages, message => message.StartsWith("Line 2: that line isn't on PO-0001", StringComparison.Ordinal));

        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(await client.PostAsync($"/api/purchase-orders/{order.Id}/receipts",
            new { receivedDate = PastDate, deliveryReceiptNumber = "DR-9", note = (string?)null, lines = Array.Empty<object>() })));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(await TryReceiveAsync(client, order, "DR-9", (2, 1m), (2, 1m))));
        Assert.Equal(new HashSet<string> { "deliveryReceiptNumber" }, await ErrorFieldsAsync(await TryReceiveAsync(client, order, "--", (2, 1m))));
        Assert.Equal(1, (await GetOrderAsync(client, order.Id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task The_same_delivery_receipt_of_the_same_supplier_is_recorded_once_across_orders_until_it_is_voided()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var otherFirm = await CreateFirmAsync(client);
        var first = await ApprovedAsync(client, project, firm);
        var second = await ApprovedAsync(client, project, firm);
        var elsewhere = await ApprovedAsync(client, project, otherFirm);

        var recorded = await ReceiveAsync(client, first, "DR-0045", (1, 5m));

        // The same supplier, even on another order, with the number written differently.
        foreach (var spelled in new[] { "DR-0045", "dr 0045", " DR0045. " })
        {
            var duplicate = await TryReceiveAsync(client, second, spelled, (1, 1m));
            Assert.Equal(new HashSet<string> { "deliveryReceiptNumber" }, await ErrorFieldsAsync(duplicate));
            Assert.Equal("This delivery receipt is already recorded.", await ErrorMessageAsync(duplicate, "deliveryReceiptNumber"));
        }

        Assert.Equal(0, (await GetOrderAsync(client, second.Id)).ReceiptCount);
        // Another supplier's delivery receipt may carry the same number, and a delivery with no number is never a duplicate.
        Assert.Equal(1, (await ReceiveAsync(client, elsewhere, "DR-0045", (1, 1m))).ReceiptCount);
        Assert.Equal(1, (await ReceiveAsync(client, second, null, (1, 1m))).ReceiptCount);
        Assert.Equal(2, (await ReceiveAsync(client, await GetOrderAsync(client, second.Id), null, (1, 1m))).ReceiptCount);

        // Voiding the delivery frees its number.
        await VoidDeliveryAsync(client, first.Id, recorded.Receipts.Single());
        var reentered = await ReceiveAsync(client, await GetOrderAsync(client, second.Id), "DR-0045", (1, 1m));
        Assert.Equal(3, reentered.ReceiptCount);
    }

    [SqlServerFact]
    public async Task A_delivery_blocks_cancelling_the_order_but_not_closing_it_and_a_stale_page_is_a_conflict()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm);
        Assert.True(order.CanCancel);

        var received = await ReceiveAsync(client, order, "DR-1", (1, 4m));
        Assert.False(received.CanCancel);
        Assert.True(received.CanClose);

        // The page opened before the delivery is stale: the cancel is a conflict, not a silent success.
        var stale = await client.PostAsync($"/api/purchase-orders/{order.Id}/cancel", new { reason = "Mistake", version = order.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        var cancel = await client.PostAsync($"/api/purchase-orders/{order.Id}/cancel", new { reason = "Mistake", version = received.Version });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("Deliveries are already recorded against this order, so it can't be cancelled. Close it instead to release what was not delivered.", await DetailAsync(cancel));

        // Voiding the only delivery lets the order be cancelled again; a close keeps the deliveries on record.
        var voided = await VoidDeliveryAsync(client, order.Id, received.Receipts.Single());
        Assert.True(voided.CanCancel);
        var again = await ReceiveAsync(client, voided, "DR-2", (1, 2m));
        var closed = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/close", new { reason = "Supplier delivered less", version = again.Version });
        Assert.Equal(("Closed", "PartlyReceived", 2, false), (closed.Status, closed.DeliveryState, closed.ReceiptCount, closed.CanReceive));
    }

    [SqlServerFact]
    public async Task A_stale_void_is_a_conflict_and_a_delivery_is_voided_once_with_a_reason()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm);
        var received = await ReceiveAsync(client, order, "DR-1", (1, 4m));
        var receipt = received.Receipts.Single();

        var stale = await TryVoidDeliveryAsync(client, order.Id, receipt, version: receipt.Version + 1);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        Assert.Equal(new HashSet<string> { "reason" }, await ErrorFieldsAsync(await TryVoidDeliveryAsync(client, order.Id, receipt, reason: "  ")));
        Assert.Equal("Posted", (await GetOrderAsync(client, order.Id)).Receipts.Single().Status);

        await VoidDeliveryAsync(client, order.Id, receipt);
        // The page that still holds the old version is stale; one that reloaded is told the delivery is already voided.
        Assert.Equal(HttpStatusCode.Conflict, (await TryVoidDeliveryAsync(client, order.Id, receipt)).StatusCode);
        var twice = await TryVoidDeliveryAsync(client, order.Id, (await GetOrderAsync(client, order.Id)).Receipts.Single());
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("This delivery was already voided.", await DetailAsync(twice));
    }

    [SqlServerFact]
    public async Task A_delivery_can_be_neither_recorded_nor_voided_on_an_archived_project()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm);
        var received = await ReceiveAsync(client, order, "DR-1", (1, 4m));
        await ArchiveAsync(client, project);

        var record = await TryReceiveAsync(client, received, "DR-2", (1, 1m));
        Assert.Equal(HttpStatusCode.Conflict, record.StatusCode);
        Assert.Contains("Restore it", await DetailAsync(record));
        var voidAttempt = await TryVoidDeliveryAsync(client, order.Id, received.Receipts.Single());
        Assert.Equal(HttpStatusCode.Conflict, voidAttempt.StatusCode);
        Assert.Contains("Restore it", await DetailAsync(voidAttempt));

        var viewed = await GetOrderAsync(client, order.Id);
        Assert.Equal((false, false, "Posted"), (viewed.CanReceive, viewed.Receipts.Single().CanVoid, viewed.Receipts.Single().Status));
    }

    [SqlServerFact]
    public async Task A_repeated_request_id_returns_the_first_delivery_and_is_refused_for_another_order()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm);
        var other = await ApprovedAsync(client, project, firm);
        var requestId = Guid.NewGuid().ToString();

        var first = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 10m)], "DR-1", clientRequestId: requestId));
        var repeat = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 10m)], "DR-1", clientRequestId: requestId));

        Assert.Equal(1, first.ReceiptCount);
        Assert.Equal((first.Receipts[0].Id, 1), (repeat.Receipts[0].Id, repeat.ReceiptCount));
        var reused = await client.PostAsync($"/api/purchase-orders/{other.Id}/receipts", DeliveryBody(other, [(1, 1m)], "DR-2", clientRequestId: requestId));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        // The same form submitted twice at the very same moment records one delivery.
        var simultaneous = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsync($"/api/purchase-orders/{other.Id}/receipts", DeliveryBody(other, [(1, 10m)], "DR-3", clientRequestId: simultaneous))));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(1, (await GetOrderAsync(client, other.Id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task Two_people_receiving_the_last_quantity_at_the_same_moment_record_it_once()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        using var second = await AnotherSessionAsync(_factory, user);

        // Several rounds, each on a fresh order, so the two requests overlap in different ways; the outcome is the same every time.
        for (var round = 0; round < 4; round++)
        {
            var order = await ApprovedAsync(client, project, firm);
            var responses = await Task.WhenAll(
                client.PostAsync($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 10m)], $"A-{round}")),
                second.PostAsync($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 10m)], $"B-{round}")));

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            // The loser either collided with the winner's save (a conflict) or found the quantity already received (a refusal on the line).
            var loser = responses.Single(response => response.StatusCode != HttpStatusCode.Created);
            Assert.Contains(loser.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity });

            var viewed = await GetOrderAsync(client, order.Id);
            Assert.Equal((1, 10m, 0m, "FullyReceived"), (viewed.ReceiptCount, viewed.Lines[0].ReceivedQuantity, viewed.Lines[0].ToReceiveQuantity, viewed.DeliveryState));
        }
    }

    [SqlServerFact]
    public async Task Invoiced_ahead_of_delivery_is_an_advisory_that_clears_as_the_goods_arrive()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 10_000_000);
        // 10 pc at 500.00, no VAT: 5,000.00 in all.
        var order = await ApprovedAsync(client, project, firm);

        // An invoice for 3,000.00 arrives before anything does: still valid, still matched, still reducing what is committed; flagged for the page.
        await InvoiceAsync(client, project.Id, order.Lines[0].Id, 300_000);
        var ahead = await GetOrderAsync(client, order.Id);
        Assert.Equal((300_000L, 0L, true), (ahead.Lines[0].InvoicedGrossMinor, ahead.Lines[0].ReceivedValueGrossMinor, ahead.Lines[0].InvoicedAheadOfDelivery));
        Assert.Equal(200_000, ahead.CommittedGrossMinor);
        Assert.Equal("NotReceived", ahead.DeliveryState);

        // 4 pc arrive (2,000.00): 3,000.00 is still more than that.
        var four = await ReceiveAsync(client, ahead, "DR-1", (1, 4m));
        Assert.Equal((200_000L, true), (four.Lines[0].ReceivedValueGrossMinor, four.Lines[0].InvoicedAheadOfDelivery));
        Assert.Equal(200_000, four.CommittedGrossMinor);

        // 2 pc more make 6 in all (3,000.00): the invoice is no longer ahead of the delivery.
        var six = await ReceiveAsync(client, four, "DR-2", (1, 2m));
        Assert.Equal((300_000L, false), (six.Lines[0].ReceivedValueGrossMinor, six.Lines[0].InvoicedAheadOfDelivery));
        Assert.Equal(200_000, six.CommittedGrossMinor);
    }

    [SqlServerFact]
    public async Task Recording_and_voiding_are_audited_without_the_typed_reason_or_note()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm);
        var received = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/receipts",
            DeliveryBody(order, [(1, 4m)], "DR-77", note: "Driver said 9,876,543 pesos"));
        await VoidDeliveryAsync(client, order.Id, received.Receipts.Single(), "Counted the wrong truck, 1,234,567 pesos");

        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>("/api/audit?entityType=GoodsReceipt");
        Assert.Contains(trail.Items, entry => entry.Action == "GoodsReceipt.Recorded" && entry.Summary.Contains("PO-0001"));
        Assert.Contains(trail.Items, entry => entry.Action == "GoodsReceipt.Voided" && entry.Summary.Contains("PO-0001"));
        Assert.DoesNotContain(trail.Items, entry => entry.Summary.Contains("9,876,543") || entry.Summary.Contains("1,234,567") || entry.Summary.Contains("DR-77") ||
            entry.Summary.Contains("Reason", StringComparison.OrdinalIgnoreCase));
        // They stay on the delivery itself.
        var kept = (await GetOrderAsync(client, order.Id)).Receipts.Single();
        Assert.Equal(("Driver said 9,876,543 pesos", "Counted the wrong truck, 1,234,567 pesos"), (kept.Note, kept.VoidReason));
    }

    [SqlServerFact]
    public async Task Each_permission_boundary_is_enforced_by_role()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var order = await ApprovedAsync(admin, project, firm, OrderLine(quantity: 100m));
        var received = await ReceiveAsync(admin, order, "DR-0", (1, 1m));
        var receipt = received.Receipts.Single();

        // Everyone who may receive can record and void; each records one unit under a number of their own.
        var current = received;
        foreach (var (role, number) in new[] { (AccessRole.ProjectManager, "DR-PM"), (AccessRole.CostEngineer, "DR-CE"), (AccessRole.Accountant, "DR-AC") })
        {
            var (person, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _p = person;
            var posted = await TryReceiveAsync(person, current, number, (1, 1m));
            Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
            current = await person.ReadAsync<PurchaseOrderDto>(posted);
            var mine = current.Receipts.Single(item => item.DeliveryReceiptNumber == number);
            Assert.Equal(HttpStatusCode.OK, (await TryVoidDeliveryAsync(person, order.Id, mine)).StatusCode);
            current = await GetOrderAsync(person, order.Id);
        }

        // Viewer reads the order with its deliveries, but records and voids nothing.
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, user.TenantId);
        using var _v = viewer;
        Assert.Equal(1, (await GetOrderAsync(viewer, order.Id)).Receipts.Count(item => item.Status == "Posted"));
        Assert.Equal(HttpStatusCode.Forbidden, (await TryReceiveAsync(viewer, current, "DR-V", (1, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TryVoidDeliveryAsync(viewer, order.Id, receipt)).StatusCode);

        // Warehouse Keeper and HR Officer: no purchasing at all.
        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (outsider, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _o = outsider;
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/purchase-orders/{order.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TryReceiveAsync(outsider, current, "DR-O", (1, 1m))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TryVoidDeliveryAsync(outsider, order.Id, receipt)).StatusCode);
        }

        Assert.Equal(1, (await GetOrderAsync(admin, order.Id)).Receipts.Count(item => item.Status == "Posted"));
    }

    [SqlServerFact]
    public async Task Another_company_never_reaches_a_delivery_even_by_its_id()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var project = await CreateProjectAsync(mine);
        var firm = await CreateFirmAsync(mine);
        var order = await ApprovedAsync(mine, project, firm);
        var other = await ApprovedAsync(mine, project, firm);
        var received = await ReceiveAsync(mine, order, "DR-1", (1, 4m));
        var receipt = received.Receipts.Single();
        var theirProject = await CreateProjectAsync(theirs);
        var theirOrder = await ApprovedAsync(theirs, theirProject, await CreateFirmAsync(theirs));

        // They can't record against my order, or void my delivery under my order's id, their own order's id or another of mine.
        Assert.Equal(HttpStatusCode.NotFound, (await TryReceiveAsync(theirs, order, "DR-X", (1, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidDeliveryAsync(theirs, order.Id, receipt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidDeliveryAsync(theirs, theirOrder.Id, receipt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/purchase-orders/{order.Id}")).StatusCode);
        // Nor can they record their own order's delivery against a line of mine.
        var crossed = await theirs.PostAsync($"/api/purchase-orders/{theirOrder.Id}/receipts",
            new { receivedDate = PastDate, deliveryReceiptNumber = "DR-Y", note = (string?)null, lines = new[] { new { purchaseOrderLineId = order.Lines[0].Id, quantity = 1m } } });
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(crossed));

        // Within my company a delivery is reached only through its own order.
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidDeliveryAsync(mine, other.Id, receipt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mine.PostAsync($"/api/purchase-orders/{order.Id}/receipts/{Guid.NewGuid()}/void", new { reason = "x", version = 0 })).StatusCode);

        var after = await GetOrderAsync(mine, order.Id);
        Assert.Equal(("Posted", 4m), (after.Receipts.Single().Status, after.Lines[0].ReceivedQuantity));
        Assert.Equal(0, (await GetOrderAsync(theirs, theirOrder.Id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task Reading_an_order_costs_a_fixed_number_of_statements_however_many_deliveries_and_lines_it_has()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var lines = Enumerable.Range(1, 6).Select(index => OrderLine($"Item {index}", 1_000m, 1_000)).ToArray();

        var none = await ApprovedAsync(client, project, firm, lines);
        var small = await ApprovedAsync(client, project, firm, lines);
        await ReceiveAsync(client, small, "DR-S-1", (1, 1m));
        var big = await ApprovedAsync(client, project, firm, lines);
        for (var index = 0; index < 15; index++)
            big = await ReceiveAsync(client, big, $"DR-B-{index}", (1 + index % 6, 1m), (1 + (index + 1) % 6, 2m));
        // A voided delivery is read too, and costs nothing extra.
        await VoidDeliveryAsync(client, big.Id, big.Receipts[0]);
        Assert.Equal(15, (await GetOrderAsync(client, big.Id)).ReceiptCount);

        async Task<(int None, int Small, int Big, int List)> MeasureAsync() => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/purchase-orders/{none.Id}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/purchase-orders/{small.Id}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/purchase-orders/{big.Id}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/purchase-orders?pageSize=100")).StatusCode)));

        var measured = await MeasureAsync();

        // One order: the order, its lines, what each line is invoiced, the newest invoices, what each line has received, the newest deliveries
        // and their lines. The list: the count and the page, which carries each order's delivery state.
        Assert.Equal((7, 7, 7, 2), measured);
    }
}
