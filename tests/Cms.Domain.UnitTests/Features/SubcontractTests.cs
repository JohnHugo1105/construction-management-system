using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Subcontracts;
using Cms.Domain.Features.Suppliers;

namespace Cms.Domain.UnitTests.Features;

/// <summary>The subcontract rules, with exact centavo examples worked out by hand (see docs/rules-drafts/subcontracts.md).</summary>
public sealed class SubcontractTests
{
    private static readonly DateOnly Today = TestData.Today;
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly Guid User = TestData.UserId;

    private static Supplier Firm(SupplierKind kind = SupplierKind.Subcontractor) =>
        Supplier.Create(Guid.NewGuid(), TestData.Tenant, "Dela Cruz Electrical", kind, null, null, null, null, null, null, Now);

    /// <summary>An awarded subcontract: 1,000,000.00 before VAT at 12%, 10% retention, 2% withholding.</summary>
    private static (Subcontract Subcontract, Project Project, Supplier Firm) Awarded(VatTreatment vat = VatTreatment.Exclusive, long sumMinor = 1_000_000_00,
        int retentionBps = 1_000, int withholdingBps = 200)
    {
        var project = TestData.NewProject();
        var firm = Firm();
        var subcontract = Subcontract.Create(Guid.NewGuid(), TestData.Tenant, firm, project, null, "Electrical works", null, sumMinor, vat, 1_200,
            retentionBps, withholdingBps, User, null, Now);
        subcontract.AssignNumber("SC-0001");
        subcontract.Award(firm, project, User, Now);
        return (subcontract, project, firm);
    }

    private static SubcontractClaim Draft(Subcontract subcontract, long cumulativeMinor, DateOnly? date = null) =>
        subcontract.DraftClaim(Guid.NewGuid(), date ?? Today.AddDays(-5), (date ?? Today.AddDays(-5)).AddDays(-1), cumulativeMinor, null, User, null,
            Today, Now);

    private static SubcontractClaim Certify(Subcontract subcontract, long cumulativeMinor, int number)
    {
        var claim = Draft(subcontract, cumulativeMinor);
        return subcontract.CertifyClaim(claim.Id, number, Guid.NewGuid(), User, Now);
    }

    // ---- The claim calculation ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_claim_before_VAT_adds_VAT_then_holds_retention_on_the_gross_and_withholds_tax_on_the_net()
    {
        // 250,000.00 of work before VAT: VAT 30,000.00, gross 280,000.00, retention 10% of gross 28,000.00, withholding 2% of net 5,000.00.
        var breakdown = ClaimBreakdown.Calculate(250_000_00, 0, VatTreatment.Exclusive, 1_200, 1_000, 200);

        Assert.Equal(new ClaimBreakdown(250_000_00, 0, 250_000_00, 250_000_00, 30_000_00, 280_000_00, 28_000_00, 5_000_00, 247_000_00), breakdown);
    }

    [Fact]
    public void A_claim_that_includes_VAT_splits_the_net_out_of_it_and_rounds_each_figure_once()
    {
        // 250,000.00 with VAT: net = round(25,000,000 x 10,000 / 11,200) = 22,321,429 (22,321,428.57); withholding = round(22,321,429 x 2%)
        // = 446,429 (446,428.58); net payable = 25,000,000 - 2,500,000 - 446,429.
        var breakdown = ClaimBreakdown.Calculate(250_000_00, 0, VatTreatment.Inclusive, 1_200, 1_000, 200);

        Assert.Equal(new ClaimBreakdown(250_000_00, 0, 250_000_00, 223_214_29, 26_785_71, 250_000_00, 25_000_00, 4_464_29, 220_535_71), breakdown);
        Assert.Equal(breakdown.GrossMinor, breakdown.NetMinor + breakdown.VatMinor);
    }

