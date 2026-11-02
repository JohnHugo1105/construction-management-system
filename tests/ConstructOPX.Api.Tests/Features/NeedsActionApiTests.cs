using System.Globalization;
using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.Bonds;
using ConstructOPX.Application.Features.ChangeOrders;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.Application.Features.Suppliers;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
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
        "deliveries-expected", "bonds-to-attend", "progress-not-updated", "attendance-not-recorded", "statutory-needs-setup"
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
                "progress-not-updated", "attendance-not-recorded"
            ],
            [AccessRole.Accountant] =
            [
                "purchase-orders-to-approve", "bills-to-issue", "claims-to-certify", "payroll-to-approve", "bills-to-chase", "deliveries-expected",
                "bonds-to-attend", "statutory-needs-setup"
            ],
            [AccessRole.CostEngineer] = ["deliveries-expected", "progress-not-updated"],
            [AccessRole.HrOfficer] = ["attendance-not-recorded"],
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
        Assert.Equal(["bills-to-chase", "deliveries-expected", "bonds-to-attend", "purchase-orders-to-approve", "change-orders-to-approve",
            "bills-to-issue", "claims-to-certify", "payroll-to-approve", "progress-not-updated", "attendance-not-recorded", "statutory-needs-setup"],
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
        Assert.Equal($"1 worker has no attendance recorded for {today.AddDays(-1).ToString("d MMM", CultureInfo.InvariantCulture)}", attendance.Detail);

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
        // Only their statutory tables and their one draft wait on the other company's administrator.
        Assert.Equal(2, (await other.GetAsync<NeedsActionCountDto>(Count)).Total);
    }

    [SqlServerFact]
    public async Task Records_of_an_archived_project_never_appear()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _c = admin;
        var today = BondTestKit.Today();
        var firm = await PurchasingTestKit.CreateFirmAsync(admin);
        var project = await StartAsync(admin, await CreateProjectAsync(admin));
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
        Assert.All(after.Groups.Where(group => group.Key != "statutory-needs-setup"), group => Assert.Equal((group.Key, 0), (group.Key, group.Count)));
        Assert.Equal(1, (await admin.GetAsync<NeedsActionCountDto>(Count)).Total);
    }

    [SqlServerFact]
    public async Task Acting_on_an_item_removes_it_from_the_list_and_the_count()
    {
        var world = await WorldAsync();
        using var _ = world.Admin;
        using var accountant = await AsRoleAsync(world, AccessRole.Accountant);
        var before = await accountant.GetAsync<NeedsActionDto>(Full);
        var totalBefore = (await accountant.GetAsync<NeedsActionCountDto>(Count)).Total;

        // Approving the draft order takes it off the approvals (it now waits to be delivered instead).
        await PurchasingTestKit.ApproveAsync(accountant, await PurchasingTestKit.GetOrderAsync(accountant, world.DraftOrder.Id));
        var approved = await accountant.GetAsync<NeedsActionDto>(Full);
        var orders = approved.Groups.Single(group => group.Key == "purchase-orders-to-approve");
        Assert.Equal((0, 0), (orders.Count, orders.Rows.Count));
        Assert.Equal(Group(before, "deliveries-expected").Count + 1, Group(approved, "deliveries-expected").Count);

        // Certifying the claim takes it off the queue altogether: the badge drops by one.
        var subcontract = await accountant.GetAsync<SubcontractDto>($"/api/subcontracts/{world.Subcontract.Id}");
        Assert.True((await SubcontractTestKit.CertifyAsync(accountant, subcontract, world.ClaimId)).IsSuccessStatusCode);
        var certified = await accountant.GetAsync<NeedsActionDto>(Full);
        Assert.Equal(0, Group(certified, "claims-to-certify").Count);
        Assert.DoesNotContain(certified.Groups.SelectMany(group => group.Rows), row => row.Id == world.ClaimId);
        Assert.Contains(Group(certified, "deliveries-expected").Rows, row => row.Id == world.DraftOrder.Id);
        Assert.Equal(totalBefore, (await accountant.GetAsync<NeedsActionCountDto>(Count)).Total + 1);
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
        await AddRecordsAsync(world.Admin, world.Firm, 9, firstPeriod: 1);
        var large = await MeasureAsync();

        var result = await world.Admin.GetAsync<NeedsActionDto>(Full);
        Assert.All(result.Groups.Where(group => group.Key != "statutory-needs-setup"), group =>
        {
            Assert.Equal((group.Key, 10), (group.Key, group.Count));
            Assert.Equal(NeedsActionRules.RowsShown, group.Rows.Count);
        });
        Assert.Equal(small, large);
        // The organization (for today's date), then a count and the rows for each of the ten record groups, and one statement for the
        // statutory tables; the count route is the organization and one count per group.
        Assert.Equal((22, 12), large);

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
        var added = await AddRecordsAsync(admin, firm, 1, firstPeriod: 0);
        var (subcontract, claimId, gross) = added.Claims[0];
        return new World(admin, user, firm, added.Drafts[0], added.Approved[0], added.ChangeOrders[0], added.DraftBills[0], added.IssuedBills[0],
            subcontract, claimId, gross, added.Bonds[0], added.Sites, added.PayrollRuns);
    }

    /// <summary>
    /// Adds <paramref name="count"/> waiting records to every group: draft and late approved orders, an expired bond and a subcontract with a draft
    /// claim on a buying project; submitted change orders on a project with a locked budget; per billed project an overdue issued bill and a
    /// second, draft bill; per active site a posted worker with attendance three days ago and none yesterday (and no progress); and draft
    /// payroll for a half-month period of 2026 paying that worker.
    /// </summary>
    private static async Task<Added> AddRecordsAsync(ApiClient admin, SupplierDto firm, int count, int firstPeriod)
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

            var site = await StartAsync(admin, await WorkforceTestKit.NewProjectAsync(admin, TenantSeeder.Unique("Site")));
            var worker = await WorkforceTestKit.AssignAsync(admin, await WorkforceTestKit.HireAsync(admin, TenantSeeder.Unique("E")[..12]), site);
            await WorkforceTestKit.RecordAsync(admin, worker.Id, site.Id, BondTestKit.Date(today.AddDays(-3)), 8m);
            added.Sites.Add(site);

            // Half-month periods from 1 Feb 2026: attendance on the second day of each, then the payroll for it.
            var period = firstPeriod + index;
            var start = new DateOnly(2026, 2 + period / 2, period % 2 == 0 ? 1 : 15);
            var end = period % 2 == 0 ? start.AddDays(13) : new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            await WorkforceTestKit.RecordAsync(admin, worker.Id, site.Id, BondTestKit.Date(start.AddDays(1)), 8m);
            added.PayrollRuns.Add((await WorkforceTestKit.CreateRunAsync(admin, BondTestKit.Date(start), BondTestKit.Date(end))).Id);
        }
        return added;
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
