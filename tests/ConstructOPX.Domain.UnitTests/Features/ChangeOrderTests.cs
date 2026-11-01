using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>
/// The budget baseline and change orders. Every figure is exact to the centavo. The project's budget in these tests:
/// concrete 10 x 500.00 VAT inclusive at 12% (net 4,464.29, VAT 535.71, gross 5,000.00), rebar 10 x 100.00 before VAT (1,000.00 + 120.00 =
/// 1,120.00) and labor 4 x 250.00 with no VAT (1,000.00): net 6,464.29, gross 7,120.00. The contract price is 500,000.00.
/// </summary>
public sealed class ChangeOrderTests
{
    private static readonly DateOnly LineDate = new(2026, 2, 1);
    private static readonly Guid Approver = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class Setup
    {
        public Project Project { get; } = TestData.NewProject();
        public BudgetLine Concrete { get; }
        public BudgetLine Rebar { get; }
        public BudgetLine Labor { get; }
        public List<BudgetLine> All { get; }

        public Setup(bool lockBudget = true)
        {
            Concrete = Line(Project, "Ready-mix concrete", 10m, 50_000, VatTreatment.Inclusive);
            Rebar = Line(Project, "Deformed bars", 10m, 10_000, VatTreatment.Exclusive);
            Labor = Line(Project, "Formworks labor", 4m, 25_000, VatTreatment.Exempt, ItemType.Labor);
            All = [Concrete, Rebar, Labor];
            if (lockBudget) LockAsTheServiceDoes();
        }

        public void LockAsTheServiceDoes()
        {
            var active = All.Where(line => !line.IsRemoved).ToArray();
            Project.LockBudgetBaseline(active.Length, active.Sum(line => line.NetMinor), active.Sum(line => line.GrossMinor), TestData.UserId, TestData.Now);
        }

        public (long Net, long Gross) ActiveSums() =>
            (All.Where(line => !line.IsRemoved).Sum(line => line.NetMinor), All.Where(line => !line.IsRemoved).Sum(line => line.GrossMinor));

        public ChangeOrder Draft(long priceDelta = 0, string title = "Add a waterproofing layer") =>
            ChangeOrder.Draft(Guid.NewGuid(), TestData.Tenant, Project, title, "The client asked for a basement membrane.", ChangeOrderReason.ClientRequest,
                priceDelta, TestData.UserId, null, TestData.Now);

        public void Submit(ChangeOrder order, long sequence = 1) =>
            order.Submit(Project, Targets(order), sequence, TestData.UserId, TestData.Now);

        public void Approve(ChangeOrder order) => All.AddRange(order.Approve(Project, Targets(order), Approver, "Agreed on site", TestData.Now));

        public void Void(ChangeOrder order, string? later = null) =>
            order.Void(Project, All.Where(line => order.TouchedIds().Contains(line.Id)).ToArray(), later, Approver, "Client withdrew the request", TestData.Now);

        public IReadOnlyCollection<BudgetLine> Targets(ChangeOrder order) => All.Where(line => order.TargetIds().Contains(line.Id)).ToArray();
    }

