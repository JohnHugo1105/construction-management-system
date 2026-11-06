using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.ChangeOrders;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CollaborationTestKit;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// Notifications: each person's own only, read and cleared, the bell's count, fixed statement counts, and the "your draft was decided" messages
/// written by purchasing, change orders, billing and subcontracts, which name people and document numbers and never an amount.
/// </summary>
public sealed class CollaborationNotificationApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public CollaborationNotificationApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_person_reads_and_clears_only_their_own_notifications()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (pia, piaUser) = await SignedInAsync(_factory, AccessRole.CostEngineer, adminUser.TenantId);
        var (leo, leoUser) = await SignedInAsync(_factory, AccessRole.Accountant, adminUser.TenantId);
        var (viewer, _) = await SignedInAsync(_factory, AccessRole.Viewer, adminUser.TenantId);
        using var _p = pia;
        using var _l = leo;
        using var _v = viewer;
        await CreateTaskAsync(admin, piaUser.UserId);
        await CreateTaskAsync(admin, piaUser.UserId);
        await CreateTaskAsync(admin, leoUser.UserId);

        Assert.Equal(new NotificationCountDto(2), await pia.GetAsync<NotificationCountDto>("/api/notifications/count"));
        var piasList = await NotificationsAsync(pia);
        Assert.Equal(2, piasList.Count);
        var leos = Assert.Single(await NotificationsAsync(leo));

        // Another person's notification is not found, to read or to mark.
        Assert.Equal(HttpStatusCode.NotFound, (await pia.PostAsync($"/api/notifications/{leos.Id}/read")).StatusCode);
        Assert.False(Assert.Single(await NotificationsAsync(leo)).IsRead);

        // Marking one read answers with the new count; again changes nothing; the unread filter leaves it out.
        Assert.Equal(new NotificationCountDto(1), await pia.PostAsync<NotificationCountDto>($"/api/notifications/{piasList[0].Id}/read"));
        Assert.Equal(new NotificationCountDto(1), await pia.PostAsync<NotificationCountDto>($"/api/notifications/{piasList[0].Id}/read"));
        var unread = await pia.GetAsync<PagedResult<NotificationDto>>("/api/notifications?unread=true");
        Assert.Equal(piasList[1].Id, Assert.Single(unread.Items).Id);

        // "Mark all as read" clears only the caller's.
        Assert.Equal(new NotificationCountDto(0), await pia.PostAsync<NotificationCountDto>("/api/notifications/read-all"));
        Assert.All(await NotificationsAsync(pia), item => Assert.True(item.IsRead));
        Assert.Equal(new NotificationCountDto(1), await leo.GetAsync<NotificationCountDto>("/api/notifications/count"));

        // Every role reads their own (a Viewer has none yet).
        Assert.Equal(new NotificationCountDto(0), await viewer.GetAsync<NotificationCountDto>("/api/notifications/count"));

        // Another company sees none of these.
        var (theirs, _) = await SignedInAsync(_factory);
        using var _t = theirs;
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/notifications/{leos.Id}/read")).StatusCode);
        Assert.Empty(await NotificationsAsync(theirs));
    }

    [SqlServerFact]
    public async Task The_drafter_is_told_when_a_purchase_order_is_approved_or_cancelled()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (drafter, _) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        using var _d = drafter;
        var firm = await PurchasingTestKit.CreateFirmAsync(world.Admin);

        var approved = await PurchasingTestKit.ApproveAsync(world.Admin, await PurchasingTestKit.DraftAsync(drafter, world.Project, firm));
        var draft = await PurchasingTestKit.DraftAsync(drafter, world.Project, firm);
        await world.Admin.PostAsync<PurchaseOrderDto>($"/api/purchase-orders/{draft.Id}/cancel", new { reason = "Ordered twice", version = draft.Version });

        var told = await NotificationsAsync(drafter);
        Assert.Equal([
            ("DraftDecided", "PurchaseOrder", draft.Id, $"Test Admin cancelled purchase order {draft.Number}."),
            ("DraftDecided", "PurchaseOrder", approved.Id, $"Test Admin approved purchase order {approved.Number}.")
        ], told.Select(item => (item.Kind, item.TargetKind, item.TargetId, item.Message)));
        Assert.All(told, item => Assert.DoesNotMatch(MoneyLike, item.Message));

        // Someone who approves their own draft is not told about it.
        await PurchasingTestKit.ApproveAsync(world.Admin, world.Order);
        Assert.Empty(await NotificationsAsync(world.Admin));
    }

    [SqlServerFact]
    public async Task The_drafter_is_told_when_a_change_order_is_approved_or_rejected()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (drafter, _) = await AsRoleAsync(_factory, world, AccessRole.CostEngineer);
        var (manager, _) = await AsRoleAsync(_factory, world, AccessRole.ProjectManager);
        using var _d = drafter;
        using var _m = manager;
        var orders = $"/api/projects/{world.Project.Id}/change-orders";
        async Task<ChangeOrderDto> SubmittedAsync() => await SubmittedChangeOrderAsync(drafter, await drafter.PostAsync<ChangeOrderDto>(orders,
            new { title = "Extra waterproofing", description = "The client asked for a membrane.", reason = "ClientRequest", contractPriceDeltaMinor = 1_500_000_00 }));

        var first = await SubmittedAsync();
        var approved = await manager.PostAsync<ChangeOrderDto>($"/api/change-orders/{first.Id}/approve", new { note = "Agreed", version = first.Version });
        var second = await SubmittedAsync();
        await manager.PostAsync<ChangeOrderDto>($"/api/change-orders/{second.Id}/reject", new { reason = "Not in scope", version = second.Version });

        var told = await NotificationsAsync(drafter);
        var projectName = world.Project.Name;
        Assert.Equal([
            ("ChangeOrder", second.Id, $"Test User rejected change order {second.Number} on {projectName}."),
            ("ChangeOrder", approved.Id, $"Test User approved change order {approved.Number} on {projectName}.")
        ], told.Select(item => (item.TargetKind, item.TargetId, item.Message)));
        Assert.All(told, item => Assert.Equal(world.Project.Id, item.ProjectId));
        Assert.All(told, item => Assert.DoesNotMatch(MoneyLike, item.Message));
    }

    [SqlServerFact]
    public async Task The_drafter_is_told_when_a_bill_is_issued_and_when_a_claim_is_certified()
    {
        var world = await WorldAsync(_factory);
        using var _a = world.Admin;
        var (accountant, _) = await AsRoleAsync(_factory, world, AccessRole.Accountant);
        var (manager, _) = await AsRoleAsync(_factory, world, AccessRole.ProjectManager);
        using var _ac = accountant;
        using var _m = manager;

        // The project manager prepares the bill and the claim; the accountant issues and certifies them.
        var bill = await DraftBillAsync(manager, (await CreateProjectAsync(world.Admin, contractPriceMinor: 5_000_000_00)).Id, 1_234_567_89);
        var issued = await accountant.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}/issue", new { version = bill.Version });
        var drafted = await SubcontractTestKit.DraftAsync(manager, world.Subcontract, 250_000_00);
        var certified = await accountant.ReadAsync<SubcontractDto>(await SubcontractTestKit.CertifyAsync(accountant, drafted,
            SubcontractTestKit.OpenDraft(drafted).Id));

        var told = await NotificationsAsync(manager);
        var claim = certified.Claims.Single(item => item.Status == "Certified");
        Assert.Equal([
            ("Subcontract", certified.Id, $"Test User certified claim {claim.ClaimNumber} on subcontract {certified.Number}."),
            ("ClientBill", issued.Id, $"Test User issued bill {issued.Number} on {issued.ProjectName}.")
        ], told.Select(item => (item.TargetKind, item.TargetId, item.Message)));
        Assert.All(told, item => Assert.Equal("DraftDecided", item.Kind));
        Assert.All(told, item => Assert.DoesNotMatch(MoneyLike, item.Message));
    }

    [SqlServerFact]
    public async Task The_list_and_the_count_cost_a_fixed_number_of_statements()
    {
        var (admin, adminUser) = await SignedInAsync(_factory);
        using var _a = admin;
        var (pia, piaUser) = await SignedInAsync(_factory, AccessRole.CostEngineer, adminUser.TenantId);
        using var _p = pia;
        await CreateTaskAsync(admin, piaUser.UserId);

        async Task<(int List, int Unread, int Count)> MeasureAsync() => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await pia.GetAsync("/api/notifications?pageSize=100")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await pia.GetAsync("/api/notifications?unread=true")).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await pia.GetAsync("/api/notifications/count")).StatusCode)));

        var withOne = await MeasureAsync();
        for (var index = 0; index < 9; index++) await CreateTaskAsync(admin, piaUser.UserId);
        var withTen = await MeasureAsync();

        Assert.Equal(10, (await pia.GetAsync<NotificationCountDto>("/api/notifications/count")).Unread);
        Assert.Equal(withOne, withTen);
        Assert.Equal((2, 2, 1), withTen);
    }
}
