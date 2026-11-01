using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Billing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

public sealed class BillingCalculatorTests
{
    [Fact]
    public void A_progress_bill_splits_out_VAT_then_takes_retention_from_the_gross_and_withholding_from_the_amount_before_VAT()
    {
        // ₱2,240,000.00 of work, VAT included at 12%: net 2,000,000.00 + VAT 240,000.00. Retention 10% of the gross: 224,000.00.
        // Withholding 2% of the net: 40,000.00. The client is asked for 2,240,000.00 - 224,000.00 - 40,000.00 = 1,976,000.00.
        var amounts = BillingCalculator.Progress(224_000_000, 1_200, 1_000, 200);

        Assert.Equal(new BillAmounts(224_000_000, 200_000_000, 24_000_000, 22_400_000, 4_000_000, 197_600_000), amounts);
    }

    [Fact]
    public void Each_figure_is_rounded_once_half_away_from_zero_and_the_parts_still_add_up()
    {
        // ₱10.05: net = 1,005 x 10,000 / 11,200 = 897.32 -> 897, VAT 108. Retention 10% = 100.5 -> 101. Withholding 1% of 897 = 8.97 -> 9.
        Assert.Equal(new BillAmounts(1_005, 897, 108, 101, 9, 895), BillingCalculator.Progress(1_005, 1_200, 1_000, 100));

        // ₱9.52: net = 952 / 1.12 = 850 exactly, VAT 102. Retention 95.2 -> 95. Withholding 1% of 850 = 8.5 -> 9.
        Assert.Equal(new BillAmounts(952, 850, 102, 95, 9, 848), BillingCalculator.Progress(952, 1_200, 1_000, 100));

        // ₱0.14: net = 14 / 1.12 = 12.5 -> 13 (half away from zero), VAT is what is left: 1.
        var tiny = BillingCalculator.Progress(14, 1_200, 0, 0);
        Assert.Equal((13, 1), (tiny.NetMinor, tiny.VatMinor));
        Assert.Equal(14, tiny.NetMinor + tiny.VatMinor);
    }

    [Fact]
    public void A_retention_release_adds_no_VAT_and_holds_nothing_back()
    {
        Assert.Equal(new BillAmounts(22_400_000, 22_400_000, 0, 0, 0, 22_400_000), BillingCalculator.RetentionRelease(22_400_000));
    }

    [Fact]
    public void VAT_withholding_is_a_share_of_the_amount_before_VAT_taken_beside_the_income_tax_withholding()
    {
        // ₱2,240,000.00 with 5% VAT withheld: 5% of the net 2,000,000.00 = 100,000.00. The client pays 2,240,000.00 - 224,000.00 - 40,000.00
        // - 100,000.00 = 1,876,000.00.
        Assert.Equal(new BillAmounts(224_000_000, 200_000_000, 24_000_000, 22_400_000, 4_000_000, 187_600_000, 10_000_000, 0),
            BillingCalculator.Progress(224_000_000, 1_200, 1_000, 200, 500));

        // ₱10.05: net 897; 5% of it is 44.85 -> 45, rounded once on its own line. 1,005 - 101 - 9 - 45 = 850.
        Assert.Equal(new BillAmounts(1_005, 897, 108, 101, 9, 850, 45, 0), BillingCalculator.Progress(1_005, 1_200, 1_000, 100, 500));

        // ₱9.52: net 850; 1% is 8.5 -> 9 (half away from zero).
        Assert.Equal(9, BillingCalculator.Progress(952, 1_200, 0, 0, 100).VatWithholdingMinor);
    }

    [Fact]
    public void The_down_payment_recovery_comes_off_last_and_a_bill_whose_deductions_are_more_than_it_is_refused()
    {
        // ₱1.00 at 30% retention, 10% withholding, 12% VAT withheld: net 89 (89.29), retention 30, withholding 9 (8.93), VAT withheld 11
        // (10.68). With 50 recovered the deductions are exactly the bill: the client pays nothing, which is allowed.
        Assert.Equal(new BillAmounts(100, 89, 11, 30, 9, 0, 11, 50), BillingCalculator.Progress(100, 1_200, 3_000, 1_000, 1_200, 50));

        var refused = Assert.Throws<DomainRuleException>(() => BillingCalculator.Progress(100, 1_200, 3_000, 1_000, 1_200, 51));
        Assert.Equal("The deductions are more than this bill. Check the down payment, retention and withholding rates.", refused.Message);
    }

    [Fact]
    public void A_down_payment_is_a_receivable_only_rounded_once_from_the_contract_price()
    {
        // 15% of ₱11,200,000.00 = 1,680,000.00, with no VAT split, retention or withholding.
        Assert.Equal(new BillAmounts(168_000_000, 168_000_000, 0, 0, 0, 168_000_000), BillingCalculator.Advance(1_120_000_000, 1_500));
        // 15% of ₱3,333.33 = 499.9995 -> 500.00; 50% of 1 centavo = 0.5 -> 1; 50% of 3 centavos = 1.5 -> 2.
        Assert.Equal(50_000, BillingCalculator.Advance(333_333, 1_500).GrossMinor);
        Assert.Equal(1, BillingCalculator.Advance(1, 5_000).GrossMinor);
        Assert.Equal(2, BillingCalculator.Advance(3, 5_000).GrossMinor);
    }

    [Fact]
    public void The_recovery_is_worked_out_on_the_work_to_date_so_it_is_exact_at_100_percent_where_per_bill_rounding_would_drift()
    {
        // A 7-centavo contract with a 50% down payment of 4 centavos (3.5 -> 4), billed one centavo at a time. Rounding each bill's own
        // share (0.5 -> 1) would recover 7; the cumulative rule (round(4 x work to date / 7)) recovers 1, 0, 1, 0, 1, 0, 1 and exactly 4.
        var recovered = 0L;
        var each = new List<long>();
        for (var cumulative = 1L; cumulative <= 7; cumulative++)
        {
            var recovery = BillingCalculator.AdvanceRecovery(cumulative, 7, 4, recovered);
            each.Add(recovery);
            recovered += recovery;
        }

        Assert.Equal([1L, 0, 1, 0, 1, 0, 1], each);
        Assert.Equal(4, recovered);
    }

