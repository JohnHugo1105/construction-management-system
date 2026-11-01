using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;

namespace Cms.Domain.UnitTests.Features;

public sealed class GoodsReceiptTests
{
    private static readonly DateTimeOffset Later = TestData.Now.AddHours(3);
    private static readonly IReadOnlyDictionary<Guid, decimal> Nothing = new Dictionary<Guid, decimal>();

    private static Supplier NewSupplier() =>
        Supplier.Create(Guid.NewGuid(), TestData.Tenant, "Visayas Steel Corp.", SupplierKind.Supplier, null, null, null, null, null, null, TestData.Now);

    private static PurchaseOrderLineInput Line(string description, decimal quantity, long unitCost, VatTreatment vat = VatTreatment.Exempt, string unit = "pc") =>
        new(3, null, null, "3200", description, ItemType.Material, quantity, unit, unitCost, vat);

    /// <summary>An approved order of two lines: 10 pc of rebar at 500.00 (VAT included) and 3 pc of tie wire at 99.99 (VAT added).</summary>
    private static (PurchaseOrder Order, Project Project) ApprovedOrder()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        var order = PurchaseOrder.CreateDraft(Guid.NewGuid(), TestData.Tenant, supplier, project, null, TestData.Today, null, null, null,
            [Line("Rebar 16 mm", 10m, 50_000, VatTreatment.Inclusive), Line("Tie wire", 3m, 9_999, VatTreatment.Exclusive)], 1_200, TestData.UserId, null,
            TestData.Today, TestData.Now);
        order.AssignNumber(7);
        order.Approve(supplier, project, TestData.UserId, TestData.Now);
        return (order, project);
    }

    private static GoodsReceipt Record(PurchaseOrder order, Project project, IReadOnlyList<GoodsReceiptLineInput> lines, IReadOnlyDictionary<Guid, decimal>? already = null,
        DateOnly? date = null, string? number = "DR-0012", string? note = null, bool numberRecorded = false, string? requestId = null) =>
        GoodsReceipt.Record(Guid.NewGuid(), TestData.Tenant, order, project, date ?? TestData.Today, number, note, lines, already ?? Nothing, numberRecorded,
            TestData.UserId, requestId, TestData.Today, Later);

    private static GoodsReceiptLineInput Arrived(PurchaseOrder order, int lineNumber, decimal quantity) => new(order.Lines[lineNumber - 1].Id, quantity);

    private static Dictionary<string, string[]> Refusal(Action record) => Assert.Throws<ValidationFailedException>(record).Errors.ToDictionary(pair => pair.Key, pair => pair.Value);

    [Fact]
    public void A_delivery_names_what_arrived_per_line_and_who_recorded_it()
    {
        var (order, project) = ApprovedOrder();

        var receipt = Record(order, project, [Arrived(order, 1, 4m), Arrived(order, 2, 1.5m)], date: TestData.Today.AddDays(-1), number: "  DR-0012 ", note: " Gate 2 ", requestId: "req-1");

        Assert.Equal(GoodsReceiptStatus.Posted, receipt.Status);
        Assert.Equal((order.Id, order.SupplierId, order.ProjectId), (receipt.PurchaseOrderId, receipt.SupplierId, receipt.ProjectId));
        Assert.Equal((TestData.Today.AddDays(-1), "DR-0012", "DR0012", "Gate 2"), (receipt.ReceivedDate, receipt.DeliveryReceiptNumber, receipt.NormalizedDeliveryReceiptNumber, receipt.Note));
        Assert.Equal((TestData.UserId, "req-1", TestData.Tenant), (receipt.RecordedByUserId, receipt.ClientRequestId, receipt.TenantId));
        Assert.Equal([(order.Lines[0].Id, 4m), (order.Lines[1].Id, 1.5m)], receipt.Lines.Select(line => (line.PurchaseOrderLineId, line.Quantity)));
        Assert.All(receipt.Lines, line => Assert.Equal((receipt.Id, TestData.Tenant), (line.GoodsReceiptId, line.TenantId)));
        Assert.False(receipt.IsVoid);
    }

    [Fact]
    public void The_number_and_the_note_are_optional_and_today_is_a_valid_day()
    {
        var (order, project) = ApprovedOrder();

        var receipt = Record(order, project, [Arrived(order, 1, 1m)], number: "  ", note: null);

        Assert.Null(receipt.DeliveryReceiptNumber);
        Assert.Null(receipt.NormalizedDeliveryReceiptNumber);
        Assert.Null(receipt.Note);
        Assert.Equal(TestData.Today, receipt.ReceivedDate);
    }

    [Fact]
    public void Recording_a_delivery_touches_the_order_so_two_deliveries_of_the_last_quantity_collide_on_its_version()
    {
        var (order, project) = ApprovedOrder();
        Assert.Equal(TestData.Now, order.UpdatedAtUtc);

        Record(order, project, [Arrived(order, 1, 10m)]);

        Assert.Equal(Later, order.UpdatedAtUtc);
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
    }

    [Fact]
    public void A_delivery_lists_at_most_as_many_lines_as_an_order_can_have_and_says_so_once()
    {
        var (order, project) = ApprovedOrder();
        var tooMany = Enumerable.Range(0, PurchaseOrder.MaximumLines + 1).Select(_ => Arrived(order, 1, 1m)).ToList();

        var errors = Refusal(() => Record(order, project, tooMany));

        Assert.Equal(["A delivery can list at most 100 lines, the most an order can have."], errors["lines"]);
    }

    [Fact]
    public void A_refused_delivery_leaves_the_order_untouched()
    {
        var (order, project) = ApprovedOrder();

        Assert.Throws<ValidationFailedException>(() => Record(order, project, [Arrived(order, 1, 11m)]));

        Assert.Equal(TestData.Now, order.UpdatedAtUtc);
    }

    [Fact]
    public void Only_an_approved_order_takes_a_delivery()
    {
        var supplier = NewSupplier();
        var project = TestData.NewProject();
        PurchaseOrder Fresh()
        {
            var order = PurchaseOrder.CreateDraft(Guid.NewGuid(), TestData.Tenant, supplier, project, null, TestData.Today, null, null, null, [Line("Rebar", 10m, 50_000)],
                1_200, TestData.UserId, null, TestData.Today, TestData.Now);
            order.AssignNumber(3);
            return order;
        }

        var draft = Fresh();
        Assert.Equal("PO-0003 is not approved yet. Approve it before recording a delivery.", Assert.Throws<DomainRuleException>(() => Record(draft, project, [Arrived(draft, 1, 1m)])).Message);

        var closed = Fresh();
        closed.Approve(supplier, project, TestData.UserId, TestData.Now);
        closed.Close(project, null, TestData.UserId, TestData.Now);
        Assert.Equal("This order is closed.", Assert.Throws<DomainRuleException>(() => Record(closed, project, [Arrived(closed, 1, 1m)])).Message);

        var cancelled = Fresh();
        cancelled.Cancel(project, "Re-tender", false, false, TestData.UserId, TestData.Now);
        Assert.Equal("This order was cancelled.", Assert.Throws<DomainRuleException>(() => Record(cancelled, project, [Arrived(cancelled, 1, 1m)])).Message);
    }

    [Fact]
    public void An_archived_project_takes_no_new_delivery()
    {
        var (order, project) = ApprovedOrder();
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Contains("Restore it", Assert.Throws<DomainRuleException>(() => Record(order, project, [Arrived(order, 1, 1m)])).Message);
    }

    [Fact]
    public void A_line_takes_what_is_left_and_not_a_hundredth_more()
    {
        var (order, project) = ApprovedOrder();
        var already = new Dictionary<Guid, decimal> { [order.Lines[0].Id] = 4m, [order.Lines[1].Id] = 3m };

        // 10 ordered, 4 received: exactly the 6 left is fine.
        Assert.Equal(6m, Record(order, project, [Arrived(order, 1, 6m)], already).Lines.Single().Quantity);

        var over = Refusal(() => Record(order, project, [Arrived(order, 1, 6.0001m)], already));
        Assert.Equal(["Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less."], over["lines"]);

        // One left reads in the singular; a line that has all arrived says so.
        var one = Refusal(() => Record(order, project, [Arrived(order, 1, 2m)], new Dictionary<Guid, decimal> { [order.Lines[0].Id] = 9m }));
        Assert.Equal(["Line 1 (Rebar 16 mm): only 1 pc is left to receive. Enter 1 or less."], one["lines"]);
        var complete = Refusal(() => Record(order, project, [Arrived(order, 2, 0.5m)], already));
        Assert.Equal(["Line 2 (Tie wire) has all been received already."], complete["lines"]);
    }

    [Fact]
    public void A_quantity_must_be_greater_than_zero_with_at_most_four_decimals()
    {
        var (order, project) = ApprovedOrder();

        foreach (var quantity in new[] { 0m, -1m, 1.00001m })
        {
            var errors = Refusal(() => Record(order, project, [Arrived(order, 1, quantity)]));
            Assert.Equal(new[] { "lines" }, errors.Keys);
            Assert.StartsWith("Line 1 (Rebar 16 mm): ", errors["lines"].Single(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_problem_with_the_delivery_is_reported_together()
    {
        var (order, project) = ApprovedOrder();
        var (other, _) = ApprovedOrder();

        var errors = Refusal(() => Record(order, project,
            [Arrived(order, 1, 0m), Arrived(order, 1, 1m), Arrived(order, 2, 4m), new GoodsReceiptLineInput(other.Lines[0].Id, 1m)],
            date: TestData.Today.AddDays(1), number: new string('d', 61), note: new string('n', 301)));

        Assert.Equal(new HashSet<string> { "receivedDate", "deliveryReceiptNumber", "note", "lines" }, errors.Keys.ToHashSet());
        Assert.Equal("The delivery date can't be in the future.", errors["receivedDate"].Single());
        Assert.Equal(4, errors["lines"].Length);
        Assert.Contains(errors["lines"], message => message.StartsWith("Line 1 (Rebar 16 mm): Enter the quantity received greater than zero", StringComparison.Ordinal));
        Assert.Contains(errors["lines"], message => message == "Line 1 (Rebar 16 mm) is listed twice. Enter the whole quantity once.");
        Assert.Contains(errors["lines"], message => message == "Line 2 (Tie wire): only 3 pc are left to receive. Enter 3 or less.");
        Assert.Contains(errors["lines"], message => message == "Line 4: that line isn't on PO-0007. Reload the order and try again.");
    }

    [Fact]
    public void A_delivery_names_at_least_one_line_and_a_real_date()
    {
        var (order, project) = ApprovedOrder();

        Assert.Equal(["Enter how much arrived for at least one line."], Refusal(() => Record(order, project, []))["lines"]);
        Assert.Equal(["Choose the day the delivery arrived."], Refusal(() => Record(order, project, [Arrived(order, 1, 1m)], date: DateOnly.MinValue))["receivedDate"]);
    }

    [Fact]
    public void The_same_delivery_receipt_number_is_refused_once_the_use_case_says_it_is_recorded()
    {
        var (order, project) = ApprovedOrder();

        var duplicate = Refusal(() => Record(order, project, [Arrived(order, 1, 1m)], numberRecorded: true));
        Assert.Equal(["This delivery receipt is already recorded."], duplicate["deliveryReceiptNumber"]);

        // With no number given there is nothing to compare.
        Assert.NotNull(Record(order, project, [Arrived(order, 1, 1m)], number: null, numberRecorded: true));
    }

    [Fact]
    public void A_number_is_compared_by_its_letters_and_digits_only()
    {
        Assert.Equal("DR0012", GoodsReceipt.NormalizeNumber("DR-0012"));
        Assert.Equal(GoodsReceipt.NormalizeNumber("DR-0012"), GoodsReceipt.NormalizeNumber(" dr 0012. "));
        Assert.NotEqual(GoodsReceipt.NormalizeNumber("DR-12"), GoodsReceipt.NormalizeNumber("DR-0012"));

        var (order, project) = ApprovedOrder();
        var punctuation = Refusal(() => Record(order, project, [Arrived(order, 1, 1m)], number: "---"));
        Assert.Equal(["The delivery receipt number needs at least one letter or digit."], punctuation["deliveryReceiptNumber"]);
    }

    [Fact]
    public void A_mistaken_delivery_is_voided_with_a_reason_and_stays_on_record()
    {
        var (order, project) = ApprovedOrder();
        var receipt = Record(order, project, [Arrived(order, 1, 4m)]);

        Assert.Contains("reason", Assert.Throws<ValidationFailedException>(() => receipt.Void(project, "  ", TestData.UserId, Later)).Errors.Keys);
        Assert.Equal(GoodsReceiptStatus.Posted, receipt.Status);

        var voidedAt = Later.AddMinutes(10);
        receipt.Void(project, " Counted the wrong truck ", Guid.Parse("33333333-3333-3333-3333-333333333333"), voidedAt);

        Assert.Equal(GoodsReceiptStatus.Void, receipt.Status);
        Assert.True(receipt.IsVoid);
        Assert.Equal(("Counted the wrong truck", voidedAt, Guid.Parse("33333333-3333-3333-3333-333333333333")), (receipt.VoidReason, receipt.VoidedAtUtc, receipt.VoidedByUserId));
        Assert.Single(receipt.Lines);
        Assert.Equal("This delivery was already voided.", Assert.Throws<DomainRuleException>(() => receipt.Void(project, "Again", TestData.UserId, voidedAt)).Message);
    }

    [Fact]
    public void A_void_on_an_archived_project_waits_until_it_is_restored()
    {
        var (order, project) = ApprovedOrder();
        var receipt = Record(order, project, [Arrived(order, 1, 4m)]);
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Contains("Restore it", Assert.Throws<DomainRuleException>(() => receipt.Void(project, "Mistake", TestData.UserId, Later)).Message);
        Assert.Equal(GoodsReceiptStatus.Posted, receipt.Status);

        project.Restore(TestData.Now);
        receipt.Void(project, "Mistake", TestData.UserId, Later);
        Assert.True(receipt.IsVoid);
    }

    [Fact]
    public void A_void_may_follow_a_close_so_a_mistake_on_a_closed_order_can_still_be_corrected()
    {
        var (order, project) = ApprovedOrder();
        var receipt = Record(order, project, [Arrived(order, 1, 4m)]);
        order.Close(project, null, TestData.UserId, Later);

        receipt.Void(project, "Wrong order", TestData.UserId, Later);

        Assert.True(receipt.IsVoid);
    }

    [Fact]
    public void A_delivery_is_voided_against_its_own_project_only()
    {
        var (order, project) = ApprovedOrder();
        var receipt = Record(order, project, [Arrived(order, 1, 4m)]);

        Assert.Throws<ArgumentException>(() => receipt.Void(TestData.NewProject("Tower B"), "Mistake", TestData.UserId, Later));
    }

    [Fact]
    public void What_is_left_to_receive_is_ordered_minus_received_and_never_below_zero()
    {
        Assert.Equal(6m, GoodsReceipt.LeftToReceive(10m, 4m));
        Assert.Equal(0m, GoodsReceipt.LeftToReceive(10m, 10m));
        Assert.Equal(0m, GoodsReceipt.LeftToReceive(10m, 12m));
        Assert.Equal(0.0001m, GoodsReceipt.LeftToReceive(1m, 0.9999m));
    }
}

public sealed class DeliveryRulesTests
{
    [Fact]
    public void An_order_is_not_received_partly_received_or_fully_received_by_its_lines()
    {
        Assert.Equal(DeliveryState.NotReceived, DeliveryRules.StateOf([]));
        Assert.Equal(DeliveryState.NotReceived, DeliveryRules.StateOf([(10m, 0m), (3m, 0m)]));
        Assert.Equal(DeliveryState.PartlyReceived, DeliveryRules.StateOf([(10m, 0.0001m), (3m, 0m)]));
        // One line complete and another untouched is still partly received.
        Assert.Equal(DeliveryState.PartlyReceived, DeliveryRules.StateOf([(10m, 10m), (3m, 0m)]));
        Assert.Equal(DeliveryState.PartlyReceived, DeliveryRules.StateOf([(10m, 10m), (3m, 2.9999m)]));
        Assert.Equal(DeliveryState.FullyReceived, DeliveryRules.StateOf([(10m, 10m), (3m, 3m)]));
        Assert.Equal(DeliveryState.FullyReceived, DeliveryRules.StateOf([(2.5m, 2.5m)]));
    }

    [Fact]
    public void The_value_received_is_the_unit_cost_times_the_quantity_rounded_once_on_the_lines_own_vat_basis()
    {
        // 4 of 10 pc at 500.00 with 12% included: base 2,000.00; net 1,785.71 (1,785.7143 rounded once), VAT 214.29.
        Assert.Equal(new LineAmounts(178_571, 21_429, 200_000), DeliveryRules.ReceivedValue(4m, 50_000, VatTreatment.Inclusive, 1_200));
        // 2 of 3 pc at 99.99 with 12% added: base 199.98; VAT 24.00 (23.9976 rounded); gross 223.98.
        Assert.Equal(new LineAmounts(19_998, 2_400, 22_398), DeliveryRules.ReceivedValue(2m, 9_999, VatTreatment.Exclusive, 1_200));
        // Half a piece at 3.33 is 1.665: rounded half away from zero, once.
        Assert.Equal(new LineAmounts(167, 0, 167), DeliveryRules.ReceivedValue(0.5m, 333, VatTreatment.Exempt, 1_200));
        Assert.Equal(LineAmounts.Zero, DeliveryRules.ReceivedValue(0m, 50_000, VatTreatment.Inclusive, 1_200));
    }

    [Fact]
    public void Receiving_the_whole_line_is_worth_exactly_the_line()
    {
        // The line's own amounts are 3 x 99.99 added VAT: 299.97 + 36.00 = 335.97.
        Assert.Equal(new LineAmounts(29_997, 3_600, 33_597), DeliveryRules.ReceivedValue(3m, 9_999, VatTreatment.Exclusive, 1_200));
        Assert.Equal(LineAmounts.Calculate(10m, 50_000, VatTreatment.Inclusive, 1_200), DeliveryRules.ReceivedValue(10m, 50_000, VatTreatment.Inclusive, 1_200));
    }

    [Fact]
    public void An_invoice_runs_ahead_of_delivery_only_when_it_is_more_than_the_value_received()
    {
        Assert.True(DeliveryRules.InvoicedAheadOfDelivery(200_001, 200_000));
        Assert.True(DeliveryRules.InvoicedAheadOfDelivery(1, 0));
        Assert.False(DeliveryRules.InvoicedAheadOfDelivery(200_000, 200_000));
        Assert.False(DeliveryRules.InvoicedAheadOfDelivery(0, 0));
        Assert.False(DeliveryRules.InvoicedAheadOfDelivery(100_000, 200_000));
    }
}
