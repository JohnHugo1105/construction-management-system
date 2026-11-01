using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Statutory;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>
/// The tables here are made up with obviously artificial figures (333.33, 3.21%) so no test can be read as stating a legal rate. Every
/// figure is worked out by hand in the comment beside it, in centavos.
/// </summary>
internal static class StatutoryTables
{
    public static readonly DateOnly From = new(2026, 1, 1);

    /// <summary>Monthly brackets: under 10,000.00 is 111.00/222.00; under 20,000.00 is 333.33/444.45; above that 555.00/666.00.</summary>
    public static StatutorySchedule SssBrackets(StatutoryPeriodBasis basis = StatutoryPeriodBasis.Monthly) =>
        StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "Artificial test brackets", From, StatutoryMethod.Brackets, basis, null,
        [
            new StatutoryRowInput(0, 1_000_000, 11_100, 22_200, 0, 0),
            new StatutoryRowInput(1_000_000, 2_000_000, 33_333, 44_445, 0, 0),
            new StatutoryRowInput(2_000_000, null, 55_500, 66_600, 0, 0)
        ], null, TestData.Now);

    /// <summary>Monthly 3.21% / 4.32% of a base held between 5,000.00 and 30,000.00; employee at most 700.00, employer at least 250.00.</summary>
    public static StatutorySchedule PhilHealthPercentage(StatutoryPeriodBasis basis = StatutoryPeriodBasis.Monthly) =>
        StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.PhilHealth, "Artificial test percentage", From, StatutoryMethod.Percentage,
            basis, new StatutoryPercentageInput(321, 432, 500_000, 3_000_000, null, 70_000, 25_000, null), null, null, TestData.Now);

    /// <summary>Per-run brackets: under 5,000.00 is 50.00/50.00; above that 100.00/100.00.</summary>
    public static StatutorySchedule PagIbigPerRun() =>
        StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.PagIbig, "Artificial per-run brackets", From, StatutoryMethod.Brackets,
            StatutoryPeriodBasis.PerRun, null, [new StatutoryRowInput(0, 500_000, 5_000, 5_000, 0, 0), new StatutoryRowInput(500_000, null, 10_000, 10_000, 0, 0)],
            null, TestData.Now);

    /// <summary>Monthly tax: nothing under 10,000.00; 10% of the excess over 10,000.00 under 30,000.00; 2,000.00 plus 20% over 30,000.00.</summary>
    public static StatutorySchedule TaxMonthly() =>
        StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.WithholdingTax, "Artificial test tax", From, StatutoryMethod.TaxBrackets,
            StatutoryPeriodBasis.Monthly, null,
            [
                new StatutoryRowInput(0, 1_000_000, 0, 0, 0, 0),
                new StatutoryRowInput(1_000_000, 3_000_000, 0, 0, 0, 1_000),
                new StatutoryRowInput(3_000_000, null, 0, 0, 200_000, 2_000)
            ], null, TestData.Now);

    /// <summary>Per-run tax: nothing under 5,000.00; above that 100.00 plus 15% of the excess over 5,000.00.</summary>
    public static StatutorySchedule TaxPerRun() =>
        StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.WithholdingTax, "Artificial per-run tax", From, StatutoryMethod.TaxBrackets,
            StatutoryPeriodBasis.PerRun, null, [new StatutoryRowInput(0, 500_000, 0, 0, 0, 0), new StatutoryRowInput(500_000, null, 0, 0, 10_000, 1_500)],
            null, TestData.Now);
}

public sealed class StatutoryCalculatorTests
{
    private static StatutoryAmount For(IReadOnlyList<StatutoryAmount> amounts, StatutoryAgency agency) => amounts.Single(amount => amount.Agency == agency);

    [Fact]
    public void Monthly_brackets_use_the_pay_times_runs_per_month_and_split_the_month_rounding_half_away_from_zero()
    {
        var table = StatutoryTables.SssBrackets();

        // 6,000.00 a run x 2 runs = 12,000.00 a month: the second row. 333.33 / 2 = 166.665 -> 166.67; 444.45 / 2 = 222.225 -> 222.23.
        var amount = Assert.Single(StatutoryCalculator.Calculate(600_000, StatutoryParticipation.Default, [table], 2));

        Assert.Equal(new StatutoryAmount(StatutoryAgency.Sss, table.Id, 16_667, 22_223), amount);
    }