    [Fact]
    public void The_recovery_follows_the_work_against_the_price_now_never_passes_the_down_payment_and_never_goes_below_zero()
    {
        // ₱1,500.00 down payment (15% of ₱10,000.00).
        Assert.Equal(0, BillingCalculator.AdvanceRecovery(500_000, 1_000_000, 0, 0));
        Assert.Equal(75_000, BillingCalculator.AdvanceRecovery(500_000, 1_000_000, 150_000, 0));
        // Exactly the down payment at 100% of the price, and never more (work past the price recovers no more).
        Assert.Equal(75_000, BillingCalculator.AdvanceRecovery(1_000_000, 1_000_000, 150_000, 75_000));
        Assert.Equal(150_000, BillingCalculator.AdvanceRecovery(1_000_001, 1_000_000, 150_000, 0));

        // The price was raised to ₱20,000.00 after half the old price was billed: 75% of the new price recovers 75% of the down payment,
        // 1,125.00 to date, so this bill takes 375.00; at 30% of the new price less was due to date than already recovered: nothing.
        Assert.Equal(37_500, BillingCalculator.AdvanceRecovery(1_500_000, 2_000_000, 150_000, 75_000));
        Assert.Equal(0, BillingCalculator.AdvanceRecovery(600_000, 2_000_000, 150_000, 75_000));

        // The price was cut to ₱9,000.00: the share of the new price decides (round(1,500.00 x 8,999.99 / 9,000.00) = 1,499.998 -> 1,500.00),
        // and the bill that reaches the price recovers everything left.
        Assert.Equal(75_000, BillingCalculator.AdvanceRecovery(899_999, 900_000, 150_000, 75_000));
        Assert.Equal(75_000, BillingCalculator.AdvanceRecovery(900_000, 900_000, 150_000, 75_000));
        // Rounded once, half away from zero: 1,500.00 x 1 / 2 = 750.00; 1 centavo x 1 / 2 = 0.5 -> 1.
        Assert.Equal(75_000, BillingCalculator.AdvanceRecovery(1, 2, 150_000, 0));
        Assert.Equal(1, BillingCalculator.AdvanceRecovery(1, 2, 1, 0));
    }

    [Fact]
    public void While_the_price_is_unchanged_the_recovery_is_within_one_centavo_of_the_down_payment_rate_on_the_work_to_date()
    {
        // The rule this replaced recovered min(down payment, round(work to date x rate)) to date. With the price as billed the proportional
        // rule gives the same figure to within the one centavo the down payment's own rounding can move it.
        foreach (var (price, bps) in new[] { (1_120_000_000L, 1_500), (333_333L, 1_500), (999_999_999L, 2_000), (7L, 5_000), (123_456_789L, 1_234) })
        {
            var advance = BillingCalculator.Advance(price, bps).GrossMinor;
            for (var step = 1; step <= 97; step++)
            {
                var cumulative = price * step / 97;
                var before = Math.Min(advance, (long)Math.Round(cumulative * (decimal)bps / 10_000, MidpointRounding.AwayFromZero));
                Assert.InRange(BillingCalculator.AdvanceRecovery(cumulative, price, advance, 0) - before, -1, 1);
            }
            Assert.Equal(advance, BillingCalculator.AdvanceRecovery(price, price, advance, 0));
        }
    }

    [Fact]
    public void The_share_billed_is_in_basis_points_rounded_once_and_absent_without_a_price()
    {
        Assert.Equal(2_000, BillingCalculator.ShareBps(224_000_000, 1_120_000_000));
        Assert.Equal(3_333, BillingCalculator.ShareBps(1, 3));
        Assert.Equal(6_667, BillingCalculator.ShareBps(2, 3));
        Assert.Null(BillingCalculator.ShareBps(5, 0));
    }
}

public sealed class ClientInvoiceTests
{
    // A ₱11,200,000.00 contract (VAT included). Terms: 10% retention, 2% withholding, 30 days.
    private const long Price = 1_120_000_000;
    private static readonly BillingTermsSnapshot Terms = new(1_000, 200, 30);
    private static readonly DateOnly BillDate = new(2026, 10, 31);

    private static Project PricedProject(long? price = Price) => TestData.NewProject(contractPriceMinor: price);

    private static Project CompletedProject()
    {
        var project = PricedProject();
        project.Start(TestData.Now);
        project.Complete(TestData.Now);
        return project;
    }

    private static ClientInvoice Progress(Project project, long cumulative, BillingLedger? ledger = null, DateOnly? date = null, string? description = "Progress billing no. 1") =>
        ClientInvoice.DraftProgress(Guid.NewGuid(), TestData.Tenant, project, date ?? BillDate, description, cumulative, ledger ?? BillingLedger.Empty, Terms, 1_200,
            TestData.UserId, null, TestData.Today, TestData.Now);

    private static ClientInvoice Issue(ClientInvoice draft, Project project, ClientInvoice? head, BillingLedger ledger, long number)
    {
        BillingChain.Issue(draft, project, head, ledger, Terms, number, TestData.UserId, TestData.Now);
        return draft;
    }

