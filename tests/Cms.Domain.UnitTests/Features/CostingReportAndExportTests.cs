using System.Globalization;
using System.Text;
using Cms.Application.Features.Costing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;

namespace Cms.Domain.UnitTests.Features;

public sealed class CsvExportTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTabbed", "'\tTabbed")]
    [InlineData("\rReturn", "'\rReturn")]
    [InlineData("Plain text", "Plain text")]
    [InlineData("a=b", "a=b")]
    [InlineData("50% - off", "50% - off")]
    [InlineData("", "")]
    public void A_text_cell_that_a_spreadsheet_would_run_as_a_formula_is_made_plain_text(string value, string expected) =>
        Assert.Equal(expected, CsvExport.Text(value));

    [Fact]
    public void A_missing_text_is_an_empty_cell() => Assert.Equal("", CsvExport.Text(null));

    [Theory]
    [InlineData(0L, "0.00")]
    [InlineData(1L, "0.01")]
    [InlineData(5L, "0.05")]
    [InlineData(100L, "1.00")]
    [InlineData(125_050L, "1250.50")]
    [InlineData(-5L, "-0.05")]
    [InlineData(-100L, "-1.00")]
    [InlineData(-876_54L, "-876.54")]
    [InlineData(long.MaxValue, "92233720368547758.07")]
    [InlineData(long.MinValue, "-92233720368547758.08")]
    public void Money_is_minor_units_as_a_plain_decimal_with_two_places_and_a_negative_amount_is_not_treated_as_text(long minor, string expected) =>
        Assert.Equal(expected, CsvExport.Money(minor));

    [Theory]
    [InlineData(null, "")]
    [InlineData(1_200, "12.00")]
    [InlineData(8_830, "88.30")]
    [InlineData(10_000, "100.00")]
    [InlineData(0, "0.00")]
    public void A_percentage_is_basis_points_with_two_places(int? basisPoints, string expected) =>
        Assert.Equal(expected, CsvExport.Percent(basisPoints));

    [Fact]
    public void Numbers_and_dates_ignore_the_language_of_the_server()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "de-DE", "fr-FR", "ar-SA", "th-TH" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                Assert.Equal("1250.50", CsvExport.Money(125_050));
                Assert.Equal("2.5", CsvExport.Quantity(2.5m));
                Assert.Equal("10", CsvExport.Quantity(10.0000m));
                Assert.Equal("0.3333", CsvExport.Quantity(0.3333m));
                Assert.Equal("2026-11-02", CsvExport.Date(new DateOnly(2026, 11, 2)));
                Assert.Equal("Tower-budget-2026-11-02.csv", CsvExport.FileName("Tower", "budget", new DateOnly(2026, 11, 2)));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void A_document_starts_with_a_byte_order_mark_uses_crlf_and_quotes_only_what_needs_quoting()
    {
        var document = new CsvExport.Document("Name", "Note");
        document.Row("Plain", CsvExport.Text("=HYPERLINK(\"x\",\"y\")"));
        document.Row("Pipe, 2\" PVC", "line one\nline two");
        document.Row("", CsvExport.Money(-5));

        var bytes = document.ToArray();

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Equal("Name,Note\r\n" +
            "Plain,\"'=HYPERLINK(\"\"x\"\",\"\"y\"\")\"\r\n" +
            "\"Pipe, 2\"\" PVC\",\"line one\nline two\"\r\n" +
            ",-0.05\r\n", text);
    }

    [Fact]
    public void Accents_survive_as_utf8()
    {
        var document = new CsvExport.Document("Name");
        document.Row(CsvExport.Text("Biñan m³ ₱"));

        var bytes = document.ToArray();

        Assert.Equal("Name\r\nBiñan m³ ₱\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Theory]
    [InlineData("Tower A", "Tower-A")]
    [InlineData("Nueva Biñan / Block 2", "Nueva-Binan-Block-2")]
    [InlineData("  --Phase 1.5 (East)--  ", "Phase-1-5-East")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("C:\\Windows\\System32", "C-Windows-System32")]
    [InlineData("Evil\"\r\nContent-Type: text/html", "Evil-Content-Type-text-html")]
    [InlineData("***", "project")]
    [InlineData("", "project")]
    [InlineData(null, "project")]
    [InlineData("日本語", "project")]
    public void A_project_name_becomes_a_safe_file_name(string? projectName, string expected) =>
        Assert.Equal(expected, CsvExport.SafeNamePart(projectName));

    [Fact]
    public void A_very_long_project_name_is_cut_without_leaving_a_trailing_dash()
    {
        var part = CsvExport.SafeNamePart(new string('a', 59) + " bbbbbbbbbb");

        Assert.Equal(59, part.Length);
        Assert.Equal(new string('a', 59), part);
    }

    [Fact]
    public void The_file_name_carries_the_kind_and_the_iso_date() =>
        Assert.Equal("Tower-A-variance-2026-01-05.csv", CsvExport.FileName("Tower A", "variance", new DateOnly(2026, 1, 5)));
}

public sealed class CostReportBuilderTests
{
    private static readonly Guid PhaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static GroupedAmounts Wbs(int? division, string? code, string? subScope, long net, long gross) =>
        new(division, code, subScope, null, null, net, gross);

    private static GroupedAmounts Sub(Guid? id, long net, long gross) => new(null, null, null, id, null, net, gross);

    private static GroupedAmounts Type(ItemType type, long net, long gross) => new(null, null, null, null, type, net, gross);

    [Fact]
    public void The_dashboard_adds_up_the_margin_only_over_projects_that_have_a_contract_price()
    {
        var withPrice = new ProjectHeader(Guid.NewGuid(), "A", "Active", 600_000);
        var withoutPrice = new ProjectHeader(Guid.NewGuid(), "B", "Planning", null);

        var dashboard = CostReportBuilder.BuildDashboard(VarianceBasis.IncludingVat, [withPrice, withoutPrice],
            [new ProjectAmounts(withPrice.Id, 80_000, 100_000), new ProjectAmounts(withoutPrice.Id, 40_000, 50_000)],
            [new ProjectAmounts(withPrice.Id, 32_000, 40_000)]);

        Assert.Equal("IncludingVat", dashboard.Basis);
        // Project B has no actual cost at all: a project with no sums is zero, not missing.
        Assert.Equal((50_000L, 0L, 50_000L, (int?)0, "OnTrack", (long?)null, (long?)null),
            (dashboard.Projects[1].BudgetMinor, dashboard.Projects[1].ActualMinor, dashboard.Projects[1].RemainingMinor, dashboard.Projects[1].UsedBps,
                dashboard.Projects[1].Health, dashboard.Projects[1].MarginAfterActualMinor, dashboard.Projects[1].BudgetMarginMinor));
        Assert.Equal(new DashboardTotalsDto(2, 600_000, 150_000, 40_000, 110_000, 2_667, "OnTrack", 560_000, 500_000), dashboard.Totals);
    }

    [Fact]
    public void Open_commitments_give_a_forecast_that_the_dashboard_and_the_summary_judge_the_same_way()
    {
        var project = new ProjectHeader(Guid.NewGuid(), "Tower", "Active", 900_000);
        var other = new ProjectHeader(Guid.NewGuid(), "Quiet", "Active", null);
        var budgets = new[] { new ProjectAmounts(project.Id, 80_000, 100_000), new ProjectAmounts(other.Id, 8_000, 10_000) };
        var actuals = new[] { new ProjectAmounts(project.Id, 56_000, 70_000) };
        // 25,000 is promised and not yet invoiced: actual 70,000 + 25,000 = a forecast of 95,000 against a budget of 100,000.
        var commitments = new[] { new ProjectAmounts(project.Id, 20_000, 25_000) };

        var dashboard = CostReportBuilder.BuildDashboard(VarianceBasis.IncludingVat, [project, other], budgets, actuals, commitments);

        // Today it is on track (70% used); counting what is promised, it is near its limit.
        Assert.Equal("OnTrack", dashboard.Projects[0].Health);
        Assert.Equal(new CommitmentPositionDto(25_000, 95_000, 5_000, "NearLimit"), dashboard.Projects[0].Commitments);
        // A project with nothing committed carries no extra figures at all.
        Assert.Null(dashboard.Projects[1].Commitments);
        Assert.Equal(new CommitmentPositionDto(25_000, 95_000, 15_000, "OnTrack"), dashboard.Totals.Commitments);

        var summary = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string>(), VarianceBasis.IncludingVat,
            new GroupedSums([], [], [Type(ItemType.Material, 80_000, 100_000)]), new GroupedSums([], [], [Type(ItemType.Material, 56_000, 70_000)]),
            new MeasuredAmounts(20_000, 25_000));
        Assert.Equal(dashboard.Projects[0].Commitments, summary.Commitments);
    }

    [Fact]
    public void Commitments_follow_the_comparison_basis_and_a_forecast_over_budget_says_so()
    {
        var project = new ProjectHeader(Guid.NewGuid(), "Tower", "Active", null);
        var summary = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string>(), VarianceBasis.ExcludingVat,
            new GroupedSums([], [], [Type(ItemType.Material, 80_000, 100_000)]), new GroupedSums([], [], [Type(ItemType.Material, 56_000, 70_000)]),
            new MeasuredAmounts(30_000, 37_500));

        // Without VAT: actual 56,000 + committed 30,000 = 86,000 against 80,000, so over budget by 6,000.
        Assert.Equal(new CommitmentPositionDto(30_000, 86_000, -6_000, "OverBudget"), summary.Commitments);
        Assert.Equal("OnTrack", summary.Position.Health);
    }

    [Fact]
    public void Nothing_committed_leaves_the_commitment_figures_out()
    {
        Assert.Null(CostReportBuilder.Forecast(new BudgetPosition(100_000, 10_000), 0));
        Assert.Null(CostReportBuilder.Forecast(new BudgetPosition(100_000, 10_000), -5));
    }

    [Fact]
    public void The_dashboard_totals_are_no_activity_when_there_are_no_projects()
    {
        var dashboard = CostReportBuilder.BuildDashboard(VarianceBasis.ExcludingVat, [], [], []);

        Assert.Equal("ExcludingVat", dashboard.Basis);
        Assert.Empty(dashboard.Projects);
        Assert.Equal(new DashboardTotalsDto(0, null, 0, 0, 0, null, "NoActivity", null, null), dashboard.Totals);
    }

    [Fact]
    public void The_summary_picks_net_or_gross_by_the_basis_and_keeps_the_margin_on_gross()
    {
        var project = new ProjectHeader(Guid.NewGuid(), "Basis", "Active", 500_000);
        GroupedSums Budget() => new([Wbs(3, "3300", "Cast-In-Place Concrete", 89_286, 100_000)], [Sub(null, 89_286, 100_000)], [Type(ItemType.Material, 89_286, 100_000)]);
        GroupedSums Actual() => new([Wbs(3, "3300", "Cast-In-Place Concrete", 93_750, 105_000)], [Sub(null, 93_750, 105_000)], [Type(ItemType.Material, 93_750, 105_000)]);

        var gross = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string>(), VarianceBasis.IncludingVat, Budget(), Actual());
        var net = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string>(), VarianceBasis.ExcludingVat, Budget(), Actual());

        Assert.Equal(new BudgetPositionDto(100_000, 105_000, -5_000, 10_500, "OverBudget"), gross.Position);
        Assert.Equal(new BudgetPositionDto(89_286, 93_750, -4_464, 10_500, "OverBudget"), net.Position);
        // 500,000 - 105,000 and 500,000 - 100,000 whichever basis.
        Assert.Equal((395_000L, 400_000L), (gross.MarginAfterActualMinor!.Value, gross.BudgetMarginMinor!.Value));
        Assert.Equal((gross.MarginAfterActualMinor, gross.BudgetMarginMinor), (net.MarginAfterActualMinor, net.BudgetMarginMinor));
    }

    [Fact]
    public void Rows_are_sorted_with_the_biggest_overrun_first_and_unbudgeted_and_unclassified_rows_are_named()
    {
        var project = new ProjectHeader(Guid.NewGuid(), "Order", "Active", null);
        var budget = new GroupedSums(
            [Wbs(3, "3300", "Cast-In-Place Concrete", 100, 100), Wbs(3, "3200", "Concrete Reinforcement", 100, 100), Wbs(null, null, null, 20, 20)],
            [Sub(null, 200, 200), Sub(PhaseId, 20, 20)],
            [Type(ItemType.Material, 200, 200), Type(ItemType.Labor, 20, 20)]);
        var actual = new GroupedSums(
            [Wbs(3, "3300", "Cast-In-Place Concrete", 50, 50), Wbs(3, "3200", "Concrete Reinforcement", 130, 130), Wbs(3, "3500", "Cementitious Decks and Underlayment", 5, 5),
                Wbs(null, null, null, 30, 30)],
            [Sub(null, 185, 185), Sub(PhaseId, 30, 30)],
            [Type(ItemType.Material, 185, 185), Type(ItemType.Labor, 30, 30)]);

        var summary = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string> { [PhaseId] = "Phase 1" }, VarianceBasis.IncludingVat, budget, actual);

        Assert.Null(summary.MarginAfterActualMinor);
        // Remaining: Unclassified 20 - 30 = -10; Division 3 = 200 - 185 = 15.
        Assert.Equal([null, 3], summary.ByDivision.Select(row => row.Division));
        Assert.Equal(["Unclassified", "Concrete"], summary.ByDivision.Select(row => row.Name));
        // Inside division 3: 3200 is 30 over, 3500 has no budget (-5), 3300 has 50 left.
        var wbs = summary.ByDivision[1].Wbs;
        Assert.Equal(["3200", "3500", "3300"], wbs.Select(row => row.WbsCode));
        Assert.Equal(["OverBudget", "Unbudgeted", "OnTrack"], wbs.Select(row => row.Health));
        Assert.Equal(-30, wbs[0].RemainingMinor);
        Assert.Null(wbs[1].UsedBps);
        Assert.Equal("Cementitious Decks and Underlayment", wbs[1].Name);
        Assert.Equal(["Phase 1", "Whole project"], summary.BySubProject.Select(row => row.Name));
        Assert.Equal([PhaseId, null], summary.BySubProject.Select(row => row.SubProjectId));
        Assert.Equal(["Labor", "Material"], summary.ByItemType.Select(row => row.ItemType));
    }

    [Fact]
    public void A_custom_code_uses_the_name_stored_on_its_lines_and_a_standard_code_uses_the_catalog_name()
    {
        var project = new ProjectHeader(Guid.NewGuid(), "Names", "Active", null);
        var actual = new GroupedSums([Wbs(3, "3X-1", "Special slab", 10, 10), Wbs(3, "3300", "Some typo", 10, 10)], [Sub(null, 20, 20)], [Type(ItemType.Other, 20, 20)]);

        var summary = CostReportBuilder.BuildSummary(project, new Dictionary<Guid, string>(), VarianceBasis.IncludingVat, GroupedSums.Empty, actual);

        var names = summary.ByDivision.Single().Wbs.ToDictionary(row => row.WbsCode!, row => row.Name);
        Assert.Equal("Special slab", names["3X-1"]);
        Assert.Equal("Cast-In-Place Concrete", names["3300"]);
        Assert.All(summary.ByDivision.Single().Wbs, row => Assert.Equal("Unbudgeted", row.Health));
    }
}