    [Fact]
    public void A_row_covers_its_start_and_stops_just_before_its_end()
    {
        var table = StatutoryTables.SssBrackets(StatutoryPeriodBasis.PerRun);

        Assert.Equal((11_100, 22_200), table.Contribution(0));
        Assert.Equal((11_100, 22_200), table.Contribution(999_999));
        Assert.Equal((33_333, 44_445), table.Contribution(1_000_000));
        Assert.Equal((55_500, 66_600), table.Contribution(2_000_000));
        Assert.Equal((55_500, 66_600), table.Contribution(MoneyLimits.MaximumAmountMinor));
    }

    [Fact]
    public void Per_run_brackets_apply_to_the_runs_pay_as_it_is()
    {
        var table = StatutoryTables.SssBrackets(StatutoryPeriodBasis.PerRun);

        // 6,000.00 is in the first row whatever the runs per month.
        Assert.Equal(new StatutoryAmount(StatutoryAgency.Sss, table.Id, 11_100, 22_200),
            Assert.Single(StatutoryCalculator.Calculate(600_000, StatutoryParticipation.Default, [table], 4)));
    }

    [Theory]
    // 6,000.00 x 2 = 12,000.00: 3.21% = 385.20 -> 192.60 a run; 4.32% = 518.40 -> 259.20 a run.
    [InlineData(600_000, 19_260, 25_920)]
    // 1,000.00 x 2 = 2,000.00 is below the 5,000.00 floor: 3.21% of 5,000.00 = 160.50 -> 80.25; 4.32% = 216.00 is below the 250.00 minimum -> 125.00.
    [InlineData(100_000, 8_025, 12_500)]
    // 20,000.00 x 2 = 40,000.00 is above the 30,000.00 ceiling: 3.21% = 963.00 is above the 700.00 maximum -> 350.00; 4.32% = 1,296.00 -> 648.00.
    [InlineData(2_000_000, 35_000, 64_800)]
    // 6,172.835 can't happen (whole centavos); 6,172.84 x 2 = 12,345.68: 3.21% = 396.296... -> 396.30 -> 198.15; 4.32% = 533.333... -> 533.33 -> 266.665 -> 266.67.
    [InlineData(617_284, 19_815, 26_667)]
    public void A_percentage_table_holds_the_base_between_floor_and_ceiling_then_each_share_between_its_limits(long gross, long employee, long employer)
    {
        var table = StatutoryTables.PhilHealthPercentage();

        var amount = Assert.Single(StatutoryCalculator.Calculate(gross, StatutoryParticipation.Default, [table], 2));

        Assert.Equal((employee, employer), (amount.EmployeeMinor, amount.EmployerMinor));
    }

    [Fact]
    public void A_per_run_percentage_table_rounds_the_share_once()
    {
        var table = StatutoryTables.PhilHealthPercentage(StatutoryPeriodBasis.PerRun);

        // 12,345.67 per run: 3.21% = 396.296... -> 396.30; 4.32% = 533.333... -> 533.33.
        var amount = Assert.Single(StatutoryCalculator.Calculate(1_234_567, StatutoryParticipation.Default, [table], 2));

        Assert.Equal((39_630, 53_333), (amount.EmployeeMinor, amount.EmployerMinor));
    }

    [Fact]
    public void Monthly_tax_is_on_the_monthly_base_less_every_contribution_on_the_same_monthly_basis()
    {
        var sss = StatutoryTables.SssBrackets();
        var philHealth = StatutoryTables.PhilHealthPercentage();
        var pagIbig = StatutoryTables.PagIbigPerRun();
        var tax = StatutoryTables.TaxMonthly();

        // 9,000.00 a run, 2 runs: monthly base 18,000.00.
        // SSS (monthly): 333.33 a month -> 166.665 -> 166.67 a run.
        // PhilHealth (monthly): 3.21% of 18,000.00 = 577.80 a month -> 288.90 a run.
        // Pag-IBIG (per run): 100.00 a run = 200.00 a month.
        // Taxable a month: 18,000.00 - 333.33 - 577.80 - 200.00 = 16,888.87. Tax: 10% of 6,888.87 = 688.887 -> 688.89 a month -> 344.445 -> 344.45 a run.
        var amounts = StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [tax, pagIbig, philHealth, sss], 2);