    internal static BillingLedger After(params ClientInvoice[] issued) => new(
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.GrossMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.RetentionMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.RetentionRelease).Sum(bill => bill.GrossMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Advance).Sum(bill => bill.GrossMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Advance).Select(bill => bill.AdvanceBps).DefaultIfEmpty(0).Max(),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.AdvanceRecoveryMinor));

    [Fact]
    public void A_draft_bills_the_work_to_date_with_the_terms_and_VAT_rate_copied_in_and_has_no_number()
    {
        var bill = Progress(PricedProject(), 224_000_000);

        Assert.Equal(ClientInvoiceStatus.Draft, bill.Status);
        Assert.Null(bill.Number);
        Assert.Equal((224_000_000L, 0L), (bill.CumulativeWorkMinor!.Value, bill.PreviousCumulativeWorkMinor));
        Assert.Equal((224_000_000L, 200_000_000L, 24_000_000L), (bill.GrossMinor, bill.NetMinor, bill.VatMinor));
        Assert.Equal((1_200, 1_000, 200, 30), (bill.VatRateBps, bill.RetentionBps, bill.WithholdingBps, bill.PaymentTermsDays));
        Assert.Equal((22_400_000L, 4_000_000L, 197_600_000L), (bill.RetentionMinor, bill.WithholdingMinor, bill.NetReceivableMinor));
        Assert.Equal(new DateOnly(2026, 11, 30), bill.DueDate);
        Assert.Equal(0, bill.OutstandingMinor);
    }

    [Fact]
    public void Successive_bills_each_bill_the_difference_from_the_previous_issued_bill_and_chain_together()
    {
        var project = PricedProject();
        var first = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        var ledger = After(first);

        // ₱5,600,000.00 to date: this bill is 3,360,000.00. Net 3,000,000.00, VAT 360,000.00, retention 336,000.00, withholding 60,000.00.
        var second = Issue(Progress(project, 560_000_000, ledger), project, first, ledger, 2);

        Assert.Equal(("BILL-0001", 1, (Guid?)second.Id), (first.Number, first.ChainIndex!.Value, first.NextInvoiceId));
        Assert.Equal(("BILL-0002", 2, (Guid?)first.Id), (second.Number, second.ChainIndex!.Value, second.PreviousInvoiceId));
        Assert.Equal(224_000_000, second.PreviousCumulativeWorkMinor);
        Assert.Equal(new BillAmounts(336_000_000, 300_000_000, 36_000_000, 33_600_000, 6_000_000, 296_400_000),
            new BillAmounts(second.GrossMinor, second.NetMinor, second.VatMinor, second.RetentionMinor, second.WithholdingMinor, second.NetReceivableMinor));
        Assert.Equal(new BillingLedger(560_000_000, 56_000_000, 0), After(first, second));
        Assert.Equal(ClientInvoiceStatus.Issued, second.Status);
        Assert.Equal(296_400_000, second.OutstandingMinor);
    }

    [Fact]
    public void A_draft_reports_every_wrong_field_together()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Progress(PricedProject(), 0, date: new DateOnly(2026, 11, 3), description: " "));

        Assert.Equal(["billingDate", "description", "cumulativeWorkMinor"], ex.Errors.Keys);
        Assert.Equal("The billing date can't be in the future.", ex.Errors["billingDate"][0]);
        Assert.Equal("Enter a description of the bill.", ex.Errors["description"][0]);
        Assert.Equal("Work completed to date must be more than what has already been billed.", ex.Errors["cumulativeWorkMinor"][0]);
    }

    [Fact]
    public void Work_to_date_must_rise_above_what_is_billed_and_stay_within_the_contract_price()
    {
        var project = PricedProject();
        var ledger = new BillingLedger(224_000_000, 22_400_000, 0);

        Assert.Equal("Work completed to date must be more than what has already been billed.",
            Assert.Throws<ValidationFailedException>(() => Progress(project, 224_000_000, ledger)).Errors["cumulativeWorkMinor"][0]);
        Assert.Equal("Work completed to date can't be more than the contract price.",
            Assert.Throws<ValidationFailedException>(() => Progress(project, Price + 1, ledger)).Errors["cumulativeWorkMinor"][0]);
        Assert.Equal(Price - 224_000_000, Progress(project, Price, ledger).GrossMinor);
    }

    [Fact]
    public void A_project_without_a_price_fully_billed_or_archived_can_not_be_billed()
    {
        Assert.Equal(ClientInvoice.NoContractPriceMessage, Assert.Throws<DomainRuleException>(() => Progress(PricedProject(null), 100)).Message);
        Assert.Equal("The whole contract price has already been billed. There is nothing more to bill on this project.",
            Assert.Throws<DomainRuleException>(() => Progress(PricedProject(), Price, new BillingLedger(Price, 0, 0))).Message);

        var archived = PricedProject();
        archived.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal("This project is archived. Restore it before making changes.", Assert.Throws<DomainRuleException>(() => Progress(archived, 100)).Message);
    }

    [Fact]
    public void Issuing_rechecks_the_chain_so_a_draft_made_before_another_bill_was_issued_is_refused_until_saved_again()
    {
        var project = PricedProject();
        var early = Progress(project, 300_000_000);
        var first = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        var ledger = After(first);

        Assert.True(early.IsStale(ledger, Price, Terms));
        var stale = Assert.Throws<DomainRuleException>(() => BillingChain.Issue(early, project, first, ledger, Terms, 2, TestData.UserId, TestData.Now));
        Assert.StartsWith("Another bill was issued or voided after this draft was prepared", stale.Message);
        Assert.Null(first.NextInvoiceId);
        Assert.Equal(ClientInvoiceStatus.Draft, early.Status);

        // Saved again, it bills only the difference and can be issued.
        early.UpdateDraft(project, BillDate, "Progress billing no. 2", 300_000_000, ledger, Terms, 1_200, TestData.Today, TestData.Now);
        Assert.False(early.IsStale(ledger, Price, Terms));
        Assert.Equal(76_000_000, early.GrossMinor);
        Issue(early, project, first, ledger, 2);
        Assert.Equal("BILL-0002", early.Number);
    }

    [Fact]
    public void Issuing_is_refused_for_an_issued_or_void_bill_and_when_the_price_dropped_below_the_work_billed()
    {
        var project = PricedProject();
        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        Assert.Equal("This bill was already issued.", Assert.Throws<DomainRuleException>(() => bill.EnsureCanIssue(project, BillingLedger.Empty, Terms)).Message);

        var draft = Progress(project, 300_000_000, After(bill));
        project.UpdateDetails(project.Name, null, null, 250_000_000, project.StartDate, null, TestData.Now);
        Assert.StartsWith("The contract price is now less than the work completed to date",
            Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, After(bill), Terms)).Message);

        draft.Discard(project, "Wrong amount", TestData.UserId, TestData.Now);
        Assert.Equal("A void bill can't be issued.", Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, After(bill), Terms)).Message);
    }

    [Fact]
    public void Only_the_latest_bill_can_be_voided_and_voiding_it_makes_the_bill_before_it_the_latest_again()
    {
        var project = PricedProject();
        var first = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        var second = Issue(Progress(project, 560_000_000, After(first)), project, first, After(first), 2);

        var notLatest = Assert.Throws<DomainRuleException>(() => BillingChain.Void(first, project, null, "BILL-0002", "Wrong", TestData.UserId, TestData.Now));
        Assert.Equal("Only the latest bill of a project can be voided. Void BILL-0002 first.", notLatest.Message);

        Assert.Equal("Enter the reason for voiding this bill.",
            Assert.Throws<ValidationFailedException>(() => BillingChain.Void(second, project, first, null, " ", TestData.UserId, TestData.Now)).Errors["reason"][0]);
        Assert.Equal(second.Id, first.NextInvoiceId);

        BillingChain.Void(second, project, first, null, "Billed the wrong period", TestData.UserId, TestData.Now);
        Assert.Equal((ClientInvoiceStatus.Void, "Billed the wrong period", "BILL-0002"), (second.Status, second.VoidReason, second.Number));
        Assert.Null(first.NextInvoiceId);
        Assert.Equal(0, second.OutstandingMinor);

        // The next bill follows the first again and takes the voided bill's place in the chain.
        var replacement = Issue(Progress(project, 500_000_000, After(first)), project, first, After(first), 3);
        Assert.Equal((2, "BILL-0003", 276_000_000L), (replacement.ChainIndex!.Value, replacement.Number, replacement.GrossMinor));
    }

    [Fact]
    public void A_bill_with_a_payment_can_not_be_voided_until_the_payment_is_voided()
    {
        var project = PricedProject();
        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        var payment = bill.RecordPayment(Guid.NewGuid(), project, 100_000_000, BillDate, ClientPaymentMethod.BankTransfer, "BDO 7781", null, TestData.UserId,
            null, TestData.Today, TestData.Now);

        Assert.Equal("This bill has payments recorded against it. Void those payments first, then void the bill.",
            Assert.Throws<DomainRuleException>(() => BillingChain.Void(bill, project, null, null, "Wrong", TestData.UserId, TestData.Now)).Message);

        bill.VoidPayment(payment.Id, project, "Cheque bounced", TestData.UserId, TestData.Now);
        BillingChain.Void(bill, project, null, null, "Wrong", TestData.UserId, TestData.Now);
        Assert.Equal(ClientInvoiceStatus.Void, bill.Status);
    }

    [Fact]
    public void A_draft_is_discarded_not_voided_and_an_issued_bill_is_voided_not_discarded()
    {
        var project = PricedProject();
        var draft = Progress(project, 224_000_000);
        Assert.Equal("A draft isn't voided: discard it instead.",
            Assert.Throws<DomainRuleException>(() => BillingChain.Void(draft, project, null, null, "x", TestData.UserId, TestData.Now)).Message);
        draft.Discard(project, "Duplicate", TestData.UserId, TestData.Now);
        Assert.Equal((ClientInvoiceStatus.Void, (string?)null, "Duplicate"), (draft.Status, draft.Number, draft.VoidReason));

        var issued = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        Assert.Equal("An issued bill can't be discarded. Void it instead.",
            Assert.Throws<DomainRuleException>(() => issued.Discard(project, "x", TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void Payments_reduce_what_is_outstanding_never_below_zero_and_a_voided_payment_is_owed_again()
    {
        var project = PricedProject();
        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        Payment(bill, project, 100_000_000);

        Assert.Equal((100_000_000L, 97_600_000L), (bill.PaidMinor, bill.OutstandingMinor));
        Assert.Equal("That is more than what is still to collect on this bill.",
            Assert.Throws<ValidationFailedException>(() => Payment(bill, project, 97_600_001)).Errors["amountMinor"][0]);

        var last = Payment(bill, project, 97_600_000);
        Assert.Equal(0, bill.OutstandingMinor);
        Assert.Equal("This bill is fully paid. There is nothing left to collect on it.",
            Assert.Throws<DomainRuleException>(() => Payment(bill, project, 1)).Message);

        bill.VoidPayment(last.Id, project, "Entered twice", TestData.UserId, TestData.Now);
        Assert.Equal(97_600_000, bill.OutstandingMinor);
        Assert.Equal("This payment was already voided.",
            Assert.Throws<DomainRuleException>(() => bill.VoidPayment(last.Id, project, "again", TestData.UserId, TestData.Now)).Message);
    }

    [Fact]
    public void A_payment_reports_every_wrong_field_and_is_refused_on_a_draft_or_an_archived_project()
    {
        var project = PricedProject();
        Assert.Equal("Issue this bill before recording a payment against it.",
            Assert.Throws<DomainRuleException>(() => Payment(Progress(project, 224_000_000), project, 1)).Message);

        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        var ex = Assert.Throws<ValidationFailedException>(() => bill.RecordPayment(Guid.NewGuid(), project, 0, new DateOnly(2026, 11, 3),
            (ClientPaymentMethod)(-1), new string('r', 81), null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Equal(["amountMinor", "paidOn", "method", "reference"], ex.Errors.Keys);
        Assert.Equal("The payment date can't be in the future.", ex.Errors["paidOn"][0]);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal("This project is archived. Restore it before making changes.",
            Assert.Throws<DomainRuleException>(() => Payment(bill, project, 1)).Message);
    }

    [Fact]
    public void A_bill_is_overdue_the_day_after_its_due_date_while_something_is_outstanding()
    {
        var project = PricedProject();
        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);

        Assert.False(bill.IsOverdue(new DateOnly(2026, 11, 30)));
        Assert.True(bill.IsOverdue(new DateOnly(2026, 12, 1)));
        Payment(bill, project, bill.NetReceivableMinor);
        Assert.False(bill.IsOverdue(new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void Retention_is_released_only_on_a_completed_project_and_only_up_to_what_is_still_held()
    {
        var active = PricedProject();
        var held = new BillingLedger(560_000_000, 56_000_000, 0);
        Assert.Equal("Retention is released once the project is completed. Mark the project completed first.",
            Assert.Throws<DomainRuleException>(() => Release(active, 1, held)).Message);

        var project = CompletedProject();
        Assert.Equal("No retention is held on this project, so there is nothing to release.",
            Assert.Throws<DomainRuleException>(() => Release(project, 1, BillingLedger.Empty)).Message);
        Assert.Equal("That is more than the retention still held on this project.",
            Assert.Throws<ValidationFailedException>(() => Release(project, 56_000_001, held)).Errors["amountMinor"][0]);

        var release = Release(project, 56_000_000, held);
        Assert.Equal(new BillAmounts(56_000_000, 56_000_000, 0, 0, 0, 56_000_000),
            new BillAmounts(release.GrossMinor, release.NetMinor, release.VatMinor, release.RetentionMinor, release.WithholdingMinor, release.NetReceivableMinor));
        Assert.Null(release.CumulativeWorkMinor);
        Assert.Equal(0, release.VatRateBps);

        // Another release issued meanwhile leaves less held: this one is out of date.
        var lessHeld = held with { RetentionReleasedMinor = 10_000_000 };
        Assert.True(release.IsStale(lessHeld, Price, Terms));
        Assert.StartsWith("Less retention is held now than when this release was prepared",
            Assert.Throws<DomainRuleException>(() => release.EnsureCanIssue(project, lessHeld, Terms)).Message);
        release.EnsureCanIssue(project, held, Terms);
    }

    [Fact]
    public void Billing_terms_are_bounded_and_report_every_wrong_field()
    {
        var project = PricedProject();
        var ex = Assert.Throws<ValidationFailedException>(() => ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 3_001, 1_001, 181, TestData.Now));
        Assert.Equal(["retentionBps", "creditableWithholdingBps", "paymentTermsDays"], ex.Errors.Keys);

        var terms = ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 3_000, 1_000, 180, TestData.Now);
        Assert.Equal(new BillingTermsSnapshot(3_000, 1_000, 180), terms.Snapshot);
        terms.Change(project, 0, 0, 0, TestData.Now);
        Assert.Equal(new BillingTermsSnapshot(0, 0, 0), terms.Snapshot);
        Assert.Equal(new BillingTermsSnapshot(1_000, 0, 30, 0, 0), BillingTermsSnapshot.Defaults);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() => terms.Change(project, 500, 0, 30, TestData.Now));
    }

    [Fact]
    public void The_down_payment_and_VAT_withholding_are_bounded_and_a_government_client_caps_the_down_payment_at_15_percent()
    {
        var project = PricedProject();
        var ex = Assert.Throws<ValidationFailedException>(() =>
            ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 1_000, 0, 30, TestData.Now, advanceBps: 5_001, vatWithholdingBps: 1_201));
        Assert.Equal(["advanceBps", "vatWithholdingBps"], ex.Errors.Keys);
        Assert.Equal("Enter a down payment between 0% and 50% of the contract price.", ex.Errors["advanceBps"][0]);
        Assert.Equal("Enter a VAT withholding between 0% and 12%.", ex.Errors["vatWithholdingBps"][0]);
        Assert.Equal("advanceBps", Assert.Single(Assert.Throws<ValidationFailedException>(() =>
            ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 1_000, 0, 30, TestData.Now, advanceBps: -1)).Errors.Keys));

        // A private client may take up to 50%; a government client up to 15%.
        var terms = ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 1_000, 200, 30, TestData.Now, 5_000, 1_200);
        Assert.Equal(new BillingTermsSnapshot(1_000, 200, 30, 5_000, 1_200), terms.Snapshot);
        Assert.False(terms.IsGovernmentClient);

        var government = Assert.Throws<ValidationFailedException>(() => terms.Change(project, 1_000, 200, 30, TestData.Now, isGovernmentClient: true));
        Assert.Equal(ProjectBillingTerms.GovernmentAdvanceMessage, government.Errors["advanceBps"][0]);
        Assert.Equal("A government project allows a down payment of up to 15% of the contract price.", ProjectBillingTerms.GovernmentAdvanceMessage);
        Assert.False(terms.IsGovernmentClient);

        terms.Change(project, 1_000, 200, 30, TestData.Now, 1_500, null, true);
        Assert.Equal((1_500, 1_200, true), (terms.AdvanceBps, terms.VatWithholdingBps, terms.IsGovernmentClient));

        // Left out, the down payment, the VAT withholding and the government flag keep what is saved.
        terms.Change(project, 500, 100, 15, TestData.Now);
        Assert.Equal(new BillingTermsSnapshot(500, 100, 15, 1_500, 1_200), terms.Snapshot);
        Assert.True(terms.IsGovernmentClient);
    }

    private static ClientPayment Payment(ClientInvoice bill, Project project, long amount) =>
        bill.RecordPayment(Guid.NewGuid(), project, amount, BillDate, ClientPaymentMethod.BankTransfer, null, null, TestData.UserId, null, TestData.Today,
            TestData.Now);

    private static ClientInvoice Release(Project project, long amount, BillingLedger ledger) =>
        ClientInvoice.DraftRetentionRelease(Guid.NewGuid(), TestData.Tenant, project, BillDate, "Release of retention", amount, ledger, Terms, TestData.UserId,
            null, TestData.Today, TestData.Now);
}

public sealed class AdvanceBillTests
{
    // A ₱11,200,000.00 contract (VAT included). Terms: 10% retention, 2% withholding, 30 days, 15% down payment, 5% VAT withheld.
    private const long Price = 1_120_000_000;
    private static readonly BillingTermsSnapshot Terms = new(1_000, 200, 30, 1_500, 500);
    private static readonly DateOnly BillDate = new(2026, 10, 31);

    private static Project PricedProject(long? price = Price) => TestData.NewProject(contractPriceMinor: price);

    private static ClientInvoice Advance(Project project, BillingLedger? ledger = null, bool anotherExists = false, BillingTermsSnapshot? terms = null,
        DateOnly? date = null, string? description = "Down payment") =>
        ClientInvoice.DraftAdvance(Guid.NewGuid(), TestData.Tenant, project, date ?? BillDate, description, ledger ?? BillingLedger.Empty, anotherExists,
            terms ?? Terms, TestData.UserId, null, TestData.Today, TestData.Now);

    private static ClientInvoice Progress(Project project, long cumulative, BillingLedger ledger, BillingTermsSnapshot? terms = null) =>
        ClientInvoice.DraftProgress(Guid.NewGuid(), TestData.Tenant, project, BillDate, "Progress billing", cumulative, ledger, terms ?? Terms, 1_200,
            TestData.UserId, null, TestData.Today, TestData.Now);

    private static ClientInvoice Issue(ClientInvoice draft, Project project, ClientInvoice? head, BillingLedger ledger, long number,
        BillingTermsSnapshot? terms = null)
    {
        BillingChain.Issue(draft, project, head, ledger, terms ?? Terms, number, TestData.UserId, TestData.Now);
        return draft;
    }

    private static BillingLedger After(params ClientInvoice[] issued) => ClientInvoiceTests.After(issued);

    private static (long Gross, long Retention, long Withholding, long VatWithholding, long Recovery, long Receivable) Figures(ClientInvoice bill) =>
        (bill.GrossMinor, bill.RetentionMinor, bill.WithholdingMinor, bill.VatWithholdingMinor, bill.AdvanceRecoveryMinor, bill.NetReceivableMinor);

    [Fact]
    public void A_down_payment_bill_asks_for_its_share_of_the_contract_price_with_nothing_withheld_and_keeps_its_rate()
    {
        var project = PricedProject();
        var advance = Advance(project);

        Assert.Equal((ClientInvoiceKind.Advance, ClientInvoiceStatus.Draft, (string?)null, (long?)null), (advance.Kind, advance.Status, advance.Number,
            advance.CumulativeWorkMinor));
        Assert.Equal((168_000_000L, 168_000_000L, 0L), (advance.GrossMinor, advance.NetMinor, advance.VatMinor));
        Assert.Equal((0L, 0L, 0L, 0L, 168_000_000L), (advance.RetentionMinor, advance.WithholdingMinor, advance.VatWithholdingMinor,
            advance.AdvanceRecoveryMinor, advance.NetReceivableMinor));
        Assert.Equal((1_500, 0, 0, 0, 0), (advance.AdvanceBps, advance.VatRateBps, advance.RetentionBps, advance.WithholdingBps, advance.VatWithholdingBps));
        Assert.Equal(new DateOnly(2026, 11, 30), advance.DueDate);
        Assert.False(advance.IsStale(BillingLedger.Empty, Price, Terms));

        Issue(advance, project, null, BillingLedger.Empty, 1);
        Assert.Equal(("BILL-0001", 1), (advance.Number, advance.ChainIndex!.Value));
        Assert.Equal(new BillingLedger(0, 0, 0, 168_000_000, 1_500, 0), After(advance));
    }

    [Fact]
    public void Progress_bills_recover_the_down_payment_on_the_work_to_date_and_the_last_bill_leaves_nothing_to_recover()
    {
        var project = PricedProject();
        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);

        // 20% of the work: recover 15% of 2,240,000.00 = 336,000.00. Retention 224,000.00, withholding 40,000.00, VAT withheld 5% of the
        // net 2,000,000.00 = 100,000.00. The client pays 2,240,000.00 - 224,000.00 - 40,000.00 - 100,000.00 - 336,000.00 = 1,540,000.00.
        var first = Issue(Progress(project, 224_000_000, After(advance)), project, advance, After(advance), 2);
        Assert.Equal((224_000_000L, 22_400_000L, 4_000_000L, 10_000_000L, 33_600_000L, 154_000_000L), Figures(first));
        Assert.Equal((1_500, 500), (first.AdvanceBps, first.VatWithholdingBps));

        // 50% to date: 15% of 5,600,000.00 = 840,000.00, less 336,000.00 already recovered = 504,000.00.
        var second = Issue(Progress(project, 560_000_000, After(advance, first)), project, first, After(advance, first), 3);
        Assert.Equal((336_000_000L, 33_600_000L, 6_000_000L, 15_000_000L, 50_400_000L, 231_000_000L), Figures(second));

        // The last bill brings the work to the contract price and recovers exactly what is left: 1,680,000.00 - 840,000.00.
        var last = Issue(Progress(project, Price, After(advance, first, second)), project, second, After(advance, first, second), 4);
        Assert.Equal((560_000_000L, 56_000_000L, 10_000_000L, 25_000_000L, 84_000_000L, 385_000_000L), Figures(last));

        var ledger = After(advance, first, second, last);
        Assert.Equal((168_000_000L, 168_000_000L, 0L), (ledger.AdvanceMinor, ledger.AdvanceRecoveredMinor, ledger.AdvanceToRecoverMinor));
        // Everything the client is asked for adds up to the price less retention and both withholdings: the down payment is never billed twice.
        Assert.Equal(Price - 112_000_000 - 20_000_000 - 50_000_000,
            new[] { advance, first, second, last }.Sum(bill => bill.NetReceivableMinor));
    }

    [Fact]
    public void Recovery_uses_the_down_payment_billed_not_the_terms_as_later_changed()
    {
        var project = PricedProject();
        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);

        var laterTerms = Terms with { AdvanceBps = 3_000 };
        var bill = Progress(project, 224_000_000, After(advance), laterTerms);

        Assert.Equal((33_600_000L, 1_500), (bill.AdvanceRecoveryMinor, bill.AdvanceBps));
    }

    [Fact]
    public void Without_an_issued_down_payment_nothing_is_recovered()
    {
        var project = PricedProject();
        var draftAdvance = Advance(project);
        var bill = Progress(project, 224_000_000, BillingLedger.Empty);

        Assert.Equal(ClientInvoiceStatus.Draft, draftAdvance.Status);
        Assert.Equal((0L, 0), (bill.AdvanceRecoveryMinor, bill.AdvanceBps));
        Assert.Equal(224_000_000 - 22_400_000 - 4_000_000 - 10_000_000, bill.NetReceivableMinor);
    }

    [Fact]
    public void Voiding_the_latest_progress_bill_gives_back_the_recovery_it_took()
    {
        var project = PricedProject();
        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);
        var first = Issue(Progress(project, 224_000_000, After(advance)), project, advance, After(advance), 2);
        var second = Issue(Progress(project, 560_000_000, After(advance, first)), project, first, After(advance, first), 3);
        Assert.Equal(84_000_000, After(advance, first, second).AdvanceRecoveredMinor);

        BillingChain.Void(second, project, first, null, "Wrong period", TestData.UserId, TestData.Now);

        var ledger = After(advance, first);
        Assert.Equal((33_600_000L, 134_400_000L), (ledger.AdvanceRecoveredMinor, ledger.AdvanceToRecoverMinor));
        // Billed again to 40%: 15% of 4,480,000.00 = 672,000.00, less the 336,000.00 the first bill recovered.
        Assert.Equal(33_600_000, Progress(project, 448_000_000, ledger).AdvanceRecoveryMinor);
        Assert.Equal(50_400_000, Issue(Progress(project, 560_000_000, ledger), project, first, ledger, 4).AdvanceRecoveryMinor);
    }

    [Fact]
    public void A_progress_draft_made_before_the_down_payment_was_issued_is_stale_and_refused_until_saved_again()
    {
        var project = PricedProject();
        var draft = Progress(project, 224_000_000, BillingLedger.Empty);
        Assert.Equal(0, draft.AdvanceRecoveryMinor);

        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);
        var ledger = After(advance);
        Assert.True(draft.IsStale(ledger, Price, Terms));
        Assert.Equal(ClientInvoice.StaleDraftMessage,
            Assert.Throws<DomainRuleException>(() => BillingChain.Issue(draft, project, advance, ledger, Terms, 2, TestData.UserId, TestData.Now)).Message);
        Assert.Equal(ClientInvoiceStatus.Draft, draft.Status);
        Assert.Null(advance.NextInvoiceId);

        draft.UpdateDraft(project, BillDate, "Progress billing", 224_000_000, ledger, Terms, 1_200, TestData.Today, TestData.Now);
        Assert.False(draft.IsStale(ledger, Price, Terms));
        Assert.Equal((33_600_000L, 154_000_000L), (draft.AdvanceRecoveryMinor, draft.NetReceivableMinor));
        Issue(draft, project, advance, ledger, 2);
        Assert.Equal("BILL-0002", draft.Number);
    }

    [Fact]
    public void The_down_payment_comes_before_the_first_progress_bill_needs_a_price_and_a_rate_and_exists_once()
    {
        var project = PricedProject();
        Assert.Equal("Bill the down payment before the first progress bill.", ClientInvoice.AdvanceAfterProgressMessage);
        Assert.Equal(ClientInvoice.AdvanceAfterProgressMessage,
            Assert.Throws<DomainRuleException>(() => Advance(project, new BillingLedger(224_000_000, 22_400_000, 0))).Message);
        Assert.Equal(ClientInvoice.AdvanceExistsMessage, Assert.Throws<DomainRuleException>(() => Advance(project, anotherExists: true)).Message);
        Assert.Equal(ClientInvoice.NoAdvanceTermsMessage,
            Assert.Throws<DomainRuleException>(() => Advance(project, terms: Terms with { AdvanceBps = 0 })).Message);
        Assert.Equal(ClientInvoice.NoContractPriceMessage, Assert.Throws<DomainRuleException>(() => Advance(PricedProject(null))).Message);

        var invalid = Assert.Throws<ValidationFailedException>(() => Advance(project, date: new DateOnly(2026, 11, 3), description: " "));
        Assert.Equal(["billingDate", "description"], invalid.Errors.Keys);

        // A draft made earlier can't be issued once a progress bill is issued.
        var advance = Advance(project);
        var progress = Issue(Progress(project, 224_000_000, BillingLedger.Empty), project, null, BillingLedger.Empty, 1);
        Assert.Equal(ClientInvoice.AdvanceAfterProgressMessage,
            Assert.Throws<DomainRuleException>(() => BillingChain.Issue(advance, project, progress, After(progress), Terms, 2, TestData.UserId, TestData.Now)).Message);
        Assert.Equal(ClientInvoiceStatus.Draft, advance.Status);

        var archived = PricedProject();
        archived.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal("This project is archived. Restore it before making changes.", Assert.Throws<DomainRuleException>(() => Advance(archived)).Message);
    }

    [Fact]
    public void A_down_payment_bill_is_voided_only_while_it_is_the_latest_bill_and_nothing_is_paid_on_it()
    {
        var project = PricedProject();
        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);
        var payment = advance.RecordPayment(Guid.NewGuid(), project, 50_000_000, BillDate, ClientPaymentMethod.Cheque, null, null, TestData.UserId, null,
            TestData.Today, TestData.Now);
        Assert.Equal(118_000_000, advance.OutstandingMinor);
        Assert.Equal("This bill has payments recorded against it. Void those payments first, then void the bill.",
            Assert.Throws<DomainRuleException>(() => BillingChain.Void(advance, project, null, null, "Wrong", TestData.UserId, TestData.Now)).Message);
        advance.VoidPayment(payment.Id, project, "Cheque bounced", TestData.UserId, TestData.Now);

        var first = Issue(Progress(project, 224_000_000, After(advance)), project, advance, After(advance), 2);
        Assert.Equal("Only the latest bill of a project can be voided. Void BILL-0002 first.",
            Assert.Throws<DomainRuleException>(() => BillingChain.Void(advance, project, null, "BILL-0002", "Wrong", TestData.UserId, TestData.Now)).Message);

        BillingChain.Void(first, project, advance, null, "Wrong", TestData.UserId, TestData.Now);
        BillingChain.Void(advance, project, null, null, "Client paid no down payment", TestData.UserId, TestData.Now);
        Assert.Equal(ClientInvoiceStatus.Void, advance.Status);
        Assert.Equal(0, Progress(project, 224_000_000, After()).AdvanceRecoveryMinor);
    }

    [Fact]
    public void A_bill_whose_deductions_are_more_than_it_is_refused()
    {
        // ₱10,000.00 with a 50% down payment, then the price is cut to ₱1,100.00: the bill that reaches it would recover 4,500.00 from a
        // 100.00 bill.
        var terms = new BillingTermsSnapshot(3_000, 1_000, 30, 5_000, 1_200);
        var project = PricedProject(1_000_000);
        var advance = Issue(Advance(project, terms: terms), project, null, BillingLedger.Empty, 1, terms);
        var first = Issue(Progress(project, 100_000, After(advance), terms), project, advance, After(advance), 2, terms);
        // 100,000 - 30,000 - 8,929 - 10,714 - 50,000 = 357 left for the client to pay.
        Assert.Equal((100_000L, 30_000L, 8_929L, 10_714L, 50_000L, 357L), Figures(first));

        project.UpdateDetails(project.Name, null, null, 110_000, project.StartDate, null, TestData.Now);
        Assert.Equal(BillingCalculator.DeductionsExceedBillMessage,
            Assert.Throws<DomainRuleException>(() => Progress(project, 110_000, After(advance, first), terms)).Message);
    }

    [Fact]
    public void A_price_change_makes_a_progress_draft_stale_and_the_bill_that_reaches_the_new_price_recovers_everything_left()
    {
        // ₱1,500.00 down payment (15% of ₱10,000.00); half the work recovers 750.00.
        var project = PricedProject(1_000_000);
        var advance = Issue(Advance(project), project, null, BillingLedger.Empty, 1);
        var half = Issue(Progress(project, 500_000, After(advance)), project, advance, After(advance), 2);
        var ledger = After(advance, half);
        var draft = Progress(project, 900_000, ledger);
        Assert.Equal(60_000, draft.AdvanceRecoveryMinor);

        // The price is cut to ₱9,000.00: the draft now reaches the price and must recover all 750.00 left.
        project.UpdateDetails(project.Name, null, null, 900_000, project.StartDate, null, TestData.Now);
        Assert.True(draft.IsStale(ledger, 900_000, Terms));
        Assert.Equal(ClientInvoice.StaleDraftMessage, Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, ledger, Terms)).Message);

        draft.UpdateDraft(project, BillDate, "Final billing", 900_000, ledger, Terms, 1_200, TestData.Today, TestData.Now);
        Assert.Equal(75_000, draft.AdvanceRecoveryMinor);
        Issue(draft, project, half, ledger, 3);
        Assert.Equal(0, After(advance, half, draft).AdvanceToRecoverMinor);
    }

    [Fact]
    public void A_contract_price_cut_by_a_change_order_still_bills_to_100_percent_with_the_down_payment_recovered_exactly()
    {
        // ₱10,000,000.00 contract, 15% down payment (1,500,000.00), 10% retention, 2% withholding, no VAT withheld.
        var terms = new BillingTermsSnapshot(1_000, 200, 30, 1_500, 0);
        var project = PricedProject(1_000_000_000);
        var advance = Issue(Advance(project, terms: terms), project, null, BillingLedger.Empty, 1, terms);
        Assert.Equal(150_000_000, advance.GrossMinor);
        // 40% of the work: 40% of the down payment, 600,000.00.
        var first = Issue(Progress(project, 400_000_000, After(advance), terms), project, advance, After(advance), 2, terms);
        Assert.Equal(60_000_000, first.AdvanceRecoveryMinor);

        // A change order cuts the price to ₱9,000,000.00. Billing to 8,800,000.00 recovers round(1,500,000.00 x 8.8 / 9) = 1,466,666.67 to
        // date, so this bill takes 866,666.67.
        project.UpdateDetails(project.Name, null, null, 900_000_000, project.StartDate, null, TestData.Now);
        var second = Issue(Progress(project, 880_000_000, After(advance, first), terms), project, first, After(advance, first), 3, terms);
        Assert.Equal(86_666_667, second.AdvanceRecoveryMinor);

        // The final 200,000.00 recovers the 33,333.33 left (the old rule would have asked 180,000.00 of it and refused the bill): net
        // 178,571.43, VAT 21,428.57, retention 20,000.00, withholding 3,571.43, and the client pays 143,095.24.
        var last = Issue(Progress(project, 900_000_000, After(advance, first, second), terms), project, second, After(advance, first, second), 4, terms);
        Assert.Equal((20_000_000L, 2_000_000L, 357_143L, 0L, 3_333_333L, 14_309_524L), Figures(last));

        var ledger = After(advance, first, second, last);
        Assert.Equal((900_000_000L, 150_000_000L, 0L), (ledger.BilledToDateMinor, ledger.AdvanceRecoveredMinor, ledger.AdvanceToRecoverMinor));
        Assert.Equal(ClientInvoiceStatus.Issued, last.Status);
    }

    [Fact]
    public void A_down_payment_draft_is_stale_when_the_terms_or_the_price_change_what_it_would_ask_for_and_is_refused_until_saved_again()
    {
        // A private client at 20%: the draft asks for 2,240,000.00 of ₱11,200,000.00.
        var privateTerms = Terms with { AdvanceBps = 2_000 };
        var project = PricedProject();
        var draft = Advance(project, terms: privateTerms);
        Assert.Equal(224_000_000, draft.GrossMinor);
        Assert.False(draft.IsStale(BillingLedger.Empty, Price, privateTerms));

        // The terms change to a government client at 15%: the draft still holds 20%, so it is stale and can't be issued.
        var governmentTerms = ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 1_000, 200, 30, TestData.Now, 1_500, 500, true).Snapshot;
        Assert.True(draft.IsStale(BillingLedger.Empty, Price, governmentTerms));
        Assert.Equal(ClientInvoice.StaleDraftMessage, Assert.Throws<DomainRuleException>(() =>
            BillingChain.Issue(draft, project, null, BillingLedger.Empty, governmentTerms, 1, TestData.UserId, TestData.Now)).Message);
        Assert.Equal((ClientInvoiceStatus.Draft, (string?)null), (draft.Status, draft.Number));

        // Saved again it asks for 15%, 1,680,000.00, and issues.
        draft.UpdateDraft(project, BillDate, "Down payment", 0, BillingLedger.Empty, governmentTerms, 1_200, TestData.Today, TestData.Now);
        Assert.Equal((168_000_000L, 1_500), (draft.GrossMinor, draft.AdvanceBps));
        Assert.False(draft.IsStale(BillingLedger.Empty, Price, governmentTerms));

        // A change order that moves the price makes it stale again; so does a price removed.
        project.UpdateDetails(project.Name, null, null, 1_000_000_000, project.StartDate, null, TestData.Now);
        Assert.True(draft.IsStale(BillingLedger.Empty, 1_000_000_000, governmentTerms));
        Assert.Equal(ClientInvoice.StaleDraftMessage,
            Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, BillingLedger.Empty, governmentTerms)).Message);
        Assert.True(draft.IsStale(BillingLedger.Empty, null, governmentTerms));

        // Terms that no longer take a down payment: stale, refused at issue, and saving it again is refused too, so it is discarded.
        var none = governmentTerms with { AdvanceBps = 0 };
        draft.UpdateDraft(project, BillDate, "Down payment", 0, BillingLedger.Empty, governmentTerms, 1_200, TestData.Today, TestData.Now);
        Assert.Equal(150_000_000, draft.GrossMinor);
        Assert.True(draft.IsStale(BillingLedger.Empty, 1_000_000_000, none));
        Assert.Equal(ClientInvoice.StaleDraftMessage, Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, BillingLedger.Empty, none)).Message);
        Assert.Equal(ClientInvoice.NoAdvanceTermsMessage, Assert.Throws<DomainRuleException>(() =>
            draft.UpdateDraft(project, BillDate, "Down payment", 0, BillingLedger.Empty, none, 1_200, TestData.Today, TestData.Now)).Message);

        // A stale draft is never stale once issued or void.
        draft.Discard(project, "No down payment any more", TestData.UserId, TestData.Now);
        Assert.False(draft.IsStale(BillingLedger.Empty, 1_000_000_000, none));
    }

    [Fact]
    public void A_government_clients_terms_never_give_a_down_payment_draft_more_than_15_percent()
    {
        var project = PricedProject();
        var terms = ProjectBillingTerms.Create(Guid.NewGuid(), TestData.Tenant, project, 1_000, 200, 30, TestData.Now, 1_500, 0, true);
        Assert.Equal(1_500, terms.Snapshot.AdvanceBps);
        Assert.Equal(168_000_000, Advance(project, terms: terms.Snapshot).GrossMinor);
    }

    [Fact]
    public void A_down_payment_draft_left_over_after_a_progress_bill_can_only_be_discarded()
    {
        var project = PricedProject();
        var advance = Advance(project);
        Assert.False(advance.CanOnlyBeDiscarded(BillingLedger.Empty));

        var progress = Issue(Progress(project, 224_000_000, BillingLedger.Empty), project, null, BillingLedger.Empty, 1);
        var ledger = After(progress);
        Assert.True(advance.CanOnlyBeDiscarded(ledger));
        Assert.False(advance.IsStale(ledger, Price, Terms));
        Assert.False(progress.CanOnlyBeDiscarded(ledger));
        Assert.Equal(ClientInvoice.AdvanceAfterProgressMessage, Assert.Throws<DomainRuleException>(() => advance.EnsureCanIssue(project, ledger, Terms)).Message);
        Assert.Equal(ClientInvoice.AdvanceAfterProgressMessage, Assert.Throws<DomainRuleException>(() =>
            advance.UpdateDraft(project, BillDate, "Down payment", 0, ledger, Terms, 1_200, TestData.Today, TestData.Now)).Message);

        advance.Discard(project, "Progress billed first", TestData.UserId, TestData.Now);
        Assert.False(advance.CanOnlyBeDiscarded(ledger));
    }
}

public sealed class GovernmentContractTests
{
    [Fact]
    public void Added_variations_are_flagged_only_once_they_pass_10_percent_of_the_original_price()
    {
        // 10% of ₱10,000.05 = 1,000.005 -> 1,000.01.
        Assert.Equal(new VariationLimit(100_001, 100_001, false), GovernmentContract.Variations(1_000_005, 60_000, 40_001));
        Assert.Equal(new VariationLimit(100_001, 100_002, true), GovernmentContract.Variations(1_000_005, 100_001, 1));
        Assert.Equal(new VariationLimit(0, 0, false), GovernmentContract.Variations(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GovernmentContract.Variations(1_000_000, -1, 0));
        Assert.Equal((1_500, 1_000), (GovernmentContract.MaximumAdvanceBps, GovernmentContract.VariationLimitBps));
    }
}
