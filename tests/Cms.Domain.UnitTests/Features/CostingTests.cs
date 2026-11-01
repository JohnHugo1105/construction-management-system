using Cms.Domain.Common;
using Cms.Domain.Features.Costing;

namespace Cms.Domain.UnitTests.Features;

public sealed class LineAmountsTests
{
    [Fact]
    public void Inclusive_vat_splits_a_gross_amount_and_the_parts_add_up()
    {
        var amounts = LineAmounts.Calculate(1m, 100_000, VatTreatment.Inclusive, 1_200);

        Assert.Equal(89_286, amounts.NetMinor);
        Assert.Equal(10_714, amounts.VatMinor);
        Assert.Equal(100_000, amounts.GrossMinor);
    }

    [Fact]
    public void Exclusive_vat_is_added_on_top()
    {
        var amounts = LineAmounts.Calculate(10m, 10_000, VatTreatment.Exclusive, 1_200);

        Assert.Equal(100_000, amounts.NetMinor);
        Assert.Equal(12_000, amounts.VatMinor);
        Assert.Equal(112_000, amounts.GrossMinor);
    }

    [Fact]
    public void Exempt_lines_carry_no_vat()
    {
        var amounts = LineAmounts.Calculate(4m, 25_000, VatTreatment.Exempt, 1_200);

        Assert.Equal(new LineAmounts(100_000, 0, 100_000), amounts);
    }

    [Fact]
    public void Half_centavos_round_away_from_zero_once()
    {
        // 2.5 x 3 centavos = 7.5 centavos. The desktop app displayed a rounded unit cost but multiplied the unrounded one.
        Assert.Equal(8, LineAmounts.Calculate(2.5m, 3, VatTreatment.Exempt, 0).GrossMinor);
    }

    [Theory]
    [InlineData(VatTreatment.Inclusive)]
    [InlineData(VatTreatment.Exclusive)]
    [InlineData(VatTreatment.Exempt)]
    public void Net_plus_vat_always_equals_gross_for_every_amount(VatTreatment treatment)
    {
        for (long unitCost = 0; unitCost <= 20_000; unitCost += 37)
        foreach (var quantity in new[] { 0.01m, 0.333m, 1m, 2.5m, 7.125m, 100m, 1234.5678m })
        foreach (var rate in new[] { 0, 500, 1_200, 2_000 })
        {
            var amounts = LineAmounts.Calculate(quantity, unitCost, treatment, rate);
            Assert.Equal(amounts.GrossMinor, amounts.NetMinor + amounts.VatMinor);
            Assert.True(amounts.VatMinor >= 0);
        }
    }

    [Fact]
    public void Summing_lines_keeps_the_identity()
    {
        var total = LineAmounts.Zero;
        for (var i = 1; i <= 50; i++) total += LineAmounts.Calculate(i * 1.5m, 1_999, VatTreatment.Inclusive, 1_200);
        Assert.Equal(total.GrossMinor, total.NetMinor + total.VatMinor);
    }

    [Theory]
    [InlineData(0, 100, 1_200)]
    [InlineData(1, -1, 1_200)]
    [InlineData(1, 100, -1)]
    [InlineData(1, 100, 10_001)]
    public void Invalid_inputs_are_rejected(double quantity, long unitCost, int rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LineAmounts.Calculate((decimal)quantity, unitCost, VatTreatment.Inclusive, rate));
}

public sealed class BudgetPositionTests
{
    [Theory]
    [InlineData(0, 0, BudgetHealth.NoActivity)]
    [InlineData(0, 5_000, BudgetHealth.Unbudgeted)]
    [InlineData(100_000, 0, BudgetHealth.OnTrack)]
    [InlineData(100_000, 89_999, BudgetHealth.OnTrack)]
    [InlineData(100_000, 90_000, BudgetHealth.NearLimit)]
    [InlineData(100_000, 100_000, BudgetHealth.NearLimit)]
    [InlineData(100_000, 100_001, BudgetHealth.OverBudget)]
    public void Health_follows_the_share_of_budget_used(long budget, long actual, BudgetHealth expected) =>
        Assert.Equal(expected, new BudgetPosition(budget, actual).Health);

