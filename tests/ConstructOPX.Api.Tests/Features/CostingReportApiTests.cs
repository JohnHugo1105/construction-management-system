using System.Net;
using System.Text.RegularExpressions;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.PurchasingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The reports, checked against small scenarios worked out by hand. Every figure below is in minor units (centavos): 100,000 is 1,000.00.
/// All the scenario lines have no VAT unless a comment says otherwise, so net equals gross and the comparison basis makes no difference.
/// </summary>
public sealed class CostingReportApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public CostingReportApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private Task<(ApiClient Client, SeededUser User)> AdminAsync() => SignedInAsync(_factory);

    private static Task<BudgetLineDto> AddLine(ApiClient client, Guid projectId, string description, string itemType, long amount, string? wbsCode = null,
        Guid? subProjectId = null, string vat = "Exempt") =>
        client.PostAsync<BudgetLineDto>($"/api/projects/{projectId}/budget-lines",
            Line(description, itemType, unitCostMinor: amount, wbsCode: wbsCode, subProjectId: subProjectId, vatTreatment: vat));

    private static Task<CostEntryDto> AddCost(ApiClient client, Guid projectId, string description, string itemType, long amount, string? wbsCode = null,
        Guid? subProjectId = null, string vat = "Exempt") =>
        client.PostAsync<CostEntryDto>($"/api/projects/{projectId}/cost-entries",
            Cost(description, itemType, unitCostMinor: amount, wbsCode: wbsCode, subProjectId: subProjectId, vatTreatment: vat));

    /// <summary>
    /// The worked scenario. Budget (no VAT): forms 3100 = 100,000; reinforcement 3200 = 100,000; cast-in-place 3300 = 100,000; precast 3400 =
    /// 100,000 (all Material); grouts 3600 = 50,000 (Equipment); an unclassified Labor line of 20,000 in sub-project Phase 1.
    /// Total budget = 4 x 100,000 + 50,000 + 20,000 = 470,000. A removed line of 200,000 must not count.
    /// Actual (no VAT): 3100 = 89,999; 3200 = 90,000; 3300 = 100,000; 3400 = 100,001; 3500 = 5,000 (Subcontract, never budgeted); an
    /// unclassified Labor cost of 30,000 in Phase 1. Total actual = 89,999 + 90,000 + 100,000 + 100,001 + 5,000 + 30,000 = 415,000.
    /// A voided cost of 777,000 must not count.
    /// </summary>
    private static async Task<(ProjectDto Project, SubProjectDto Phase)> BuildVarianceScenarioAsync(ApiClient client, string name = "Variance Tower")
    {
        var project = await CreateProjectAsync(client, name, contractPriceMinor: 600_000);
        var phase = (await AddSubProjectAsync(client, project, "Phase 1")).SubProjects.Single();

        foreach (var code in new[] { "3100", "3200", "3300", "3400" }) await AddLine(client, project.Id, $"Budget {code}", "Material", 100_000, code);
        await AddLine(client, project.Id, "Budget 3600", "Equipment", 50_000, "3600");
        await AddLine(client, project.Id, "Budget unclassified", "Labor", 20_000, subProjectId: phase.Id);

        await AddCost(client, project.Id, "Cost 3100", "Material", 89_999, "3100");
        await AddCost(client, project.Id, "Cost 3200", "Material", 90_000, "3200");
        await AddCost(client, project.Id, "Cost 3300", "Material", 100_000, "3300");
        await AddCost(client, project.Id, "Cost 3400", "Material", 100_001, "3400");
        await AddCost(client, project.Id, "Cost 3500", "Subcontract", 5_000, "3500");
        await AddCost(client, project.Id, "Cost unclassified", "Labor", 30_000, subProjectId: phase.Id);

        // Noise that must never count.
        var removed = await AddLine(client, project.Id, "Removed estimate", "Material", 200_000, "3100");
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{removed.Id}/remove", new { reason = "Superseded", version = removed.Version });
        var voided = await AddCost(client, project.Id, "Voided cost", "Material", 777_000, "3100");
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{voided.Id}/void", new { reason = "Wrong project", version = voided.Version });
        return (project, phase);
    }

    [SqlServerFact]
    public async Task A_projects_dashboard_row_is_the_same_answer_as_its_cost_summary_on_either_basis()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, _) = await BuildVarianceScenarioAsync(client);

        foreach (var basis in new[] { "IncludingVat", "ExcludingVat" })
        {
            await ChangeOrganizationAsync(client, varianceBasis: basis);
            var row = (await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Single(item => item.Id == project.Id);
            var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");

            // Two places answering one question must never disagree: the dashboard row and the project page come from one calculation.
            Assert.Equal(summary.Basis, basis);
            Assert.Equal(summary.Position.BudgetMinor, row.BudgetMinor);
            Assert.Equal(summary.Position.ActualMinor, row.ActualMinor);
            Assert.Equal(summary.Position.RemainingMinor, row.RemainingMinor);
            Assert.Equal(summary.Position.UsedBps, row.UsedBps);
            Assert.Equal(summary.Position.Health, row.Health);
            Assert.Equal(summary.MarginAfterActualMinor, row.MarginAfterActualMinor);
            Assert.Equal(summary.BudgetMarginMinor, row.BudgetMarginMinor);
            Assert.Equal(summary.ContractPriceMinor, row.ContractPriceMinor);
        }
    }

    [SqlServerFact]
    public async Task The_cost_summary_matches_a_worked_scenario_at_every_health_threshold()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, phase) = await BuildVarianceScenarioAsync(client);

        var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");

        Assert.Equal(project.Id, summary.ProjectId);
        Assert.Equal("Variance Tower", summary.ProjectName);
        Assert.Equal("IncludingVat", summary.Basis);

        // Project: budget 470,000; actual 415,000; remaining 55,000; used 415,000 / 470,000 = 88.30% (8,830 bps); under 90%, so on track.
        Assert.Equal(new BudgetPositionDto(470_000, 415_000, 55_000, 8_830, "OnTrack"), summary.Position);
        // Margin uses the VAT-inclusive contract price of 600,000: 600,000 - 415,000 = 185,000 now, 600,000 - 470,000 = 130,000 on budget.
        Assert.Equal(600_000, summary.ContractPriceMinor);
        Assert.Equal(185_000, summary.MarginAfterActualMinor);
        Assert.Equal(130_000, summary.BudgetMarginMinor);

        // By division, worst first. "Unclassified" (budget 20,000, actual 30,000, remaining -10,000, 150.00%) is over budget.
        // Division 3 holds the rest: budget 450,000, actual 385,000, remaining 65,000, 385,000 / 450,000 = 85.56%.
        Assert.Equal(2, summary.ByDivision.Count);
        var unclassified = summary.ByDivision[0];
        Assert.Equal((null, "Unclassified", 20_000L, 30_000L, -10_000L, (int?)15_000, "OverBudget"),
            (unclassified.Division, unclassified.Name, unclassified.BudgetMinor, unclassified.ActualMinor, unclassified.RemainingMinor, unclassified.UsedBps, unclassified.Health));
        Assert.Empty(unclassified.Wbs);
        var concrete = summary.ByDivision[1];
        Assert.Equal((3, "Concrete", 450_000L, 385_000L, 65_000L, (int?)8_556, "OnTrack"),
            ((int?)concrete.Division, concrete.Name, concrete.BudgetMinor, concrete.ActualMinor, concrete.RemainingMinor, concrete.UsedBps, concrete.Health));

        // Work-breakdown rows, biggest overrun first. Each sits on one side of a threshold:
        //   3500: nothing budgeted, 5,000 spent       -> Unbudgeted, remaining -5,000, no percentage
        //   3400: 100,001 of 100,000 = 100.001%       -> OverBudget (a centavo over), remaining -1, 10,000 bps once rounded
        //   3300: 100,000 of 100,000 = exactly 100%   -> NearLimit (at the limit, not over)
        //   3200: 90,000 of 100,000 = exactly 90%     -> NearLimit (the threshold itself)
        //   3100: 89,999 of 100,000 = 89.999%         -> OnTrack (rounds to 9,000 bps but is under the threshold)
        //   3600: 0 of 50,000                         -> OnTrack, 0%
        var wbs = concrete.Wbs.Select(row => (row.WbsCode, row.Name, row.BudgetMinor, row.ActualMinor, row.RemainingMinor, row.UsedBps, row.Health)).ToArray();
        Assert.Equal(new (string?, string, long, long, long, int?, string)[]
        {
            ("3500", "Cementitious Decks and Underlayment", 0, 5_000, -5_000, null, "Unbudgeted"),
            ("3400", "Precast Concrete", 100_000, 100_001, -1, 10_000, "OverBudget"),
            ("3300", "Cast-In-Place Concrete", 100_000, 100_000, 0, 10_000, "NearLimit"),
            ("3200", "Concrete Reinforcement", 100_000, 90_000, 10_000, 9_000, "NearLimit"),
            ("3100", "Concrete Forms and Accessories", 100_000, 89_999, 10_001, 9_000, "OnTrack"),
            ("3600", "Grouts", 50_000, 0, 50_000, 0, "OnTrack")
        }, wbs);

        // By sub-project: Phase 1 is over budget; the rest of the project is not.
        var subs = summary.BySubProject.Select(row => (row.SubProjectId, row.Name, row.BudgetMinor, row.ActualMinor, row.RemainingMinor, row.UsedBps, row.Health)).ToArray();
        Assert.Equal(new (Guid?, string, long, long, long, int?, string)[]
        {
            (phase.Id, "Phase 1", 20_000, 30_000, -10_000, 15_000, "OverBudget"),
            (null, "Whole project", 450_000, 385_000, 65_000, 8_556, "OnTrack")
        }, subs);

        // By item type. Labor 30,000 of 20,000; Subcontract 5,000 of nothing; Material 380,000 of 400,000 = 95% (near limit); Equipment 0 of 50,000.
        var types = summary.ByItemType.Select(row => (row.ItemType, row.Label, row.BudgetMinor, row.ActualMinor, row.RemainingMinor, row.UsedBps, row.Health)).ToArray();
        Assert.Equal(new (string, string, long, long, long, int?, string)[]
        {
            ("Labor", "Labor", 20_000, 30_000, -10_000, 15_000, "OverBudget"),
            ("Subcontract", "Subcontract work", 0, 5_000, -5_000, null, "Unbudgeted"),
            ("Material", "Material", 400_000, 380_000, 20_000, 9_500, "NearLimit"),
            ("Equipment", "Equipment", 50_000, 0, 50_000, 0, "OnTrack")
        }, types);

        // Every breakdown adds up to the same project totals.
        Assert.Equal(470_000, summary.ByDivision.Sum(row => row.BudgetMinor));
        Assert.Equal(415_000, summary.ByDivision.Sum(row => row.ActualMinor));
        Assert.Equal(470_000, summary.BySubProject.Sum(row => row.BudgetMinor));
        Assert.Equal(415_000, summary.ByItemType.Sum(row => row.ActualMinor));
    }

    [SqlServerFact]
    public async Task Removed_lines_and_voided_costs_never_count_and_a_project_without_lines_reports_no_activity()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var empty = await CreateProjectAsync(client, "Empty", contractPriceMinor: null);

        var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{empty.Id}/cost-summary");
        Assert.Equal(new BudgetPositionDto(0, 0, 0, null, "NoActivity"), summary.Position);
        Assert.Null(summary.ContractPriceMinor);
        Assert.Null(summary.MarginAfterActualMinor);
        Assert.Null(summary.BudgetMarginMinor);
        Assert.Empty(summary.ByDivision);
        Assert.Empty(summary.BySubProject);
        Assert.Empty(summary.ByItemType);

        // Everything is removed or voided again: the project is still at zero.
        var line = await AddLine(client, empty.Id, "Estimate", "Material", 100_000, "3300");
        var cost = await AddCost(client, empty.Id, "Invoice", "Material", 50_000, "3300");
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{line.Id}/remove", new { reason = "Mistake", version = line.Version });
        await client.PostAsync<CostEntryDto>($"/api/cost-entries/{cost.Id}/void", new { reason = "Mistake", version = cost.Version });
        var after = await client.GetAsync<CostSummaryDto>($"/api/projects/{empty.Id}/cost-summary");
        Assert.Equal(new BudgetPositionDto(0, 0, 0, null, "NoActivity"), after.Position);
        Assert.Empty(after.ByDivision);
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal((0L, 0L), (dashboard.Projects.Single().BudgetMinor, dashboard.Projects.Single().ActualMinor));
    }

    [SqlServerFact]
    public async Task The_company_basis_decides_whether_budget_and_actual_are_compared_with_or_without_vat()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        // Budget 100,000 with no VAT. Actual: one invoice of 105,000 with 12% VAT included, so net = 105,000 / 1.12 = 93,750 and VAT = 11,250.
        var project = await CreateProjectAsync(client, "Basis Check", contractPriceMinor: 500_000);
        await AddLine(client, project.Id, "Budget", "Material", 100_000, "3300");
        await AddCost(client, project.Id, "Invoice", "Material", 105_000, "3300", vat: "Inclusive");
        var url = $"/api/projects/{project.Id}/cost-summary";

        // Including VAT: 105,000 against 100,000 is 105%, over budget by 5,000.
        var gross = await client.GetAsync<CostSummaryDto>(url);
        Assert.Equal("IncludingVat", gross.Basis);
        Assert.Equal(new BudgetPositionDto(100_000, 105_000, -5_000, 10_500, "OverBudget"), gross.Position);
        Assert.Equal(("3300", 100_000L, 105_000L, -5_000L, "OverBudget"),
            (gross.ByDivision.Single().Wbs.Single().WbsCode, gross.ByDivision.Single().Wbs.Single().BudgetMinor, gross.ByDivision.Single().Wbs.Single().ActualMinor,
                gross.ByDivision.Single().Wbs.Single().RemainingMinor, gross.ByDivision.Single().Wbs.Single().Health));
        var grossRow = (await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Single();
        Assert.Equal((100_000L, 105_000L, -5_000L, (int?)10_500, "OverBudget"), (grossRow.BudgetMinor, grossRow.ActualMinor, grossRow.RemainingMinor, grossRow.UsedBps, grossRow.Health));

        await ChangeOrganizationAsync(client, varianceBasis: "ExcludingVat");

        // Excluding VAT: 93,750 against 100,000 is 93.75%, 6,250 left, near the limit but not over.
        var net = await client.GetAsync<CostSummaryDto>(url);
        Assert.Equal("ExcludingVat", net.Basis);
        Assert.Equal(new BudgetPositionDto(100_000, 93_750, 6_250, 9_375, "NearLimit"), net.Position);
        var netWbs = net.ByDivision.Single().Wbs.Single();
        Assert.Equal((100_000L, 93_750L, 6_250L, (int?)9_375, "NearLimit"), (netWbs.BudgetMinor, netWbs.ActualMinor, netWbs.RemainingMinor, netWbs.UsedBps, netWbs.Health));
        Assert.Equal(93_750, net.ByItemType.Single().ActualMinor);
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal("ExcludingVat", dashboard.Basis);
        var netRow = dashboard.Projects.Single();
        Assert.Equal((100_000L, 93_750L, 6_250L, (int?)9_375, "NearLimit"), (netRow.BudgetMinor, netRow.ActualMinor, netRow.RemainingMinor, netRow.UsedBps, netRow.Health));

        // The margin is always on VAT-inclusive amounts (the contract price includes VAT), whichever basis is chosen:
        // 500,000 - 105,000 = 395,000 after the actual cost, 500,000 - 100,000 = 400,000 on budget.
        foreach (var margins in new[] { (gross.MarginAfterActualMinor, gross.BudgetMarginMinor), (net.MarginAfterActualMinor, net.BudgetMarginMinor),
                     (netRow.MarginAfterActualMinor, netRow.BudgetMarginMinor) })
            Assert.Equal(((long?)395_000, (long?)400_000), margins);
    }

    [SqlServerFact]
    public async Task The_dashboard_has_one_row_per_project_with_margins_and_an_overall_row()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;

        // A Tower: contract price 600,000. Budget 100,000, actual 40,000.
        var a = await CreateProjectAsync(client, "A Tower", contractPriceMinor: 600_000);
        a = await client.PostAsync<ProjectDto>($"/api/projects/{a.Id}/start", new { version = a.Version });
        await AddLine(client, a.Id, "Budget", "Material", 100_000, "3300");
        await AddCost(client, a.Id, "Invoice", "Material", 40_000, "3300");
        // B Villas: no contract price. Budget 50,000, actual 60,000.
        var b = await CreateProjectAsync(client, "B Villas", contractPriceMinor: null);
        await AddLine(client, b.Id, "Budget", "Material", 50_000, "3300");
        await AddCost(client, b.Id, "Invoice", "Material", 60_000, "3300");
        // C Clinic: contract price 300,000, nothing entered yet.
        var c = await CreateProjectAsync(client, "C Clinic", contractPriceMinor: 300_000);
        // D Annex: archived. Contract price 100,000, budget 10,000, nothing spent.
        var d = await CreateProjectAsync(client, "D Annex", contractPriceMinor: 100_000);
        await AddLine(client, d.Id, "Budget", "Material", 10_000, "3300");
        await ArchiveAsync(client, d);

        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");

        Assert.Equal("IncludingVat", dashboard.Basis);
        Assert.Equal(["A Tower", "B Villas", "C Clinic"], dashboard.Projects.Select(row => row.Name));
        var rows = dashboard.Projects.Select(row => (row.Id, row.Status, row.ContractPriceMinor, row.BudgetMinor, row.ActualMinor, row.RemainingMinor, row.UsedBps,
            row.Health, row.MarginAfterActualMinor, row.BudgetMarginMinor)).ToArray();
        Assert.Equal(new (Guid, string, long?, long, long, long, int?, string, long?, long?)[]
        {
            // 40,000 of 100,000 = 40%; margin 600,000 - 40,000 = 560,000, on budget 600,000 - 100,000 = 500,000.
            (a.Id, "Active", 600_000, 100_000, 40_000, 60_000, 4_000, "OnTrack", 560_000, 500_000),
            // 60,000 of 50,000 = 120%, over by 10,000. No contract price, so no margin.
            (b.Id, "Planning", null, 50_000, 60_000, -10_000, 12_000, "OverBudget", null, null),
            // Nothing budgeted or spent. The whole contract price is margin so far.
            (c.Id, "Planning", 300_000, 0, 0, 0, null, "NoActivity", 300_000, 300_000)
        }, rows);

        // Overall: budget 150,000, actual 100,000, remaining 50,000, 66.67%. The price and margins add up only projects that have a price (A and C).
        Assert.Equal(new DashboardTotalsDto(3, 900_000, 150_000, 100_000, 50_000, 6_667, "OnTrack", 860_000, 800_000), dashboard.Totals);

        // With archived projects: D adds budget 10,000 and a contract price of 100,000 (margin 100,000 now, 90,000 on budget).
        var all = await client.GetAsync<DashboardDto>("/api/reports/dashboard?includeArchived=true");
        Assert.Equal(["A Tower", "B Villas", "C Clinic", "D Annex"], all.Projects.Select(row => row.Name));
        Assert.Equal("Archived", all.Projects[3].Status);
        Assert.Equal(new DashboardTotalsDto(4, 1_000_000, 160_000, 100_000, 60_000, 6_250, "OnTrack", 960_000, 890_000), all.Totals);

        // The dashboard and each project page come from the same calculation.
        var summary = await client.GetAsync<CostSummaryDto>($"/api/projects/{a.Id}/cost-summary");
        Assert.Equal(dashboard.Projects[0].BudgetMinor, summary.Position.BudgetMinor);
        Assert.Equal(dashboard.Projects[0].ActualMinor, summary.Position.ActualMinor);
        Assert.Equal(dashboard.Projects[0].MarginAfterActualMinor, summary.MarginAfterActualMinor);
    }

    [SqlServerFact]
    public async Task The_dashboard_costs_the_same_number_of_statements_for_two_projects_as_for_twelve()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        for (var index = 0; index < 2; index++) await MakeProjectWithActivityAsync(client, $"Small {index}");
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode));
        Assert.Equal(2, (await client.GetAsync<DashboardDto>("/api/reports/dashboard")).Projects.Count);

        for (var index = 0; index < 10; index++) await MakeProjectWithActivityAsync(client, $"Large {index}");
        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode));
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(12, dashboard.Projects.Count);
        Assert.Equal(12 * 150_000, dashboard.Totals.BudgetMinor);

        Assert.Equal(small, large);
        // The company, the projects, the budget sums, the actual sums, and one grouped statement for each commitment source.
        Assert.Equal(4 + _factory.CommitmentSourceCount(), large);
    }

    [SqlServerFact]
    public async Task The_cost_summary_costs_the_same_number_of_statements_whatever_the_number_of_lines()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var url = $"/api/projects/{project.Id}/cost-summary";
        await AddLine(client, project.Id, "One", "Material", 10_000, "3300");
        await AddCost(client, project.Id, "One", "Material", 5_000, "3300");
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        var phase = (await AddSubProjectAsync(client, project, "Phase 1")).SubProjects.Single();
        var codes = new[] { "3100", "3200", "3300", "4200", "5100", "9300", "15100" };
        var sevenCodes = codes.Length;
        var types = new[] { "Material", "Labor", "Equipment", "Subcontract", "Other" };
        for (var index = 0; index < 40; index++)
        {
            await AddLine(client, project.Id, $"Line {index}", types[index % types.Length], 1_000 + index, codes[index % codes.Length], index % 3 == 0 ? phase.Id : null);
            await AddCost(client, project.Id, $"Cost {index}", types[index % types.Length], 500 + index, codes[(index + 1) % codes.Length], index % 4 == 0 ? phase.Id : null);
        }

        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));
        var summary = await client.GetAsync<CostSummaryDto>(url);
        // Divisions 3, 4, 5, 9 and 15, every line classified.
        Assert.Equal([3, 4, 5, 9, 15], summary.ByDivision.Select(row => row.Division!.Value).Order());
        Assert.Equal(sevenCodes, summary.ByDivision.Sum(row => row.Wbs.Count));

        Assert.Equal(small, large);
        // The project, the company, three grouped sums each for budget and actual, and one grouped statement for each commitment source.
        Assert.Equal(8 + _factory.CommitmentSourceCount(), large);
    }

    [SqlServerFact]
    public async Task Reports_are_for_people_who_can_read_costs_and_never_show_another_companys_projects()
    {
        var (admin, adminUser) = await AdminAsync();
        using var _a = admin;
        var (project, _) = await BuildVarianceScenarioAsync(admin);

        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/projects/{project.Id}/cost-summary")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/projects/{project.Id}/cost-summary/export")).StatusCode);
        }

        foreach (var role in new[] { AccessRole.Viewer, AccessRole.ProjectManager, AccessRole.Accountant })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _r = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{project.Id}/cost-summary")).StatusCode);
        }

        var (theirs, _) = await AdminAsync();
        using var _t = theirs;
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/cost-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/projects/{project.Id}/cost-summary/export")).StatusCode);
        var theirDashboard = await theirs.GetAsync<DashboardDto>("/api/reports/dashboard?includeArchived=true");
        Assert.Empty(theirDashboard.Projects);
        Assert.Equal(new DashboardTotalsDto(0, null, 0, 0, 0, null, "NoActivity", null, null), theirDashboard.Totals);

        // Their own busy project does not leak into mine.
        await MakeProjectWithActivityAsync(theirs, "Theirs");
        var mine = await admin.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(["Variance Tower"], mine.Projects.Select(row => row.Name));
        Assert.Equal(470_000, mine.Totals.BudgetMinor);
    }

    [SqlServerFact]
    public async Task The_variance_export_lists_each_work_breakdown_code_with_plain_decimals_and_the_unclassified_amounts_last()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, _) = await BuildVarianceScenarioAsync(client);

        var file = await DownloadCsvAsync(client, $"/api/projects/{project.Id}/cost-summary/export");

        Assert.Equal("text/csv; charset=utf-8", file.Response.Content.Headers.ContentType?.ToString());
        Assert.Matches(new Regex(@"^Variance-Tower-variance-\d{4}-\d{2}-\d{2}\.csv$"), FileNameOf(file.Response));
        Assert.Equal(["Division", "Main scope", "WBS code", "Work item", "Budget", "Actual", "Remaining", "Used %", "Status", "Compared on"], file.Header);

        // Division and code order, then the unclassified amounts. Amounts are minor units / 100 with two places.
        var rows = file.Rows.Skip(1).Select(row => string.Join('|', row)).ToArray();
        Assert.Equal(new[]
        {
            "3|Concrete|3100|Concrete Forms and Accessories|1000.00|899.99|100.01|90.00|On track|With VAT",
            "3|Concrete|3200|Concrete Reinforcement|1000.00|900.00|100.00|90.00|Near limit|With VAT",
            "3|Concrete|3300|Cast-In-Place Concrete|1000.00|1000.00|0.00|100.00|Near limit|With VAT",
            "3|Concrete|3400|Precast Concrete|1000.00|1000.01|-0.01|100.00|Over budget|With VAT",
            "3|Concrete|3500|Cementitious Decks and Underlayment|0.00|50.00|-50.00||Not budgeted|With VAT",
            "3|Concrete|3600|Grouts|500.00|0.00|500.00|0.00|On track|With VAT",
            "|Unclassified|||200.00|300.00|-100.00|150.00|Over budget|With VAT"
        }, rows);

        // A negative remaining is a number, not text: it is not given the injection apostrophe.
        Assert.DoesNotContain(file.Rows.Skip(1).SelectMany(row => row), cell => cell.StartsWith('\''));

        await ChangeOrganizationAsync(client, varianceBasis: "ExcludingVat");
        var net = await DownloadCsvAsync(client, $"/api/projects/{project.Id}/cost-summary/export");
        Assert.All(net.Rows.Skip(1), row => Assert.Equal("Without VAT", row[^1]));
    }

    [SqlServerFact]
    public async Task The_variance_export_protects_custom_work_item_names_from_formula_injection()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await CreateProjectAsync(client, "Custom Codes");
        // A custom code names its own work item. Division 3, so it groups with the standard concrete codes.
        await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines",
            Line("Special slab", wbsCode: "3X-1", division: 3, subScope: "=HYPERLINK(\"http://evil.example\",\"Open\")", unitCostMinor: 12_345));
        await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries",
            Cost("Special slab", wbsCode: "3X-1", division: 3, subScope: "=HYPERLINK(\"http://evil.example\",\"Open\")", unitCostMinor: 99_999));

        var file = await DownloadCsvAsync(client, $"/api/projects/{project.Id}/cost-summary/export");

        var row = Assert.Single(file.Rows.Skip(1));
        Assert.Equal("3", file.Cell(1, "Division"));
        Assert.Equal("3X-1", file.Cell(1, "WBS code"));
        Assert.Equal("'=HYPERLINK(\"http://evil.example\",\"Open\")", file.Cell(1, "Work item"));
        Assert.Equal("123.45", file.Cell(1, "Budget"));
        Assert.Equal("999.99", file.Cell(1, "Actual"));
        Assert.Equal("-876.54", file.Cell(1, "Remaining"));
        Assert.Equal("Over budget", row[Array.IndexOf(file.Header, "Status")]);
    }

    private static async Task MakeProjectWithActivityAsync(ApiClient client, string name)
    {
        // Budget 150,000 (100,000 + 50,000) and actual 70,000 (40,000 + 30,000), over two codes and two item types.
        var project = await CreateProjectAsync(client, name);
        await AddLine(client, project.Id, "Concrete", "Material", 100_000, "3300");
        await AddLine(client, project.Id, "Crew", "Labor", 50_000);
        await AddCost(client, project.Id, "Concrete invoice", "Material", 40_000, "3300");
        await AddCost(client, project.Id, "Crew payroll", "Labor", 30_000);
    }
}
