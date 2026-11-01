using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests.Features;

/// <summary>
/// Every amount a person types is bounded, so a typing mistake is refused with a message and a total over tens of thousands of lines
/// can never overflow a <c>long</c>.
/// </summary>
public sealed class MoneyLimitsTests
{
    private static BudgetLine Line(long unitCostMinor, decimal quantity = 1m, VatTreatment vat = VatTreatment.Exclusive) =>
        BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, TestData.NewProject(), null, 3, null, null, "3300", "Ready-mix concrete 4000 psi",
            ItemType.Material, quantity, "m³", unitCostMinor, vat, 1_200, new DateOnly(2026, 2, 1), null, TestData.UserId,
            null, TestData.Now);

    [Fact]
    public void The_largest_accepted_line_total_is_accepted_and_one_centavo_more_is_not()
    {
        Assert.Equal(MoneyLimits.MaximumAmountMinor, Line(MoneyLimits.MaximumAmountMinor, 1m, VatTreatment.Exempt).GrossMinor);

        var over = Assert.Throws<ValidationFailedException>(() => Line(MoneyLimits.MaximumAmountMinor + 1, 1m, VatTreatment.Exempt));
        Assert.Contains("unitCostMinor", over.Errors.Keys);

        // The limit is on the total with VAT: 10^13 before 12% VAT is already over it, though the unit cost alone is exactly at the limit.
        var withVat = Assert.Throws<ValidationFailedException>(() => Line(MoneyLimits.MaximumAmountMinor, 1m, VatTreatment.Exclusive));
        Assert.Contains("unitCostMinor", withVat.Errors.Keys);
    }

    [Theory]
    [InlineData(long.MaxValue, 1)]
    [InlineData(9_000_000_000_000_000_000, 100)]
    [InlineData(5_000_000_000_000, 5)]
    public void An_enormous_unit_cost_or_total_is_a_validation_message_not_an_overflow(long unitCostMinor, int quantity)
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Line(unitCostMinor, quantity));
        Assert.Contains("unitCostMinor", ex.Errors.Keys);
    }

    [Fact]
    public void A_contract_price_above_the_limit_is_refused_and_one_at_the_limit_is_kept()
    {
        var atLimit = TestData.NewProject(contractPriceMinor: MoneyLimits.MaximumAmountMinor);
        Assert.Equal(MoneyLimits.MaximumAmountMinor, atLimit.ContractPriceMinor);

        var ex = Assert.Throws<ValidationFailedException>(() => TestData.NewProject(contractPriceMinor: MoneyLimits.MaximumAmountMinor + 1));
        Assert.Contains("contractPriceMinor", ex.Errors.Keys);
    }

    [Fact]
    public void A_pay_rate_above_the_limit_is_refused()
    {
        Assert.Equal(MoneyLimits.MaximumRateMinor, TestData.NewWorker(rateMinor: MoneyLimits.MaximumRateMinor).CurrentRate.RateMinor);

        var ex = Assert.Throws<ValidationFailedException>(() => TestData.NewWorker(rateMinor: MoneyLimits.MaximumRateMinor + 1));
        Assert.Contains("rateMinor", ex.Errors.Keys);
    }

    [Fact]
    public void Even_a_thousand_lines_at_the_limit_total_inside_a_long()
    {
        // The reason for the bound: 10^13 per line times 10^5 lines is 10^18, below the 9.2 x 10^18 a long holds.
        var total = 0L;
        for (var i = 0; i < 100_000; i++) total = checked(total + MoneyLimits.MaximumAmountMinor);
        Assert.Equal(1_000_000_000_000_000_000, total);
    }
}