        Assert.Equal([StatutoryAgency.Sss, StatutoryAgency.PhilHealth, StatutoryAgency.PagIbig, StatutoryAgency.WithholdingTax], amounts.Select(item => item.Agency));
        Assert.Equal(16_667, For(amounts, StatutoryAgency.Sss).EmployeeMinor);
        Assert.Equal(28_890, For(amounts, StatutoryAgency.PhilHealth).EmployeeMinor);
        Assert.Equal(10_000, For(amounts, StatutoryAgency.PagIbig).EmployeeMinor);
        Assert.Equal(new StatutoryAmount(StatutoryAgency.WithholdingTax, tax.Id, 34_445, 0), For(amounts, StatutoryAgency.WithholdingTax));
    }

    [Fact]
    public void Per_run_tax_is_on_the_runs_pay_less_the_contributions_taken_in_the_run()
    {
        var sss = StatutoryTables.SssBrackets();
        var philHealth = StatutoryTables.PhilHealthPercentage();
        var pagIbig = StatutoryTables.PagIbigPerRun();
        var tax = StatutoryTables.TaxPerRun();

        // Contributions a run, as above: 166.67 + 288.90 + 100.00 = 555.57. Taxable: 9,000.00 - 555.57 = 8,444.43.
        // Tax: 100.00 + 15% of (8,444.43 - 5,000.00) = 100.00 + 516.6645 -> 100.00 + 516.66 = 616.66.
        var amounts = StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [sss, philHealth, pagIbig, tax], 2);

        Assert.Equal(61_666, For(amounts, StatutoryAgency.WithholdingTax).EmployeeMinor);
    }

    [Fact]
    public void A_worker_outside_a_fund_pays_nothing_to_it_and_that_raises_the_taxable_pay()
    {
        var sss = StatutoryTables.SssBrackets();
        var tax = StatutoryTables.TaxPerRun();

        // Not an SSS member: no SSS line, and the tax is on the whole 9,000.00: 100.00 + 15% of 4,000.00 = 700.00.
        var amounts = StatutoryCalculator.Calculate(900_000, new StatutoryParticipation(false, true, true, false), [sss, tax], 2);

        var only = Assert.Single(amounts);
        Assert.Equal((StatutoryAgency.WithholdingTax, 70_000L), (only.Agency, only.EmployeeMinor));
    }

    [Fact]
    public void An_exempt_worker_has_no_tax_and_tax_is_never_negative()
    {
        var tax = StatutoryTables.TaxMonthly();

        Assert.Empty(StatutoryCalculator.Calculate(900_000, new StatutoryParticipation(true, true, true, true), [tax], 2));
        // Contributions larger than the base can't give a refund: the taxable amount stops at zero, and so does the tax.
        Assert.Equal(0, tax.Tax(-50_000));
        Assert.Equal(0, Assert.Single(StatutoryCalculator.Calculate(0, StatutoryParticipation.Default, [tax], 1)).EmployeeMinor);
    }

    [Fact]
    public void An_agency_without_a_table_is_left_out_and_runs_per_month_must_be_one_to_four()
    {
        Assert.Empty(StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [], 5));
        Assert.Throws<ArgumentException>(() =>
            StatutoryCalculator.Calculate(900_000, StatutoryParticipation.Default, [StatutoryTables.SssBrackets(), StatutoryTables.SssBrackets()], 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_monthly_amount_is_divided_by_the_companys_runs_per_month(int runs)
    {
        var table = StatutoryTables.SssBrackets();

        // 4,000.00 a run: x1 = 4,000.00 (row 1, 111.00), x3 = 12,000.00 (row 2, 333.33 / 3 = 111.11), x4 = 16,000.00 (row 2, 333.33 / 4 = 83.3325 -> 83.33).
        var expected = runs switch { 1 => 11_100L, 3 => 11_111L, _ => 8_333L };
        Assert.Equal(expected, Assert.Single(StatutoryCalculator.Calculate(400_000, StatutoryParticipation.Default, [table], runs)).EmployeeMinor);
    }

    [Fact]
    public void Amounts_above_the_pay_are_limited_in_agency_order_so_they_add_up_to_the_pay()
    {
        Assert.Equal([30_000L, 15_000L, 5_000L, 0L], StatutoryCalculator.LimitToPay([30_000, 15_000, 10_000, 2_000], 50_000));
        Assert.Equal([30_000L, 15_000L, 10_000L, 2_000L], StatutoryCalculator.LimitToPay([30_000, 15_000, 10_000, 2_000], 60_000));
        Assert.Equal([0L, 0L], StatutoryCalculator.LimitToPay([30_000, 15_000], 0));
    }

    [Fact]
    public void The_table_in_force_is_the_active_one_with_the_latest_date_on_or_before_the_day()
    {
        var older = StatutoryTables.SssBrackets();
        var newer = StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "Later", new DateOnly(2026, 7, 1), StatutoryMethod.Brackets,
            StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 1, 1, 0, 0)], null, TestData.Now);
        var retired = StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "Retired", new DateOnly(2026, 9, 1), StatutoryMethod.Brackets,
            StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 1, 1, 0, 0)], null, TestData.Now);
        retired.Retire(TestData.UserId, TestData.Now);

        Assert.Null(StatutoryCalculator.InForce([older, newer, retired], StatutoryAgency.Sss, new DateOnly(2025, 12, 31)));
        Assert.Same(older, StatutoryCalculator.InForce([older, newer, retired], StatutoryAgency.Sss, new DateOnly(2026, 6, 30)));
        Assert.Same(newer, StatutoryCalculator.InForce([older, newer, retired], StatutoryAgency.Sss, new DateOnly(2026, 7, 1)));
        Assert.Same(newer, StatutoryCalculator.InForce([older, newer, retired], StatutoryAgency.Sss, new DateOnly(2026, 10, 1)));
        Assert.Null(StatutoryCalculator.InForce([older, newer], StatutoryAgency.PhilHealth, new DateOnly(2026, 10, 1)));
    }
}