    [Fact]
    public void Remaining_is_budget_minus_actual_and_negative_when_over()
    {
        Assert.Equal(25_000, new BudgetPosition(100_000, 75_000).RemainingMinor);
        Assert.Equal(-5_000, new BudgetPosition(100_000, 105_000).RemainingMinor);
    }

    [Fact]
    public void Used_share_is_in_basis_points_and_absent_without_a_budget()
    {
        Assert.Equal(7_500, new BudgetPosition(100_000, 75_000).UsedBps);
        Assert.Equal(10_500, new BudgetPosition(100_000, 105_000).UsedBps);
        Assert.Null(new BudgetPosition(0, 10).UsedBps);
    }

    [Fact]
    public void Positions_add_together()
    {
        var sum = new BudgetPosition(100, 40) + new BudgetPosition(50, 70);
        Assert.Equal(new BudgetPosition(150, 110), sum);
    }
}

public sealed class WbsCatalogTests
{
    [Fact]
    public void Has_sixteen_divisions_and_unique_codes_that_each_belong_to_a_division()
    {
        Assert.Equal(16, WbsCatalog.Divisions.Count);
        Assert.Equal(WbsCatalog.Entries.Count, WbsCatalog.Entries.Select(entry => entry.Code).Distinct().Count());
        Assert.All(WbsCatalog.Entries, entry => Assert.InRange(entry.Division, 1, 16));
        Assert.All(WbsCatalog.Divisions, division => Assert.NotEmpty(WbsCatalog.ForDivision(division.Number)));
    }

    [Fact]
    public void Looks_up_a_standard_code()
    {
        var entry = WbsCatalog.FindByCode("3300");

        Assert.NotNull(entry);
        Assert.Equal(3, entry.Division);
        Assert.Equal("Concrete", entry.MainScope);
        Assert.Equal("Cast-In-Place Concrete", entry.SubScope);
        Assert.Null(WbsCatalog.FindByCode("9999"));
    }
}

public sealed class CostClassificationTests
{
    [Fact]
    public void A_standard_code_decides_the_division_and_names()
    {
        var result = CostClassification.Create(null, null, null, "3200");

        Assert.Equal(3, result.Division);
        Assert.Equal("Concrete", result.MainScope);
        Assert.Equal("Concrete Reinforcement", result.SubScope);
    }

    [Fact]
    public void A_standard_code_in_the_wrong_division_is_rejected_with_a_helpful_message()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => CostClassification.Create(5, null, null, "3200"));

        Assert.Contains("Division 3", ex.Errors["division"][0]);
    }

    [Fact]
    public void A_custom_code_needs_a_division_and_a_sub_scope()
    {
        Assert.Contains("division", Assert.Throws<ValidationFailedException>(() => CostClassification.Create(null, null, "Rebar tying", "C-9")).Errors.Keys);
        Assert.Contains("subScope", Assert.Throws<ValidationFailedException>(() => CostClassification.Create(3, null, null, "C-9")).Errors.Keys);

        var ok = CostClassification.Create(3, null, "Rebar tying", "C-9");
        Assert.Equal("Concrete", ok.MainScope);
        Assert.Equal("C-9", ok.WbsCode);
    }

    [Fact]
    public void Nothing_filled_in_is_unclassified_but_a_scope_without_a_code_is_not_allowed()
    {
        Assert.True(CostClassification.Create(null, null, null, null).IsUnclassified);
        Assert.Throws<ValidationFailedException>(() => CostClassification.Create(3, null, null, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Divisions_outside_one_to_sixteen_are_rejected(int division) =>
        Assert.Throws<ValidationFailedException>(() => CostClassification.Create(division, null, null, "3200"));
}

public sealed class UnitOfMeasureTests
{
    [Theory]
    [InlineData("pcs", "pc")]
    [InlineData("Bags", "bag")]
    [InlineData("SETS", "set")]
    [InlineData("sht", "sheet")]
    [InlineData("m3", "m³")]
    [InlineData("sqm", "m²")]
    [InlineData("lump sum", "l.s")]
    [InlineData("  kg ", "kg")]
    [InlineData("drum", "drum")]
    public void Near_duplicates_fold_into_one_spelling(string input, string expected) =>
        Assert.Equal(expected, UnitOfMeasure.Normalize(input));

    [Fact]
    public void A_blank_unit_is_rejected() => Assert.Throws<ValidationFailedException>(() => UnitOfMeasure.Normalize("  "));
}
