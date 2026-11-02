using System.Globalization;
using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.Bonds;
using ConstructOPX.Application.Features.ChangeOrders;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.Application.Features.Suppliers;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// "Needs your action" end to end on SQL Server: which groups each role receives (a group it can't act on is absent, not empty), what each
/// group counts, that another company's and archived projects' records never appear, that acting on an item removes it, that a Warehouse
/// Keeper never receives anything money-shaped, and that both routes cost a fixed number of statements however many records wait.
/// </summary>
public sealed class NeedsActionApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const string Full = "/api/needs-action";
    private const string Count = "/api/needs-action/count";

    private static readonly string[] EveryGroup =
    [
        "purchase-orders-to-approve", "change-orders-to-approve", "bills-to-issue", "claims-to-certify", "payroll-to-approve", "bills-to-chase",
        "deliveries-expected", "bonds-to-attend", "progress-not-updated", "attendance-not-recorded", "time-clock-to-review", "statutory-needs-setup"
    ];

    private readonly ConstructOpxApiFactory _factory;

    public NeedsActionApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    // ---- Who receives what ------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Each_role_receives_exactly_the_groups_it_can_act_on_and_the_count_route_adds_them_up()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;

        var expected = new Dictionary<AccessRole, string[]>
        {
            [AccessRole.Administrator] = EveryGroup,
            [AccessRole.ProjectManager] =
            [
                "purchase-orders-to-approve", "change-orders-to-approve", "bills-to-chase", "deliveries-expected", "bonds-to-attend",
                "progress-not-updated", "attendance-not-recorded", "time-clock-to-review"
            ],
            [AccessRole.Accountant] =
            [
                "purchase-orders-to-approve", "bills-to-issue", "claims-to-certify", "payroll-to-approve", "bills-to-chase", "deliveries-expected",
                "bonds-to-attend", "statutory-needs-setup"
            ],
            [AccessRole.CostEngineer] = ["deliveries-expected", "progress-not-updated"],
            [AccessRole.HrOfficer] = ["attendance-not-recorded", "time-clock-to-review"],
            [AccessRole.WarehouseKeeper] = ["deliveries-expected"],
            [AccessRole.Viewer] = []
        };

        foreach (var (role, keys) in expected)
        {
            using var client = await AsRoleAsync(world, role);
            var result = await client.GetAsync<NeedsActionDto>(Full);
            var total = (await client.GetAsync<NeedsActionCountDto>(Count)).Total;

            // Absent, not empty: a group the role can't act on is not in the answer at all.
            Assert.Equal(keys.Order(), result.Groups.Select(group => group.Key).Order());
            // The world holds one waiting record in every group.
            Assert.All(result.Groups, group => Assert.Equal((group.Key, 1), (group.Key, group.Count)));
            Assert.Equal(result.Groups.Sum(group => group.Count), total);
            Assert.Equal(keys.Length, total);
        }
    }

    [SqlServerFact]
    public async Task Every_group_counts_the_right_records_in_plain_words_with_amounts_only_where_listed()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;
        var today = BondTestKit.Today();

        var result = await world.Admin.GetAsync<NeedsActionDto>(Full);
        var groups = result.Groups.ToDictionary(group => group.Key);

        // Urgent groups first (overdue bills, a late delivery, an expired bond), then the page order.
        Assert.Equal(["bills-to-chase", "deliveries-expected", "bonds-to-attend", "time-clock-to-review", "purchase-orders-to-approve",
            "change-orders-to-approve", "bills-to-issue", "claims-to-certify", "payroll-to-approve", "progress-not-updated", "attendance-not-recorded", "statutory-needs-setup"],
            result.Groups.Select(group => group.Key));

        var order = Single(groups, "purchase-orders-to-approve");
        Assert.Equal((world.DraftOrder.Id, world.DraftOrder.ProjectId, world.DraftOrder.GrossMinor, false),
            (order.Id, order.ProjectId!.Value, order.AmountMinor!.Value, order.IsUrgent));
        Assert.Equal($"{world.DraftOrder.Number}, {world.Firm.Name}", order.Label);
        Assert.Equal("Waiting since today", order.Detail);

        var change = Single(groups, "change-orders-to-approve");
        Assert.Equal((world.ChangeOrder.Id, world.ChangeOrder.BudgetEffectGrossMinor), (change.Id, change.AmountMinor!.Value));
        Assert.Equal($"{world.ChangeOrder.Number} Extra waterproofing", change.Label);

        var draftBill = Single(groups, "bills-to-issue");
        Assert.Equal((world.DraftBill.Id, world.DraftBill.NetReceivableMinor), (draftBill.Id, draftBill.AmountMinor!.Value));
        Assert.Equal("Progress bill for ACME Realty", draftBill.Label);

        var claim = Single(groups, "claims-to-certify");
        Assert.Equal((world.ClaimId, world.Subcontract.Id, world.ClaimGrossMinor), (claim.Id, claim.ParentId!.Value, claim.AmountMinor!.Value));
        Assert.Equal($"Claim on {world.Subcontract.Number}, {world.Subcontract.SubcontractorName}", claim.Label);

        // Pay data stays out: the period and the worker count, no amount.
        var payroll = Single(groups, "payroll-to-approve");
        Assert.Equal((world.PayrollRunIds[0], (long?)null), (payroll.Id, payroll.AmountMinor));
        Assert.Equal("Payroll for 1 Feb - 14 Feb 2026", payroll.Label);
        Assert.Equal("1 worker, waiting since today", payroll.Detail);

        var chase = Single(groups, "bills-to-chase");
        Assert.Equal((world.IssuedBill.Id, world.IssuedBill.NetReceivableMinor, true, world.IssuedBill.DueDate),
            (chase.Id, chase.AmountMinor!.Value, chase.IsUrgent, chase.DueDate!.Value));
        Assert.Equal($"{world.IssuedBill.Number}, ACME Realty", chase.Label);
        Assert.Equal($"10 days overdue, due {Day(today.AddDays(-10))}", chase.Detail);

        // Deliveries come from the quantity-only read model: never an amount.
        var delivery = Single(groups, "deliveries-expected");
        Assert.Equal((world.ApprovedOrder.Id, (long?)null, true), (delivery.Id, delivery.AmountMinor, delivery.IsUrgent));
        Assert.Equal($"1 day late, expected {Day(today.AddDays(-1))}", delivery.Detail);

        var bond = Single(groups, "bonds-to-attend");
        Assert.Equal((world.Bond.Id, world.Bond.AmountMinor, true), (bond.Id, bond.AmountMinor!.Value, bond.IsUrgent));
        Assert.Equal($"Performance bond {world.Bond.BondNumber}, Metro Surety", bond.Label);
        Assert.Equal($"Expired 3 days ago, on {Day(today.AddDays(-3))}", bond.Detail);

        var progress = Single(groups, "progress-not-updated");
        Assert.Equal((world.Sites[0].Id, world.Sites[0].Name, "No progress recorded yet", (long?)null),
            (progress.Id, progress.Label, progress.Detail, progress.AmountMinor));

        var attendance = Single(groups, "attendance-not-recorded");
        Assert.Equal((world.Sites[0].Id, (long?)null), (attendance.Id, attendance.AmountMinor));
        Assert.Equal($"No attendance recorded for {today.AddDays(-1).ToString("ddd d MMM", CultureInfo.InvariantCulture)}", attendance.Detail);

        var clock = Single(groups, "time-clock-to-review");
        Assert.Equal((world.Sites[0].Id, (long?)null, true), (clock.Id, clock.AmountMinor, clock.IsUrgent));
        Assert.Equal($"1 worker to review for {TimeClockTestKit.Today.AddDays(-2).ToString("ddd d MMM", CultureInfo.InvariantCulture)}; 1 needs a closer look", clock.Detail);

        var tables = Single(groups, "statutory-needs-setup");
        Assert.Equal("No table in force for SSS, PhilHealth, Pag-IBIG and withholding tax, so payroll can't work them out.", tables.Detail);
    }

    [SqlServerFact]
    public async Task A_warehouse_keeper_receives_only_deliveries_and_nothing_money_shaped_from_either_route()
    {
        var world = await WorldAsync();
        using var admin = world.Admin;
        using var keeper = await AsRoleAsync(world, AccessRole.WarehouseKeeper);

        var full = await RawAsync(keeper, Full);
        var count = await RawAsync(keeper, Count);

        Assert.Equal(["deliveries-expected"], full.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("key").GetString()));
        var row = full.GetProperty("groups")[0].GetProperty("rows")[0];
        Assert.False(row.TryGetProperty("amountMinor", out var _));

        string[] banned = ["minor", "cost", "price", "vat", "total", "invoice", "committed", "amount", "currency", "pay", "gross", "net"];
        Assert.DoesNotContain(PropertyNames(full), name => banned.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        // The badge's own number is the one exception: a count of waiting records named "total" by the route's contract, not money.
        Assert.Equal(["total"], PropertyNames(count));
        Assert.Equal(JsonValueKind.Number, count.GetProperty("total").ValueKind);
        Assert.Equal(1, count.GetProperty("total").GetInt32());
    }

    // ---- What is left out ---------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Another_companys_records_never_appear()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;
        var (other, _) = await SignedInAsync(_factory);
        using var __ = other;
        var otherProject = await CreateProjectAsync(other);
        var otherDraft = await PurchasingTestKit.DraftAsync(other, otherProject, await PurchasingTestKit.CreateFirmAsync(other));

        var mine = await world.Admin.GetAsync<NeedsActionDto>(Full);
        var theirs = await other.GetAsync<NeedsActionDto>(Full);

        Assert.DoesNotContain(mine.Groups.SelectMany(group => group.Rows), row => row.Id == otherDraft.Id || row.ProjectId == otherProject.Id);
        Assert.Equal(1, mine.Groups.Single(group => group.Key == "purchase-orders-to-approve").Count);
        Assert.Equal([otherDraft.Id], theirs.Groups.Single(group => group.Key == "purchase-orders-to-approve").Rows.Select(row => row.Id));
        Assert.DoesNotContain(theirs.Groups.SelectMany(group => group.Rows), row => world.AllIds.Contains(row.Id));
        // Only their one draft waits on the other company's administrator: with no worker and no payroll their statutory tables are not yet a task.
        Assert.Equal(1, (await other.GetAsync<NeedsActionCountDto>(Count)).Total);
    }

    [SqlServerFact]
    public async Task Records_of_an_archived_project_never_appear()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var today = BondTestKit.Today();
        var firm = await PurchasingTestKit.CreateFirmAsync(admin);
        var project = await StartedWithBudgetLineAsync(admin, null);
        await PurchasingTestKit.DraftAsync(admin, project, firm);
        await LateOrderAsync(admin, project, firm, today);
        await BondTestKit.CreateAsync(admin, project.Id, validUntil: BondTestKit.DaysFromToday(-3));
        await DraftBillAsync(admin, project.Id, 100_000_00, today.AddDays(-40));
        await SubcontractTestKit.DraftAsync(admin, await SubcontractTestKit.AwardedAsync(admin, project.Id), 100_000_00);

        var before = await admin.GetAsync<NeedsActionDto>(Full);
        foreach (var key in new[] { "purchase-orders-to-approve", "deliveries-expected", "bonds-to-attend", "bills-to-issue", "claims-to-certify", "progress-not-updated" })
            Assert.Equal((key, 1), (key, before.Groups.Single(group => group.Key == key).Count));

        await ArchiveAsync(admin, await admin.GetAsync<ProjectDto>($"/api/projects/{project.Id}"));

        var after = await admin.GetAsync<NeedsActionDto>(Full);
        Assert.All(after.Groups, group => Assert.Equal((group.Key, 0), (group.Key, group.Count)));
        Assert.Equal(0, (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);
    }

    [SqlServerFact]
    public async Task Acting_on_an_item_removes_it_from_the_list_and_the_count()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;
        using var accountant = await AsRoleAsync(world, AccessRole.Accountant);
        var before = await accountant.GetAsync<NeedsActionDto>(Full);
        var totalBefore = (await accountant.GetAsync<NeedsActionCountDto>(Count)).Total;

        // Approving the draft order takes it off the approvals. It has no expected date, so it is not yet due and joins no other group.
        await PurchasingTestKit.ApproveAsync(accountant, await PurchasingTestKit.GetOrderAsync(accountant, world.DraftOrder.Id));
        var approved = await accountant.GetAsync<NeedsActionDto>(Full);
        var orders = approved.Groups.Single(group => group.Key == "purchase-orders-to-approve");
        Assert.Equal((0, 0), (orders.Count, orders.Rows.Count));
        Assert.Equal(Group(before, "deliveries-expected").Count, Group(approved, "deliveries-expected").Count);

        // Certifying the claim takes it off the queue altogether: with the approved order, the badge is down by two.
        var subcontract = await accountant.GetAsync<SubcontractDto>($"/api/subcontracts/{world.Subcontract.Id}");
        Assert.True((await SubcontractTestKit.CertifyAsync(accountant, subcontract, world.ClaimId)).IsSuccessStatusCode);
        var certified = await accountant.GetAsync<NeedsActionDto>(Full);
        Assert.Equal(0, Group(certified, "claims-to-certify").Count);
        Assert.DoesNotContain(certified.Groups.SelectMany(group => group.Rows), row => row.Id == world.ClaimId);
        Assert.DoesNotContain(Group(certified, "deliveries-expected").Rows, row => row.Id == world.DraftOrder.Id);
        Assert.Equal(totalBefore, (await accountant.GetAsync<NeedsActionCountDto>(Count)).Total + 2);
    }

    // ---- What each rule leaves out ------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Attendance_lists_a_project_only_when_it_recorded_a_week_ago_and_nothing_yesterday_and_orders_the_biggest_crews_first()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var today = BondTestKit.Today();
        var yesterday = today.AddDays(-1);
        string Yesterday() => BondTestKit.Date(yesterday);
        string WeekAgo() => BondTestKit.Date(yesterday.AddDays(-7));

        // Listed: nothing recorded yesterday, entries a week ago, two workers posted.
        var (quiet, quietCrew) = await SiteWithCrewAsync(admin, "Beta quiet site", 2);
        await WorkforceTestKit.RecordAsync(admin, quietCrew[0].Id, quiet.Id, WeekAgo(), 8m);

        // Listed, but with one worker only: the second is on their rest day yesterday.
        var (resting, restingCrew) = await SiteWithCrewAsync(admin, "Alpha resting site", 2);
        await SetRestDayAsync(admin, restingCrew[1], yesterday.DayOfWeek.ToString());
        await WorkforceTestKit.RecordAsync(admin, restingCrew[0].Id, resting.Id, WeekAgo(), 8m);

        // Listed: the only entry yesterday was voided, and a voided entry is not recorded attendance.
        var (voidedToday, voidedTodayCrew) = await SiteWithCrewAsync(admin, "Epsilon voided site", 1);
        await WorkforceTestKit.RecordAsync(admin, voidedTodayCrew[0].Id, voidedToday.Id, WeekAgo(), 8m);
        var entered = await WorkforceTestKit.RecordAsync(admin, voidedTodayCrew[0].Id, voidedToday.Id, Yesterday(), 8m);
        await admin.PostAsync<AttendanceEntryDto>($"/api/attendance/{entered.Id}/void", new { reason = "Entered twice", version = entered.Version });

        // Not listed: some entries yesterday (the other worker is sick, and absences cannot be recorded, so one entry is enough).
        var (recorded, recordedCrew) = await SiteWithCrewAsync(admin, "Gamma recorded site", 2);
        await WorkforceTestKit.RecordAsync(admin, recordedCrew[0].Id, recorded.Id, WeekAgo(), 8m);
        await WorkforceTestKit.RecordAsync(admin, recordedCrew[0].Id, recorded.Id, Yesterday(), 8m);

        // Not listed: nothing yesterday, but nothing a week ago either (a Monday-to-Saturday site on a Sunday).
        var (sixDay, sixDayCrew) = await SiteWithCrewAsync(admin, "Delta six-day site", 1);
        await WorkforceTestKit.RecordAsync(admin, sixDayCrew[0].Id, sixDay.Id, BondTestKit.Date(yesterday.AddDays(-2)), 8m);

        // Not listed: the entry a week ago was voided, so the project did not record that day.
        var (voidedBefore, voidedBeforeCrew) = await SiteWithCrewAsync(admin, "Zeta voided last week site", 1);
        var before = await WorkforceTestKit.RecordAsync(admin, voidedBeforeCrew[0].Id, voidedBefore.Id, WeekAgo(), 8m);
        await admin.PostAsync<AttendanceEntryDto>($"/api/attendance/{before.Id}/void", new { reason = "Entered twice", version = before.Version });

        // Not listed: the only posted worker rests yesterday, so nobody is expected.
        var (restOnly, restOnlyCrew) = await SiteWithCrewAsync(admin, "Eta rest day site", 1);
        await SetRestDayAsync(admin, restOnlyCrew[0], yesterday.DayOfWeek.ToString());
        await WorkforceTestKit.RecordAsync(admin, restOnlyCrew[0].Id, restOnly.Id, WeekAgo(), 8m);

        var group = Group(await admin.GetAsync<NeedsActionDto>(Full), "attendance-not-recorded");

        // The biggest crew first, then by name: "Alpha" would sort before "Beta" if the crew sizes (2 and 1) were not what orders them.
        Assert.Equal(["Beta quiet site", "Alpha resting site", "Epsilon voided site"], group.Rows.Select(row => row.Label));
        Assert.Equal([quiet.Id, resting.Id, voidedToday.Id], group.Rows.Select(row => row.Id));
        Assert.Equal(3, group.Count);
        Assert.All(group.Rows, row => Assert.Equal($"No attendance recorded for {yesterday.ToString("ddd d MMM", CultureInfo.InvariantCulture)}", row.Detail));
    }

    [SqlServerFact]
    public async Task Attendance_is_not_asked_for_on_a_company_holiday()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var yesterday = BondTestKit.Today().AddDays(-1);
        var (site, crew) = await SiteWithCrewAsync(admin, "Holiday site", 1);
        await WorkforceTestKit.RecordAsync(admin, crew[0].Id, site.Id, BondTestKit.Date(yesterday.AddDays(-7)), 8m);
        Assert.Equal([site.Id], Group(await admin.GetAsync<NeedsActionDto>(Full), "attendance-not-recorded").Rows.Select(row => row.Id));

        Assert.True((await admin.PostAsync("/api/payroll-holidays", new { date = BondTestKit.Date(yesterday), name = "Founders day (test)", type = "Regular" })).IsSuccessStatusCode);

        Assert.Equal(0, Group(await admin.GetAsync<NeedsActionDto>(Full), "attendance-not-recorded").Count);
        // The badge agrees with the page.
        var after = await admin.GetAsync<NeedsActionDto>(Full);
        Assert.Equal(after.Groups.Sum(group => group.Count), (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);
    }

    [SqlServerFact]
    public async Task Progress_is_only_asked_for_on_a_project_that_has_a_budget_line_to_record_it_against()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var withoutLine = await StartAsync(admin, await WorkforceTestKit.NewProjectAsync(admin, "No budget line yet"));
        Assert.Equal(0, Group(await admin.GetAsync<NeedsActionDto>(Full), "progress-not-updated").Count);

        var withLine = await StartedWithBudgetLineAsync(admin, "With a budget line");

        var group = Group(await admin.GetAsync<NeedsActionDto>(Full), "progress-not-updated");
        Assert.Equal(1, group.Count);
        Assert.Equal([withLine.Id], group.Rows.Select(row => row.Id));
        Assert.DoesNotContain(group.Rows, row => row.Id == withoutLine.Id);
    }

    [SqlServerFact]
    public async Task Deliveries_are_listed_and_counted_only_once_due_while_the_receiving_screen_still_lists_every_open_order()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var today = BondTestKit.Today();
        var project = await CreateProjectAsync(admin);
        var firm = await PurchasingTestKit.CreateFirmAsync(admin);
        async Task<PurchaseOrderDto> ApprovedAsync(DateOnly? expected) => await PurchasingTestKit.ApproveAsync(admin, await admin.PostAsync<PurchaseOrderDto>(
            "/api/purchase-orders", PurchasingTestKit.OrderBody(project.Id, firm.Id, expectedDate: expected is { } date ? BondTestKit.Date(date) : null)));
        var late = await ApprovedAsync(today.AddDays(-3));
        var dueToday = await ApprovedAsync(today);
        await ApprovedAsync(today.AddDays(1));
        await ApprovedAsync(null);

        var result = await admin.GetAsync<NeedsActionDto>(Full);
        var group = Group(result, "deliveries-expected");

        // The most overdue first; tomorrow's order and the one with no date are not due, so they are neither listed nor counted.
        Assert.Equal(2, group.Count);
        Assert.Equal([late.Id, dueToday.Id], group.Rows.Select(row => row.Id));
        Assert.Equal([($"3 days late, expected {Day(today.AddDays(-3))}", true), ("Due today", false)], group.Rows.Select(row => (row.Detail, row.IsUrgent)));
        Assert.Equal(result.Groups.Sum(item => item.Count), (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);

        // The receiving screen is unchanged: every open order.
        Assert.Equal(4, (await admin.GetAsync<ReceivingOrderPageDto>("/api/receiving/orders")).OrderCount);
    }

    [SqlServerFact]
    public async Task The_statutory_tables_are_a_task_only_once_the_company_has_a_worker_or_a_payroll()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;

        // A brand-new company: the group is there for those who may act on it, but empty, and the badge stays at 0.
        var empty = Group(await admin.GetAsync<NeedsActionDto>(Full), "statutory-needs-setup");
        Assert.Equal((0, 0), (empty.Count, empty.Rows.Count));
        Assert.Equal(0, (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);

        // An active worker makes payroll real: the tables are missing, so the row appears.
        var worker = await WorkforceTestKit.HireAsync(admin, "W-001");
        var needed = Group(await admin.GetAsync<NeedsActionDto>(Full), "statutory-needs-setup");
        Assert.Equal(1, needed.Count);
        Assert.Contains("No table in force for SSS", Assert.Single(needed.Rows).Detail);
        Assert.Equal(1, (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);

        // A draft payroll keeps it there when the worker has left.
        var site = await WorkforceTestKit.NewProjectAsync(admin, "Payroll site");
        worker = await WorkforceTestKit.AssignAsync(admin, worker, site);
        await WorkforceTestKit.RecordAsync(admin, worker.Id, site.Id, "2026-02-02", 8m);
        await WorkforceTestKit.CreateRunAsync(admin, "2026-02-01", "2026-02-14");
        var left = await admin.PostAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/status",
            new { status = "Resigned", effectiveDate = "2026-06-30", reason = (string?)null, version = worker.Version });
        Assert.Equal("Resigned", left.Status);
        Assert.Equal(1, Group(await admin.GetAsync<NeedsActionDto>(Full), "statutory-needs-setup").Count);
    }

    [SqlServerFact]
    public async Task A_change_order_that_is_still_a_draft_and_a_discarded_bill_are_not_waiting_for_anyone()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var budgeted = await LockedBudgetProjectAsync(admin);

        var draft = await admin.PostAsync<ChangeOrderDto>($"/api/projects/{budgeted.Id}/change-orders",
            new { title = "Extra waterproofing", description = "The client asked for a membrane.", reason = "ClientRequest", contractPriceDeltaMinor = 0 });
        Assert.Equal(0, Group(await admin.GetAsync<NeedsActionDto>(Full), "change-orders-to-approve").Count);
        var withLine = await admin.PostAsync<ChangeOrderDto>($"/api/change-orders/{draft.Id}/lines", new
        {
            kind = "Add", description = "Waterproofing membrane", itemType = "Material", quantity = 3m, unit = "roll", unitCostMinor = 33_333,
            vatTreatment = "Inclusive", entryDate = "2026-02-01", version = draft.Version
        });
        await admin.PostAsync<ChangeOrderDto>($"/api/change-orders/{draft.Id}/submit", new { version = withLine.Version });
        Assert.Equal([draft.Id], Group(await admin.GetAsync<NeedsActionDto>(Full), "change-orders-to-approve").Rows.Select(row => row.Id));

        var billed = await CreateProjectAsync(admin);
        var bill = await DraftBillAsync(admin, billed.Id, 100_000_00, BondTestKit.Today().AddDays(-35));
        Assert.Equal(1, Group(await admin.GetAsync<NeedsActionDto>(Full), "bills-to-issue").Count);
        Assert.True((await admin.PostAsync($"/api/client-invoices/{bill.Id}/discard", new { reason = "Wrong period", version = bill.Version })).IsSuccessStatusCode);
        Assert.Equal(0, Group(await admin.GetAsync<NeedsActionDto>(Full), "bills-to-issue").Count);
    }

    [SqlServerFact]
    public async Task The_amount_to_chase_is_what_is_still_outstanding_after_partial_payments_and_ignores_voided_ones()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var today = BondTestKit.Today();
        var project = await CreateProjectAsync(admin);
        var draft = await DraftBillAsync(admin, project.Id, 100_000_00, today.AddDays(-40));
        var bill = await admin.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{draft.Id}/issue", new { version = draft.Version });
        var net = bill.NetReceivableMinor;
        Task<ClientInvoiceDto> PayAsync(ClientInvoiceDto current, long amount) => admin.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{current.Id}/payments", new
        {
            amountMinor = amount, paidOn = BondTestKit.Date(today.AddDays(-5)), method = "BankTransfer", reference = "BDO 1234", note = (string?)null,
            clientRequestId = (string?)null, version = current.Version
        });
        async Task<NeedsActionRowDto> ChaseAsync() => Assert.Single(Group(await admin.GetAsync<NeedsActionDto>(Full), "bills-to-chase").Rows);

        Assert.Equal(net, (await ChaseAsync()).AmountMinor);

        // A partial payment stands: the bill is chased for the rest.
        bill = await PayAsync(bill, 10_000_00);
        Assert.Equal(net - 10_000_00, (await ChaseAsync()).AmountMinor);

        // A payment that is then voided does not count.
        bill = await PayAsync(bill, 20_000_00);
        Assert.Equal(net - 30_000_00, (await ChaseAsync()).AmountMinor);
        var second = bill.Payments.Single(payment => payment.AmountMinor == 20_000_00);
        bill = await admin.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{bill.Id}/payments/{second.Id}/void", new { reason = "Bounced", version = bill.Version });
        Assert.Equal(net - 10_000_00, (await ChaseAsync()).AmountMinor);

        // Paid in full, it is no longer chased.
        await PayAsync(bill, net - 10_000_00);
        Assert.Equal(0, Group(await admin.GetAsync<NeedsActionDto>(Full), "bills-to-chase").Count);
    }

    // ---- Groups other features contribute ------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task A_contributed_group_appears_only_for_the_roles_it_acts_for_adds_into_both_totals_and_sorts_urgent_first()
    {
        var source = new FakeSource();
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<INeedsActionSource>(source)));
        var admin = await TenantSeeder.CreateTenantAsync(_factory);
        async Task<ApiClient> SignInAsync(AccessRole role)
        {
            var user = role == AccessRole.Administrator ? admin : await TenantSeeder.AddUserAsync(_factory, admin.TenantId, role);
            var client = new ApiClient(host);
            await client.SignInAsync(user.Username, user.Password);
            return client;
        }

        // Roles that cannot act on it never cost it a read, on either route (a Viewer holds no group at all).
        foreach (var role in new[] { AccessRole.Administrator, AccessRole.WarehouseKeeper, AccessRole.Viewer })
        {
            using var other = await SignInAsync(role);
            var full = await other.GetAsync<NeedsActionDto>(Full);
            var count = await other.GetAsync<NeedsActionCountDto>(Count);
            Assert.DoesNotContain(full.Groups, group => group.Key == FakeSource.GroupKey);
            Assert.Equal(full.Groups.Sum(group => group.Count), count.Total);
        }
        Assert.Equal((0, 0), (source.CountCalls, source.ReadCalls));

        // The HR Officer can: its true count is in both totals, and its urgent row puts it above the attendance group, which comes first on page order.
        using var hr = await SignInAsync(AccessRole.HrOfficer);
        var result = await hr.GetAsync<NeedsActionDto>(Full);
        Assert.Equal([FakeSource.GroupKey, "attendance-not-recorded", "time-clock-to-review"], result.Groups.Select(group => group.Key));
        Assert.Equal(FakeSource.Rows, result.Groups[0].Count);
        Assert.Equal(FakeSource.Rows, result.Groups.Sum(group => group.Count));
        Assert.Equal(FakeSource.Rows, (await hr.GetAsync<NeedsActionCountDto>(Count)).Total);
        // The queue read it once for its rows; the badge route read its count once.
        Assert.Equal((1, 1), (source.CountCalls, source.ReadCalls));
    }

    [SqlServerFact]
    public void Every_registered_source_acts_only_for_roles_holding_a_permission_the_web_page_knows()
    {
        using var scope = _factory.Services.CreateScope();
        var sources = scope.ServiceProvider.GetServices<INeedsActionSource>().ToArray();

        foreach (var role in AccessRoles.Split(AccessRoles.All))
            foreach (var source in sources.Where(source => source.CanActOn(role)))
                Assert.True(NeedsActionGroups.ActingPermissions.Any(permission => Permissions.IsAllowed(role, permission)),
                    $"{source.Key} acts for {role}, which holds none of NeedsActionGroups.ActingPermissions (and the web NEEDS_ACTION_PERMISSIONS)");
    }

    /// <summary>A group a feature contributes: three waiting records, one urgent row, for the HR Officer only; it counts how often it is read.</summary>
    private sealed class FakeSource : INeedsActionSource
    {
        public const string GroupKey = "fake-group";
        public const int Rows = 3;
        private int _countCalls, _readCalls;

        public int CountCalls => Volatile.Read(ref _countCalls);

        public int ReadCalls => Volatile.Read(ref _readCalls);

        public string Key => GroupKey;

        public bool CanActOn(AccessRole roles) => roles.HasFlag(AccessRole.HrOfficer);

        public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _countCalls);
            return Task.FromResult(Rows);
        }

        public Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _readCalls);
            return Task.FromResult(new NeedsActionGroupDto(GroupKey, Rows,
                [new NeedsActionRowDto(Guid.NewGuid(), null, "Something waiting", null, null, "Overdue by a day", null, null, true, null)]));
        }
    }

    // ---- Cost ---------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Both_routes_cost_a_fixed_number_of_statements_however_many_records_wait()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;

        async Task<(int Full, int Count)> MeasureAsync() => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync(Full)).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync(Count)).StatusCode)));

        var small = await MeasureAsync();
        await AddRecordsAsync(_factory, world.Admin, world.Firm, 9, firstPeriod: 1);
        var large = await MeasureAsync();

        var result = await world.Admin.GetAsync<NeedsActionDto>(Full);
        Assert.All(result.Groups.Where(group => group.Key != "statutory-needs-setup"), group =>
        {
            Assert.Equal((group.Key, 10), (group.Key, group.Count));
            Assert.Equal(NeedsActionRules.RowsShown, group.Rows.Count);
        });
        Assert.Equal(small, large);
        // The organization (for today's date), then a count and the rows for each of the eleven record groups (the time clock group is one per
        // project, so it also reads rows), and two statements for the statutory tables (the status, then whether the company has a worker or a
        // payroll, asked only while a table is missing); the count route is the organization, one count per group and the same two statements.
        Assert.Equal((25, 14), large);

        // A role with no group costs nothing beyond signing in.
        using var viewer = await AsRoleAsync(world, AccessRole.Viewer);
        Assert.Equal(0, await StatementsAsync(_factory, async () => await viewer.GetAsync<NeedsActionDto>(Full)));
        Assert.Equal(0, await StatementsAsync(_factory, async () => await viewer.GetAsync<NeedsActionCountDto>(Count)));
    }

    // ---- The world ----------------------------------------------------------------------------------------------------------

    private sealed record World(ApiClient Admin, SeededUser User, SupplierDto Firm, PurchaseOrderDto DraftOrder, PurchaseOrderDto ApprovedOrder,
        ChangeOrderDto ChangeOrder, ClientInvoiceDto DraftBill, ClientInvoiceDto IssuedBill, SubcontractDto Subcontract, Guid ClaimId, long ClaimGrossMinor,
        BondDto Bond, IReadOnlyList<ProjectDto> Sites, IReadOnlyList<Guid> PayrollRunIds)
    {
        public HashSet<Guid> AllIds =>
        [
            DraftOrder.Id, ApprovedOrder.Id, ChangeOrder.Id, DraftBill.Id, IssuedBill.Id, Subcontract.Id, ClaimId, Bond.Id, ..Sites.Select(site => site.Id),
            ..PayrollRunIds
        ];
    }

    private sealed record Added(List<PurchaseOrderDto> Drafts, List<PurchaseOrderDto> Approved, List<ChangeOrderDto> ChangeOrders,
        List<ClientInvoiceDto> DraftBills, List<ClientInvoiceDto> IssuedBills, List<(SubcontractDto Subcontract, Guid ClaimId, long GrossMinor)> Claims,
        List<BondDto> Bonds, List<ProjectDto> Sites, List<Guid> PayrollRuns);

    /// <summary>A company with exactly one waiting record in every group (and no statutory tables, so those need setup).</summary>
    private async Task<World> WorldAsync()
    {
        var (admin, user) = await SignedInAsync(_factory);
        var firm = await PurchasingTestKit.CreateFirmAsync(admin);
        var added = await AddRecordsAsync(_factory, admin, firm, 1, firstPeriod: 0);
        var (subcontract, claimId, gross) = added.Claims[0];
        return new World(admin, user, firm, added.Drafts[0], added.Approved[0], added.ChangeOrders[0], added.DraftBills[0], added.IssuedBills[0],
            subcontract, claimId, gross, added.Bonds[0], added.Sites, added.PayrollRuns);
    }

    /// <summary>
    /// Adds <paramref name="count"/> waiting records to every group: draft and late approved orders, an expired bond and a subcontract with a draft
    /// claim on a buying project; submitted change orders on a project with a locked budget; per billed project an overdue issued bill and a
    /// second, draft bill; per active site (with a budget line) a posted worker with attendance a week before yesterday and none yesterday (and no progress) and
    /// one scan two days ago with no time-out, waiting for review; and draft
    /// payroll for a half-month period of 2026 paying that worker.
    /// </summary>
    private static async Task<Added> AddRecordsAsync(ConstructOpxApiFactory factory, ApiClient admin, SupplierDto firm, int count, int firstPeriod)
    {
        var today = BondTestKit.Today();
        var added = new Added([], [], [], [], [], [], [], [], []);
        var buying = await CreateProjectAsync(admin);
        var budgeted = await LockedBudgetProjectAsync(admin);
        for (var index = 0; index < count; index++)
        {
            added.Drafts.Add(await PurchasingTestKit.DraftAsync(admin, buying, firm));
            added.Approved.Add(await LateOrderAsync(admin, buying, firm, today));
            added.Bonds.Add(await BondTestKit.CreateAsync(admin, buying.Id, validUntil: BondTestKit.DaysFromToday(-3)));

            var subcontract = await SubcontractTestKit.DraftAsync(admin, await SubcontractTestKit.AwardedAsync(admin, buying.Id), 100_000_00);
            var claim = SubcontractTestKit.OpenDraft(subcontract);
            added.Claims.Add((subcontract, claim.Id, claim.GrossMinor));

            added.ChangeOrders.Add(await SubmittedChangeOrderAsync(admin, budgeted));

            var billed = await CreateProjectAsync(admin);
            var first = await DraftBillAsync(admin, billed.Id, 100_000_00, today.AddDays(-40));
            added.IssuedBills.Add(await admin.PostAsync<ClientInvoiceDto>($"/api/client-invoices/{first.Id}/issue", new { version = first.Version }));
            added.DraftBills.Add(await DraftBillAsync(admin, billed.Id, 200_000_00, today.AddDays(-35)));

            var site = await StartedWithBudgetLineAsync(admin, TenantSeeder.Unique("Site"));
            var worker = await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, TenantSeeder.Unique("E")[..12]), site);
            // The same weekday a week before yesterday: this site recorded then and nothing yesterday.
            await WorkforceTestKit.RecordAsync(admin, worker.Id, site.Id, BondTestKit.Date(NeedsActionRules.AttendanceCompareDay(today)), 8m);
            added.Sites.Add(site);

            // A scan two days ago with no time-out: the worker's day waits for a person to review it.
            var issued = await admin.PostAsync<PunchCodeIssuedDto>($"/api/workers/{worker.Id}/punch-code",
                new { version = (await admin.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}")).Version });
            var device = await TimeClockTestKit.RegisterAsync(admin, site.Id);
            await TimeClockTestKit.PushAsync(factory, device.Key, TimeClockTestKit.Scan(Guid.NewGuid().ToString("N"), issued.Code, TimeClockTestKit.At(2, 7, 30)));

            // Half-month periods from 1 Feb 2026: attendance on the second day of each, then the payroll for it.
            var period = firstPeriod + index;
            var start = new DateOnly(2026, 2 + period / 2, period % 2 == 0 ? 1 : 15);
            var end = period % 2 == 0 ? start.AddDays(13) : new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            await WorkforceTestKit.RecordAsync(admin, worker.Id, site.Id, BondTestKit.Date(start.AddDays(1)), 8m);
            added.PayrollRuns.Add((await WorkforceTestKit.CreateRunAsync(admin, BondTestKit.Date(start), BondTestKit.Date(end))).Id);
        }
        return added;
    }

    /// <summary>An Active project with one active budget line (so it can take progress), started long ago.</summary>
    private static async Task<ProjectDto> StartedWithBudgetLineAsync(ApiClient client, string? name)
    {
        var planned = name is null ? await CreateProjectAsync(client) : await WorkforceTestKit.NewProjectAsync(client, name);
        await client.PostAsync<BudgetLineDto>($"/api/projects/{planned.Id}/budget-lines", Line("Ready-mix concrete", unitCostMinor: 50_000));
        return await StartAsync(client, await client.GetAsync<ProjectDto>($"/api/projects/{planned.Id}"));
    }

    private static async Task<(ProjectDto Site, List<WorkerDetailDto> Crew)> SiteWithCrewAsync(ApiClient admin, string name, int size)
    {
        var site = await StartedWithBudgetLineAsync(admin, name);
        var crew = new List<WorkerDetailDto>();
        for (var index = 0; index < size; index++)
            crew.Add(await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, TenantSeeder.Unique("E")[..12]), site));
        return (site, crew);
    }

    private static async Task SetRestDayAsync(ApiClient client, WorkerDetailDto worker, string restDay)
    {
        var current = await client.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}");
        await client.PutAsync<WorkerDetailDto>($"/api/workers/{worker.Id}", new
        {
            firstName = current.FirstName, middleName = current.MiddleName, lastName = current.LastName, dateOfBirth = current.DateOfBirth,
            maritalStatus = current.MaritalStatus, spouseName = current.SpouseName, religion = current.Religion,
            educationalAttainment = current.EducationalAttainment, presentAddress = current.PresentAddress, provincialAddress = current.ProvincialAddress,
            contactNumber = current.ContactNumber, reference = current.Reference, version = current.Version, restDay
        });
    }

    private static async Task<ProjectDto> StartAsync(ApiClient client, ProjectDto project) =>
        await client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/start", new { version = project.Version });

    private static async Task<PurchaseOrderDto> LateOrderAsync(ApiClient client, ProjectDto project, SupplierDto firm, DateOnly today) =>
        await PurchasingTestKit.ApproveAsync(client, await client.PostAsync<PurchaseOrderDto>("/api/purchase-orders",
            PurchasingTestKit.OrderBody(project.Id, firm.Id, expectedDate: BondTestKit.Date(today.AddDays(-1)))));

    private static Task<ClientInvoiceDto> DraftBillAsync(ApiClient client, Guid projectId, long cumulativeMinor, DateOnly billingDate) =>
        client.PostAsync<ClientInvoiceDto>($"/api/projects/{projectId}/billing/invoices",
            new { kind = "Progress", billingDate = BondTestKit.Date(billingDate), description = "Progress billing", cumulativeWorkMinor = cumulativeMinor });

    private static async Task<ProjectDto> LockedBudgetProjectAsync(ApiClient client)
    {
        var project = await CreateProjectAsync(client);
        await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Ready-mix concrete", unitCostMinor: 50_000));
        var current = await client.GetAsync<ProjectDto>($"/api/projects/{project.Id}");
        await client.PostAsync<BudgetBaselineDto>($"/api/projects/{project.Id}/budget-baseline/lock", new { version = current.Version });
        return current;
    }

    private static async Task<ChangeOrderDto> SubmittedChangeOrderAsync(ApiClient client, ProjectDto project)
    {
        var order = await client.PostAsync<ChangeOrderDto>($"/api/projects/{project.Id}/change-orders",
            new { title = "Extra waterproofing", description = "The client asked for a membrane.", reason = "ClientRequest", contractPriceDeltaMinor = 0 });
        order = await client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/lines", new
        {
            kind = "Add", description = "Waterproofing membrane", itemType = "Material", quantity = 3m, unit = "roll", unitCostMinor = 33_333,
            vatTreatment = "Inclusive", entryDate = "2026-02-01", version = order.Version
        });
        return await client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/submit", new { version = order.Version });
    }

    private async Task<ApiClient> AsRoleAsync(World world, AccessRole role)
    {
        var user = await TenantSeeder.AddUserAsync(_factory, world.User.TenantId, role);
        var client = new ApiClient(_factory);
        await client.SignInAsync(user.Username, user.Password);
        return client;
    }

    private static NeedsActionRowDto Single(Dictionary<string, NeedsActionGroupDto> groups, string key)
    {
        var group = groups[key];
        Assert.Equal((key, 1), (key, group.Count));
        return Assert.Single(group.Rows);
    }

    private static NeedsActionGroupDto Group(NeedsActionDto result, string key) => result.Groups.Single(group => group.Key == key);

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> RawAsync(ApiClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>Every property name anywhere in a JSON document.</summary>
    private static List<string> PropertyNames(JsonElement element)
    {
        var names = new List<string>();
        void Walk(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.Object)
                foreach (var property in item.EnumerateObject())
                {
                    names.Add(property.Name);
                    Walk(property.Value);
                }
            else if (item.ValueKind == JsonValueKind.Array)
                foreach (var child in item.EnumerateArray()) Walk(child);
        }
        Walk(element);
        return names;
    }
}
