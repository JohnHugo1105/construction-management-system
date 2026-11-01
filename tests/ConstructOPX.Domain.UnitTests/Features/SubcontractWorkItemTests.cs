using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Subcontracts;
using ConstructOPX.Domain.Features.Suppliers;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>Where a subcontract's certified work is counted in the project's work breakdown.</summary>
public sealed class SubcontractWorkItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private static (Subcontract Subcontract, Project Project, Supplier Firm) Draft()
    {
        var project = TestData.NewProject();
        var firm = Supplier.Create(Guid.NewGuid(), TestData.Tenant, "Dela Cruz Electrical", SupplierKind.Subcontractor, null, null, null, null, null, null, Now);
        var subcontract = Subcontract.Create(Guid.NewGuid(), TestData.Tenant, firm, project, null, "Electrical works", null, 1_000_000_00,
            VatTreatment.Exclusive, 1_200, 1_000, 200, User, null, Now);
        return (subcontract, project, firm);
    }

    [Fact]
    public void A_standard_code_places_the_work_in_its_division_and_a_blank_clears_it()
    {
        var (subcontract, _, _) = Draft();
        Assert.True(subcontract.Classification.IsUnclassified);

        subcontract.SetWorkItem(" 3300 ");

        Assert.Equal("3300", subcontract.WbsCode);
        Assert.Equal(3, subcontract.Classification.Division);
        Assert.Equal("Concrete", subcontract.Classification.MainScope);

        subcontract.SetWorkItem("  ");
        Assert.Null(subcontract.WbsCode);
        Assert.True(subcontract.Classification.IsUnclassified);
    }

    [Fact]
    public void A_code_that_is_not_in_the_standard_list_is_refused_with_its_field()
    {
        var (subcontract, _, _) = Draft();

        var ex = Assert.Throws<ValidationFailedException>(() => subcontract.SetWorkItem("9999-nope"));

        Assert.Contains("wbsCode", ex.Errors.Keys);
        Assert.Null(subcontract.WbsCode);
    }

    [Fact]
    public void The_work_item_is_fixed_once_the_subcontract_is_awarded()
    {
        var (subcontract, project, firm) = Draft();
        subcontract.SetWorkItem("3300");
        subcontract.AssignNumber("SC-0001");
        subcontract.Award(firm, project, User, Now);

        Assert.Throws<DomainRuleException>(() => subcontract.SetWorkItem("3400"));
        Assert.Equal("3300", subcontract.WbsCode);
    }

    [Fact]
    public void A_system_cost_can_be_placed_in_the_breakdown_and_otherwise_stays_unclassified()
    {
        var project = TestData.NewProject();
        var placed = CostEntry.RecordSystem(Guid.NewGuid(), TestData.Tenant, project, null, CostOrigin.Subcontract, Guid.NewGuid(), "SC-0001 claim 1",
            ItemType.Subcontract, 1m, "l.s", 100_000, VatTreatment.Exempt, 0, new DateOnly(2026, 11, 1), User, Now, "Dela Cruz Electrical",
            CostClassification.Create(null, null, null, "3300"));
        var plain = CostEntry.RecordSystem(Guid.NewGuid(), TestData.Tenant, project, null, CostOrigin.Payroll, Guid.NewGuid(), "Payroll",
            ItemType.Labor, 1m, "l.s", 100_000, VatTreatment.Exempt, 0, new DateOnly(2026, 11, 1), User, Now);

        Assert.Equal((3, "3300", "Dela Cruz Electrical"), (placed.Division, placed.WbsCode, placed.SourceDealer));
        Assert.Null(plain.Division);
        Assert.Null(plain.WbsCode);
    }
}
