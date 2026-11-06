using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.ChangeOrders;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.Infrastructure.Seeding;
using ConstructOPX.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The budget baseline and change orders through the real API and SQL Server. The project's budget: concrete 10 x 500.00 VAT inclusive
/// (net 4,464.29, gross 5,000.00), rebar 10 x 100.00 before VAT (1,000.00 + 120.00) and labor 4 x 250.00 with no VAT (1,000.00):
/// net 6,464.29, gross 7,120.00. The contract price is 500,000.00.
/// </summary>
public sealed class ChangeOrderApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const long OriginalNet = 646_429;
    private const long OriginalGross = 712_000;
    private const long Price = 500_000_00;

    private readonly ConstructOpxApiFactory _factory;

    public ChangeOrderApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private sealed record Fixture(ApiClient Client, SeededUser User, ProjectDto Project, BudgetLineDto Concrete, BudgetLineDto Rebar, BudgetLineDto Labor)
    {
        public string Baseline => $"/api/projects/{Project.Id}/budget-baseline";
        public string Orders => $"/api/projects/{Project.Id}/change-orders";
    }

    private async Task<Fixture> ProjectWithBudgetAsync(bool lockIt = true)
    {
        var (client, user) = await SignedInAsync(_factory);
        var project = await CreateProjectAsync(client, contractPriceMinor: Price);
        var url = $"/api/projects/{project.Id}/budget-lines";
        var concrete = await client.PostAsync<BudgetLineDto>(url, Line("Ready-mix concrete", quantity: 10m, unit: "m3", unitCostMinor: 50_000, vatTreatment: "Inclusive"));
        var rebar = await client.PostAsync<BudgetLineDto>(url, Line("Deformed bars", quantity: 10m, unit: "kg", unitCostMinor: 10_000, vatTreatment: "Exclusive"));
        var labor = await client.PostAsync<BudgetLineDto>(url, Line("Formworks labor", "Labor", 4m, "l.s", 25_000));
        var fixture = new Fixture(client, user, project, concrete, rebar, labor);
        if (lockIt) await LockAsync(fixture);
        return fixture;
    }

    private static async Task<BudgetBaselineDto> LockAsync(Fixture fixture)
    {
        var project = await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}");
        return await fixture.Client.PostAsync<BudgetBaselineDto>($"{fixture.Baseline}/lock", new { version = project.Version });
    }

    private static Task<ChangeOrderDto> DraftAsync(Fixture fixture, long priceDelta = 0, string title = "Basement waterproofing", string? requestId = null) =>
        fixture.Client.PostAsync<ChangeOrderDto>(fixture.Orders, new
        {
            title, description = "The client asked for a membrane under the basement slab.", reason = "ClientRequest", contractPriceDeltaMinor = priceDelta,
            clientRequestId = requestId
        });

    private static object AddLine(int version, string description = "Waterproofing membrane", decimal quantity = 3m, long unitCost = 33_333) => new
    {
        kind = "Add", description, itemType = "Material", quantity, unit = "roll", unitCostMinor = unitCost, vatTreatment = "Inclusive", entryDate = "2026-02-01",
        version
    };

    private static object ReviseLine(Guid budgetLineId, decimal quantity, long unitCost, int version) =>
        new { kind = "Revise", budgetLineId, quantity, unitCostMinor = unitCost, version };

    private static object OmitLine(Guid budgetLineId, int version) => new { kind = "Omit", budgetLineId, version };

    private static Task<ChangeOrderDto> PostLineAsync(Fixture fixture, ChangeOrderDto order, object line) =>
        fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/lines", line);

    private static Task<ChangeOrderDto> SubmitAsync(Fixture fixture, ChangeOrderDto order) =>
        fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/submit", new { version = order.Version });

    private static Task<ChangeOrderDto> ApproveAsync(Fixture fixture, ChangeOrderDto order) =>
        fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/approve", new { note = "Agreed with the client", version = order.Version });

    private static Task<HttpResponseMessage> VoidAsync(ApiClient client, ChangeOrderDto order) =>
        client.PostAsync($"/api/change-orders/{order.Id}/void", new { reason = "Client withdrew the request", version = order.Version });

    /// <summary>The full CO-001 from the domain tests: add a membrane, revise the concrete up, omit the labor, and +1,500.00 on the price.</summary>
    private static async Task<ChangeOrderDto> SubmittedFullOrderAsync(Fixture fixture)
    {
        var order = await DraftAsync(fixture, priceDelta: 150_000);
        order = await PostLineAsync(fixture, order, AddLine(order.Version));
        order = await PostLineAsync(fixture, order, ReviseLine(fixture.Concrete.Id, 12m, 50_000, order.Version));
        order = await PostLineAsync(fixture, order, OmitLine(fixture.Labor.Id, order.Version));
        return await SubmitAsync(fixture, order);
    }

    private static async Task<(long Summary, long Dashboard, long? Price)> BudgetEverywhereAsync(Fixture fixture)
    {
        var summary = await fixture.Client.GetAsync<CostSummaryDto>($"/api/projects/{fixture.Project.Id}/cost-summary");
        var dashboard = await fixture.Client.GetAsync<DashboardDto>("/api/reports/dashboard");
        var project = await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}");
        return (summary.Position.BudgetMinor, dashboard.Projects.Single(row => row.Id == fixture.Project.Id).BudgetMinor, project.ContractPriceMinor);
    }

    // ---- Baseline ---------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Locking_freezes_the_budget_and_direct_budget_line_changes_are_refused_with_the_plain_message()
    {
        var fixture = await ProjectWithBudgetAsync(lockIt: false);
        using var _c = fixture.Client;

        var open = await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline);
        Assert.False(open.Locked);
        Assert.Null(open.OriginalBudgetGrossMinor);
        Assert.Equal((OriginalNet, OriginalGross), (open.RevisedBudgetNetMinor, open.RevisedBudgetGrossMinor));
        Assert.Equal(3, open.ActiveLineCount);
        Assert.False((await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}")).BudgetBaselineLocked);

        var locked = await LockAsync(fixture);
        Assert.True(locked.Locked);
        Assert.Equal("Test Admin", locked.LockedBy);
        Assert.Equal((OriginalNet, OriginalGross, (long?)Price), (locked.OriginalBudgetNetMinor!.Value, locked.OriginalBudgetGrossMinor!.Value, locked.OriginalContractPriceMinor));
        Assert.Equal((OriginalNet, OriginalGross, (long?)Price), (locked.RevisedBudgetNetMinor, locked.RevisedBudgetGrossMinor, locked.RevisedContractPriceMinor));
        Assert.True(locked.Reconciles);
        var project = await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}");
        Assert.True(project.BudgetBaselineLocked);
        Assert.Equal(project.Version, locked.ProjectVersion);

        const string message = "The budget is locked. Record a change order to change it.";
        var create = await fixture.Client.PostAsync($"/api/projects/{fixture.Project.Id}/budget-lines", Line("Late addition"));
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        Assert.Equal(message, await DetailAsync(create));
        var update = await fixture.Client.PutAsync($"/api/budget-lines/{fixture.Rebar.Id}",
            Line("Deformed bars", quantity: 20m, unit: "kg", unitCostMinor: 10_000, vatTreatment: "Exclusive").With("version", fixture.Rebar.Version));
        Assert.Equal(message, await DetailAsync(update));
        var remove = await fixture.Client.PostAsync($"/api/budget-lines/{fixture.Labor.Id}/remove", new { reason = "Not needed", version = fixture.Labor.Version });
        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
        Assert.Equal(message, await DetailAsync(remove));

        // The contract price is frozen with the budget.
        var price = await fixture.Client.PutAsync($"/api/projects/{project.Id}", new
        {
            name = project.Name, client = project.Client, location = project.Location, contractPriceMinor = Price + 1, startDate = project.StartDate,
            targetEndDate = project.TargetEndDate, version = project.Version
        });
        Assert.Equal("The contract price is locked with the budget. Record a change order to change it.", await ErrorMessageAsync(price, "contractPriceMinor"));

        // Nothing changed.
        var after = await fixture.Client.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{fixture.Project.Id}/budget-lines");
        Assert.Equal(3, after.Total);
        Assert.Equal(OriginalGross, after.Totals.GrossMinor);
    }

    [SqlServerFact]
    public async Task Lock_and_unlock_refusals_stale_versions_and_an_unlock_while_nothing_is_approved()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var empty = await CreateProjectAsync(client);
        var noLines = await client.PostAsync($"/api/projects/{empty.Id}/budget-baseline/lock", new { version = empty.Version });
        Assert.Equal(HttpStatusCode.Conflict, noLines.StatusCode);
        Assert.Equal("Add at least one budget line before locking the budget.", await DetailAsync(noLines));

        var fixture = await ProjectWithBudgetAsync(lockIt: false);
        using var _f = fixture.Client;
        var stale = await fixture.Client.PostAsync($"{fixture.Baseline}/lock", new { version = fixture.Project.Version + 5 });
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        var locked = await LockAsync(fixture);

        var again = await fixture.Client.PostAsync($"{fixture.Baseline}/lock", new { version = locked.ProjectVersion });
        Assert.Equal("The budget is already locked.", await DetailAsync(again));

        var noReason = await fixture.Client.PostAsync($"{fixture.Baseline}/unlock", new { reason = " ", version = locked.ProjectVersion });
        Assert.Equal("Enter the reason for unlocking the budget.", await ErrorMessageAsync(noReason, "reason"));

        // A submitted (not approved) change order does not stop an unlock.
        await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: 10_000));
        var unlocked = await fixture.Client.PostAsync<BudgetBaselineDto>($"{fixture.Baseline}/unlock",
            new { reason = "Locked before the final take-off", version = locked.ProjectVersion });
        Assert.False(unlocked.Locked);
        Assert.Equal("Locked before the final take-off", unlocked.LastUnlockReason);
        Assert.Equal(1, unlocked.PendingCount);
        var added = await fixture.Client.PostAsync($"/api/projects/{fixture.Project.Id}/budget-lines", Line("Now allowed"));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        // Change orders need a locked budget.
        var draft = await fixture.Client.PostAsync(fixture.Orders, new { title = "Extra", reason = "Other", contractPriceDeltaMinor = 0 });
        Assert.Equal("Lock the budget before recording change orders. Until then, change the budget lines directly.", await DetailAsync(draft));
    }

    // ---- The whole journey ------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Approving_moves_the_budget_and_the_price_by_exactly_the_effect_everywhere_and_voiding_puts_every_figure_back()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;
        var before = await BudgetEverywhereAsync(fixture);
        Assert.Equal((OriginalGross, OriginalGross, (long?)Price), before);

        var order = await SubmittedFullOrderAsync(fixture);
        Assert.Equal("CO-001", order.Number);
        Assert.Equal("Submitted", order.Status);
        Assert.Equal((78_570L, 21_429L, 99_999L), (order.BudgetEffectNetMinor, order.BudgetEffectVatMinor, order.BudgetEffectGrossMinor));
        Assert.Equal(["Add", "Revise", "Omit"], order.Lines.Select(line => line.Kind));
        Assert.Equal([1, 2, 3], order.Lines.Select(line => line.LineNumber));

        // Submitted: pending exposure only; nothing has moved.
        var pending = await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline);
        Assert.Equal((1, 78_570L, 99_999L, 150_000L), (pending.PendingCount, pending.PendingBudgetNetMinor, pending.PendingBudgetGrossMinor,
            pending.PendingContractPriceDeltaMinor));
        Assert.Equal(before, await BudgetEverywhereAsync(fixture));

        var approved = await ApproveAsync(fixture, order);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Test Admin", approved.DecidedBy);
        Assert.Equal("Agreed with the client", approved.DecisionReason);
        Assert.Null(approved.VoidBlockedBy);
        var revise = approved.Lines[1];
        Assert.Equal((10m, 50_000L, 446_429L, 500_000L), (revise.PreviousQuantity!.Value, revise.PreviousUnitCostMinor!.Value, revise.PreviousNetMinor!.Value,
            revise.PreviousGrossMinor!.Value));
        Assert.Equal((535_714L, 600_000L), (revise.NetMinor, revise.GrossMinor));

        // The dashboard, the cost summary and the project price moved by exactly the approved effect.
        Assert.Equal((OriginalGross + 99_999, OriginalGross + 99_999, (long?)(Price + 150_000)), await BudgetEverywhereAsync(fixture));

        // Original + approved changes = revised = the active lines now.
        var baseline = await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline);
        Assert.True(baseline.Reconciles);
        Assert.Equal((OriginalNet, OriginalGross), (baseline.OriginalBudgetNetMinor!.Value, baseline.OriginalBudgetGrossMinor!.Value));
        Assert.Equal((1, 78_570L, 99_999L, 150_000L), (baseline.ApprovedCount, baseline.ApprovedBudgetNetMinor, baseline.ApprovedBudgetGrossMinor,
            baseline.ApprovedContractPriceDeltaMinor));
        Assert.Equal((724_999L, 811_999L, (long?)50_150_000), (baseline.RevisedBudgetNetMinor, baseline.RevisedBudgetGrossMinor, baseline.RevisedContractPriceMinor));
        Assert.Equal((baseline.RevisedBudgetNetMinor, baseline.RevisedBudgetGrossMinor), (baseline.CurrentBudgetNetMinor, baseline.CurrentBudgetGrossMinor));
        Assert.Equal(baseline.OriginalBudgetGrossMinor + baseline.ApprovedBudgetGrossMinor, baseline.CurrentBudgetGrossMinor);
        Assert.Equal(baseline.OriginalBudgetNetMinor + baseline.ApprovedBudgetNetMinor, baseline.CurrentBudgetNetMinor);
        Assert.Equal(0, baseline.PendingCount);

        // The line the order added carries its number on the Budget tab, in the list and when opened.
        var lines = await fixture.Client.GetAsync<TotaledPage<BudgetLineDto>>($"/api/projects/{fixture.Project.Id}/budget-lines?includeRemoved=true");
        var membrane = lines.Items.Single(line => line.ChangeOrderId == order.Id);
        Assert.Equal("CO-001", membrane.ChangeOrderNumber);
        Assert.Equal((89_285L, 10_714L, 99_999L), (membrane.NetMinor, membrane.VatMinor, membrane.GrossMinor));
        Assert.Equal("CO-001", (await fixture.Client.GetAsync<BudgetLineDto>($"/api/budget-lines/{membrane.Id}")).ChangeOrderNumber);
        var labor = lines.Items.Single(line => line.Id == fixture.Labor.Id);
        Assert.Equal(("Removed", "Change order CO-001"), (labor.Status, labor.RemovedReason));
        Assert.Null(lines.Items.Single(line => line.Id == fixture.Rebar.Id).ChangeOrderNumber);

        // Unlocking is refused now.
        var unlock = await fixture.Client.PostAsync($"{fixture.Baseline}/unlock", new { reason = "Try", version = baseline.ProjectVersion });
        Assert.Equal("The budget can't be unlocked because a change order has been approved. Record a change order to change it.", await DetailAsync(unlock));

        var voided = await fixture.Client.ReadAsync<ChangeOrderDto>(await VoidAsync(fixture.Client, approved));
        Assert.Equal(("Voided", "Approved", "Client withdrew the request"), (voided.Status, voided.VoidedFromStatus, voided.VoidReason));

        // Every figure is back to the centavo.
        Assert.Equal(before, await BudgetEverywhereAsync(fixture));
        var restored = await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline);
        Assert.True(restored.Reconciles);
        Assert.Equal((0, 0L, 0L), (restored.ApprovedCount, restored.ApprovedBudgetNetMinor, restored.ApprovedBudgetGrossMinor));
        Assert.Equal((OriginalNet, OriginalGross, (long?)Price), (restored.CurrentBudgetNetMinor, restored.CurrentBudgetGrossMinor, restored.CurrentContractPriceMinor));
        var concrete = await fixture.Client.GetAsync<BudgetLineDto>($"/api/budget-lines/{fixture.Concrete.Id}");
        Assert.Equal((10m, 50_000L, 446_429L, 53_571L, 500_000L), (concrete.Quantity, concrete.UnitCostMinor, concrete.NetMinor, concrete.VatMinor, concrete.GrossMinor));
        Assert.Equal("Active", (await fixture.Client.GetAsync<BudgetLineDto>($"/api/budget-lines/{fixture.Labor.Id}")).Status);
        var removedMembrane = await fixture.Client.GetAsync<BudgetLineDto>($"/api/budget-lines/{membrane.Id}");
        Assert.Equal(("Removed", "Change order CO-001 was voided"), (removedMembrane.Status, removedMembrane.RemovedReason));

        // The audit trail recorded each step.
        var audit = await fixture.Client.GetAsync<PagedResult<ConstructOPX.Application.Features.Audit.AuditEntryDto>>("/api/audit?pageSize=100");
        Assert.Superset(new HashSet<string> { "BudgetBaseline.Locked", "ChangeOrder.Drafted", "ChangeOrder.Submitted", "ChangeOrder.Approved", "ChangeOrder.Voided" },
            audit.Items.Select(entry => entry.Action).ToHashSet());
    }

    [SqlServerFact]
    public async Task Change_orders_are_voided_newest_first_and_each_void_returns_the_figures_it_moved()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;

        var down = await DraftAsync(fixture, priceDelta: -10_000_00, title: "Less rebar");
        down = await PostLineAsync(fixture, down, ReviseLine(fixture.Rebar.Id, 5m, 10_000, down.Version));
        down = await ApproveAsync(fixture, await SubmitAsync(fixture, down));
        var up = await DraftAsync(fixture, priceDelta: 2_500_00, title: "Rebar re-priced");
        up = await PostLineAsync(fixture, up, ReviseLine(fixture.Rebar.Id, 7.5m, 12_345, up.Version));
        up = await ApproveAsync(fixture, await SubmitAsync(fixture, up));
        Assert.Equal("CO-002", up.Number);
        Assert.Equal((42_588L, 47_699L), (up.BudgetEffectNetMinor, up.BudgetEffectGrossMinor));
        Assert.Equal((OriginalGross - 56_000 + 47_699, OriginalGross - 56_000 + 47_699, (long?)(Price - 10_000_00 + 2_500_00)),
            await BudgetEverywhereAsync(fixture));

        var first = await fixture.Client.GetAsync<ChangeOrderDto>($"/api/change-orders/{down.Id}");
        Assert.Equal("CO-002", first.VoidBlockedBy);
        var blocked = await VoidAsync(fixture.Client, first);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("Void CO-002 first. Change orders are voided newest first, so each one undoes exactly what it changed.", await DetailAsync(blocked));

        Assert.Equal(HttpStatusCode.OK, (await VoidAsync(fixture.Client, up)).StatusCode);
        Assert.Equal((OriginalGross - 56_000, OriginalGross - 56_000, (long?)(Price - 10_000_00)), await BudgetEverywhereAsync(fixture));
        first = await fixture.Client.GetAsync<ChangeOrderDto>($"/api/change-orders/{down.Id}");
        Assert.Null(first.VoidBlockedBy);
        Assert.Equal(HttpStatusCode.OK, (await VoidAsync(fixture.Client, first)).StatusCode);
        Assert.Equal((OriginalGross, OriginalGross, (long?)Price), await BudgetEverywhereAsync(fixture));
        var rebar = await fixture.Client.GetAsync<BudgetLineDto>($"/api/budget-lines/{fixture.Rebar.Id}");
        Assert.Equal((10m, 10_000L, 100_000L, 12_000L, 112_000L), (rebar.Quantity, rebar.UnitCostMinor, rebar.NetMinor, rebar.VatMinor, rebar.GrossMinor));
        Assert.True((await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline)).Reconciles);
    }

    // ---- Refusals ---------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Every_refusal_reports_a_plain_reason_and_changes_nothing()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;

        var invalid = await fixture.Client.PostAsync(fixture.Orders, new { title = " ", reason = "Whim", contractPriceDeltaMinor = -Price - 1 });
        Assert.Superset(new HashSet<string> { "title", "reason", "contractPriceDeltaMinor" }, await ErrorFieldsAsync(invalid));

        var empty = await DraftAsync(fixture);
        var nothing = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/submit", new { version = empty.Version });
        Assert.Equal("Add at least one line, or a change to the contract price, before submitting this change order.", await DetailAsync(nothing));

        var badLine = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/lines", new
        {
            kind = "Add", description = " ", itemType = "Gadget", quantity = 0m, unit = " ", unitCostMinor = -1, vatTreatment = "Maybe",
            entryDate = "2026-02-01", version = empty.Version
        });
        Assert.Superset(new HashSet<string> { "description", "itemType", "quantity", "unit", "unitCostMinor", "vatTreatment" }, await ErrorFieldsAsync(badLine));
        var badKind = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/lines", new { kind = "Swap", version = empty.Version });
        Assert.Contains("kind", await ErrorFieldsAsync(badKind));
        var unchanged = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/lines", ReviseLine(fixture.Rebar.Id, 10m, 10_000, empty.Version));
        Assert.Equal("Change the quantity or the unit cost: both are the same as the budget line now.", await ErrorMessageAsync(unchanged, "quantity"));

        // A line of another project is "not this project's".
        var other = await ProjectWithBudgetAsync(lockIt: false);
        using var _o = other.Client;
        var foreign = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/lines", OmitLine(other.Labor.Id, empty.Version));
        Assert.Equal("That budget line doesn't belong to this project.", await ErrorMessageAsync(foreign, "budgetLineId"));

        // Stale version.
        var current = await PostLineAsync(fixture, empty, OmitLine(fixture.Labor.Id, empty.Version));
        var stale = await fixture.Client.PutAsync($"/api/change-orders/{empty.Id}",
            new { title = "Renamed", reason = "DesignChange", contractPriceDeltaMinor = 0, version = empty.Version });
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));

        // Editing and removing a draft line.
        var lineId = current.Lines.Single().Id;
        current = await fixture.Client.PutAsync<ChangeOrderDto>($"/api/change-orders/{empty.Id}/lines/{lineId}", ReviseLine(fixture.Labor.Id, 5m, 25_000, current.Version));
        Assert.Equal(("Revise", 25_000L), (current.Lines.Single().Kind, current.BudgetEffectGrossMinor));
        current = await fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{empty.Id}/lines/{lineId}/remove", new { version = current.Version });
        Assert.Empty(current.Lines);
        Assert.Equal(0, current.BudgetEffectGrossMinor);

        // Approving a draft, and rejecting without a reason.
        var draftApprove = await fixture.Client.PostAsync($"/api/change-orders/{empty.Id}/approve", new { version = current.Version });
        Assert.Equal("Submit this change order before approving it.", await DetailAsync(draftApprove));
        var priced = await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: 5_000_00));
        var noReason = await fixture.Client.PostAsync($"/api/change-orders/{priced.Id}/reject", new { reason = "", version = priced.Version });
        Assert.Equal("Enter the reason for rejecting this change order.", await ErrorMessageAsync(noReason, "reason"));
        var rejected = await fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{priced.Id}/reject", new { reason = "Outside the scope", version = priced.Version });
        Assert.Equal(("Rejected", "Outside the scope"), (rejected.Status, rejected.DecisionReason));

        // Discarding a draft keeps it on record; a discarded or rejected order can't be approved or voided.
        var discarded = await fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{empty.Id}/discard", new { reason = "Duplicate", version = current.Version });
        Assert.Equal(("Voided", "Draft", "Duplicate"), (discarded.Status, discarded.VoidedFromStatus, discarded.VoidReason));
        Assert.Equal("Only an approved change order can be voided.", await DetailAsync(await VoidAsync(fixture.Client, rejected)));
        Assert.Equal("This change order isn't approved, so nothing needs reversing. Discard it instead.",
            await DetailAsync(await VoidAsync(fixture.Client, await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: 1)))));

        // None of it moved the budget or the price.
        Assert.Equal((OriginalGross, OriginalGross, (long?)Price), await BudgetEverywhereAsync(fixture));
        var list = await fixture.Client.GetAsync<PagedResult<ChangeOrderListItemDto>>(fixture.Orders);
        // The discarded draft, the rejected order and the submitted one.
        Assert.Equal(3, list.Total);
        Assert.Equal(1, (await fixture.Client.GetAsync<PagedResult<ChangeOrderListItemDto>>($"{fixture.Orders}?status=Rejected")).Total);
        Assert.Contains("status", await ErrorFieldsAsync(await fixture.Client.GetAsync($"{fixture.Orders}?status=Maybe")));
    }

    [SqlServerFact]
    public async Task An_approval_of_a_line_removed_since_submission_and_a_void_on_an_archived_project_are_refused()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;
        var omit = await DraftAsync(fixture, title: "Omit labor");
        omit = await SubmitAsync(fixture, await PostLineAsync(fixture, omit, OmitLine(fixture.Labor.Id, omit.Version)));
        var revise = await DraftAsync(fixture, title: "Revise labor");
        revise = await SubmitAsync(fixture, await PostLineAsync(fixture, revise, ReviseLine(fixture.Labor.Id, 6m, 25_000, revise.Version)));
        await ApproveAsync(fixture, omit);

        var refused = await fixture.Client.PostAsync($"/api/change-orders/{revise.Id}/approve", new { version = revise.Version });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("Line 1 (Formworks labor): that budget line was removed since. Edit or remove this line first.", await DetailAsync(refused));

        var priced = await ApproveAsync(fixture, await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: 20_000_00, title: "Price only")));
        var project = await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}");
        await ArchiveAsync(fixture.Client, project);
        var archived = await VoidAsync(fixture.Client, priced);
        Assert.Equal("This project is archived. Restore it before making changes.", await DetailAsync(archived));
        Assert.Equal(Price + 20_000_00, (await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}")).ContractPriceMinor);
    }

    [SqlServerFact]
    public async Task A_repeated_request_id_returns_the_original_draft_and_is_refused_for_another_project()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;
        var requestId = Guid.NewGuid().ToString();
        var first = await DraftAsync(fixture, requestId: requestId);
        var repeat = await DraftAsync(fixture, requestId: requestId);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(1, (await fixture.Client.GetAsync<PagedResult<ChangeOrderListItemDto>>(fixture.Orders)).Total);

        var other = await CreateProjectAsync(fixture.Client);
        var reused = await fixture.Client.PostAsync($"/api/projects/{other.Id}/change-orders",
            new { title = "x", reason = "Other", contractPriceDeltaMinor = 0, clientRequestId = requestId });
        Assert.Equal("That request was already used for another project. Reload the page and try again.", await DetailAsync(reused));
    }

    // ---- Access -----------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Each_permission_boundary_is_enforced_and_drafting_never_includes_approving()
    {
        var fixture = await ProjectWithBudgetAsync(lockIt: false);
        using var _c = fixture.Client;
        var tenant = fixture.User.TenantId;
        var projectVersion = (await fixture.Client.GetAsync<ProjectDto>($"/api/projects/{fixture.Project.Id}")).Version;

        // A cost engineer drafts and submits but may not lock or approve.
        var (cost, _) = await SignedInAsync(_factory, AccessRole.CostEngineer, tenant);
        using var _cost = cost;
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"{fixture.Baseline}/lock", new { version = projectVersion })).StatusCode);
        await LockAsync(fixture);
        var byCost = await cost.PostAsync<ChangeOrderDto>(fixture.Orders, new { title = "Extra slab", reason = "DesignChange", contractPriceDeltaMinor = 10_000 });
        byCost = await cost.PostAsync<ChangeOrderDto>($"/api/change-orders/{byCost.Id}/submit", new { version = byCost.Version });
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"/api/change-orders/{byCost.Id}/approve", new { version = byCost.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cost.PostAsync($"/api/change-orders/{byCost.Id}/reject", new { reason = "x", version = byCost.Version })).StatusCode);

        // A project manager approves and voids.
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, tenant);
        using var _m = manager;
        var approved = await manager.PostAsync<ChangeOrderDto>($"/api/change-orders/{byCost.Id}/approve", new { version = byCost.Version });
        Assert.Equal(HttpStatusCode.Forbidden, (await VoidAsync(cost, approved)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await VoidAsync(manager, approved)).StatusCode);

        // An accountant and a viewer read only.
        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer })
        {
            var (reader, _) = await SignedInAsync(_factory, role, tenant);
            using var _r = reader;
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(fixture.Orders)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(fixture.Baseline)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/change-orders/{byCost.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync(fixture.Orders, new { title = "x", reason = "Other" })).StatusCode);
        }

        // A warehouse keeper and an HR officer don't see change orders at all.
        foreach (var role in new[] { AccessRole.WarehouseKeeper, AccessRole.HrOfficer })
        {
            var (outsider, _) = await SignedInAsync(_factory, role, tenant);
            using var _o = outsider;
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(fixture.Orders)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(fixture.Baseline)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/change-orders/{byCost.Id}")).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Another_company_gets_not_found_for_every_id_it_did_not_create()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;
        var order = await DraftAsync(fixture, priceDelta: 10_000);
        var lineOrder = await PostLineAsync(fixture, order, OmitLine(fixture.Labor.Id, order.Version));
        var lineId = lineOrder.Lines.Single().Id;

        var (stranger, _) = await SignedInAsync(_factory);
        using var _s = stranger;
        var version = new { version = lineOrder.Version };
        var reason = new { reason = "x", version = lineOrder.Version };
        var responses = new[]
        {
            await stranger.GetAsync(fixture.Baseline),
            await stranger.PostAsync($"{fixture.Baseline}/lock", new { version = 0 }),
            await stranger.PostAsync($"{fixture.Baseline}/unlock", new { reason = "x", version = 0 }),
            await stranger.GetAsync(fixture.Orders),
            await stranger.PostAsync(fixture.Orders, new { title = "x", reason = "Other", contractPriceDeltaMinor = 0 }),
            await stranger.GetAsync($"/api/change-orders/{order.Id}"),
            await stranger.PutAsync($"/api/change-orders/{order.Id}", new { title = "x", reason = "Other", contractPriceDeltaMinor = 0, version = lineOrder.Version }),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/lines", AddLine(lineOrder.Version)),
            await stranger.PutAsync($"/api/change-orders/{order.Id}/lines/{lineId}", AddLine(lineOrder.Version)),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/lines/{lineId}/remove", version),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/submit", version),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/approve", version),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/reject", reason),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/void", reason),
            await stranger.PostAsync($"/api/change-orders/{order.Id}/discard", reason)
        };
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));

        // A stranger's own draft can't pull in this company's budget line either.
        var (strangerProject, strangerOrder) = await StrangerDraftAsync(stranger);
        var pulled = await stranger.PostAsync($"/api/change-orders/{strangerOrder.Id}/lines", OmitLine(fixture.Labor.Id, strangerOrder.Version));
        Assert.Equal("That budget line doesn't belong to this project.", await ErrorMessageAsync(pulled, "budgetLineId"));
        Assert.NotEqual(fixture.Project.Id, strangerProject.Id);

        Assert.Equal("Draft", (await fixture.Client.GetAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}")).Status);
    }

    private async Task<(ProjectDto Project, ChangeOrderDto Order)> StrangerDraftAsync(ApiClient stranger)
    {
        var project = await CreateProjectAsync(stranger);
        await stranger.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Their line"));
        var version = (await stranger.GetAsync<ProjectDto>($"/api/projects/{project.Id}")).Version;
        await stranger.PostAsync<BudgetBaselineDto>($"/api/projects/{project.Id}/budget-baseline/lock", new { version });
        var order = await stranger.PostAsync<ChangeOrderDto>($"/api/projects/{project.Id}/change-orders", new { title = "Theirs", reason = "Other", contractPriceDeltaMinor = 0 });
        return (project, order);
    }

    [SqlServerFact]
    public async Task The_demo_data_locks_the_budget_and_its_change_orders_reconcile()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client, contractPriceMinor: 48_500_000_00);
        await AddSubProjectAsync(client, project, "Clubhouse");
        var url = $"/api/projects/{project.Id}/budget-lines";
        await client.PostAsync<BudgetLineDto>(url, Line("Floor tiles 60x60", quantity: 5_200m, unit: "m2", unitCostMinor: 780_00, vatTreatment: "Inclusive"));
        await client.PostAsync<BudgetLineDto>(url, Line("Deformed bars, grade 60", quantity: 42_000m, unit: "kg", unitCostMinor: 52_00, vatTreatment: "Inclusive"));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
            await ChangeOrderDemo.SeedAsync(db, user.TenantId, project.Id, user.UserId, user.UserId, 1_200,
                new DateOnly(2026, 10, 30), DateTimeOffset.UtcNow);
        }

        var baseline = await client.GetAsync<BudgetBaselineDto>($"/api/projects/{project.Id}/budget-baseline");
        Assert.True(baseline.Locked);
        Assert.True(baseline.Reconciles);
        // Panels 120 x 1,850.00 = 222,000.00 and 400 more m² of tiles at 780.00 = 312,000.00.
        Assert.Equal((1, 534_000_00L, 350_000_00L), (baseline.ApprovedCount, baseline.ApprovedBudgetGrossMinor, baseline.ApprovedContractPriceDeltaMinor));
        Assert.Equal((1, 189_000_00L), (baseline.PendingCount, baseline.PendingBudgetGrossMinor));
        Assert.Equal(48_850_000_00, baseline.CurrentContractPriceMinor);
        var orders = await client.GetAsync<PagedResult<ChangeOrderListItemDto>>($"/api/projects/{project.Id}/change-orders");
        Assert.Equal(["Draft", "Submitted", "Approved"], orders.Items.Select(item => item.Status));

        // The sequence carries on: the draft becomes CO-003.
        var draft = orders.Items[0];
        var submitted = await client.PostAsync<ChangeOrderDto>($"/api/change-orders/{draft.Id}/submit", new { version = draft.Version });
        Assert.Equal("CO-003", submitted.Number);
    }

    // ---- Performance ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_list_the_detail_and_the_baseline_cost_the_same_number_of_statements_however_many_orders_and_lines_exist()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;
        var first = await SubmittedFullOrderAsync(fixture);
        var list = fixture.Orders;
        var detail = $"/api/change-orders/{first.Id}";

        async Task<(int List, int Detail, int Baseline)> MeasureAsync() => (
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync(list)).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync(detail)).StatusCode)),
            await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync(fixture.Baseline)).StatusCode)));

        var small = await MeasureAsync();

        await ApproveAsync(fixture, first);
        for (var index = 0; index < 12; index++)
        {
            var order = await DraftAsync(fixture, priceDelta: 1_000 + index, title: $"Change {index}");
            for (var line = 0; line < 4; line++) order = await PostLineAsync(fixture, order, AddLine(order.Version, $"Item {index}-{line}", 1m, 10_000 + line));
            if (index % 2 == 0) order = await SubmitAsync(fixture, order);
            if (index % 4 == 0) await ApproveAsync(fixture, order);
        }

        var large = await MeasureAsync();
        Assert.Equal(13, (await fixture.Client.GetAsync<PagedResult<ChangeOrderListItemDto>>(list)).Total);
        Assert.True((await fixture.Client.GetAsync<BudgetBaselineDto>(fixture.Baseline)).Reconciles);

        Assert.Equal(small, large);
        // List: the project, the count and the page. Detail: the order with names, its lines, and the government limit (the terms, the
        // baseline price and the approved increases in one statement). Baseline: the project, the active lines, the orders.
        Assert.Equal((3, 3, 3), large);
    }

    // ---- Government limit -------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task A_government_project_flags_added_work_past_10_percent_of_the_original_price_and_still_allows_approval()
    {
        var fixture = await ProjectWithBudgetAsync();
        using var _c = fixture.Client;

        // A private client: nothing applies.
        var large = await DraftAsync(fixture, priceDelta: 6_000_000, title: "Extra basement");
        Assert.Equal(new GovernmentLimitDto(false, 0, 0, false), large.GovernmentLimit);

        await fixture.Client.PutAsync<BillingTermsDto>($"/api/projects/{fixture.Project.Id}/billing/terms",
            new { retentionBps = 1_000, creditableWithholdingBps = 0, paymentTermsDays = 30, version = 0, isGovernmentClient = true });

        // The limit is 10% of the ₱500,000.00 original price: 50,000.00. An order for exactly that reaches the limit without passing it;
        // the 60,000.00 draft passes it.
        var drainage = await DraftAsync(fixture, priceDelta: 5_000_000, title: "Drainage");
        Assert.Equal(new GovernmentLimitDto(true, 5_000_000, 5_000_000, false), drainage.GovernmentLimit);
        Assert.Equal(new GovernmentLimitDto(true, 5_000_000, 6_000_000, true),
            (await fixture.Client.GetAsync<ChangeOrderDto>($"/api/change-orders/{large.Id}")).GovernmentLimit);

        var approved = await ApproveAsync(fixture, await SubmitAsync(fixture, drainage));
        Assert.Equal(new GovernmentLimitDto(true, 5_000_000, 5_000_000, false), approved.GovernmentLimit);

        // A price cut does not reduce the added work.
        var cut = await ApproveAsync(fixture, await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: -1_000_000, title: "Omit the fence")));
        Assert.Equal(new GovernmentLimitDto(true, 5_000_000, 5_000_000, false), cut.GovernmentLimit);

        // Now even 1.00 more passes the limit. It is a warning: approval still goes through.
        var signage = await SubmitAsync(fixture, await DraftAsync(fixture, priceDelta: 100, title: "Signage"));
        Assert.Equal(new GovernmentLimitDto(true, 5_000_000, 5_000_100, true), signage.GovernmentLimit);
        var approvedOver = await ApproveAsync(fixture, signage);
        Assert.Equal(("Approved", true), (approvedOver.Status, approvedOver.GovernmentLimit!.Exceeds));

        // A discarded order no longer counts its own increase: only the approved ones do.
        var discarded = await fixture.Client.PostAsync<ChangeOrderDto>($"/api/change-orders/{large.Id}/discard",
            new { reason = "Client withdrew it", version = large.Version });
        Assert.Equal(("Voided", new GovernmentLimitDto(true, 5_000_000, 5_000_100, true)), (discarded.Status, discarded.GovernmentLimit));
    }
}
