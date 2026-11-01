using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.UnitTests.Features;

public sealed class BudgetLineTests
{
    private static BudgetLine NewLine(Project project, decimal quantity = 10m, long unitCost = 50_000, VatTreatment vat = VatTreatment.Inclusive) =>
        BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, project, null, 3, null, null, "3300", "Ready-mix concrete 4000 psi", ItemType.Material,
            quantity, "m³", unitCost, vat, 1_200, new DateOnly(2026, 2, 1), null, TestData.UserId, null, TestData.Now);

    [Fact]
    public void Creating_a_line_computes_and_stores_the_money_once()
    {
        var line = NewLine(TestData.NewProject());

        Assert.Equal(500_000, line.GrossMinor);
        Assert.Equal(446_429, line.NetMinor);
        Assert.Equal(53_571, line.VatMinor);
        Assert.Equal("Concrete", line.MainScope);
        Assert.Equal("Cast-In-Place Concrete", line.SubScope);
        Assert.Equal("m³", line.Unit);
    }

    [Fact]
    public void A_line_keeps_the_vat_rate_it_was_entered_with()
    {
        var line = NewLine(TestData.NewProject());
        Assert.Equal(1_200, line.VatRateBps);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var project = TestData.NewProject();

        var ex = Assert.Throws<ValidationFailedException>(() => BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, project, null, null, null,
            null, null, "  ", ItemType.Material, 0m, " ", -1, VatTreatment.Inclusive, 1_200, new DateOnly(2026, 2, 1), null, TestData.UserId,
            null, TestData.Now));

        Assert.Contains("description", ex.Errors.Keys);
        Assert.Contains("quantity", ex.Errors.Keys);
        Assert.Contains("unit", ex.Errors.Keys);
        Assert.Contains("unitCostMinor", ex.Errors.Keys);
    }

    [Fact]
    public void An_archived_project_takes_no_new_lines()
    {
        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Throws<DomainRuleException>(() => NewLine(project));
    }

    [Fact]
    public void A_sub_project_from_another_project_is_rejected()
    {
        var project = TestData.NewProject();

        Assert.Throws<ValidationFailedException>(() => BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, project, Guid.NewGuid(), null, null,
            null, null, "Cement", ItemType.Material, 1m, "bag", 100, VatTreatment.Inclusive, 1_200, new DateOnly(2026, 2, 1), null,
            TestData.UserId, null, TestData.Now));
    }

    [Fact]
    public void Revising_recomputes_the_money_and_removing_needs_a_reason_and_is_final()
    {
        var project = TestData.NewProject();
        var line = NewLine(project);

        line.Revise(project, null, 3, null, null, "3300", "Ready-mix concrete 4000 psi", ItemType.Material, 20m, "m3", 50_000,
            VatTreatment.Inclusive, 1_200, new DateOnly(2026, 2, 1), null, TestData.Now);
        Assert.Equal(1_000_000, line.GrossMinor);
        Assert.Equal("m³", line.Unit);

        Assert.Throws<ValidationFailedException>(() => line.Remove(TestData.UserId, " ", TestData.Now));
        line.Remove(TestData.UserId, "Superseded by revised estimate", TestData.Now);
        Assert.True(line.IsRemoved);
        Assert.Throws<DomainRuleException>(() => line.Remove(TestData.UserId, "again", TestData.Now));
        Assert.Throws<DomainRuleException>(() => line.Revise(project, null, 3, null, null, "3300", "x", ItemType.Material, 1m, "pc", 1,
            VatTreatment.Exempt, 0, new DateOnly(2026, 2, 1), null, TestData.Now));
    }
}

public sealed class CostEntryTests
{
    private static CostEntry NewEntry(Project project, string? dealer = "Cebu Builders Depot", string? invoice = "SI-0001",
        DateOnly? date = null, decimal quantity = 100m, long unitCost = 25_000) =>
        CostEntry.RecordManual(Guid.NewGuid(), TestData.Tenant, project, null, 3, null, null, "3200", "12mm rebar", ItemType.Material, quantity,
            "pcs", unitCost, VatTreatment.Inclusive, 1_200, date ?? new DateOnly(2026, 10, 30), null, dealer, invoice, TestData.UserId, null,
            TestData.Today, TestData.Now);

