using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;

namespace Cms.Domain.UnitTests.Features;

public sealed class PurchaseOrderTests
{
    private static Supplier NewSupplier(SupplierKind kind = SupplierKind.Supplier) =>
        Supplier.Create(Guid.NewGuid(), TestData.Tenant, "Visayas Steel Corp.", kind, null, null, null, null, null, null, TestData.Now);

    private static PurchaseOrderLineInput Line(string? description = "Rebar 16 mm", decimal quantity = 10m, long unitCost = 50_000,
        VatTreatment vat = VatTreatment.Inclusive, ItemType type = ItemType.Material, string? unit = "pc") =>
        new(3, null, null, "3200", description, type, quantity, unit, unitCost, vat);

    private static PurchaseOrder NewOrder(Supplier? supplier = null, Project? project = null, Guid? subProjectId = null, DateOnly? orderDate = null,
        DateOnly? expectedDate = null, IReadOnlyList<PurchaseOrderLineInput>? lines = null, string? reference = "Quote 88", string? notes = null)
    {
        var order = PurchaseOrder.CreateDraft(Guid.NewGuid(), TestData.Tenant, supplier ?? NewSupplier(), project ?? TestData.NewProject(), subProjectId,
            orderDate ?? TestData.Today, expectedDate, reference, notes, lines ?? [Line()], 1_200, TestData.UserId, null, TestData.Today, TestData.Now);
        order.AssignNumber(7);
        return order;
    }

    [Fact]
    public void A_draft_takes_its_money_from_the_shared_line_rules_and_totals_the_lines_to_the_centavo()
    {
        // 10 x 500.00 including 12% VAT: gross 5,000.00, net 4,464.29 (4,464.2857 rounded once), VAT 535.71.
        // 3 x 99.99 before VAT: net 299.97, VAT 36.00 (35.9964 rounded), gross 335.97. No VAT: 1 x 10.01.
        var order = NewOrder(lines:
        [
            Line(),
            Line("Tie wire", 3m, 9_999, VatTreatment.Exclusive),
            Line("Delivery", 1m, 1_001, VatTreatment.Exempt, ItemType.Other, "l.s")
        ]);

        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal("PO-0007", order.Number);
        Assert.Equal([1, 2, 3], order.Lines.Select(line => line.LineNumber));
        Assert.Equal((446_429L, 53_571L, 500_000L), (order.Lines[0].NetMinor, order.Lines[0].VatMinor, order.Lines[0].GrossMinor));
        Assert.Equal((29_997L, 3_600L, 33_597L), (order.Lines[1].NetMinor, order.Lines[1].VatMinor, order.Lines[1].GrossMinor));
        Assert.Equal((1_001L, 0L, 1_001L), (order.Lines[2].NetMinor, order.Lines[2].VatMinor, order.Lines[2].GrossMinor));
        Assert.Equal(446_429L + 29_997 + 1_001, order.NetMinor);
        Assert.Equal(53_571L + 3_600, order.VatMinor);
        Assert.Equal(500_000L + 33_597 + 1_001, order.GrossMinor);
        Assert.Equal(order.GrossMinor, order.NetMinor + order.VatMinor);
        Assert.All(order.Lines, line => Assert.Equal(1_200, line.VatRateBps));
        Assert.Equal("Concrete", order.Lines[0].MainScope);
    }

    [Fact]
    public void A_number_is_assigned_once_and_formatted_with_four_digits()
    {
        var order = NewOrder();

        Assert.Throws<InvalidOperationException>(() => order.AssignNumber(8));
        Assert.Equal("PO-0001", PurchaseOrder.NumberFor(1));
        Assert.Equal("PO-12345", PurchaseOrder.NumberFor(12_345));
    }