public sealed class StatutoryScheduleTests
{
    private static ValidationFailedException Brackets(params StatutoryRowInput[] rows) =>
        Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "Test", StatutoryTables.From,
            StatutoryMethod.Brackets, StatutoryPeriodBasis.Monthly, null, rows, null, TestData.Now));

    [Fact]
    public void Rows_must_start_at_zero_follow_on_without_gaps_or_overlaps_and_only_the_last_is_open()
    {
        Assert.Equal(["Row 1 must start at ₱0.00, so every amount is covered."],
            Brackets(new StatutoryRowInput(100, 500, 1, 1, 0, 0), new StatutoryRowInput(500, null, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Equal(["Row 2 must start at ₱5.00, where row 1 ends, so there are no gaps or overlaps."],
            Brackets(new StatutoryRowInput(0, 500, 1, 1, 0, 0), new StatutoryRowInput(600, null, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Equal(["Row 2 must start at ₱5.00, where row 1 ends, so there are no gaps or overlaps."],
            Brackets(new StatutoryRowInput(0, 500, 1, 1, 0, 0), new StatutoryRowInput(400, null, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Equal(["Row 2 is the last row, so leave its upper limit empty: it covers every larger amount."],
            Brackets(new StatutoryRowInput(0, 500, 1, 1, 0, 0), new StatutoryRowInput(500, 900, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Equal(["Row 1 needs an upper limit. Only the last row is open-ended."],
            Brackets(new StatutoryRowInput(0, null, 1, 1, 0, 0), new StatutoryRowInput(500, null, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Contains("Row 1: the upper limit must be above ₱0.00.",
            Brackets(new StatutoryRowInput(0, 0, 1, 1, 0, 0), new StatutoryRowInput(0, null, 1, 1, 0, 0)).Errors["rows"]);
        Assert.Equal(["Add the table's rows, starting at ₱0.00."], Brackets().Errors["rows"]);
        Assert.Equal(["Row 1: the employee's share can't be negative.", "Row 1: the employer's share can be at most ₱100,000,000,000.00."],
            Brackets(new StatutoryRowInput(0, null, -1, MoneyLimits.MaximumAmountMinor + 1, 0, 0)).Errors["rows"]);
    }

    [Fact]
    public void A_table_has_at_most_one_hundred_rows()
    {
        var rows = Enumerable.Range(0, 101).Select(index => new StatutoryRowInput(index * 100L, index == 100 ? null : (index + 1) * 100L, 1, 1, 0, 0)).ToArray();

        Assert.Equal(["A table can have at most 100 rows."], Brackets(rows).Errors["rows"]);
    }

    [Fact]
    public void Tax_rows_need_a_rate_of_at_most_one_hundred_percent_and_a_base_tax_that_is_not_negative()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.WithholdingTax, "Tax",
            StatutoryTables.From, StatutoryMethod.TaxBrackets, StatutoryPeriodBasis.Monthly, null,
            [new StatutoryRowInput(0, 100, 0, 0, -1, 0), new StatutoryRowInput(100, null, 0, 0, 0, 10_001)], null, TestData.Now));

        Assert.Equal(["Row 1: the base tax can't be negative.", "Row 2: enter a rate between 0% and 100%."], ex.Errors["rows"]);
    }

    [Fact]
    public void A_percentage_table_checks_its_rates_base_and_limits_and_reports_every_problem_at_once()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.PagIbig, " ", default,
            StatutoryMethod.Percentage, (StatutoryPeriodBasis)9, new StatutoryPercentageInput(10_001, -1, 500_000, 400_000, 2_000, 1_000, -5, null), null, null,
            TestData.Now));

        Assert.Equal(
            ["name", "effectiveFrom", "periodBasis", "employeeRateBps", "employerRateBps", "baseCeilingMinor", "employeeMaximumMinor", "employerMinimumMinor"],
            ex.Errors.Keys);
        Assert.Equal(["The highest base can't be below the lowest base."], ex.Errors["baseCeilingMinor"]);
        Assert.Equal(["The employee's largest share can't be below the smallest share."], ex.Errors["employeeMaximumMinor"]);
        Assert.Equal(["The employer's smallest share can't be negative."], ex.Errors["employerMinimumMinor"]);
    }

    [Fact]
    public void Tax_uses_tax_brackets_and_contributions_never_do()
    {
        var tax = Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.WithholdingTax, "Tax",
            StatutoryTables.From, StatutoryMethod.Brackets, StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 1, 1, 0, 0)], null, TestData.Now));
        var sss = Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "SSS",
            StatutoryTables.From, StatutoryMethod.TaxBrackets, StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 0, 0, 0, 0)], null, TestData.Now));
        var agency = Assert.Throws<ValidationFailedException>(() => StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, (StatutoryAgency)7, "?",
            StatutoryTables.From, StatutoryMethod.Brackets, StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 0, 0, 0, 0)], null, TestData.Now));

        Assert.Equal(["Withholding tax uses tax brackets."], tax.Errors["method"]);
        Assert.Equal(["Tax brackets are only for withholding tax. Choose brackets or a percentage."], sss.Errors["method"]);
        Assert.Equal(["Choose the agency: SSS, PhilHealth, Pag-IBIG or withholding tax."], agency.Errors["agency"]);
    }

    [Fact]
    public void Fields_the_method_does_not_use_are_not_kept()
    {
        var brackets = StatutorySchedule.Create(Guid.NewGuid(), TestData.Tenant, StatutoryAgency.Sss, "SSS", StatutoryTables.From, StatutoryMethod.Brackets,
            StatutoryPeriodBasis.Monthly, new StatutoryPercentageInput(100, 100, 0, null, null, null, null, null), [new StatutoryRowInput(0, null, 7, 8, 9, 10)],
            null, TestData.Now);

        Assert.Null(brackets.EmployeeRateBps);
        var row = Assert.Single(brackets.Rows);
        Assert.Equal((7L, 8L, 0L, 0), (row.EmployeeMinor, row.EmployerMinor, row.BaseTaxMinor, row.RateBps));

        brackets.Update("SSS", StatutoryTables.From, StatutoryMethod.Percentage, StatutoryPeriodBasis.PerRun,
            new StatutoryPercentageInput(100, 200, 0, null, null, null, null, null), [new StatutoryRowInput(0, null, 7, 8, 9, 10)], TestData.Now);
        Assert.Empty(brackets.Rows);
        Assert.Equal((100, 200), (brackets.EmployeeRateBps, brackets.EmployerRateBps));
    }

    [Fact]
    public void A_retired_table_can_not_be_changed_or_retired_again()
    {
        var table = StatutoryTables.SssBrackets();
        table.Retire(TestData.UserId, TestData.Now);

        Assert.Equal(StatutoryScheduleStatus.Retired, table.Status);
        Assert.Equal("A retired table can't be changed. Add a new table instead.", Assert.Throws<DomainRuleException>(() =>
            table.Update("x", StatutoryTables.From, StatutoryMethod.Brackets, StatutoryPeriodBasis.Monthly, null, [new StatutoryRowInput(0, null, 1, 1, 0, 0)],
                TestData.Now)).Message);
        Assert.Throws<DomainRuleException>(() => table.Retire(TestData.UserId, TestData.Now));
    }
}