    private static BudgetLine Line(Project project, string description, decimal quantity, long unitCost, VatTreatment vat, ItemType type = ItemType.Material) =>
        BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, project, null, null, null, null, null, description, type, quantity, "pc", unitCost, vat, 1_200,
            LineDate, null, TestData.UserId, null, TestData.Now);

    private static ChangeOrderLineInput Add(string description = "Waterproofing membrane", decimal quantity = 3m, long unitCost = 33_333,
        VatTreatment vat = VatTreatment.Inclusive) =>
        new(ChangeOrderLineKind.Add, null, null, null, null, null, null, description, ItemType.Material, quantity, "roll", unitCost, vat, LineDate, null);

    private static ChangeOrderLineInput Revise(BudgetLine target, decimal quantity, long unitCost) =>
        new(ChangeOrderLineKind.Revise, target.Id, null, null, null, null, null, null, (ItemType)(-1), quantity, null, unitCost, (VatTreatment)(-1), default, null);

    private static ChangeOrderLineInput Omit(BudgetLine target) =>
        new(ChangeOrderLineKind.Omit, target.Id, null, null, null, null, null, null, (ItemType)(-1), 0m, null, 0, (VatTreatment)(-1), default, null);

    private static (long Net, long Vat, long Gross) Effect(ChangeOrder order) =>
        (order.BudgetEffectNetMinor, order.BudgetEffectVatMinor, order.BudgetEffectGrossMinor);

    private static (long, long, long) Money(BudgetLine line) => (line.NetMinor, line.VatMinor, line.GrossMinor);

    // ---- Baseline ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Locking_records_the_active_sums_and_the_contract_price_and_closes_the_budget_to_direct_edits()
    {
        var setup = new Setup();

        Assert.True(setup.Project.IsBudgetLocked);
        Assert.Equal(646_429, setup.Project.BaselineBudgetNetMinor);
        Assert.Equal(712_000, setup.Project.BaselineBudgetGrossMinor);
        Assert.Equal(500_000_00, setup.Project.BaselineContractPriceMinor);
        Assert.Equal(TestData.UserId, setup.Project.BudgetBaselineLockedByUserId);

        var refused = Assert.Throws<DomainRuleException>(() => setup.Project.EnsureBudgetIsOpen());
        Assert.Equal("The budget is locked. Record a change order to change it.", refused.Message);
        Assert.Equal("The budget is already locked.",
            Assert.Throws<DomainRuleException>(() => setup.Project.LockBudgetBaseline(3, 1, 1, TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void A_budget_with_no_active_line_or_an_archived_project_cannot_be_locked()
    {
        var project = TestData.NewProject();
        Assert.Equal("Add at least one budget line before locking the budget.",
            Assert.Throws<DomainRuleException>(() => project.LockBudgetBaseline(0, 0, 0, TestData.UserId, TestData.Now)).Message);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal("This project is archived. Restore it before making changes.",
            Assert.Throws<DomainRuleException>(() => project.LockBudgetBaseline(1, 100, 100, TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void Unlocking_needs_a_reason_and_is_refused_once_a_change_order_is_approved()
    {
        var setup = new Setup();

        var missing = Assert.Throws<ValidationFailedException>(() => setup.Project.UnlockBudgetBaseline(" ", false, TestData.Now));
        Assert.Equal("Enter the reason for unlocking the budget.", missing.Errors["reason"][0]);
        Assert.Equal("The budget can't be unlocked because a change order has been approved. Record a change order to change it.",
            Assert.Throws<DomainRuleException>(() => setup.Project.UnlockBudgetBaseline("Mistake", true, TestData.Now)).Message);

        setup.Project.UnlockBudgetBaseline("Locked before the final quantities", false, TestData.Now);
        Assert.False(setup.Project.IsBudgetLocked);
        Assert.Null(setup.Project.BaselineBudgetNetMinor);
        Assert.Null(setup.Project.BaselineBudgetGrossMinor);
        Assert.Null(setup.Project.BaselineContractPriceMinor);
        Assert.Equal("Locked before the final quantities", setup.Project.BudgetBaselineUnlockReason);
        setup.Project.EnsureBudgetIsOpen();
        Assert.Equal("The budget isn't locked.",
            Assert.Throws<DomainRuleException>(() => setup.Project.UnlockBudgetBaseline("Again", false, TestData.Now)).Message);
    }

    [Fact]
    public void The_contract_price_is_locked_with_the_budget_but_a_first_price_can_still_be_recorded()
    {
        var setup = new Setup();
        var project = setup.Project;

        var changed = Assert.Throws<ValidationFailedException>(() =>
            project.UpdateDetails(project.Name, project.Client, project.Location, 600_000_00, project.StartDate, project.TargetEndDate, TestData.Now));
        Assert.Equal("The contract price is locked with the budget. Record a change order to change it.", changed.Errors["contractPriceMinor"][0]);

        // Other details still change, with the same price.
        project.UpdateDetails("Tower A, phase 1", project.Client, project.Location, 500_000_00, project.StartDate, project.TargetEndDate, TestData.Now);
        Assert.Equal("Tower A, phase 1", project.Name);

        // A project locked before it had a price may record its first one, which becomes the baseline price.
        var unpriced = TestData.NewProject(contractPriceMinor: null);
        unpriced.LockBudgetBaseline(1, 100, 100, TestData.UserId, TestData.Now);
        unpriced.UpdateDetails(unpriced.Name, null, null, 750_000_00, unpriced.StartDate, null, TestData.Now);
        Assert.Equal(750_000_00, unpriced.ContractPriceMinor);
        Assert.Equal(750_000_00, unpriced.BaselineContractPriceMinor);
        Assert.Throws<ValidationFailedException>(() => unpriced.UpdateDetails(unpriced.Name, null, null, 800_000_00, unpriced.StartDate, null, TestData.Now));
    }

    // ---- Drafting ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_change_order_needs_a_locked_budget_and_reports_every_problem_at_once()
    {
        var open = new Setup(lockBudget: false);
        Assert.Equal("Lock the budget before recording change orders. Until then, change the budget lines directly.",
            Assert.Throws<DomainRuleException>(() => open.Draft()).Message);

        var setup = new Setup();
        var ex = Assert.Throws<ValidationFailedException>(() => ChangeOrder.Draft(Guid.NewGuid(), TestData.Tenant, setup.Project, " ", new string('x', 1_001),
            (ChangeOrderReason)(-1), -500_000_01, TestData.UserId, null, TestData.Now));
        Assert.Equal("Enter a title for the change.", ex.Errors["title"][0]);
        Assert.Equal("The description can be at most 1000 characters.", ex.Errors["description"][0]);
        Assert.Equal("Choose why the change is needed.", ex.Errors["reason"][0]);
        Assert.Equal("The contract price can't go below zero. Check the change to the price.", ex.Errors["contractPriceDeltaMinor"][0]);
    }

    [Fact]
    public void The_price_change_needs_an_existing_price_and_keeps_it_within_the_limit()
    {
        var setup = new Setup();
        var tooHigh = Assert.Throws<ValidationFailedException>(() => setup.Draft(priceDelta: MoneyLimits.MaximumAmountMinor));
        Assert.Equal("The contract price can be at most ₱100,000,000,000.00. Check the change to the price.", tooHigh.Errors["contractPriceDeltaMinor"][0]);

        // Down to exactly zero is allowed.
        Assert.Equal(-500_000_00, setup.Draft(priceDelta: -500_000_00).ContractPriceDeltaMinor);

        var unpriced = TestData.NewProject(contractPriceMinor: null);
        unpriced.LockBudgetBaseline(1, 100, 100, TestData.UserId, TestData.Now);
        var noPrice = Assert.Throws<ValidationFailedException>(() => ChangeOrder.Draft(Guid.NewGuid(), TestData.Tenant, unpriced, "Extra", null,
            ChangeOrderReason.Other, 10_000, TestData.UserId, null, TestData.Now));
        Assert.Equal("This project has no contract price yet. Enter the contract price on the project first.", noPrice.Errors["contractPriceDeltaMinor"][0]);
        // A change order that leaves the price alone is fine without one.
        Assert.Equal(0, ChangeOrder.Draft(Guid.NewGuid(), TestData.Tenant, unpriced, "Extra", null, ChangeOrderReason.Other, 0, TestData.UserId, null,
            TestData.Now).ContractPriceDeltaMinor);
    }

    [Fact]
    public void Add_revise_and_omit_lines_estimate_the_budget_effect_to_the_centavo()
    {
        var setup = new Setup();
        var order = setup.Draft();

        // Add 3 x 333.33 VAT inclusive = 999.99; net 999.99 / 1.12 = 892.848 -> 892.85, VAT 107.14.
        var add = order.AddLine(setup.Project, Add(), null, 1_200, TestData.Now);
        Assert.Equal((89_285L, 10_714L, 99_999L), (add.EffectNetMinor, add.EffectVatMinor, add.EffectGrossMinor));
        Assert.Equal(1, add.LineNumber);

        // Revise concrete to 12 x 500.00 = 6,000.00 inclusive: net 5,357.14, VAT 642.86. Effect: +892.85, +107.15, +1,000.00.
        var revise = order.AddLine(setup.Project, Revise(setup.Concrete, 12m, 50_000), setup.Concrete, 1_200, TestData.Now);
        Assert.Equal((535_714L, 64_286L, 600_000L), (revise.NetMinor, revise.VatMinor, revise.GrossMinor));
        Assert.Equal((446_429L, 500_000L), (revise.PreviousNetMinor!.Value, revise.PreviousGrossMinor!.Value));
        Assert.Equal((10m, 50_000L), (revise.PreviousQuantity!.Value, revise.PreviousUnitCostMinor!.Value));
        Assert.Equal((89_285L, 10_715L, 100_000L), (revise.EffectNetMinor, revise.EffectVatMinor, revise.EffectGrossMinor));
        Assert.Equal("Ready-mix concrete", revise.Description);

        // Omit labor: -1,000.00, no VAT.
        var omit = order.AddLine(setup.Project, Omit(setup.Labor), setup.Labor, 1_200, TestData.Now);
        Assert.Equal((-100_000L, 0L, -100_000L), (omit.EffectNetMinor, omit.EffectVatMinor, omit.EffectGrossMinor));

        Assert.Equal((78_570L, 21_429L, 99_999L), Effect(order));
        Assert.Equal(3, order.Lines.Count);

        // A draft only estimates: nothing in the budget has changed.
        Assert.Equal((446_429L, 53_571L, 500_000L), Money(setup.Concrete));
        Assert.False(setup.Labor.IsRemoved);
    }

    [Fact]
    public void A_line_must_target_an_active_line_of_the_project_once_and_a_revision_must_change_something()
    {
        var setup = new Setup();
        var order = setup.Draft();

        var same = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Revise(setup.Rebar, 10m, 10_000), setup.Rebar, 1_200, TestData.Now));
        Assert.Equal("Change the quantity or the unit cost: both are the same as the budget line now.", same.Errors["quantity"][0]);

        order.AddLine(setup.Project, Revise(setup.Rebar, 12m, 10_000), setup.Rebar, 1_200, TestData.Now);
        var twice = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Omit(setup.Rebar), setup.Rebar, 1_200, TestData.Now));
        Assert.Equal("This change order already changes that budget line. Edit that line instead.", twice.Errors["budgetLineId"][0]);

        var other = Line(TestData.NewProject("Other"), "Elsewhere", 1m, 100, VatTreatment.Exempt);
        var foreign = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Omit(other), other, 1_200, TestData.Now));
        Assert.Equal("That budget line doesn't belong to this project.", foreign.Errors["budgetLineId"][0]);

        var missing = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project,
            Omit(setup.Labor) with { BudgetLineId = null }, null, 1_200, TestData.Now));
        Assert.Equal("Choose the budget line to change.", missing.Errors["budgetLineId"][0]);

        var badKind = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Add() with { Kind = (ChangeOrderLineKind)9 }, null, 1_200,
            TestData.Now));
        Assert.Contains("kind", badKind.Errors.Keys);

        var invalidAdd = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Add(description: " ", quantity: 0m, unitCost: -1), null,
            1_200, TestData.Now));
        Assert.Superset(new HashSet<string> { "description", "quantity", "unitCostMinor" }, invalidAdd.Errors.Keys.ToHashSet());

        var badRevision = Assert.Throws<ValidationFailedException>(() => order.AddLine(setup.Project, Revise(setup.Concrete, 0m, -1), setup.Concrete, 1_200,
            TestData.Now));
        Assert.Superset(new HashSet<string> { "quantity", "unitCostMinor" }, badRevision.Errors.Keys.ToHashSet());
    }

    [Fact]
    public void Draft_lines_can_be_replaced_and_removed_and_are_renumbered()
    {
        var setup = new Setup();
        var order = setup.Draft();
        var first = order.AddLine(setup.Project, Add("First"), null, 1_200, TestData.Now);
        var second = order.AddLine(setup.Project, Add("Second", 1m, 10_000, VatTreatment.Exempt), null, 1_200, TestData.Now);

        // The company rate changed to 10% before the edit: the Add line keeps the 12% it was entered with.
        order.ReplaceLine(first.Id, setup.Project, Add("First, revised", 2m, 10_000, VatTreatment.Exclusive), null, 1_000, TestData.Now);
        Assert.Equal(1_200, first.VatRateBps);
        Assert.Equal((20_000L, 2_400L, 22_400L), (first.EffectNetMinor, first.EffectVatMinor, first.EffectGrossMinor));

        order.RemoveLine(setup.Project, first.Id, TestData.Now);
        Assert.Equal(1, second.LineNumber);
        Assert.Equal((10_000L, 0L, 10_000L), Effect(order));
        Assert.Throws<NotFoundException>(() => order.RemoveLine(setup.Project, first.Id, TestData.Now));
    }

    // ---- Submitting -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Submitting_needs_a_line_or_a_price_change_and_numbers_the_order_per_project()
    {
        var setup = new Setup();
        var empty = setup.Draft();
        Assert.Equal("Add at least one line, or a change to the contract price, before submitting this change order.",
            Assert.Throws<DomainRuleException>(() => setup.Submit(empty)).Message);
        Assert.Null(empty.Number);

        var priceOnly = setup.Draft(priceDelta: 25_000_00);
        setup.Submit(priceOnly, 1);
        Assert.Equal("CO-001", priceOnly.Number);
        Assert.Equal(ChangeOrderStatus.Submitted, priceOnly.Status);
        Assert.Equal(TestData.Now, priceOnly.SubmittedAtUtc);

        Assert.Equal("CO-042", ChangeOrder.FormatNumber(42));
        Assert.Equal("CO-1000", ChangeOrder.FormatNumber(1_000));

        // A submitted order is no longer a draft.
        Assert.Equal("Only a draft change order can be changed. Discard this one and record a new one if it needs to change.",
            Assert.Throws<DomainRuleException>(() => priceOnly.AddLine(setup.Project, Add(), null, 1_200, TestData.Now)).Message);
    }

    [Fact]
    public void A_target_removed_before_submission_stops_the_submission_with_the_line_named()
    {
        var setup = new Setup(lockBudget: false);
        setup.LockAsTheServiceDoes();
        var order = setup.Draft();
        order.AddLine(setup.Project, Omit(setup.Labor), setup.Labor, 1_200, TestData.Now);
        setup.Labor.Remove(TestData.UserId, "Removed by another change", TestData.Now);

        Assert.Equal("Line 1 (Formworks labor): that budget line was removed since. Edit or remove this line first.",
            Assert.Throws<DomainRuleException>(() => setup.Submit(order)).Message);
    }

    // ---- Approving and voiding --------------------------------------------------------------------------------------------------

    [Fact]
    public void Approving_applies_every_line_and_the_price_and_voiding_returns_everything_to_the_centavo()
    {
        var setup = new Setup();
        var order = setup.Draft(priceDelta: 150_000);
        order.AddLine(setup.Project, Add(), null, 1_200, TestData.Now);
        order.AddLine(setup.Project, Revise(setup.Concrete, 12m, 50_000), setup.Concrete, 1_200, TestData.Now);
        order.AddLine(setup.Project, Omit(setup.Labor), setup.Labor, 1_200, TestData.Now);
        setup.Submit(order);
        var before = setup.ActiveSums();

        setup.Approve(order);

        Assert.Equal(ChangeOrderStatus.Approved, order.Status);
        Assert.Equal(Approver, order.DecidedByUserId);
        Assert.Equal("Agreed on site", order.DecisionReason);
        Assert.Equal((78_570L, 21_429L, 99_999L), Effect(order));

        // The Add line is now a budget line marked with its change order.
        var added = setup.All.Single(line => line.ChangeOrderId == order.Id);
        Assert.Equal((89_285L, 10_714L, 99_999L), Money(added));
        Assert.Equal(1_200, added.VatRateBps);
        Assert.Equal(added.Id, order.Lines[0].BudgetLineId);

        // The revision changed only the quantity; the previous values are on the change order line.
        Assert.Equal((12m, 50_000L), (setup.Concrete.Quantity, setup.Concrete.UnitCostMinor));
        Assert.Equal((535_714L, 64_286L, 600_000L), Money(setup.Concrete));
        Assert.Equal(VatTreatment.Inclusive, setup.Concrete.VatTreatment);

        // The omitted line is removed with the change order named as the reason.
        Assert.True(setup.Labor.IsRemoved);
        Assert.Equal("Change order CO-001", setup.Labor.RemovedReason);

        // Original + approved effect = what the active lines add up to now; the price moved by the delta.
        var after = setup.ActiveSums();
        Assert.Equal((724_999L, 811_999L), after);
        Assert.Equal(before.Net + order.BudgetEffectNetMinor, after.Net);
        Assert.Equal(before.Gross + order.BudgetEffectGrossMinor, after.Gross);
        Assert.Equal(501_500_00, setup.Project.ContractPriceMinor);

        setup.Void(order);

        Assert.Equal(ChangeOrderStatus.Voided, order.Status);
        Assert.Equal(ChangeOrderStatus.Approved, order.VoidedFromStatus);
        Assert.Equal("Client withdrew the request", order.VoidReason);
        Assert.True(added.IsRemoved);
        Assert.Equal("Change order CO-001 was voided", added.RemovedReason);
        Assert.Equal((10m, 50_000L), (setup.Concrete.Quantity, setup.Concrete.UnitCostMinor));
        Assert.Equal((446_429L, 53_571L, 500_000L), Money(setup.Concrete));
        Assert.False(setup.Labor.IsRemoved);
        Assert.Null(setup.Labor.RemovedReason);
        Assert.Equal((646_429L, 712_000L), setup.ActiveSums());
        Assert.Equal(500_000_00, setup.Project.ContractPriceMinor);
    }

    [Fact]
    public void Revising_down_and_back_up_round_trips_to_the_original_money()
    {
        var setup = new Setup();

        // Down: rebar 5 x 100.00 before VAT = 500.00 + 60.00 = 560.00. Effect -500.00, -60.00, -560.00.
        var down = setup.Draft(priceDelta: -10_000_00, title: "Less rebar");
        down.AddLine(setup.Project, Revise(setup.Rebar, 5m, 10_000), setup.Rebar, 1_200, TestData.Now);
        setup.Submit(down, 1);
        setup.Approve(down);
        Assert.Equal((-50_000L, -6_000L, -56_000L), Effect(down));
        Assert.Equal((50_000L, 6_000L, 56_000L), Money(setup.Rebar));
        Assert.Equal(490_000_00, setup.Project.ContractPriceMinor);

        // Up again, at a new price: 7.5 x 123.45 = 925.875 -> 925.88 before VAT; VAT 111.1056 -> 111.11; gross 1,036.99.
        var up = setup.Draft(priceDelta: 2_500_00, title: "Rebar re-priced");
        up.AddLine(setup.Project, Revise(setup.Rebar, 7.5m, 12_345), setup.Rebar, 1_200, TestData.Now);
        setup.Submit(up, 2);
        setup.Approve(up);
        Assert.Equal((92_588L, 11_111L, 103_699L), Money(setup.Rebar));
        Assert.Equal((42_588L, 5_111L, 47_699L), Effect(up));
        Assert.Equal(492_500_00, setup.Project.ContractPriceMinor);
        Assert.Equal((646_429L - 50_000 + 42_588, 712_000L - 56_000 + 47_699), setup.ActiveSums());

        // Newest first: the first one can't be voided while the second stands.
        var blocked = Assert.Throws<DomainRuleException>(() => setup.Void(down, later: "CO-002"));
        Assert.Equal("Void CO-002 first. Change orders are voided newest first, so each one undoes exactly what it changed.", blocked.Message);
        Assert.Equal(ChangeOrderStatus.Approved, down.Status);

        setup.Void(up);
        Assert.Equal((50_000L, 6_000L, 56_000L), Money(setup.Rebar));
        Assert.Equal(490_000_00, setup.Project.ContractPriceMinor);
        setup.Void(down);
        Assert.Equal((100_000L, 12_000L, 112_000L), Money(setup.Rebar));
        Assert.Equal((10m, 10_000L), (setup.Rebar.Quantity, setup.Rebar.UnitCostMinor));
        Assert.Equal((646_429L, 712_000L), setup.ActiveSums());
        Assert.Equal(500_000_00, setup.Project.ContractPriceMinor);
    }

    [Fact]
    public void A_later_order_can_revise_and_omit_a_line_an_earlier_one_added_and_voiding_both_in_order_returns_to_the_baseline()
    {
        var setup = new Setup();
        var first = setup.Draft(title: "Membrane");
        first.AddLine(setup.Project, Add(), null, 1_200, TestData.Now);
        setup.Submit(first, 1);
        setup.Approve(first);
        var membrane = setup.All.Single(line => line.ChangeOrderId == first.Id);

        var second = setup.Draft(title: "Membrane, more");
        second.AddLine(setup.Project, Revise(membrane, 4m, 33_333), membrane, 1_200, TestData.Now);
        second.AddLine(setup.Project, Omit(setup.Rebar), setup.Rebar, 1_200, TestData.Now);
        setup.Submit(second, 2);
        setup.Approve(second);
        // 4 x 333.33 = 1,333.32 inclusive: net 1,190.46, VAT 142.86. Effect of the revision +297.61 net, +333.33 gross; rebar -1,000.00, -1,120.00.
        Assert.Equal((119_046L, 14_286L, 133_332L), Money(membrane));
        Assert.Equal((29_761L - 100_000, 3_572L - 12_000, 33_333L - 112_000), Effect(second));

        setup.Void(second);
        Assert.Equal((89_285L, 10_714L, 99_999L), Money(membrane));
        Assert.False(setup.Rebar.IsRemoved);
        setup.Void(first);
        Assert.True(membrane.IsRemoved);
        Assert.Equal((646_429L, 712_000L), setup.ActiveSums());
    }

    [Fact]
    public void Approval_refuses_a_target_that_was_removed_and_changes_nothing()
    {
        var setup = new Setup();
        var order = setup.Draft(priceDelta: 50_000);
        order.AddLine(setup.Project, Add(), null, 1_200, TestData.Now);
        order.AddLine(setup.Project, Omit(setup.Labor), setup.Labor, 1_200, TestData.Now);
        setup.Submit(order);
        setup.Labor.Remove(TestData.UserId, "Removed elsewhere", TestData.Now);

        var refused = Assert.Throws<DomainRuleException>(() => setup.Approve(order));
        Assert.Equal("Line 2 (Formworks labor): that budget line was removed since. Edit or remove this line first.", refused.Message);
        Assert.Equal(ChangeOrderStatus.Submitted, order.Status);
        Assert.Equal(500_000_00, setup.Project.ContractPriceMinor);
        Assert.Null(order.Lines[0].BudgetLineId);
    }

    [Fact]
    public void Each_step_is_refused_from_the_wrong_state_with_a_plain_message()
    {
        var setup = new Setup();
        var order = setup.Draft(priceDelta: 10_000);

        Assert.Equal("Submit this change order before approving it.", Assert.Throws<DomainRuleException>(() => setup.Approve(order)).Message);
        Assert.Equal("Only a submitted change order can be rejected. Discard the draft instead.",
            Assert.Throws<DomainRuleException>(() => order.Reject(setup.Project, Approver, "No", TestData.Now)).Message);
        Assert.Equal("This change order isn't approved, so nothing needs reversing. Discard it instead.",
            Assert.Throws<DomainRuleException>(() => setup.Void(order)).Message);

        setup.Submit(order);
        var noReason = Assert.Throws<ValidationFailedException>(() => order.Reject(setup.Project, Approver, " ", TestData.Now));
        Assert.Equal("Enter the reason for rejecting this change order.", noReason.Errors["reason"][0]);
        order.Reject(setup.Project, Approver, "Not in the contract scope", TestData.Now);
        Assert.Equal(ChangeOrderStatus.Rejected, order.Status);
        Assert.Equal("Not in the contract scope", order.DecisionReason);
        Assert.Equal("This change order was rejected. Record a new one instead.", Assert.Throws<DomainRuleException>(() => setup.Approve(order)).Message);
        Assert.Equal("This change order is already closed.",
            Assert.Throws<DomainRuleException>(() => order.Discard(setup.Project, TestData.UserId, "x", TestData.Now)).Message);

        var approved = setup.Draft(priceDelta: 10_000);
        setup.Submit(approved, 2);
        setup.Approve(approved);
        Assert.Equal("This change order is already approved.", Assert.Throws<DomainRuleException>(() => setup.Approve(approved)).Message);
        Assert.Equal("An approved change order can't be discarded. Void it instead, which reverses its changes.",
            Assert.Throws<DomainRuleException>(() => approved.Discard(setup.Project, TestData.UserId, "x", TestData.Now)).Message);
        var voidReason = Assert.Throws<ValidationFailedException>(() => approved.Void(setup.Project, [], null, Approver, null, TestData.Now));
        Assert.Equal("Enter the reason for voiding this change order.", voidReason.Errors["reason"][0]);
    }

    [Fact]
    public void Discarding_a_draft_or_a_submitted_order_keeps_it_on_record_and_changes_nothing()
    {
        var setup = new Setup();
        var draft = setup.Draft();
        draft.AddLine(setup.Project, Omit(setup.Labor), setup.Labor, 1_200, TestData.Now);
        draft.Discard(setup.Project, TestData.UserId, "Duplicate of another request", TestData.Now);
        Assert.Equal(ChangeOrderStatus.Voided, draft.Status);
        Assert.Equal(ChangeOrderStatus.Draft, draft.VoidedFromStatus);
        Assert.False(setup.Labor.IsRemoved);

        var submitted = setup.Draft(priceDelta: 10_000);
        setup.Submit(submitted);
        submitted.Discard(setup.Project, TestData.UserId, "Withdrawn", TestData.Now);
        Assert.Equal(ChangeOrderStatus.Submitted, submitted.VoidedFromStatus);
        Assert.Equal(500_000_00, setup.Project.ContractPriceMinor);
    }

    [Fact]
    public void An_archived_project_takes_no_decision_and_no_void()
    {
        var setup = new Setup();
        var order = setup.Draft(priceDelta: 10_000);
        setup.Submit(order);
        setup.Approve(order);
        setup.Project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Equal("This project is archived. Restore it before making changes.", Assert.Throws<DomainRuleException>(() => setup.Void(order)).Message);
        Assert.Equal(500_100_00, setup.Project.ContractPriceMinor);

        setup.Project.Restore(TestData.Now);
        setup.Void(order);
        Assert.Equal(500_000_00, setup.Project.ContractPriceMinor);
    }

    [Fact]
    public void A_change_order_holds_at_most_one_hundred_lines()
    {
        var setup = new Setup();
        var order = setup.Draft();
        for (var index = 0; index < ChangeOrder.MaximumLines; index++)
            order.AddLine(setup.Project, Add($"Item {index}", 1m, 100, VatTreatment.Exempt), null, 1_200, TestData.Now);

        Assert.Equal("A change order can have at most 100 lines. Record another change order for the rest.",
            Assert.Throws<DomainRuleException>(() => order.AddLine(setup.Project, Add(), null, 1_200, TestData.Now)).Message);
        Assert.Equal((10_000L, 0L, 10_000L), Effect(order));
    }
}
