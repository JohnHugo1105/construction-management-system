using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The quantity-only receiving screen, end to end on the real host: a warehouse keeper lists, opens, records and voids deliveries through
/// the new routes and meets the same rules as everyone else, is refused on every purchasing route, never receives a field that names money,
/// and the reads cost a fixed number of statements however many orders, lines and deliveries exist.
/// </summary>
public sealed class ReceivingApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private static readonly string[] MoneyWords = ["minor", "cost", "price", "vat", "total", "invoice", "committed", "amount", "currency"];

    private readonly ConstructOpxApiFactory _factory;

    public ReceivingApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private static Dictionary<string, object?>[] TwoLines() =>
        [OrderLine("Rebar 16 mm", 10m, 50_000, "Inclusive"), OrderLine("Tie wire", 3m, 9_999, "Exclusive", wbsCode: "3100")];

    private static Task<ReceivingOrderDto> OpenAsync(ApiClient client, Guid id) => client.GetAsync<ReceivingOrderDto>($"/api/receiving/orders/{id}");

    private static object Delivery(ReceivingOrderDto order, string? number, params (int LineNumber, decimal Quantity)[] lines) =>
        DeliveryRequest(order, number, null, lines);

    private static Dictionary<string, object?> DeliveryRequest(ReceivingOrderDto order, string? number, string? requestId, params (int LineNumber, decimal Quantity)[] lines) => new()
    {
        ["receivedDate"] = PastDate, ["deliveryReceiptNumber"] = number, ["note"] = null, ["clientRequestId"] = requestId,
        ["lines"] = lines.Select(line => new { purchaseOrderLineId = order.Lines.Single(item => item.LineNumber == line.LineNumber).Id, quantity = line.Quantity }).ToArray()
    };

    private static Task<HttpResponseMessage> TryRecordAsync(ApiClient client, ReceivingOrderDto order, string? number, params (int LineNumber, decimal Quantity)[] lines) =>
        client.PostAsync($"/api/receiving/orders/{order.Id}/deliveries", Delivery(order, number, lines));

    private static async Task<ReceivingOrderDto> RecordAsync(ApiClient client, ReceivingOrderDto order, string? number, params (int LineNumber, decimal Quantity)[] lines)
    {
        var response = await TryRecordAsync(client, order, number, lines);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadAsync<ReceivingOrderDto>(response);
    }

    private static Task<HttpResponseMessage> TryVoidAsync(ApiClient client, Guid orderId, ReceivingDeliveryDto delivery, string? reason = "Counted the wrong truck", int? version = null) =>
        client.PostAsync($"/api/receiving/orders/{orderId}/deliveries/{delivery.Id}/void", new { reason, version = version ?? delivery.Version });

    [SqlServerFact]
    public async Task A_warehouse_keeper_lists_opens_records_and_voids_with_the_same_rules_and_never_sees_a_price()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin, "Visayas Steel Corp.");
        var later = await admin.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id, TwoLines(), expectedDate: "2026-03-20"));
        later = await ApproveAsync(admin, later);
        var sooner = await admin.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id, TwoLines(), expectedDate: "2026-03-05"));
        sooner = await ApproveAsync(admin, sooner);
        var undated = await ApprovedAsync(admin, project, firm);
        var draft = await DraftAsync(admin, project, firm);
        var closed = await ApprovedAsync(admin, project, firm);
        await admin.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{closed.Id}/close", new { reason = "Done", version = closed.Version });
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _k = keeper;

        // Approved orders with something left, soonest expected first, an order with no date last; a draft and a closed order are not listed.
        var list = await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders");
        Assert.Equal([sooner.Id, later.Id, undated.Id], list.Items.Select(item => item.Id));
        Assert.Equal(3, list.OrderCount);
        var row = list.Items[0];
        Assert.Equal((sooner.Number, "Visayas Steel Corp.", project.Name, null, new DateOnly(2026, 3, 5), "NotReceived"),
            (row.Number, row.SupplierName, row.ProjectName, row.SubProjectName, row.ExpectedDate, row.DeliveryState));
        Assert.Equal(sooner.Version, row.Version);
        Assert.Equal([sooner.Id], (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?search=" + sooner.Number)).Items.Select(item => item.Id));
        Assert.Empty((await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?search=nothing-like-this")).Items);
        Assert.Equal(2, (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?pageSize=2")).Items.Count);
        Assert.Single((await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?pageSize=2&page=2")).Items);
        Assert.Equal(3, (await keeper.GetAsync<ReceivingOrderPageDto>($"/api/receiving/orders?projectId={project.Id}")).OrderCount);
        Assert.Equal(0, (await keeper.GetAsync<ReceivingOrderPageDto>($"/api/receiving/orders?projectId={Guid.NewGuid()}")).OrderCount);

        // Opening shows the lines in quantities and no deliveries yet.
        var opened = await OpenAsync(keeper, sooner.Id);
        Assert.Equal(("Approved", "NotReceived", true, 0), (opened.Status, opened.DeliveryState, opened.CanRecord, opened.DeliveryCount));
        Assert.Equal([(1, "Rebar 16 mm", "pc", 10m, 0m, 10m), (2, "Tie wire", "pc", 3m, 0m, 3m)],
            opened.Lines.Select(line => (line.LineNumber, line.Description, line.Unit, line.OrderedQuantity, line.ReceivedQuantity, line.RemainingQuantity)));

        // Recording a partial delivery answers with the order in quantities; it is listed as partly received.
        var partly = await RecordAsync(keeper, opened, "DR-0012", (1, 4m), (2, 1.5m));
        Assert.Equal(("PartlyReceived", 1, true), (partly.DeliveryState, partly.DeliveryCount, partly.CanRecord));
        var delivery = Assert.Single(partly.Deliveries);
        Assert.Equal(("Posted", "DR-0012", "Test User", true), (delivery.Status, delivery.DeliveryReceiptNumber, delivery.RecordedByName, delivery.CanVoid));
        Assert.Equal([(1, 4m), (2, 1.5m)], delivery.Lines.Select(line => (line.LineNumber, line.Quantity)));
        Assert.Equal([(4m, 6m), (1.5m, 1.5m)], partly.Lines.Select(line => (line.ReceivedQuantity, line.RemainingQuantity)));
        Assert.True(partly.Version > opened.Version);
        Assert.Equal("PartlyReceived", (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders")).Items.Single(item => item.Id == sooner.Id).DeliveryState);

        // The rules are the purchasing ones: over-receipt, a number already used by the supplier (even spelled differently), nothing typed.
        var over = await TryRecordAsync(keeper, partly, "DR-1", (1, 6.0001m));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(over));
        Assert.Equal("Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less.", await ErrorMessageAsync(over, "lines"));
        var duplicate = await TryRecordAsync(keeper, await OpenAsync(keeper, later.Id), "dr 0012", (1, 1m));
        Assert.Equal(new HashSet<string> { "deliveryReceiptNumber" }, await ErrorFieldsAsync(duplicate));
        Assert.Equal("This delivery receipt is already recorded.", await ErrorMessageAsync(duplicate, "deliveryReceiptNumber"));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(await TryRecordAsync(keeper, partly, "DR-2")));
        Assert.Equal(1, (await OpenAsync(keeper, sooner.Id)).DeliveryCount);

        // The rest arrives: fully received, and the order leaves the list of what is awaited.
        var full = await RecordAsync(keeper, partly, "DR-0013", (1, 6m), (2, 1.5m));
        Assert.Equal(("FullyReceived", false, 2), (full.DeliveryState, full.CanRecord, full.DeliveryCount));
        Assert.Equal(["DR-0013", "DR-0012"], full.Deliveries.Select(item => item.DeliveryReceiptNumber));
        Assert.DoesNotContain(sooner.Id, (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders")).Items.Select(item => item.Id));
        var refusedFull = await TryRecordAsync(keeper, full, "DR-3", (1, 1m));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(refusedFull));

        // Voiding the second delivery takes its quantities back and puts the order on the list again. Its reason is never shown.
        var voided = await keeper.ReadAsync<ReceivingOrderDto>(await TryVoidAsync(keeper, sooner.Id, full.Deliveries[0], "Counted the wrong truck, 9,876,543 pesos"));
        Assert.Equal(("PartlyReceived", true), (voided.DeliveryState, voided.CanRecord));
        var shown = voided.Deliveries[0];
        Assert.Equal(("Void", "Test User", false), (shown.Status, shown.VoidedByName, shown.CanVoid));
        Assert.NotNull(shown.VoidedAtUtc);
        Assert.Equal([(4m, 6m), (1.5m, 1.5m)], voided.Lines.Select(line => (line.ReceivedQuantity, line.RemainingQuantity)));
        Assert.Contains(sooner.Id, (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders")).Items.Select(item => item.Id));
        Assert.DoesNotContain("9,876,543", JsonSerializer.Serialize(voided));
        var twice = await TryVoidAsync(keeper, sooner.Id, await Reload(keeper, sooner.Id, 0));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("This delivery was already voided.", await DetailAsync(twice));
        Assert.Equal(new HashSet<string> { "reason" }, await ErrorFieldsAsync(await TryVoidAsync(keeper, sooner.Id, voided.Deliveries[1], reason: "  ")));

        // Reading a draft or a closed order by id is allowed (to see what was received); recording on them refuses with the usual words.
        var draftView = await OpenAsync(keeper, draft.Id);
        Assert.Equal(("Draft", false), (draftView.Status, draftView.CanRecord));
        var refusedDraft = await keeper.PostAsync($"/api/receiving/orders/{draft.Id}/deliveries", Delivery(draftView, "DR-D", (1, 1m)));
        Assert.Equal(HttpStatusCode.Conflict, refusedDraft.StatusCode);
        Assert.Contains("is not approved yet", await DetailAsync(refusedDraft));
        var closedView = await OpenAsync(keeper, closed.Id);
        Assert.False(closedView.CanRecord);
        var refusedClosed = await TryRecordAsync(keeper, closedView, "DR-C", (1, 1m));
        Assert.Equal(HttpStatusCode.Conflict, refusedClosed.StatusCode);
        Assert.Equal("This order is closed.", await DetailAsync(refusedClosed));

        // What the keeper recorded is what purchasing sees on the order, audited the same way, without the typed reason.
        var purchasing = await GetOrderAsync(admin, sooner.Id);
        Assert.Equal(("PartlyReceived", 2), (purchasing.DeliveryState, purchasing.ReceiptCount));
        var trail = await admin.GetAsync<ConstructOPX.Application.Common.PagedResult<AuditEntryDto>>("/api/audit?entityType=GoodsReceipt");
        Assert.Contains(trail.Items, entry => entry.Action == "GoodsReceipt.Recorded" && entry.Summary.Contains(sooner.Number));
        Assert.Contains(trail.Items, entry => entry.Action == "GoodsReceipt.Voided" && entry.Summary.Contains(sooner.Number));
        Assert.DoesNotContain(trail.Items, entry => entry.Summary.Contains("9,876,543"));
    }

    private static async Task<ReceivingDeliveryDto> Reload(ApiClient client, Guid orderId, int index) => (await OpenAsync(client, orderId)).Deliveries[index];

    [SqlServerFact]
    public async Task A_warehouse_keeper_is_refused_on_every_purchasing_route_and_holds_no_purchasing_permission()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var order = await ApprovedAsync(admin, project, firm, OrderLine(quantity: 100m));
        var received = await ReceiveAsync(admin, order, "DR-0", (1, 1m));
        var receipt = received.Receipts.Single();
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _k = keeper;
        var id = order.Id;

        foreach (var response in new[]
                 {
                     await keeper.GetAsync("/api/purchase-orders"),
                     await keeper.GetAsync($"/api/purchase-orders/{id}"),
                     await keeper.GetAsync($"/api/projects/{project.Id}/purchase-order-lines"),
                     await keeper.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id)),
                     await keeper.PutAsync($"/api/purchase-orders/{id}", UpdateBody(order)),
                     await keeper.PostAsync($"/api/purchase-orders/{id}/approve", new { version = order.Version }),
                     await keeper.PostAsync($"/api/purchase-orders/{id}/cancel", new { reason = "x", version = order.Version }),
                     await keeper.PostAsync($"/api/purchase-orders/{id}/close", new { reason = "x", version = order.Version }),
                     await TryReceiveAsync(keeper, received, "DR-K", (1, 1m)),
                     await TryVoidDeliveryAsync(keeper, id, receipt)
                 })
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var session = await keeper.GetAsync("/api/identity/session");
        var permissions = (await ApiClient.ProblemAsync(session)).GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("receiving.record", permissions);
        Assert.DoesNotContain("purchasing.read", permissions);
        Assert.DoesNotContain("purchasing.receive", permissions);
        Assert.Equal(1, (await GetOrderAsync(admin, id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task Everyone_without_the_receiving_permission_is_refused_on_all_four_routes()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var order = await ApprovedAsync(admin, project, firm);
        var received = await ReceiveAsync(admin, order, "DR-0", (1, 1m));
        var view = await OpenAsync(admin, order.Id);

        // An administrator holds it too; the matrix test says why.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/receiving/orders")).StatusCode);

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant, AccessRole.HrOfficer, AccessRole.Viewer })
        {
            var (person, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _p = person;
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/receiving/orders")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync($"/api/receiving/orders/{order.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TryRecordAsync(person, view, "DR-R", (1, 1m))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await person.PostAsync($"/api/receiving/orders/{order.Id}/deliveries/{received.Receipts[0].Id}/void", new { reason = "x", version = received.Receipts[0].Version })).StatusCode);
        }

        Assert.Equal(1, (await GetOrderAsync(admin, order.Id)).ReceiptCount);
    }

    [SqlServerFact]
    public async Task Another_company_never_reaches_an_order_or_a_delivery_through_the_receiving_routes()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, theirUser) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var (theirKeeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, theirUser.TenantId);
        using var _tk = theirKeeper;
        var project = await CreateProjectAsync(mine);
        var firm = await CreateFirmAsync(mine);
        var order = await ApprovedAsync(mine, project, firm);
        var other = await ApprovedAsync(mine, project, firm);
        var received = await ReceiveAsync(mine, order, "DR-1", (1, 4m));
        var delivery = (await OpenAsync(mine, order.Id)).Deliveries.Single();
        var theirOrder = await ApprovedAsync(theirs, await CreateProjectAsync(theirs), await CreateFirmAsync(theirs));
        var view = await OpenAsync(mine, order.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await theirKeeper.GetAsync($"/api/receiving/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TryRecordAsync(theirKeeper, view, "DR-X", (1, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidAsync(theirKeeper, order.Id, delivery)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidAsync(theirKeeper, theirOrder.Id, delivery)).StatusCode);
        Assert.Equal([theirOrder.Id], (await theirKeeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders")).Items.Select(item => item.Id));
        Assert.Equal(0, (await theirKeeper.GetAsync<ReceivingOrderPageDto>($"/api/receiving/orders?projectId={project.Id}")).OrderCount);

        // Within a company a delivery is reached only through its own order.
        Assert.Equal(HttpStatusCode.NotFound, (await TryVoidAsync(mine, other.Id, delivery)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mine.PostAsync($"/api/receiving/orders/{order.Id}/deliveries/{Guid.NewGuid()}/void", new { reason = "x", version = 0 })).StatusCode);
        Assert.Equal(("Posted", 4m), ((await OpenAsync(mine, order.Id)).Deliveries.Single().Status, (await OpenAsync(mine, order.Id)).Lines[0].ReceivedQuantity));
        Assert.Equal(1, received.ReceiptCount);
    }

    [SqlServerFact]
    public async Task A_stale_void_is_a_conflict_and_a_repeated_request_returns_the_first_delivery()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var order = await ApprovedAsync(admin, project, firm);
        var other = await ApprovedAsync(admin, project, firm);
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _k = keeper;
        var view = await OpenAsync(keeper, order.Id);
        var requestId = Guid.NewGuid().ToString();

        var first = await keeper.PostAsync<ReceivingOrderDto>($"/api/receiving/orders/{order.Id}/deliveries", DeliveryRequest(view, "DR-1", requestId, (1, 10m)));
        var repeat = await keeper.PostAsync<ReceivingOrderDto>($"/api/receiving/orders/{order.Id}/deliveries", DeliveryRequest(view, "DR-1", requestId, (1, 10m)));
        Assert.Equal((first.Deliveries[0].Id, 1), (repeat.Deliveries[0].Id, repeat.DeliveryCount));
        // The id of one delivery may not be reused for another order.
        var reused = await keeper.PostAsync($"/api/receiving/orders/{other.Id}/deliveries", DeliveryRequest(await OpenAsync(keeper, other.Id), "DR-2", requestId, (1, 1m)));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        // The same id sent from the purchasing route answers with the purchasing view of the same delivery: one rule set, one idempotency.
        var viaPurchasing = await admin.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/receipts", DeliveryBody(order, [(1, 10m)], "DR-1", clientRequestId: requestId));
        Assert.Equal(1, viaPurchasing.ReceiptCount);

        var delivery = first.Deliveries[0];
        var stale = await TryVoidAsync(keeper, order.Id, delivery, version: delivery.Version + 1);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        Assert.Equal("Posted", (await OpenAsync(keeper, order.Id)).Deliveries.Single().Status);
        Assert.Equal(HttpStatusCode.OK, (await TryVoidAsync(keeper, order.Id, delivery)).StatusCode);
    }

    [SqlServerFact]
    public async Task No_response_from_the_receiving_routes_has_a_property_that_names_money()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var order = await ApprovedAsync(admin, project, firm, TwoLines());
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        using var _k = keeper;
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task<JsonElement> Walk(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, text);
            using var document = JsonDocument.Parse(text);
            Collect(document.RootElement, names);
            return document.RootElement.Clone();
        }

        await Walk(await keeper.GetAsync("/api/receiving/orders"));
        await Walk(await keeper.GetAsync($"/api/receiving/orders/{order.Id}"));
        var recorded = await Walk(await TryRecordAsync(keeper, await OpenAsync(keeper, order.Id), "DR-J1", (1, 4m), (2, 1m)));
        await Walk(await TryRecordAsync(keeper, await OpenAsync(keeper, order.Id), "DR-J2", (1, 1m)));
        var deliveryId = recorded.GetProperty("deliveries")[0].GetProperty("id").GetGuid();
        var version = recorded.GetProperty("deliveries")[0].GetProperty("version").GetInt32();
        await Walk(await keeper.PostAsync($"/api/receiving/orders/{order.Id}/deliveries/{deliveryId}/void", new { reason = "Wrong truck", version }));
        // The order now holds a posted delivery, a voided one, and lines: every shape of the response has been read.
        await Walk(await keeper.GetAsync($"/api/receiving/orders/{order.Id}"));
        await Walk(await keeper.GetAsync("/api/receiving/orders"));

        // The walk saw real names (so it is not passing on an empty read), and none names money.
        Assert.Contains("orderedQuantity", names);
        Assert.Contains("remainingQuantity", names);
        Assert.Contains("voidedByName", names);
        Assert.Contains("lines", names);
        // The exact names the Angular models (web/src/app/features/receiving/receiving.models.ts) read: the two sides of the contract agree.
        Assert.Equal(new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "items", "orderCount", "page", "pageSize", "id", "number", "supplierName", "projectName", "subProjectName", "expectedDate", "deliveryState",
            "version", "status", "supplierReference", "canRecord", "lines", "deliveries", "deliveryCount", "lineNumber", "description", "unit",
            "orderedQuantity", "receivedQuantity", "remainingQuantity", "orderLineId", "quantity", "receivedDate", "deliveryReceiptNumber", "note",
            "recordedByName", "recordedAtUtc", "voidedByName", "voidedAtUtc", "canVoid"
        }, names);
        Assert.DoesNotContain(names, name => MoneyWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static void Collect(JsonElement element, ISet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Collect(property.Value, names);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Collect(item, names);
                break;
        }
    }

    [SqlServerFact]
    public async Task The_list_and_the_order_read_cost_a_fixed_number_of_statements_however_many_orders_lines_and_deliveries_exist()
    {
        var (admin, user) = await SignedInAsync(_factory);
        var (soloAdmin, soloUser) = await SignedInAsync(_factory);
        using var _a = admin;
        using var _s = soloAdmin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var lines = Enumerable.Range(1, 6).Select(index => OrderLine($"Item {index}", 1_000m, 1_000)).ToArray();
        var (keeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, user.TenantId);
        var (soloKeeper, _) = await SignedInAsync(_factory, AccessRole.WarehouseKeeper, soloUser.TenantId);
        using var _k = keeper;
        using var _sk = soloKeeper;

        // One company with a single order awaiting delivery, another with ten.
        var single = await ApprovedAsync(soloAdmin, await CreateProjectAsync(soloAdmin), await CreateFirmAsync(soloAdmin), lines);
        var none = await ApprovedAsync(admin, project, firm, lines);
        var small = await ApprovedAsync(admin, project, firm, lines);
        await ReceiveAsync(admin, small, "DR-S-1", (1, 1m));
        var big = await ApprovedAsync(admin, project, firm, lines);
        for (var index = 0; index < 15; index++)
            big = await ReceiveAsync(admin, big, $"DR-B-{index}", (1 + index % 6, 1m), (1 + (index + 1) % 6, 2m));
        await VoidDeliveryAsync(admin, big.Id, big.Receipts[0]);
        for (var index = 0; index < 7; index++) await ApprovedAsync(admin, project, firm, lines);
        Assert.Equal(10, (await keeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?pageSize=100")).OrderCount);
        Assert.Equal(1, (await soloKeeper.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders?pageSize=100")).OrderCount);

        async Task<int> Count(ApiClient client, string url) =>
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        var measured = (
            ListOne: await Count(soloKeeper, "/api/receiving/orders?pageSize=100"),
            ListTen: await Count(keeper, "/api/receiving/orders?pageSize=100"),
            ListSearch: await Count(keeper, "/api/receiving/orders?search=PO&pageSize=100"),
            OpenSingle: await Count(soloKeeper, $"/api/receiving/orders/{single.Id}"),
            OpenNone: await Count(keeper, $"/api/receiving/orders/{none.Id}"),
            OpenSmall: await Count(keeper, $"/api/receiving/orders/{small.Id}"),
            OpenBig: await Count(keeper, $"/api/receiving/orders/{big.Id}"));

        // The list: a count and a page. One order: the order, its lines, what each line has received, the newest deliveries and their lines.
        Assert.Equal((2, 2, 2, 5, 5, 5, 5), measured);
    }
}