public sealed class PayrollStatutoryTests
{
    /// <summary>One worker earning 1,050.00 (800.00 for 8 hours + 250.00 overtime) in a draft.</summary>
    private static PayrollRun NewRun(out Worker worker)
    {
        worker = TestData.NewWorker();
        var lines = PayrollCalculator.Calculate(
            [new AttendanceFact(Guid.NewGuid(), worker.Id, Guid.NewGuid(), null, new DateOnly(2026, 11, 3), 8m, 2m)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, 8m, 12_500);
        return PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), lines, 8m, 12_500, TestData.UserId,
            null, TestData.Today, TestData.Now);
    }

    private static readonly Guid Table = Guid.NewGuid();

    private static Dictionary<Guid, IReadOnlyList<StatutoryAmount>> Amounts(Guid workerId, long sss, long philHealth, long pagIbig, long tax) => new()
    {
        [workerId] =
        [
            new StatutoryAmount(StatutoryAgency.Sss, Table, sss, sss * 2), new StatutoryAmount(StatutoryAgency.PhilHealth, Table, philHealth, philHealth),
            new StatutoryAmount(StatutoryAgency.PagIbig, Table, pagIbig, pagIbig), new StatutoryAmount(StatutoryAgency.WithholdingTax, Table, tax, 0)
        ]
    };

    [Fact]
    public void Calculated_amounts_become_statutory_deductions_and_the_employer_share_is_kept_but_not_deducted()
    {
        var run = NewRun(out var worker);

        run.ApplyStatutory(Amounts(worker.Id, 11_111, 2_222, 3_333, 4_444), 2, TestData.Now);

        var line = Assert.Single(run.Lines);
        Assert.Equal(["SSS contribution", "PhilHealth contribution", "Pag-IBIG contribution", "Withholding tax"], line.Deductions.Select(item => item.Description));
        Assert.All(line.Deductions, item => Assert.Equal((PayrollDeductionSource.Statutory, Table), (item.Source, item.ScheduleId)));
        // 111.11 + 22.22 + 33.33 + 44.44 = 211.10 taken; net 1,050.00 - 211.10 = 838.90. Employer: 222.22 + 22.22 + 33.33 = 277.77.
        Assert.Equal(21_110, line.DeductionsMinor);
        Assert.Equal(83_890, run.TotalNetMinor);
        Assert.Equal(27_777, line.EmployerSharesMinor);
        Assert.False(line.StatutoryLimited);
        Assert.Equal(2, run.RunsPerMonth);
        Assert.Equal(105_000, run.TotalGrossMinor);
    }

    [Fact]
    public void When_the_amounts_would_take_more_than_the_pay_they_are_limited_in_agency_order_and_the_line_says_so()
    {
        var run = NewRun(out var worker);

        // 600.00 + 300.00 + 200.00 + 100.00 = 1,200.00 against 1,050.00 pay: SSS 600.00, PhilHealth 300.00, Pag-IBIG 150.00, tax 0.00.
        run.ApplyStatutory(Amounts(worker.Id, 60_000, 30_000, 20_000, 10_000), 2, TestData.Now);

        var line = Assert.Single(run.Lines);
        Assert.Equal([60_000L, 30_000L, 15_000L, 0L], line.Deductions.Select(item => item.AmountMinor));
        Assert.Equal([60_000L, 30_000L, 20_000L, 10_000L], line.Deductions.Select(item => item.CalculatedMinor!.Value));
        Assert.Equal(105_000, line.DeductionsMinor);
        Assert.Equal(0, line.NetPayMinor);
        Assert.True(line.StatutoryLimited);
        Assert.Equal([false, false, true, true], line.Deductions.Select(item => item.IsLimited));
    }

    [Fact]
    public void Manual_deductions_leave_the_calculated_ones_alone_and_together_they_can_not_exceed_the_pay()
    {
        var run = NewRun(out var worker);
        run.ApplyStatutory(Amounts(worker.Id, 10_000, 0, 0, 0), 2, TestData.Now);

        run.SetDeductions(worker.Id, [("Cash advance", 20_000)], TestData.Now);
        var line = Assert.Single(run.Lines);
        Assert.Equal(30_000, line.DeductionsMinor);
        Assert.Equal(4, line.Deductions.Count(item => item.IsStatutory));

        run.SetDeductions(worker.Id, [], TestData.Now);
        Assert.Equal(10_000, line.DeductionsMinor);

        // 100.00 calculated + 950.01 typed = 1,050.01, a centavo over the pay.
        var ex = Assert.Throws<ValidationFailedException>(() => run.SetDeductions(worker.Id, [("Loan", 95_001)], TestData.Now));
        Assert.Equal(["Deductions for Juan Dela Cruz, together with the calculated contributions and tax, are more than the pay. Check the amounts."],
            ex.Errors["deductions"]);
        run.SetDeductions(worker.Id, [("Loan", 95_000)], TestData.Now);
        Assert.Equal(0, line.NetPayMinor);
    }

    [Fact]
    public void Recalculating_keeps_manual_deductions_and_limits_the_calculated_ones_to_the_pay_they_leave()
    {
        var run = NewRun(out var worker);
        run.SetDeductions(worker.Id, [("Cash advance", 100_000)], TestData.Now);

        // Only 50.00 of the pay is left: SSS gets it, the rest are limited to nothing.
        run.ApplyStatutory(Amounts(worker.Id, 10_000, 2_000, 1_000, 500), 2, TestData.Now);

        var line = Assert.Single(run.Lines);
        Assert.Equal(105_000, line.DeductionsMinor);
        Assert.Equal(5_000, line.Deductions.Single(item => item.Agency == StatutoryAgency.Sss).AmountMinor);
        Assert.True(line.StatutoryLimited);
        Assert.Equal(100_000, line.ManualDeductionsMinor);
    }

    [Fact]
    public void A_person_can_adjust_a_calculated_amount_with_a_reason_and_recalculating_resets_it()
    {
        var run = NewRun(out var worker);
        var amounts = Amounts(worker.Id, 10_000, 2_000, 1_000, 500);
        run.ApplyStatutory(amounts, 2, TestData.Now);

        run.AdjustStatutory(worker.Id, StatutoryAgency.PhilHealth, 2_500, "Agency statement shows 25.00", TestData.Now);
        var line = Assert.Single(run.Lines);
        var adjusted = line.Deductions.Single(item => item.Agency == StatutoryAgency.PhilHealth);
        Assert.Equal((2_500L, 2_000L, true, "Agency statement shows 25.00"), (adjusted.AmountMinor, adjusted.CalculatedMinor!.Value, adjusted.IsAdjusted, adjusted.AdjustmentReason));
        Assert.False(adjusted.IsLimited);

        // Zero is allowed (take nothing); a negative amount, no reason, or an amount that would pass the pay are not.
        run.AdjustStatutory(worker.Id, StatutoryAgency.WithholdingTax, 0, "Exempt this month", TestData.Now);
        var ex = Assert.Throws<ValidationFailedException>(() => run.AdjustStatutory(worker.Id, StatutoryAgency.Sss, -1, " ", TestData.Now));
        Assert.Equal(["reason", "amountMinor"], ex.Errors.Keys);
        var over = Assert.Throws<ValidationFailedException>(() => run.AdjustStatutory(worker.Id, StatutoryAgency.Sss, 101_501, "Big", TestData.Now));
        Assert.Equal(["That would make the deductions for Juan Dela Cruz more than the pay. Enter a smaller amount."], over.Errors["amountMinor"]);
        run.AdjustStatutory(worker.Id, StatutoryAgency.Sss, 101_500, "Exactly the pay", TestData.Now);
        Assert.Equal(0, line.NetPayMinor);

        run.ApplyStatutory(amounts, 2, TestData.Now);
        Assert.All(line.Deductions, item => Assert.False(item.IsAdjusted));
        Assert.Equal(13_500, line.DeductionsMinor);
    }

    [Fact]
    public void Adjusting_an_agency_that_was_not_calculated_or_a_final_payroll_is_refused()
    {
        var run = NewRun(out var worker);
        run.ApplyStatutory(new Dictionary<Guid, IReadOnlyList<StatutoryAmount>>(), 2, TestData.Now);

        Assert.Equal("SSS wasn't calculated for Juan Dela Cruz, so there is nothing to adjust. Enter it as a deduction instead.",
            Assert.Throws<DomainRuleException>(() => run.AdjustStatutory(worker.Id, StatutoryAgency.Sss, 100, "x", TestData.Now)).Message);
        Assert.Throws<NotFoundException>(() => run.AdjustStatutory(Guid.NewGuid(), StatutoryAgency.Sss, 100, "x", TestData.Now));

        run.Approve(TestData.UserId, TestData.Now);
        Assert.Equal("Contributions and tax can only be recalculated while the payroll is a draft.",
            Assert.Throws<DomainRuleException>(() => run.ApplyStatutory(Amounts(worker.Id, 1, 1, 1, 1), 2, TestData.Now)).Message);
        Assert.Throws<DomainRuleException>(() => run.AdjustStatutory(worker.Id, StatutoryAgency.Sss, 100, "x", TestData.Now));
    }
}

