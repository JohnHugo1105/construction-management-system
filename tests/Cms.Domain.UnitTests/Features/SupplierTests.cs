using Cms.Domain.Common;
using Cms.Domain.Features.Suppliers;

namespace Cms.Domain.UnitTests.Features;

public sealed class SupplierTests
{
    private static Supplier Make(string? name = "  Visayas Steel Corp. ", SupplierKind kind = SupplierKind.Supplier, string? email = "sales@vsc.example") =>
        Supplier.Create(Guid.NewGuid(), TestData.Tenant, name, kind, "123-456-789-000", "Ana Reyes", "0917 000 0000", email, "Cebu City", null, TestData.Now);

    [Fact]
    public void A_firm_is_trimmed_and_matched_by_a_normalized_name()
    {
        var supplier = Make();

        Assert.Equal("Visayas Steel Corp.", supplier.Name);
        Assert.Equal("VISAYAS STEEL CORP.", supplier.NormalizedName);
        Assert.True(supplier.IsSupplier);
        Assert.False(supplier.IsSubcontractor);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Make(name: " ", kind: SupplierKind.None, email: "not an email"));

        Assert.Contains("name", ex.Errors.Keys);
        Assert.Contains("kind", ex.Errors.Keys);
        Assert.Contains("email", ex.Errors.Keys);
    }

    [Fact]
    public void A_firm_can_be_both_a_supplier_and_a_subcontractor()
    {
        var both = Make(kind: SupplierKind.Supplier | SupplierKind.Subcontractor);

        Assert.True(both.IsSupplier);
        Assert.True(both.IsSubcontractor);
        both.EnsureCanBeUsedAs(SupplierKind.Subcontractor);
        both.EnsureCanBeUsedAs(SupplierKind.Supplier);
    }

    [Fact]
    public void A_firm_of_the_wrong_kind_or_an_archived_one_is_refused_with_the_way_out()
    {
        var supplier = Make();
        var wrongKind = Assert.Throws<DomainRuleException>(() => supplier.EnsureCanBeUsedAs(SupplierKind.Subcontractor));
        Assert.Contains("Subcontractor", wrongKind.Message);

        supplier.Archive(TestData.Now);
        var archived = Assert.Throws<DomainRuleException>(() => supplier.EnsureCanBeUsedAs(SupplierKind.Supplier));
        Assert.Contains("Restore it", archived.Message);

        supplier.Restore(TestData.Now);
        supplier.EnsureCanBeUsedAs(SupplierKind.Supplier);
    }

    [Fact]
    public void Archiving_twice_or_restoring_a_live_firm_is_refused()
    {
        var supplier = Make();
        Assert.Throws<DomainRuleException>(() => supplier.Restore(TestData.Now));
        supplier.Archive(TestData.Now);
        Assert.Throws<DomainRuleException>(() => supplier.Archive(TestData.Now));
    }
}