    [Fact]
    public void A_claim_without_VAT_has_the_same_net_and_gross()
    {
        var breakdown = ClaimBreakdown.Calculate(250_000_00, 0, VatTreatment.Exempt, 1_200, 1_000, 200);

        Assert.Equal(new ClaimBreakdown(250_000_00, 0, 250_000_00, 250_000_00, 0, 250_000_00, 25_000_00, 5_000_00, 220_000_00), breakdown);
    }

    [Fact]
    public void Retention_and_withholding_round_half_away_from_zero()
    {
        // 123.45 at 10% retention is 12.345, which is 12.35.
        Assert.Equal(12_35, ClaimBreakdown.Calculate(123_45, 0, VatTreatment.Exempt, 0, 1_000, 0).RetentionMinor);

        // 10,000.01 with VAT at 12%: net 8,928.58; retention 5% of 10,000.01 = 500.0005, so 500.00; withholding 1.5% of 8,928.58 = 133.9287, so 133.93.
        var odd = ClaimBreakdown.Calculate(1_000_001, 0, VatTreatment.Inclusive, 1_200, 500, 150);
        Assert.Equal(new ClaimBreakdown(1_000_001, 0, 1_000_001, 892_858, 107_143, 1_000_001, 50_000, 13_393, 936_608), odd);
    }

    [Fact]
    public void Each_claim_is_the_work_added_since_the_last_certified_claim()
    {
        var (subcontract, _, _) = Awarded();
        Certify(subcontract, 250_000_00, 1);

        var second = Certify(subcontract, 600_000_00, 2);

        // 600,000.00 - 250,000.00 = 350,000.00: VAT 42,000.00, gross 392,000.00, retention 39,200.00, withholding 7,000.00.
        Assert.Equal(new ClaimBreakdown(600_000_00, 250_000_00, 350_000_00, 350_000_00, 42_000_00, 392_000_00, 39_200_00, 7_000_00, 345_800_00),
            second.Breakdown);
        Assert.Equal(600_000_00, subcontract.CertifiedWorkMinor);
        Assert.Equal(280_000_00 + 392_000_00, subcontract.CertifiedGrossMinor);
        Assert.Equal(28_000_00 + 39_200_00, subcontract.RetentionHeldMinor);
        Assert.Equal(5_000_00 + 7_000_00, subcontract.WithholdingMinor);
        Assert.Equal(247_000_00 + 345_800_00, subcontract.NetPayableMinor);
        Assert.Equal(6_000, Subcontract.PercentCertifiedBps(subcontract.CertifiedWorkMinor, subcontract.RevisedSumMinor));
    }

    // ---- The subcontract ----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_subcontract_keeps_the_VAT_rate_and_works_out_its_revised_sum()
    {
        var (subcontract, _, _) = Awarded();

        Assert.Equal(SubcontractStatus.Active, subcontract.Status);
        Assert.Equal(1_200, subcontract.VatRateBps);
        Assert.Equal((1_000_000_00L, 1_000_000_00L, 120_000_00L, 1_120_000_00L),
            (subcontract.RevisedSumMinor, subcontract.RevisedNetMinor, subcontract.RevisedVatMinor, subcontract.RevisedGrossMinor));
    }