public sealed class StatutorySettingsTests
{
    [Fact]
    public void A_new_worker_pays_every_fund_and_is_taxed_and_the_flags_can_be_changed()
    {
        var worker = TestData.NewWorker();
        Assert.Equal(StatutoryParticipation.Default, worker.Statutory);

        worker.SetStatutory(new StatutoryParticipation(false, true, false, true), TestData.Now);
        Assert.Equal((false, true, false, true), (worker.IsSssMember, worker.IsPhilHealthMember, worker.IsPagIbigMember, worker.IsTaxExempt));

        var hired = Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-9", "Ana", null, "Reyes", TestData.HireDate, "Helper", 60_000, RateUnit.Daily, TestData.UserId,
            TestData.Today, TestData.Now, null, new StatutoryParticipation(true, false, true, true));
        Assert.Equal(new StatutoryParticipation(true, false, true, true), hired.Statutory);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Runs_per_month_is_one_to_four_and_defaults_to_two(int runs)
    {
        var organization = Organization.Create(Guid.NewGuid(), "Test Builders", TestData.Now);
        Assert.Equal(2, organization.PayrollRunsPerMonth);

        var ex = Assert.Throws<ValidationFailedException>(() => organization.UpdateSettings("Test Builders", "PHP", 1_200, "Asia/Manila", 8m, 12_500,
            VarianceBasis.IncludingVat, TestData.Now, runs));
        Assert.Equal(["Enter how many pay runs you make in a month, from 1 to 4."], ex.Errors["payrollRunsPerMonth"]);

        organization.UpdateSettings("Test Builders", "PHP", 1_200, "Asia/Manila", 8m, 12_500, VarianceBasis.IncludingVat, TestData.Now, 4);
        Assert.Equal(4, organization.PayrollRunsPerMonth);
        // Leaving it out keeps the current value.
        organization.UpdateSettings("Test Builders", "PHP", 1_200, "Asia/Manila", 8m, 12_500, VarianceBasis.IncludingVat, TestData.Now);
        Assert.Equal(4, organization.PayrollRunsPerMonth);
    }
}
