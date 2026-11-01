using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Audit;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Purchasing;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.CostingTestKit;
using static Cms.Api.Tests.Features.PurchasingTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>
/// Purchase orders end to end on the real host: drafting, numbering, the approval step, cancelling and closing, every refusal, who may do
/// what, and that another company never sees an order. Money is in centavos: 50,000 is 500.00.
/// </summary>
public sealed class PurchasingApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public PurchasingApiTests(CmsApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task An_order_is_drafted_numbered_totalled_listed_changed_approved_and_closed()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client, "Riverside Tower");
        var firm = await CreateFirmAsync(client, "Visayas Steel Corp.");

        // 10 x 500.00 with 12% VAT included: gross 5,000.00, net 4,464.29, VAT 535.71. 3 x 99.99 before VAT: net 299.97, VAT 36.00, gross 335.97.
        var created = await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id,
            [OrderLine(vatTreatment: "Inclusive"), OrderLine("Tie wire", 3m, 9_999, "Exclusive", wbsCode: "3100")], supplierReference: "Quote 88", notes: "Deliver to gate 2"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await client.ReadAsync<PurchaseOrderDto>(created);
        Assert.Equal($"/api/purchase-orders/{draft.Id}", created.Headers.Location!.ToString());

        Assert.Equal(("PO-0001", "Draft"), (draft.Number, draft.Status));
        Assert.Equal(("Visayas Steel Corp.", "Riverside Tower"), (draft.SupplierName, draft.ProjectName));
        Assert.Equal((476_426L, 57_171L, 533_597L), (draft.NetMinor, draft.VatMinor, draft.GrossMinor));
        Assert.Equal([(1, 446_429L, 53_571L, 500_000L), (2, 29_997L, 3_600L, 33_597L)],
            draft.Lines.Select(line => (line.LineNumber, line.NetMinor, line.VatMinor, line.GrossMinor)));
        Assert.All(draft.Lines, line => Assert.Equal(1_200, line.VatRateBps));
        Assert.Equal(("Test Admin", "Quote 88", "Deliver to gate 2"), (draft.CreatedByName, draft.SupplierReference, draft.Notes));
        Assert.Equal((true, true, true, false), (draft.CanEdit, draft.CanApprove, draft.CanCancel, draft.CanClose));
        Assert.Equal(0, draft.CommittedGrossMinor);

        // Listing: newest first, filters, and a search that takes the percent sign literally.
        var second = await DraftAsync(client, project, firm);
        Assert.Equal("PO-0002", second.Number);
        Assert.Equal(["PO-0002", "PO-0001"], (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items.Select(item => item.Number));
        var item = (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>($"/api/purchase-orders?projectId={project.Id}&supplierId={firm.Id}&status=draft&search=quote")).Items.Single();
        Assert.Equal(("PO-0001", 2, 533_597L), (item.Number, item.LineCount, item.GrossMinor));
        Assert.Equal(["PO-0001"], (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders?search=visayas%20steel&status=Draft&pageSize=1&page=2")).Items.Select(row => row.Number));
        Assert.Empty((await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders?search=100%25")).Items);
        Assert.Empty((await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders?status=Approved")).Items);
        Assert.Equal(new HashSet<string> { "status" }, await ErrorFieldsAsync(await client.GetAsync("/api/purchase-orders?status=Done")));

        // A draft is replaced whole, lines included.
        var changed = await client.PutAsync<PurchaseOrderDto>($"/api/purchase-orders/{draft.Id}", UpdateBody(draft, [OrderLine("Cement", 4m, 25_000)], "Revised"));
        Assert.Equal(["Cement"], changed.Lines.Select(line => line.Description));
        Assert.Equal((100_000L, "Revised"), (changed.GrossMinor, changed.Notes));
        Assert.True(changed.Version > draft.Version);

        // Approving locks the order and commits what has not been invoiced.
        var approved = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{changed.Id}/approve", new { version = changed.Version });
        Assert.Equal(("Approved", "Test Admin"), (approved.Status, approved.ApprovedByName));
        Assert.NotNull(approved.ApprovedAtUtc);
        Assert.Equal((false, false, true, true), (approved.CanEdit, approved.CanApprove, approved.CanCancel, approved.CanClose));
        Assert.Equal((100_000L, 100_000L), (approved.CommittedNetMinor, approved.CommittedGrossMinor));
        Assert.Equal((100_000L, 0L, 100_000L, 0L), (approved.Lines[0].RemainingGrossMinor, approved.Lines[0].InvoicedGrossMinor, approved.Lines[0].GrossMinor, approved.Lines[0].OverByGrossMinor));

        // Closing releases the remainder; a closed order is final.
        var closed = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{approved.Id}/close", new { reason = "Delivered in full by another supplier", version = approved.Version });
        Assert.Equal(("Closed", "Delivered in full by another supplier", "Test Admin"), (closed.Status, closed.CloseReason, closed.ClosedByName));
        Assert.Equal(0, closed.CommittedGrossMinor);
        Assert.Equal((false, false, false, false), (closed.CanEdit, closed.CanApprove, closed.CanCancel, closed.CanClose));
        foreach (var step in new[] { "approve", "close", "cancel" })
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/purchase-orders/{closed.Id}/{step}", new { reason = "Again", version = closed.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsync($"/api/purchase-orders/{closed.Id}", UpdateBody(closed))).StatusCode);
    }

    [SqlServerFact]
    public async Task A_cancelled_order_records_why_and_a_firm_archived_later_does_not_block_closing_an_approved_order()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);

        var draft = await DraftAsync(client, project, firm);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/api/purchase-orders/{draft.Id}/cancel", new { reason = " ", version = draft.Version })).StatusCode);
        var cancelled = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{draft.Id}/cancel", new { reason = "Duplicate of PO-0002", version = draft.Version });
        Assert.Equal(("Cancelled", "Duplicate of PO-0002", "Test Admin"), (cancelled.Status, cancelled.CancelReason, cancelled.CancelledByName));
        Assert.Equal(0, cancelled.CommittedGrossMinor);

        // A firm archived after the order was approved doesn't strand the order.
        var approved = await ApprovedAsync(client, project, firm);
        var archivedFirm = await client.PostAsync<Cms.Application.Features.Suppliers.SupplierDto>($"/api/suppliers/{firm.Id}/archive", new { version = firm.Version });
        Assert.True(archivedFirm.IsArchived);
        var closed = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{approved.Id}/close", new { reason = (string?)null, version = approved.Version });
        Assert.Equal(("Closed", null), (closed.Status, closed.CloseReason));
        Assert.Equal(firm.Name, closed.SupplierName);
    }

    [SqlServerFact]
    public async Task Every_refusal_says_what_is_wrong_and_a_form_with_mistakes_does_not_use_up_a_number()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var other = await CreateProjectAsync(client);
        var phase = (await AddSubProjectAsync(client, other, "Phase 1")).SubProjects.Single();
        var firm = await CreateFirmAsync(client);
        var crew = await CreateFirmAsync(client, supplier: false, subcontractor: true);

        // Validation: every problem together, per field.
        var bad = await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id, [OrderLine(" ", 0m), OrderLine(unitCostMinor: -1)],
            orderDate: "2999-01-01", expectedDate: "2025-01-01", supplierReference: new string('r', 81), notes: new string('n', 501)));
        var fields = await ErrorFieldsAsync(bad);
        Assert.Equal(new HashSet<string> { "orderDate", "expectedDate", "supplierReference", "notes", "lines" }, fields);
        var lineMessages = (await ApiClient.ProblemAsync(bad)).GetProperty("errors").GetProperty("lines").EnumerateArray().Select(message => message.GetString()!).ToArray();
        Assert.Contains(lineMessages, message => message.StartsWith("Line 1: Enter a description", StringComparison.Ordinal));
        Assert.Contains(lineMessages, message => message.StartsWith("Line 1: Enter the quantity greater than zero", StringComparison.Ordinal));
        Assert.Contains(lineMessages, message => message.StartsWith("Line 2: The unit cost can't be negative", StringComparison.Ordinal));

        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id, []))));
        Assert.Equal(new HashSet<string> { "lines" }, await ErrorFieldsAsync(await client.PostAsync("/api/purchase-orders",
            OrderBody(project.Id, firm.Id, Enumerable.Range(1, 101).Select(index => OrderLine($"Item {index}", 1m, 100))))));
        Assert.Equal(new HashSet<string> { "supplierId" }, await ErrorFieldsAsync(await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, Guid.Empty))));
        Assert.Equal(new HashSet<string> { "projectId" }, await ErrorFieldsAsync(await client.PostAsync("/api/purchase-orders", OrderBody(Guid.Empty, firm.Id))));
        Assert.Equal(new HashSet<string> { "subProjectId" }, await ErrorFieldsAsync(await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id, subProjectId: phase.Id))));

        // Business rules: a firm of the wrong kind, an archived firm, an archived project.
        var wrongKind = await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, crew.Id));
        Assert.Equal(HttpStatusCode.Conflict, wrongKind.StatusCode);
        Assert.Contains("isn't set up as a supplier", await DetailAsync(wrongKind));
        var archivedFirm = await CreateFirmAsync(client);
        await client.PostAsync($"/api/suppliers/{archivedFirm.Id}/archive", new { version = archivedFirm.Version });
        var refused = await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, archivedFirm.Id));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("is archived", await DetailAsync(refused));

        // None of the refusals above used a number: the first good order is PO-0001.
        var first = await DraftAsync(client, project, firm);
        Assert.Equal("PO-0001", first.Number);

        // An order of an archived project can't be drafted, approved, cancelled or closed until the project is restored.
        var approved = await ApprovedAsync(client, project, firm);
        var draftToApprove = await DraftAsync(client, project, firm);
        var archivedProject = await ArchiveAsync(client, project);
        foreach (var attempt in new Func<Task<HttpResponseMessage>>[]
                 {
                     () => client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id)),
                     () => client.PostAsync($"/api/purchase-orders/{draftToApprove.Id}/approve", new { version = draftToApprove.Version }),
                     () => client.PostAsync($"/api/purchase-orders/{draftToApprove.Id}/cancel", new { reason = "x", version = draftToApprove.Version }),
                     () => client.PostAsync($"/api/purchase-orders/{approved.Id}/close", new { reason = "x", version = approved.Version }),
                     () => client.PostAsync($"/api/purchase-orders/{approved.Id}/cancel", new { reason = "x", version = approved.Version })
                 })
        {
            var response = await attempt();
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Restore it", await DetailAsync(response));
        }

        var archivedView = await GetOrderAsync(client, approved.Id);
        Assert.True(archivedView.ProjectArchived);
        Assert.Equal((false, false, false, false), (archivedView.CanEdit, archivedView.CanApprove, archivedView.CanCancel, archivedView.CanClose));
        Assert.NotNull(archivedProject);
    }

    [SqlServerFact]
    public async Task An_approved_order_cannot_be_changed_approved_twice_or_closed_while_still_a_draft_and_a_matched_invoice_blocks_cancelling()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);

        var draft = await DraftAsync(client, project, firm);
        var closeDraft = await client.PostAsync($"/api/purchase-orders/{draft.Id}/close", new { reason = (string?)null, version = draft.Version });
        Assert.Equal(HttpStatusCode.Conflict, closeDraft.StatusCode);
        Assert.Contains("Only an approved order can be closed", await DetailAsync(closeDraft));

        var approved = await ApproveAsync(client, draft);
        var again = await client.PostAsync($"/api/purchase-orders/{approved.Id}/approve", new { version = approved.Version });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("This order is already approved.", await DetailAsync(again));
        var edit = await client.PutAsync($"/api/purchase-orders/{approved.Id}", UpdateBody(approved));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Contains("lines are locked", await DetailAsync(edit));

        await InvoiceAsync(client, project.Id, approved.Lines[0].Id, 10_000);

        // Matching the invoice touched the order, so a page opened before it is stale: the cancel is a conflict, not a silent success.
        var stale = await client.PostAsync($"/api/purchase-orders/{approved.Id}/cancel", new { reason = "Mistake", version = approved.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));

        var fresh = await GetOrderAsync(client, approved.Id);
        var cancel = await client.PostAsync($"/api/purchase-orders/{approved.Id}/cancel", new { reason = "Mistake", version = fresh.Version });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Contains("Close it instead", await DetailAsync(cancel));
        Assert.False(fresh.CanCancel);
    }

    [SqlServerFact]
    public async Task Cancelling_and_closing_keep_the_typed_reason_off_the_activity_log()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var toCancel = await ApprovedAsync(client, project, firm);
        var toClose = await ApprovedAsync(client, project, firm);

        await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toCancel.Id}/cancel", new { reason = "Supplier quoted 9,876,543 pesos in error", version = toCancel.Version });
        await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toClose.Id}/close", new { reason = "Delivered short by 1,234,567 pesos", version = toClose.Version });

        var trail = await client.GetAsync<PagedResult<AuditEntryDto>>("/api/audit?entityType=PurchaseOrder");
        Assert.Contains(trail.Items, entry => entry.Action == "PurchaseOrder.Cancelled");
        Assert.Contains(trail.Items, entry => entry.Action == "PurchaseOrder.Closed");
        Assert.DoesNotContain(trail.Items, entry => entry.Summary.Contains("9,876,543") || entry.Summary.Contains("1,234,567") || entry.Summary.Contains("Reason"));
        // The reasons are still on the orders themselves.
        Assert.Equal("Supplier quoted 9,876,543 pesos in error", (await GetOrderAsync(client, toCancel.Id)).CancelReason);
    }

    [SqlServerFact]
    public async Task A_stale_version_is_a_conflict_on_every_change()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var draft = await DraftAsync(client, project, firm);
        await client.PutAsync<PurchaseOrderDto>($"/api/purchase-orders/{draft.Id}", UpdateBody(draft, notes: "Someone else saved first"));

        foreach (var response in new[]
                 {
                     await client.PutAsync($"/api/purchase-orders/{draft.Id}", UpdateBody(draft)),
                     await client.PostAsync($"/api/purchase-orders/{draft.Id}/approve", new { version = draft.Version }),
                     await client.PostAsync($"/api/purchase-orders/{draft.Id}/cancel", new { reason = "x", version = draft.Version }),
                     await client.PostAsync($"/api/purchase-orders/{draft.Id}/close", new { reason = "x", version = draft.Version })
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("concurrency_conflict", await RuleCodeAsync(response));
        }

        Assert.Equal("Someone else saved first", (await GetOrderAsync(client, draft.Id)).Notes);
    }

    [SqlServerFact]
    public async Task A_repeated_request_id_returns_the_first_order_and_is_refused_for_anything_else()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var otherFirm = await CreateFirmAsync(client);
        var requestId = Guid.NewGuid().ToString();

        var first = await client.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id, clientRequestId: requestId));
        var repeat = await client.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id, clientRequestId: requestId));

        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal("PO-0001", repeat.Number);
        Assert.Equal(1, (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Total);

        var reused = await client.PostAsync("/api/purchase-orders", OrderBody(project.Id, otherFirm.Id, clientRequestId: requestId));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        // The same form submitted twice at the very same moment still raises one order.
        var simultaneous = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id, clientRequestId: simultaneous))));
        var orders = await Task.WhenAll(responses.Select(response => client.ReadAsync<PurchaseOrderDto>(response)));
        Assert.Single(orders.Select(order => order.Id).Distinct());
        Assert.Equal(2, (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Total);
    }

    [SqlServerFact]
    public async Task Numbers_run_per_company_and_each_company_starts_at_one()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var myProject = await CreateProjectAsync(mine);
        var theirProject = await CreateProjectAsync(theirs);
        var myFirm = await CreateFirmAsync(mine);
        var theirFirm = await CreateFirmAsync(theirs);

        Assert.Equal("PO-0001", (await DraftAsync(mine, myProject, myFirm)).Number);
        Assert.Equal("PO-0002", (await DraftAsync(mine, myProject, myFirm)).Number);
        Assert.Equal("PO-0001", (await DraftAsync(theirs, theirProject, theirFirm)).Number);
    }

    [SqlServerFact]
    public async Task Each_permission_boundary_is_enforced_by_role()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var firm = await CreateFirmAsync(admin);
        var draft = await DraftAsync(admin, project, firm);
        var approved = await ApprovedAsync(admin, project, firm);

        // Cost Engineer: reads and drafts, but does not approve, cancel or close.
        var (cost, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, user.TenantId);
        using var _cost = cost;
        Assert.Equal(HttpStatusCode.OK, (await cost.GetAsync("/api/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cost.GetAsync($"/api/purchase-orders/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cost.GetAsync($"/api/projects/{project.Id}/purchase-order-lines")).StatusCode);
        var drafted = await cost.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id));
        Assert.Equal(HttpStatusCode.OK, (await cost.PutAsync($"/api/purchase-orders/{drafted.Id}", UpdateBody(drafted))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"/api/purchase-orders/{draft.Id}/approve", new { version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"/api/purchase-orders/{approved.Id}/cancel", new { reason = "x", version = approved.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"/api/purchase-orders/{approved.Id}/close", new { reason = "x", version = approved.Version })).StatusCode);

        // Project Manager: reads and approves, but does not draft or edit.
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        using var _pm = manager;
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsync($"/api/purchase-orders/{draft.Id}", UpdateBody(draft))).StatusCode);
        Assert.Equal("Approved", (await manager.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{draft.Id}/approve", new { version = draft.Version })).Status);
        Assert.Equal("Closed", (await manager.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{approved.Id}/close", new { reason = "Done", version = approved.Version })).Status);

        // Accountant: drafts and approves.
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, user.TenantId);
        using var _acct = accountant;
        var byAccountant = await accountant.PostAsync<PurchaseOrderDto>("/api/purchase-orders", OrderBody(project.Id, firm.Id));
        Assert.Equal("Approved", (await ApproveAsync(accountant, byAccountant)).Status);

        // Viewer: read only.
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, user.TenantId);
        using var _v = viewer;
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/purchase-orders/{drafted.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/purchase-orders/{drafted.Id}/approve", new { version = drafted.Version })).StatusCode);

        // Warehouse Keeper and HR Officer: no purchasing at all.
        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (outsider, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _o = outsider;
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/purchase-orders")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/purchase-orders/{draft.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/projects/{project.Id}/purchase-order-lines")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync("/api/purchase-orders", OrderBody(project.Id, firm.Id))).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Another_company_never_sees_or_changes_an_order_even_by_its_id()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var project = await CreateProjectAsync(mine);
        var firm = await CreateFirmAsync(mine);
        var order = await ApprovedAsync(mine, project, firm);
        var draft = await DraftAsync(mine, project, firm);
        var theirProject = await CreateProjectAsync(theirs);
        var theirFirm = await CreateFirmAsync(theirs);

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/purchase-orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PutAsync($"/api/purchase-orders/{draft.Id}", UpdateBody(draft, supplierId: theirFirm.Id, projectId: theirProject.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/purchase-orders/{draft.Id}/approve", new { version = draft.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/purchase-orders/{order.Id}/cancel", new { reason = "x", version = order.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/purchase-orders/{order.Id}/close", new { reason = "x", version = order.Version })).StatusCode);
        Assert.Empty((await theirs.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items);
        Assert.Empty((await theirs.GetAsync<PagedResult<PurchaseOrderListItemDto>>($"/api/purchase-orders?projectId={project.Id}")).Items);

        // They can't raise an order with my firm or for my project, nor read my project's lines.
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/purchase-orders", OrderBody(theirProject.Id, firm.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync("/api/purchase-orders", OrderBody(project.Id, theirFirm.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/purchase-order-lines")).StatusCode);

        // Nor match one of their invoices to my order line; the answer doesn't confirm that the line exists.
        var attempt = await theirs.PostAsync($"/api/projects/{theirProject.Id}/cost-entries", Cost().With("purchaseOrderLineId", order.Lines[0].Id));
        Assert.Equal(new HashSet<string> { "purchaseOrderLineId" }, await ErrorFieldsAsync(attempt));
        Assert.Equal("approved", (await GetOrderAsync(mine, order.Id)).Status.ToLowerInvariant());
        Assert.Equal(0, (await GetOrderAsync(mine, order.Id)).InvoiceCount);
    }

    [SqlServerFact]
    public async Task An_invoice_is_matched_to_a_line_of_an_approved_order_of_the_same_project_and_shows_on_both_pages()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var otherProject = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        var order = await ApprovedAsync(client, project, firm, OrderLine("Rebar 16 mm", 10m, 50_000), OrderLine("Tie wire", 2m, 10_000));
        var draft = await DraftAsync(client, project, firm);
        var elsewhere = await ApprovedAsync(client, otherProject, firm);
        var toCancel = await DraftAsync(client, project, firm);
        var cancelled = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toCancel.Id}/cancel", new { reason = "x", version = toCancel.Version });
        var toClose = await ApprovedAsync(client, project, firm);
        var closed = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{toClose.Id}/close", new { reason = "x", version = toClose.Version });

        // Refused: a draft's line, another project's line, a cancelled or closed order's line, an unknown line.
        foreach (var (lineId, fragment) in new[]
                 {
                     (draft.Lines[0].Id, "not approved yet"), (elsewhere.Lines[0].Id, "another project"), (cancelled.Lines[0].Id, "was cancelled"),
                     (closed.Lines[0].Id, "is closed"), (Guid.NewGuid(), "wasn't found")
                 })
        {
            var response = await client.PostAsync($"/api/projects/{project.Id}/cost-entries", Cost().With("purchaseOrderLineId", lineId));
            Assert.Equal(new HashSet<string> { "purchaseOrderLineId" }, await ErrorFieldsAsync(response));
            Assert.Contains(fragment, await ErrorMessageAsync(response, "purchaseOrderLineId"));
        }

        // Matched: the invoice carries the line and the order number, and the order shows what was invoiced and what remains.
        var invoice = await InvoiceAsync(client, project.Id, order.Lines[0].Id, 200_000);
        Assert.Equal((order.Lines[0].Id, "PO-0001"), (invoice.PurchaseOrderLineId, invoice.PurchaseOrderNumber));
        var viewed = await GetOrderAsync(client, order.Id);
        Assert.Equal(1, viewed.InvoiceCount);
        Assert.Equal((200_000L, 300_000L, 0L), (viewed.Lines[0].InvoicedGrossMinor, viewed.Lines[0].RemainingGrossMinor, viewed.Lines[0].OverByGrossMinor));
        Assert.Equal((0L, 20_000L), (viewed.Lines[1].InvoicedGrossMinor, viewed.Lines[1].RemainingGrossMinor));
        var shown = Assert.Single(viewed.Invoices);
        Assert.Equal((invoice.Id, 1, 200_000L, "Visayas Steel Corp."), (shown.CostEntryId, shown.LineNumber, shown.GrossMinor, shown.SourceDealer));
        Assert.Equal((200_000L, 320_000L), (viewed.InvoicedGrossMinor, viewed.CommittedGrossMinor));

        // The cost list shows which order each invoice is against.
        var listed = (await client.GetAsync<TotaledPage<CostEntryDto>>($"/api/projects/{project.Id}/cost-entries")).Items.Single();
        Assert.Equal(("PO-0001", order.Lines[0].Id), (listed.PurchaseOrderNumber, listed.PurchaseOrderLineId!.Value));
        Assert.Equal("PO-0001", (await client.GetAsync<CostEntryDto>($"/api/cost-entries/{invoice.Id}")).PurchaseOrderNumber);

        // The choice list for the cost dialog: lines of approved orders of this project only, with what remains on each.
        var open = await client.GetAsync<List<OpenPurchaseOrderLineDto>>($"/api/projects/{project.Id}/purchase-order-lines");
        Assert.Equal([("PO-0001", 1, "Rebar 16 mm", 300_000L), ("PO-0001", 2, "Tie wire", 20_000L)],
            open.Select(line => (line.OrderNumber, line.LineNumber, line.Description, line.RemainingGrossMinor)));

        // Editing: move the match, keep it after the order closed, refuse a new match to a closed order, clear it.
        var moved = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{invoice.Id}", Edit(invoice, order.Lines[1].Id));
        Assert.Equal(order.Lines[1].Id, moved.PurchaseOrderLineId);
        // The 200,000 invoice now sits on the 20,000 line: that line is over by 180,000 and the first line is untouched.
        var afterMove = await GetOrderAsync(client, order.Id);
        Assert.Equal((500_000L, 0L, 180_000L), (afterMove.Lines[0].RemainingGrossMinor, afterMove.Lines[1].RemainingGrossMinor, afterMove.Lines[1].OverByGrossMinor));
        var back = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{invoice.Id}", Edit(moved, order.Lines[0].Id));
        var orderNow = await GetOrderAsync(client, order.Id);
        var closedLater = await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/close", new { reason = "Done", version = orderNow.Version });
        var kept = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{invoice.Id}", Edit(back, order.Lines[0].Id, description: "Rebar delivery, corrected"));
        Assert.Equal((order.Lines[0].Id, "Rebar delivery, corrected"), (kept.PurchaseOrderLineId, kept.Description));
        var toClosed = await client.PutAsync($"/api/cost-entries/{invoice.Id}", Edit(kept, closed.Lines[0].Id));
        Assert.Equal(new HashSet<string> { "purchaseOrderLineId" }, await ErrorFieldsAsync(toClosed));
        var cleared = await client.PutAsync<CostEntryDto>($"/api/cost-entries/{invoice.Id}", Edit(kept, null));
        Assert.Null(cleared.PurchaseOrderLineId);
        Assert.Null(cleared.PurchaseOrderNumber);
        Assert.Equal(0, (await GetOrderAsync(client, order.Id)).InvoiceCount);
        Assert.Equal("Closed", closedLater.Status);
    }

    /// <summary>
    /// The dashboard row, the dashboard total (the company has this one project) and the project's cost summary must all say the same thing
    /// about commitments: two places answering one question never disagree. Null means no figures at all.
    /// </summary>
    private static async Task AssertAgreeAsync(ApiClient client, Guid projectId, CommitmentPositionDto? expected)
    {
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(expected, dashboard.Projects.Single(row => row.Id == projectId).Commitments);
        Assert.Equal(expected, dashboard.Totals.Commitments);
        Assert.Equal(expected, (await SummaryAsync(client, projectId)).Commitments);
    }

    private static Dictionary<string, object?> Edit(CostEntryDto entry, Guid? lineId, string? description = null) => new()
    {
        ["subProjectId"] = entry.SubProjectId, ["division"] = entry.Division, ["mainScope"] = entry.MainScope, ["subScope"] = entry.SubScope, ["wbsCode"] = entry.WbsCode,
        ["description"] = description ?? entry.Description, ["itemType"] = entry.ItemType, ["quantity"] = entry.Quantity, ["unit"] = entry.Unit,
        ["unitCostMinor"] = entry.UnitCostMinor, ["vatTreatment"] = entry.VatTreatment, ["entryDate"] = entry.EntryDate.ToString("yyyy-MM-dd"),
        ["remarks"] = entry.Remarks, ["sourceDealer"] = entry.SourceDealer, ["invoiceNumber"] = entry.InvoiceNumber, ["version"] = entry.Version,
        ["purchaseOrderLineId"] = lineId
    };

    [SqlServerFact]
    public async Task Committed_cost_follows_the_order_and_the_dashboard_and_the_project_page_agree_at_every_step()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 1_000_000);

        // Nothing committed: no commitment figures anywhere.
        await AssertAgreeAsync(client, project.Id, null);

        // A draft commits nothing.
        var draft = await DraftAsync(client, project, firm, OrderLine("Rebar", 1m, 500_000), OrderLine("Cement", 1m, 300_000));
        await AssertAgreeAsync(client, project.Id, null);

        // Approved: 800,000 is committed; forecast = actual 0 + 800,000, 80% of the 1,000,000 budget.
        var order = await ApproveAsync(client, draft);
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(800_000, 800_000, 200_000, "OnTrack"));

        // An invoice for 200,000 on the first line: committed falls to 600,000, actual rises to 200,000; the forecast is unchanged.
        var firstInvoice = await InvoiceAsync(client, project.Id, order.Lines[0].Id, 200_000);
        Assert.Equal(200_000, (await DashboardRowAsync(client, project.Id)).ActualMinor);
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(600_000, 800_000, 200_000, "OnTrack"));

        // An invoice larger than its line (another 400,000 on a line of 500,000, 600,000 in all): the line is over by 100,000, its remainder is
        // zero, and it does not reduce what the other line still owes.
        var overInvoice = await InvoiceAsync(client, project.Id, order.Lines[0].Id, 400_000);
        var viewed = await GetOrderAsync(client, order.Id);
        Assert.Equal((600_000L, 0L, 100_000L), (viewed.Lines[0].InvoicedGrossMinor, viewed.Lines[0].RemainingGrossMinor, viewed.Lines[0].OverByGrossMinor));
        Assert.Equal(300_000, viewed.CommittedGrossMinor);
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(300_000, 900_000, 100_000, "NearLimit"));

        // Voiding the larger invoice returns the remainder it had consumed.
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{overInvoice.Id}/void", new { reason = "Wrong supplier", version = overInvoice.Version });
        Assert.Equal(600_000, (await GetOrderAsync(client, order.Id)).CommittedGrossMinor);
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(600_000, 800_000, 200_000, "OnTrack"));

        // Invoice both lines in full: nothing is open any more, and the figures disappear.
        await InvoiceAsync(client, project.Id, order.Lines[0].Id, 300_000);
        var lastInvoice = await InvoiceAsync(client, project.Id, order.Lines[1].Id, 300_000);
        Assert.Equal(0, (await GetOrderAsync(client, order.Id)).CommittedGrossMinor);
        await AssertAgreeAsync(client, project.Id, null);

        // Voiding one of them brings back exactly what it matched.
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{lastInvoice.Id}/void", new { reason = "Duplicate", version = lastInvoice.Version });
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(300_000, 800_000, 200_000, "OnTrack"));

        // Closing releases the rest.
        var current = await GetOrderAsync(client, order.Id);
        await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/close", new { reason = "Supplier delivered less", version = current.Version });
        await AssertAgreeAsync(client, project.Id, null);
        Assert.Equal(firstInvoice.Id, (await GetOrderAsync(client, order.Id)).Invoices.Last().CostEntryId);
    }

    [SqlServerFact]
    public async Task A_cancelled_order_commits_nothing_and_a_forecast_beyond_the_budget_says_so()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 1_000_000);

        var order = await ApprovedAsync(client, project, firm, OrderLine("Steel", 1m, 1_200_000));
        // Forecast 1,200,000 against a budget of 1,000,000: over budget before a peso is spent.
        await AssertAgreeAsync(client, project.Id, new CommitmentPositionDto(1_200_000, 1_200_000, -200_000, "OverBudget"));
        Assert.Equal("OnTrack", (await DashboardRowAsync(client, project.Id)).Health);

        await client.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{order.Id}/cancel", new { reason = "Re-tender", version = order.Version });
        await AssertAgreeAsync(client, project.Id, null);
    }

    [SqlServerFact]
    public async Task Committed_cost_is_worked_out_on_net_and_gross_separately_and_follows_the_companys_comparison_basis()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 1_000_000);

        // 10 x 100.00 before 12% VAT: net 1,000.00 (100,000), VAT 120.00, gross 1,120.00 (112,000).
        var order = await ApprovedAsync(client, project, firm, OrderLine("Cement", 10m, 10_000, "Exclusive"));
        Assert.Equal((100_000L, 112_000L), (order.CommittedNetMinor, order.CommittedGrossMinor));
        Assert.Equal(112_000, (await DashboardRowAsync(client, project.Id)).Commitments!.CommittedMinor);

        // An invoice of 5 x 100.00 before VAT (net 500.00, gross 560.00) takes half of each.
        await InvoiceAsync(client, project.Id, order.Lines[0].Id, 50_000, "Exclusive");
        var viewed = await GetOrderAsync(client, order.Id);
        Assert.Equal((50_000L, 56_000L), (viewed.CommittedNetMinor, viewed.CommittedGrossMinor));
        Assert.Equal(56_000, (await SummaryAsync(client, project.Id)).Commitments!.CommittedMinor);

        // Where the company recovers its VAT, budget, actual and commitments are all compared before VAT.
        await ChangeOrganizationAsync(client, varianceBasis: "ExcludingVat");
        Assert.Equal(50_000, (await DashboardRowAsync(client, project.Id)).Commitments!.CommittedMinor);
        Assert.Equal(50_000, (await SummaryAsync(client, project.Id)).Commitments!.CommittedMinor);
    }

    [SqlServerFact]
    public async Task Commitments_of_an_archived_project_show_only_where_archived_projects_are_included()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await ApprovedAsync(client, project, firm);
        await ArchiveAsync(client, project);

        Assert.DoesNotContain((await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects, row => row.Id == project.Id);
        var row = (await client.GetAsync<DashboardDto>("/api/reports/dashboard?includeArchived=true")).Projects.Single(item => item.Id == project.Id);
        Assert.Equal(500_000, row.Commitments!.CommittedMinor);
        Assert.Equal(row.Commitments, (await SummaryAsync(client, project.Id)).Commitments);
    }

    [SqlServerFact]
    public async Task Another_companys_orders_never_count_in_my_commitments()
    {
        var (mine, _) = await SignedInAsync(_factory);
        var (theirs, _) = await SignedInAsync(_factory);
        using var _m = mine;
        using var _t = theirs;
        var project = await CreateProjectAsync(mine);
        var theirProject = await CreateProjectAsync(theirs);
        await ApprovedAsync(mine, project, await CreateFirmAsync(mine));
        await ApprovedAsync(theirs, theirProject, await CreateFirmAsync(theirs), OrderLine("Theirs", 1m, 900_000));

        var dashboard = await mine.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(500_000, dashboard.Totals.Commitments!.CommittedMinor);
        Assert.Equal(500_000, dashboard.Projects.Single().Commitments!.CommittedMinor);
    }

    [SqlServerFact]
    public async Task The_list_the_order_page_the_choice_list_and_the_dashboard_cost_a_fixed_number_of_statements()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var firm = await CreateFirmAsync(client);
        await BudgetAsync(client, project.Id, 1_000_000);

        var one = await ApprovedAsync(client, project, firm, OrderLine());
        await InvoiceAsync(client, project.Id, one.Lines[0].Id, 1_000);

        async Task<(int List, int Get, int Lines, int Dashboard, int Summary, int Costs)> MeasureAsync(Guid orderId) => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/purchase-orders?pageSize=100")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/purchase-orders/{orderId}")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/purchase-order-lines")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/cost-summary")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/cost-entries")).StatusCode)));

        var small = await MeasureAsync(one.Id);

        // Twelve more orders of several lines each, a second project, and many invoices matched across the lines.
        var second = await CreateProjectAsync(client);
        var big = one;
        for (var index = 0; index < 12; index++)
        {
            big = await ApprovedAsync(client, index % 2 == 0 ? project : second, firm, OrderLine("A", 1m, 10_000), OrderLine("B", 2m, 7_500), OrderLine("C", 3m, 1_000));
            if (index % 2 == 0)
                foreach (var line in big.Lines) await InvoiceAsync(client, project.Id, line.Id, 500);
        }

        var large = await MeasureAsync(big.Id);

        Assert.Equal(small, large);
        // The count and the page of orders; the order, its lines, what each line is invoiced and the newest invoices; the project and the
        // approved lines with what is invoiced; the company, projects, budget, actual and one grouped statement for every commitment source;
        // the project, company, six grouped sums and the sources; the cost list's project check, totals and page.
        var sources = _factory.CommitmentSourceCount();
        Assert.Equal((2, 4, 3, 4 + sources, 8 + sources, 3), large);
        Assert.Equal(13, (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders?pageSize=100")).Total);
    }

    [SqlServerFact]
    public async Task The_purchasing_demo_seeds_four_orders_whose_open_remainder_is_what_the_dashboard_shows()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client, "Riverside Demo");
        await BudgetAsync(client, project.Id, 50_000_000);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Cms.Infrastructure.Persistence.CmsDbContext>();
            var organization = await db.Set<Cms.Domain.Features.Organizations.Organization>().IgnoreQueryFilters().SingleAsync(item => item.Id == user.TenantId);
            var domainProject = await db.Set<Cms.Domain.Features.Projects.Project>().IgnoreQueryFilters().Include(item => item.SubProjects).SingleAsync(item => item.Id == project.Id);
            await Cms.Infrastructure.Seeding.PurchasingDemo.SeedAsync(db, organization, domainProject, user.UserId, user.UserId,
                organization.TodayLocal(DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);
        }

        var orders = (await client.GetAsync<PagedResult<PurchaseOrderListItemDto>>("/api/purchase-orders")).Items;
        Assert.Equal(["PO-0004", "PO-0003", "PO-0002", "PO-0001"], orders.OrderBy(order => order.Number, StringComparer.Ordinal).Reverse().Select(order => order.Number));
        Assert.Equal(["Closed", "Draft", "Approved", "Approved"], orders.OrderByDescending(order => order.Number, StringComparer.Ordinal).Select(order => order.Status));
        // Tiles: 1,580,000.00 ordered less 632,000.00 invoiced, plus the plumbing set of 1,850,000.00 not yet invoiced.
        Assert.Equal(948_000_00 + 1_850_000_00, (await DashboardRowAsync(client, project.Id)).Commitments!.CommittedMinor);
        Assert.Equal("PO-0001", (await client.GetAsync<List<OpenPurchaseOrderLineDto>>($"/api/projects/{project.Id}/purchase-order-lines")).First().OrderNumber);
    }
}
