using Cms.Domain.Common;
using Cms.Domain.Features.Billing;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.UnitTests.Features;

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
        BillingChain.Issue(draft, project, head, ledger, number, TestData.UserId, TestData.Now);
        return draft;
    }

    private static BillingLedger After(params ClientInvoice[] issued) => new(
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.GrossMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.RetentionMinor),
        issued.Where(bill => bill.Kind == ClientInvoiceKind.RetentionRelease).Sum(bill => bill.GrossMinor));

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

        Assert.True(early.IsStale(ledger));
        var stale = Assert.Throws<DomainRuleException>(() => BillingChain.Issue(early, project, first, ledger, 2, TestData.UserId, TestData.Now));
        Assert.StartsWith("Another bill was issued or voided after this draft was prepared", stale.Message);
        Assert.Null(first.NextInvoiceId);
        Assert.Equal(ClientInvoiceStatus.Draft, early.Status);

        // Saved again, it bills only the difference and can be issued.
        early.UpdateDraft(project, BillDate, "Progress billing no. 2", 300_000_000, ledger, Terms, 1_200, TestData.Today, TestData.Now);
        Assert.False(early.IsStale(ledger));
        Assert.Equal(76_000_000, early.GrossMinor);
        Issue(early, project, first, ledger, 2);
        Assert.Equal("BILL-0002", early.Number);
    }

    [Fact]
    public void Issuing_is_refused_for_an_issued_or_void_bill_and_when_the_price_dropped_below_the_work_billed()
    {
        var project = PricedProject();
        var bill = Issue(Progress(project, 224_000_000), project, null, BillingLedger.Empty, 1);
        Assert.Equal("This bill was already issued.", Assert.Throws<DomainRuleException>(() => bill.EnsureCanIssue(project, BillingLedger.Empty)).Message);

        var draft = Progress(project, 300_000_000, After(bill));
        project.UpdateDetails(project.Name, null, null, 250_000_000, project.StartDate, null, TestData.Now);
        Assert.StartsWith("The contract price is now less than the work completed to date",
            Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, After(bill))).Message);

        draft.Discard(project, "Wrong amount", TestData.UserId, TestData.Now);
        Assert.Equal("A void bill can't be issued.", Assert.Throws<DomainRuleException>(() => draft.EnsureCanIssue(project, After(bill))).Message);
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
        Assert.True(release.IsStale(lessHeld));
        Assert.StartsWith("Less retention is held now than when this release was prepared",
            Assert.Throws<DomainRuleException>(() => release.EnsureCanIssue(project, lessHeld)).Message);
        release.EnsureCanIssue(project, held);
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
        Assert.Equal(new BillingTermsSnapshot(1_000, 0, 30), BillingTermsSnapshot.Defaults);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() => terms.Change(project, 500, 0, 30, TestData.Now));
    }

    private static ClientPayment Payment(ClientInvoice bill, Project project, long amount) =>
        bill.RecordPayment(Guid.NewGuid(), project, amount, BillDate, ClientPaymentMethod.BankTransfer, null, null, TestData.UserId, null, TestData.Today,
            TestData.Now);

    private static ClientInvoice Release(Project project, long amount, BillingLedger ledger) =>
        ClientInvoice.DraftRetentionRelease(Guid.NewGuid(), TestData.Tenant, project, BillDate, "Release of retention", amount, ledger, Terms, TestData.UserId,
            null, TestData.Today, TestData.Now);
}