    [Fact]
    public void Every_problem_with_the_order_and_its_lines_is_reported_together()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => NewOrder(orderDate: TestData.Today.AddDays(1), expectedDate: TestData.Today.AddDays(-30),
            lines: [Line(" "), Line(quantity: 0m), Line(unitCost: -1)], reference: new string('x', 81), notes: new string('n', 501)));

        Assert.Equal(new HashSet<string> { "orderDate", "expectedDate", "supplierReference", "notes", "lines" }, ex.Errors.Keys.ToHashSet());
        Assert.Equal("The order date can't be in the future.", ex.Errors["orderDate"][0]);
        Assert.Contains(ex.Errors["lines"], message => message.StartsWith("Line 1:", StringComparison.Ordinal));
        Assert.Contains(ex.Errors["lines"], message => message.StartsWith("Line 2:", StringComparison.Ordinal));
        Assert.Contains(ex.Errors["lines"], message => message.StartsWith("Line 3:", StringComparison.Ordinal));
    }

    [Fact]
    public void The_expected_date_may_equal_but_not_precede_the_order_date()
    {
        Assert.NotNull(NewOrder(expectedDate: TestData.Today));
        var ex = Assert.Throws<ValidationFailedException>(() => NewOrder(expectedDate: TestData.Today.AddDays(-1)));
        Assert.Equal("The expected date can't be before the order date.", ex.Errors["expectedDate"][0]);
    }

    [Fact]
    public void An_order_has_between_one_and_a_hundred_lines()
    {
        var none = Assert.Throws<ValidationFailedException>(() => NewOrder(lines: []));
        Assert.Equal("Add at least one line to the order.", none.Errors["lines"][0]);

        Assert.Equal(100, NewOrder(lines: Enumerable.Range(1, 100).Select(index => Line($"Item {index}", 1m, 100)).ToArray()).Lines.Count);
        var tooMany = Assert.Throws<ValidationFailedException>(() => NewOrder(lines: Enumerable.Range(1, 101).Select(index => Line($"Item {index}", 1m, 100)).ToArray()));
        Assert.Contains("at most 100 lines", tooMany.Errors["lines"][0]);
    }

    [Fact]
    public void An_order_total_beyond_the_limit_is_refused_instead_of_overflowing()
    {
        // Each line is within the limit (₱100,000,000,000.00 with VAT); two of them together are not.
        var big = Line("Crane", 1m, MoneyLimits.MaximumAmountMinor, VatTreatment.Exempt, ItemType.Equipment, "l.s");

        var ex = Assert.Throws<ValidationFailedException>(() => NewOrder(lines: [big, big]));

        Assert.Contains("too large", ex.Errors["lines"][0]);
        Assert.NotNull(NewOrder(lines: [big]));
    }

    [Fact]
    public void An_archived_firm_or_one_that_is_not_a_supplier_is_refused()
    {
        var subcontractorOnly = NewSupplier(SupplierKind.Subcontractor);
        Assert.Contains("isn't set up as a supplier", Assert.Throws<DomainRuleException>(() => NewOrder(subcontractorOnly)).Message);

        var archived = NewSupplier();
        archived.Archive(TestData.Now);
        Assert.Contains("is archived", Assert.Throws<DomainRuleException>(() => NewOrder(archived)).Message);

        Assert.NotNull(NewOrder(NewSupplier(SupplierKind.Supplier | SupplierKind.Subcontractor)));
    }

    [Fact]
    public void An_archived_project_or_sub_project_cannot_receive_a_new_order()
    {
        var project = TestData.NewProject();
        var sub = project.AddSubProject(Guid.NewGuid(), "Phase 1", new DateOnly(2026, 2, 1), TestData.Now);
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Contains("archived", Assert.Throws<DomainRuleException>(() => NewOrder(project: project)).Message);

        var live = TestData.NewProject();
        var other = TestData.NewProject("Other").AddSubProject(Guid.NewGuid(), "Elsewhere", new DateOnly(2026, 2, 1), TestData.Now);
        var wrongSub = Assert.Throws<ValidationFailedException>(() => NewOrder(project: live, subProjectId: other.Id));
        Assert.Contains("subProjectId", wrongSub.Errors.Keys);
        Assert.NotNull(sub);
    }

    [Fact]
    public void A_draft_is_replaced_whole_and_an_approved_order_is_locked()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = NewOrder(supplier, project);
        var firstLineId = order.Lines[0].Id;

        order.ReviseDraft(supplier, project, null, TestData.Today.AddDays(-1), null, null, "Revised", [Line("Cement", 2m, 25_000, VatTreatment.Exempt), Line("Sand", 1m, 10_000, VatTreatment.Exempt)],
            1_200, TestData.Today, TestData.Now.AddHours(1));

        Assert.Equal(["Cement", "Sand"], order.Lines.Select(line => line.Description));
        Assert.DoesNotContain(order.Lines, line => line.Id == firstLineId);
        Assert.Equal(60_000, order.GrossMinor);
        Assert.Equal("Revised", order.Notes);
        Assert.Null(order.SupplierReference);

        order.Approve(supplier, project, TestData.UserId, TestData.Now);
        var locked = Assert.Throws<DomainRuleException>(() => order.ReviseDraft(supplier, project, null, TestData.Today, null, null, null, [Line()], 1_200, TestData.Today, TestData.Now));
        Assert.Contains("lines are locked", locked.Message);
    }

    [Fact]
    public void Approving_locks_the_order_records_who_and_when_and_cannot_repeat()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = NewOrder(supplier, project);
        var approver = Guid.NewGuid();

        order.Approve(supplier, project, approver, TestData.Now.AddMinutes(5));

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        Assert.Equal(approver, order.ApprovedByUserId);
        Assert.Equal(TestData.Now.AddMinutes(5), order.ApprovedAtUtc);
        Assert.Equal("This order is already approved.", Assert.Throws<DomainRuleException>(() => order.Approve(supplier, project, approver, TestData.Now)).Message);
    }

    [Fact]
    public void Approving_is_refused_when_the_firm_was_archived_or_the_project_is_archived_since_drafting()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = NewOrder(supplier, project);
        supplier.Archive(TestData.Now);
        Assert.Contains("is archived", Assert.Throws<DomainRuleException>(() => order.Approve(supplier, project, TestData.UserId, TestData.Now)).Message);

        supplier.Restore(TestData.Now);
        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Contains("Restore it", Assert.Throws<DomainRuleException>(() => order.Approve(supplier, project, TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void A_draft_or_an_order_with_no_invoices_can_be_cancelled_with_a_reason()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var draft = NewOrder(supplier, project);
        Assert.Contains("reason", Assert.Throws<ValidationFailedException>(() => draft.Cancel(project, " ", false, TestData.UserId, TestData.Now)).Errors.Keys);

        draft.Cancel(project, " Supplier out of stock ", false, TestData.UserId, TestData.Now);
        Assert.Equal(PurchaseOrderStatus.Cancelled, draft.Status);
        Assert.Equal("Supplier out of stock", draft.CancelReason);

        var approved = NewOrder(supplier, project);
        approved.Approve(supplier, project, TestData.UserId, TestData.Now);
        approved.Cancel(project, "Changed design", hasMatchedInvoices: false, TestData.UserId, TestData.Now);
        Assert.Equal(PurchaseOrderStatus.Cancelled, approved.Status);
    }

    [Fact]
    public void An_approved_order_with_a_matched_invoice_cannot_be_cancelled_but_can_be_closed()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = NewOrder(supplier, project);
        order.Approve(supplier, project, TestData.UserId, TestData.Now);

        var refused = Assert.Throws<DomainRuleException>(() => order.Cancel(project, "Mistake", hasMatchedInvoices: true, TestData.UserId, TestData.Now));
        Assert.Contains("Close it instead", refused.Message);
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);

        order.Close(project, null, TestData.UserId, TestData.Now);
        Assert.Equal(PurchaseOrderStatus.Closed, order.Status);
        Assert.Null(order.CloseReason);
        Assert.NotNull(order.ClosedAtUtc);
    }

    [Fact]
    public void Only_an_approved_order_can_be_closed_and_a_closed_or_cancelled_one_is_final()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var draft = NewOrder(supplier, project);
        Assert.Contains("Only an approved order can be closed", Assert.Throws<DomainRuleException>(() => draft.Close(project, null, TestData.UserId, TestData.Now)).Message);

        draft.Approve(supplier, project, TestData.UserId, TestData.Now);
        draft.Close(project, "Delivered 8 of 10", TestData.UserId, TestData.Now);
        Assert.Equal("Delivered 8 of 10", draft.CloseReason);
        Assert.True(draft.IsFinal);
        Assert.Contains("already closed", Assert.Throws<DomainRuleException>(() => draft.Close(project, null, TestData.UserId, TestData.Now)).Message);
        Assert.Contains("already closed", Assert.Throws<DomainRuleException>(() => draft.Cancel(project, "x", false, TestData.UserId, TestData.Now)).Message);
        Assert.Contains("can't be approved", Assert.Throws<DomainRuleException>(() => draft.Approve(supplier, project, TestData.UserId, TestData.Now)).Message);
        Assert.Contains("can't be changed", Assert.Throws<DomainRuleException>(() => draft.ReviseDraft(supplier, project, null, TestData.Today, null, null, null, [Line()], 1_200, TestData.Today, TestData.Now)).Message);
    }

    [Fact]
    public void Cancelling_or_closing_on_an_archived_project_is_refused_until_it_is_restored()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = NewOrder(supplier, project);
        order.Approve(supplier, project, TestData.UserId, TestData.Now);
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Contains("Restore it", Assert.Throws<DomainRuleException>(() => order.Close(project, null, TestData.UserId, TestData.Now)).Message);
        Assert.Contains("Restore it", Assert.Throws<DomainRuleException>(() => order.Cancel(project, "x", false, TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void An_invoice_can_be_matched_only_to_an_approved_order_of_the_same_project()
    {
        var project = Guid.NewGuid();
        PurchaseOrderMatching.EnsureLineCanBeMatched(project, project, PurchaseOrderStatus.Approved, "PO-0001");

        var other = Assert.Throws<ValidationFailedException>(() => PurchaseOrderMatching.EnsureLineCanBeMatched(project, Guid.NewGuid(), PurchaseOrderStatus.Approved, "PO-0001"));
        Assert.Contains("another project", other.Errors["purchaseOrderLineId"][0]);
        foreach (var (status, fragment) in new[]
                 {
                     (PurchaseOrderStatus.Draft, "not approved yet"), (PurchaseOrderStatus.Closed, "is closed"), (PurchaseOrderStatus.Cancelled, "was cancelled")
                 })
        {
            var ex = Assert.Throws<ValidationFailedException>(() => PurchaseOrderMatching.EnsureLineCanBeMatched(project, project, status, "PO-0002"));
            Assert.Contains(fragment, ex.Errors["purchaseOrderLineId"][0]);
            Assert.Contains("PO-0002", ex.Errors["purchaseOrderLineId"][0]);
        }
    }
}

public sealed class CostEntryPurchaseOrderLinkTests
{
    private static CostEntry Manual(Project project) => CostEntry.RecordManual(Guid.NewGuid(), TestData.Tenant, project, null, 3, null, null, "3200", "Rebar",
        ItemType.Material, 1m, "pc", 10_000, VatTreatment.Exempt, 0, new DateOnly(2026, 10, 1), null, "Visayas Steel Corp.", "INV-1", TestData.UserId, null,
        TestData.Today, TestData.Now);

    [Fact]
    public void A_hand_entered_invoice_can_be_matched_to_a_line_and_the_match_cleared()
    {
        var entry = Manual(TestData.NewProject());
        var line = Guid.NewGuid();

        entry.LinkToPurchaseOrderLine(line);
        Assert.Equal(line, entry.PurchaseOrderLineId);

        entry.LinkToPurchaseOrderLine(null);
        Assert.Null(entry.PurchaseOrderLineId);
    }

    [Fact]
    public void A_system_made_cost_cannot_be_matched_and_an_empty_id_is_not_a_line()
    {
        var project = TestData.NewProject();
        var system = CostEntry.RecordSystem(Guid.NewGuid(), TestData.Tenant, project, null, CostOrigin.Payroll, Guid.NewGuid(), "Payroll", ItemType.Labor, 1m, "l.s",
            10_000, VatTreatment.Exempt, 0, new DateOnly(2026, 10, 1), TestData.UserId, TestData.Now);

        Assert.Contains("recorded by hand", Assert.Throws<DomainRuleException>(() => system.LinkToPurchaseOrderLine(Guid.NewGuid())).Message);
        Assert.Throws<ArgumentException>(() => Manual(project).LinkToPurchaseOrderLine(Guid.Empty));
    }
}