    [Fact]
    public void Every_problem_with_the_terms_is_reported_at_once()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Subcontract.Create(Guid.NewGuid(), TestData.Tenant, Firm(), TestData.NewProject(), null,
            " ", new string('x', 501), 0, (VatTreatment)(-1), 1_200, 3_001, 1_001, User, null, Now));

        Assert.Equal(["contractSumMinor", "notes", "retentionBps", "title", "vatTreatment", "withholdingBps"], ex.Errors.Keys.Order());
    }

    [Fact]
    public void A_firm_that_is_not_a_subcontractor_or_an_archived_project_is_refused()
    {
        var supplierOnly = Assert.Throws<DomainRuleException>(() => Subcontract.Create(Guid.NewGuid(), TestData.Tenant, Firm(SupplierKind.Supplier),
            TestData.NewProject(), null, "Works", null, 100_00, VatTreatment.Exempt, 0, 1_000, 0, User, null, Now));
        Assert.Contains("isn't set up as a subcontractor", supplierOnly.Message);

        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, Now);
        var archived = Assert.Throws<DomainRuleException>(() => Subcontract.Create(Guid.NewGuid(), TestData.Tenant, Firm(), project, null, "Works", null,
            100_00, VatTreatment.Exempt, 0, 1_000, 0, User, null, Now));
        Assert.Contains("archived", archived.Message);
    }

    [Fact]
    public void Only_a_draft_is_edited_and_awarded()
    {
        var (subcontract, project, firm) = Awarded();

        Assert.Throws<DomainRuleException>(() => subcontract.UpdateDraft(firm, project, null, "Works", null, 5_00, VatTreatment.Exempt, 1_000, 0, Now));
        Assert.Throws<DomainRuleException>(() => subcontract.Award(firm, project, User, Now));
    }

    [Fact]
    public void A_variation_revises_the_sum_but_never_below_the_certified_work_or_to_zero()
    {
        var (subcontract, _, _) = Awarded();
        Certify(subcontract, 400_000_00, 1);

        subcontract.AddVariation(Guid.NewGuid(), "Extra circuits", 150_000_00, Today, User, Today, Now);
        Assert.Equal(1_150_000_00, subcontract.RevisedSumMinor);
        Assert.Equal(1_288_000_00, subcontract.RevisedGrossMinor);

        var omission = subcontract.AddVariation(Guid.NewGuid(), "Omit garden lights", -750_000_00, Today, User, Today, Now);
        Assert.Equal(2, omission.Number);
        Assert.Equal(400_000_00, subcontract.RevisedSumMinor);

        var below = Assert.Throws<ValidationFailedException>(() =>
            subcontract.AddVariation(Guid.NewGuid(), "Omit more", -1, Today, User, Today, Now));
        Assert.Equal("The revised contract sum can't be less than the ₱400,000.00 of work already certified.", below.Errors["amountMinor"][0]);

        var empty = Assert.Throws<ValidationFailedException>(() => subcontract.AddVariation(Guid.NewGuid(), "", 0, Today.AddDays(1), User, Today, Now));
        Assert.Equal(["amountMinor", "description", "variationDate"], empty.Errors.Keys.Order());
    }

    [Fact]
    public void Completing_needs_no_open_draft_and_cancelling_needs_no_certified_work()
    {
        var (subcontract, _, _) = Awarded();
        Certify(subcontract, 100_000_00, 1);
        Draft(subcontract, 200_000_00);

        Assert.Contains("draft claim is still open", Assert.Throws<DomainRuleException>(() => subcontract.Complete(User, Now)).Message);
        Assert.Contains("can't be cancelled", Assert.Throws<DomainRuleException>(() => subcontract.Cancel("No longer needed", User, Now)).Message);

        var (fresh, _, _) = Awarded();
        var draft = Draft(fresh, 50_000_00);
        fresh.Cancel("Client cancelled the scope", User, Now);
        Assert.Equal(SubcontractStatus.Cancelled, fresh.Status);
        Assert.Equal(SubcontractClaimStatus.Voided, draft.Status);
        Assert.Equal("Client cancelled the scope", draft.VoidReason);
    }

    // ---- Claims ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_claim_must_add_work_and_stay_within_the_revised_sum()
    {
        var (subcontract, _, _) = Awarded();
        Certify(subcontract, 250_000_00, 1);

        var notMore = Assert.Throws<ValidationFailedException>(() => Draft(subcontract, 250_000_00));
        Assert.Equal("Enter the value of all work done to date. It must be more than the ₱250,000.00 already certified.",
            notMore.Errors["cumulativeWorkMinor"][0]);

        var over = Assert.Throws<ValidationFailedException>(() => Draft(subcontract, 1_000_000_01));
        Assert.Equal("Work done to date can't be more than the revised contract sum of ₱1,000,000.00. Record a variation first if the work grew.",
            over.Errors["cumulativeWorkMinor"][0]);

        var dates = Assert.Throws<ValidationFailedException>(() =>
            subcontract.DraftClaim(Guid.NewGuid(), Today.AddDays(1), Today.AddDays(2), 300_000_00, null, User, null, Today, Now));
        Assert.Equal(["claimDate", "periodEnd"], dates.Errors.Keys.Order());
    }

    [Fact]
    public void Only_one_draft_claim_is_open_and_claims_need_an_awarded_subcontract()
    {
        var (subcontract, _, _) = Awarded();
        Draft(subcontract, 100_00);
        Assert.Contains("already open", Assert.Throws<DomainRuleException>(() => Draft(subcontract, 200_00)).Message);

        var draft = Subcontract.Create(Guid.NewGuid(), TestData.Tenant, Firm(), TestData.NewProject(), null, "Works", null, 100_000_00,
            VatTreatment.Exempt, 0, 1_000, 0, User, null, Now);
        Assert.Equal("Award the subcontract before recording claims.", Assert.Throws<DomainRuleException>(() => Draft(draft, 100_00)).Message);
    }

    [Fact]
    public void A_draft_worked_out_on_a_chain_that_has_moved_is_refused_until_it_is_saved_again()
    {
        var (subcontract, _, _) = Awarded();
        var first = Certify(subcontract, 250_000_00, 1);
        var second = Draft(subcontract, 600_000_00);
        subcontract.VoidClaim(first.Id, "Measured wrongly", User, Now);

        var stale = Assert.Throws<DomainRuleException>(() => subcontract.CertifyClaim(second.Id, 2, Guid.NewGuid(), User, Now));
        Assert.Contains("worked out on ₱250,000.00 of work certified before it, but the certified work is now ₱0.00", stale.Message);

        subcontract.UpdateDraftClaim(second.Id, Today.AddDays(-5), Today.AddDays(-6), 600_000_00, null, Today, Now);
        var certified = subcontract.CertifyClaim(second.Id, 2, Guid.NewGuid(), User, Now);
        Assert.Equal(600_000_00, certified.ThisClaimBaseMinor);
        Assert.Equal(600_000_00, subcontract.CertifiedWorkMinor);
    }

    [Fact]
    public void A_draft_over_a_sum_reduced_since_is_refused_at_certification()
    {
        var (subcontract, _, _) = Awarded();
        var claim = Draft(subcontract, 900_000_00);
        subcontract.AddVariation(Guid.NewGuid(), "Omit lights", -200_000_00, Today, User, Today, Now);

        var ex = Assert.Throws<DomainRuleException>(() => subcontract.CertifyClaim(claim.Id, 1, Guid.NewGuid(), User, Now));
        Assert.Contains("more than the revised contract sum (₱800,000.00)", ex.Message);
    }

    [Fact]
    public void Only_the_latest_certified_claim_without_payments_is_voided()
    {
        var (subcontract, _, _) = Awarded();
        var first = Certify(subcontract, 250_000_00, 1);
        var second = Certify(subcontract, 600_000_00, 2);

        Assert.Equal("Only the latest certified claim can be voided. Void claim 2 first.",
            Assert.Throws<DomainRuleException>(() => subcontract.VoidClaim(first.Id, "Wrong", User, Now)).Message);

        var payment = subcontract.RecordClaimPayment(Guid.NewGuid(), second.Id, 100_000_00, Today, SubcontractPaymentMethod.Cheque, "CHK-1", null, User,
            null, Today, Now);
        Assert.Contains("Payments are recorded", Assert.Throws<DomainRuleException>(() => subcontract.VoidClaim(second.Id, "Wrong", User, Now)).Message);

        subcontract.VoidPayment(payment.Id, "Cheque bounced", User, Now);
        subcontract.VoidClaim(second.Id, "Wrong", User, Now);
        Assert.Equal(SubcontractClaimStatus.Voided, second.Status);
        Assert.Equal(250_000_00, subcontract.CertifiedWorkMinor);
        Assert.Equal(247_000_00, subcontract.NetPayableMinor);
        Assert.Equal(0, subcontract.PaidOnClaimsMinor);
    }

    // ---- Payments -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Payments_never_exceed_what_is_owed_on_the_claim()
    {
        var (subcontract, _, _) = Awarded();
        var claim = Certify(subcontract, 250_000_00, 1);

        subcontract.RecordClaimPayment(Guid.NewGuid(), claim.Id, 200_000_00, Today, SubcontractPaymentMethod.BankTransfer, null, null, User, null, Today, Now);
        Assert.Equal(47_000_00, subcontract.ClaimOutstandingMinor(claim));

        var over = Assert.Throws<ValidationFailedException>(() => subcontract.RecordClaimPayment(Guid.NewGuid(), claim.Id, 47_000_01, Today,
            SubcontractPaymentMethod.Cash, null, null, User, null, Today, Now));
        Assert.Equal("That is more than the ₱47,000.00 still owed on this claim.", over.Errors["amountMinor"][0]);

        subcontract.RecordClaimPayment(Guid.NewGuid(), claim.Id, 47_000_00, Today, SubcontractPaymentMethod.Cash, null, null, User, null, Today, Now);
        Assert.Equal(0, subcontract.OutstandingMinor);
        Assert.Contains("fully paid", Assert.Throws<DomainRuleException>(() => subcontract.RecordClaimPayment(Guid.NewGuid(), claim.Id, 1, Today,
            SubcontractPaymentMethod.Cash, null, null, User, null, Today, Now)).Message);

        var second = Certify(subcontract, 300_000_00, 2);
        var bad = Assert.Throws<ValidationFailedException>(() => subcontract.RecordClaimPayment(Guid.NewGuid(), second.Id, 0, Today.AddDays(1),
            (SubcontractPaymentMethod)9, null, null, User, null, Today, Now));
        Assert.Equal(["amountMinor", "method", "paidOn"], bad.Errors.Keys.Order());
    }

    [Fact]
    public void Retention_is_released_once_completed_and_never_more_than_is_held()
    {
        var (subcontract, _, _) = Awarded();
        var claim = Certify(subcontract, 250_000_00, 1);
        Assert.Contains("Complete it first", Assert.Throws<DomainRuleException>(() => subcontract.RecordRetentionRelease(Guid.NewGuid(), 1_00, Today,
            SubcontractPaymentMethod.BankTransfer, null, null, User, null, Today, Now)).Message);

        subcontract.Complete(User, Now);
        subcontract.RecordRetentionRelease(Guid.NewGuid(), 20_000_00, Today, SubcontractPaymentMethod.BankTransfer, null, null, User, null, Today, Now);
        Assert.Equal((28_000_00L, 20_000_00L, 8_000_00L), (subcontract.RetentionHeldMinor, subcontract.RetentionReleasedMinor, subcontract.RetentionBalanceMinor));

        var over = Assert.Throws<ValidationFailedException>(() => subcontract.RecordRetentionRelease(Guid.NewGuid(), 8_000_01, Today,
            SubcontractPaymentMethod.BankTransfer, null, null, User, null, Today, Now));
        Assert.Equal("That is more than the ₱8,000.00 retention still held.", over.Errors["amountMinor"][0]);

        // Voiding the claim would leave more released than held.
        Assert.Contains("retention payment first", Assert.Throws<DomainRuleException>(() => subcontract.VoidClaim(claim.Id, "Wrong", User, Now)).Message);
    }

    [Fact]
    public void Claims_are_closed_after_completion_and_cancellation()
    {
        var (subcontract, _, _) = Awarded();
        subcontract.Complete(User, Now);

        Assert.Equal("This subcontract is completed, so it is closed to recording claims.",
            Assert.Throws<DomainRuleException>(() => Draft(subcontract, 100_00)).Message);
        Assert.Throws<DomainRuleException>(() => subcontract.Cancel("Too late", User, Now));
        Assert.Throws<DomainRuleException>(() => subcontract.AddVariation(Guid.NewGuid(), "More", 1_00, Today, User, Today, Now));
    }
}