    [Fact]
    public void A_manual_cost_stores_amounts_and_normalizes_the_invoice_for_duplicate_checks()
    {
        var entry = NewEntry(TestData.NewProject(), invoice: " si-0001 ");

        Assert.Equal(2_500_000, entry.GrossMinor);
        Assert.Equal(entry.GrossMinor, entry.NetMinor + entry.VatMinor);
        Assert.Equal("pc", entry.Unit);
        Assert.Equal("SI0001", entry.NormalizedInvoiceNumber);
        Assert.Equal("CEBU BUILDERS DEPOT", entry.NormalizedSourceDealer);
        Assert.Equal(CostOrigin.Manual, entry.Origin);
    }

    [Fact]
    public void Invoice_numbers_that_differ_only_by_punctuation_and_case_normalize_the_same() =>
        Assert.Equal(CostEntry.NormalizeInvoice("SI-0001"), CostEntry.NormalizeInvoice("si 0001"));

    [Fact]
    public void The_supplier_and_invoice_are_required_and_reported_together_with_line_problems()
    {
        var project = TestData.NewProject();

        var ex = Assert.Throws<ValidationFailedException>(() => NewEntry(project, dealer: "", invoice: "", quantity: 0m));

        Assert.Contains("sourceDealer", ex.Errors.Keys);
        Assert.Contains("invoiceNumber", ex.Errors.Keys);
        Assert.Contains("quantity", ex.Errors.Keys);
    }

    [Fact]
    public void An_invoice_made_only_of_symbols_is_rejected() =>
        Assert.Throws<ValidationFailedException>(() => NewEntry(TestData.NewProject(), invoice: "---"));

    [Fact]
    public void A_cost_can_not_be_dated_in_the_future() =>
        Assert.Contains("entryDate", Assert.Throws<ValidationFailedException>(() => NewEntry(TestData.NewProject(), date: TestData.Today.AddDays(1))).Errors.Keys);

    [Fact]
    public void Voiding_needs_a_reason_and_keeps_the_record()
    {
        var entry = NewEntry(TestData.NewProject());

        Assert.Throws<ValidationFailedException>(() => entry.Void(TestData.UserId, "", TestData.Now));
        entry.Void(TestData.UserId, "Recorded against the wrong project", TestData.Now);

        Assert.True(entry.IsVoided);
        Assert.Equal("Recorded against the wrong project", entry.VoidReason);
        Assert.Throws<DomainRuleException>(() => entry.Void(TestData.UserId, "again", TestData.Now));
    }

    [Fact]
    public void System_costs_have_no_invoice_and_can_not_be_edited_by_hand()
    {
        var project = TestData.NewProject();
        var entry = CostEntry.RecordSystem(Guid.NewGuid(), TestData.Tenant, project, null, CostOrigin.Payroll, Guid.NewGuid(), "Payroll 1-15 Nov",
            ItemType.Labor, 1m, "l.s", 105_000, VatTreatment.Exempt, 0, new DateOnly(2026, 11, 15), TestData.UserId, TestData.Now);

        Assert.Equal(105_000, entry.GrossMinor);
        Assert.Equal(0, entry.VatMinor);
        Assert.Null(entry.InvoiceNumber);
        Assert.Throws<DomainRuleException>(() => entry.Revise(project, null, null, null, null, null, "x", ItemType.Labor, 1m, "l.s", 1,
            VatTreatment.Exempt, 0, new DateOnly(2026, 11, 15), null, "x", "y", TestData.Today, TestData.Now));
    }

    [Fact]
    public void A_manual_entry_can_be_revised_and_the_amounts_follow()
    {
        var project = TestData.NewProject();
        var entry = NewEntry(project);

        entry.Revise(project, null, 3, null, null, "3200", "12mm rebar", ItemType.Material, 50m, "pc", 25_000, VatTreatment.Inclusive, 1_200,
            new DateOnly(2026, 10, 30), "Partial delivery", "Cebu Builders Depot", "SI-0001", TestData.Today, TestData.Now);

        Assert.Equal(1_250_000, entry.GrossMinor);
        Assert.Equal("Partial delivery", entry.Remarks);
    }
}
