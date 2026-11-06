using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// Progress and earned value on the real API and a real SQL Server. Amounts are centavos; every line below has no VAT unless a comment says
/// otherwise, so net equals gross. The worked project starts 1 Jan 2026 and ends 11 Apr 2026 (100 days).
/// </summary>
public sealed class ProgressApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private const string Start = "2026-01-01";
    private const string End = "2026-04-11";
    private const string Feb10 = "2026-02-10"; // 40 of the 100 planned days gone

    private readonly ConstructOpxApiFactory _factory;

    public ProgressApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    private Task<(ApiClient Client, SeededUser User)> AdminAsync() => SignedInAsync(_factory);

    // The company's calendar day (Asia/Manila by default), which is what the API reads as "today". The UTC day is a day behind it
    // from 16:00 UTC, so a test that compared against the UTC day failed whenever it ran in that window.
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(ConstructOPX.Domain.Features.Organizations.Organization.DefaultTimeZoneId)).DateTime);

    private static Task<ProjectDto> NewProjectAsync(ApiClient client, long? price = 3_000_000, string? end = End, string? name = null) =>
        client.PostAsync<ProjectDto>("/api/projects", new
        {
            name = name ?? TenantSeeder.Unique("Progress"), client = "ACME Realty", location = "Cebu City", contractPriceMinor = price, startDate = Start,
            targetEndDate = end
        });

    /// <summary>A budget line with no VAT unless asked. The standard code "3100" puts it in division 3 (Concrete); no code leaves it unclassified.</summary>
    private static Task<BudgetLineDto> AddLine(ApiClient client, Guid projectId, string description, long amount, string? wbsCode = "3100", string vat = "Exempt") =>
        client.PostAsync<BudgetLineDto>($"/api/projects/{projectId}/budget-lines",
            Line(description, unitCostMinor: amount, wbsCode: wbsCode, vatTreatment: vat));

    private static Task<CostEntryDto> AddCost(ApiClient client, Guid projectId, long amount, string date = PastDate, string vat = "Exempt") =>
        client.PostAsync<CostEntryDto>($"/api/projects/{projectId}/cost-entries", Cost("Spent", unitCostMinor: amount, entryDate: date, vatTreatment: vat));

    private static object Entry(Guid lineId, int? percentBps, string? note = null, int? expected = null) =>
        new { budgetLineId = lineId, percentBps, note, expectedPercentBps = expected };

    private static Task<HttpResponseMessage> RecordAsync(ApiClient client, Guid projectId, string asOf, string? requestId = null, params object[] lines) =>
        client.PostAsync($"/api/projects/{projectId}/progress", new { asOfDate = asOf, lines, clientRequestId = requestId });

    private static Task<ProgressDto> ReadAsync(ApiClient client, Guid projectId, string? asOf = null) =>
        client.GetAsync<ProgressDto>($"/api/projects/{projectId}/progress{(asOf is null ? "" : $"?asOf={asOf}")}");

    private static async Task<RecordProgressResult> RecordOkAsync(ApiClient client, Guid projectId, string asOf, params object[] lines)
    {
        var response = await RecordAsync(client, projectId, asOf, null, lines);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadAsync<RecordProgressResult>(response);
    }

    /// <summary>
    /// The worked project. Lines: Footings 1,000,000 and Columns 500,000 (division 3), Site office 200,000 (no division). Budget 1,700,000.
    /// Actual cost 400,000. Contract price 3,000,000.
    /// </summary>
    private static async Task<(ProjectDto Project, BudgetLineDto Footings, BudgetLineDto Columns, BudgetLineDto Office)> WorkedProjectAsync(ApiClient client)
    {
        var project = await NewProjectAsync(client);
        var footings = await AddLine(client, project.Id, "Footings", 1_000_000);
        var columns = await AddLine(client, project.Id, "Columns", 500_000);
        var office = await AddLine(client, project.Id, "Site office", 200_000, wbsCode: null);
        await AddCost(client, project.Id, 400_000);
        return (project, footings, columns, office);
    }

    // ---- Reading ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Progress_turns_into_earned_value_worked_out_by_hand()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns, office) = await WorkedProjectAsync(client);

        var result = await RecordOkAsync(client, project.Id, Feb10, Entry(footings.Id, 5_000), Entry(columns.Id, 2_000), Entry(office.Id, 10_000));
        Assert.Equal(3, result.LinesUpdated);
        Assert.Equal(DateOnly.Parse(Feb10), result.AsOfDate);

        var report = await ReadAsync(client, project.Id, Feb10);
        var ev = report.EarnedValue;
        Assert.Equal(DateOnly.Parse(Feb10), report.AsOfDate);
        Assert.Equal("IncludingVat", report.Basis);
        Assert.True(report.HasProgress);
        Assert.Equal(DateOnly.Parse(Feb10), report.LastUpdatedOn);

        Assert.Equal(1_700_000, ev.BudgetMinor);
        Assert.Equal(800_000, ev.EarnedMinor);          // 500,000 + 100,000 + 200,000
        Assert.Equal(400_000, ev.SpentMinor);
        Assert.Equal(680_000, ev.PlannedMinor);         // 40% of the time
        Assert.Equal(400_000, ev.CostVarianceMinor);
        Assert.Equal(120_000, ev.ScheduleVarianceMinor);
        Assert.Equal(20_000, ev.CpiBps);                // each peso spent earns two pesos of work
        Assert.Equal(11_765, ev.SpiBps);                // 800 / 680
        Assert.Equal(850_000, ev.ForecastMinor);        // 1,700,000 x 400,000 / 800,000
        Assert.Equal(450_000, ev.ToCompleteMinor);
        Assert.Equal(850_000, ev.ForecastVarianceMinor);
        Assert.Equal(4_706, ev.PercentCompleteBps);     // 800 / 1,700
        Assert.Equal(4_000, ev.PlannedPercentBps);
        Assert.Equal(3_000_000, ev.ContractPriceMinor);
        Assert.Equal(2_150_000, ev.ForecastMarginMinor); // 3,000,000 - 850,000

        Assert.Equal(["Concrete", "Unclassified"], report.Divisions.Select(division => division.Name));
        var concrete = report.Divisions[0];
        Assert.Equal(3, concrete.Division);
        Assert.Equal(1_500_000, concrete.BudgetMinor);
        Assert.Equal(600_000, concrete.EarnedMinor);
        Assert.Equal(4_000, concrete.PercentBps);
        Assert.Equal(["Columns", "Footings"], concrete.Lines.Select(line => line.Description));
        var line = concrete.Lines.Single(item => item.BudgetLineId == footings.Id);
        Assert.Equal((1_000_000, 5_000, 500_000), (line.BudgetMinor, line.PercentBps, line.EarnedMinor));
        Assert.Equal(DateOnly.Parse(Feb10), line.LastUpdatedOn);
        Assert.Equal(10_000, report.Divisions[1].PercentBps);
    }

    [SqlServerFact]
    public async Task A_lines_current_percent_is_its_latest_entry_on_or_before_the_date_asked_for()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);

        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(footings.Id, 3_000));
        await RecordOkAsync(client, project.Id, "2026-03-01", Entry(footings.Id, 6_000));
        // A second statement on the same day: the later one is the current one.
        await RecordOkAsync(client, project.Id, "2026-03-01", Entry(footings.Id, 7_000));

        ProgressLineDto Footings(ProgressDto report) => report.Divisions.SelectMany(division => division.Lines).Single(line => line.BudgetLineId == footings.Id);

        var before = await ReadAsync(client, project.Id, "2026-01-31");
        Assert.Equal(0, Footings(before).PercentBps);
        Assert.Null(Footings(before).LastUpdatedOn);
        Assert.False(before.HasProgress);
        Assert.Equal(3_000, Footings(await ReadAsync(client, project.Id, "2026-02-01")).PercentBps);
        Assert.Equal(3_000, Footings(await ReadAsync(client, project.Id, "2026-02-28")).PercentBps);
        Assert.Equal(7_000, Footings(await ReadAsync(client, project.Id, "2026-03-01")).PercentBps);
        var today = await ReadAsync(client, project.Id);
        Assert.Equal(7_000, Footings(today).PercentBps);
        Assert.Equal(DateOnly.Parse("2026-03-01"), Footings(today).LastUpdatedOn);
        Assert.Equal(Today, today.AsOfDate);
    }

    [SqlServerFact]
    public async Task Spending_counts_only_what_was_dated_on_or_before_the_date_asked_for()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);     // 400,000 dated 1 Dec 2025
        await AddCost(client, project.Id, 100_000, date: "2026-03-01");
        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(footings.Id, 5_000));

        Assert.Equal(400_000, (await ReadAsync(client, project.Id, "2026-02-01")).EarnedValue.SpentMinor);
        Assert.Equal(500_000, (await ReadAsync(client, project.Id, "2026-03-01")).EarnedValue.SpentMinor);
        // Today, spending is the same figure the dashboard adds up.
        var dashboard = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        Assert.Equal(dashboard.Projects.Single(row => row.Id == project.Id).ActualMinor, (await ReadAsync(client, project.Id)).EarnedValue.SpentMinor);
    }

    [SqlServerFact]
    public async Task On_the_net_basis_the_figures_exclude_vat_but_the_margin_still_uses_amounts_with_vat()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        await ChangeOrganizationAsync(client, vatRateBps: 1_200, varianceBasis: "ExcludingVat");
        var project = await NewProjectAsync(client, price: 2_000_000);
        // 1,000,000 before VAT: 1,120,000 with VAT. Spent 600,000 before VAT: 672,000 with VAT.
        var line = await AddLine(client, project.Id, "Slab", 1_000_000, vat: "Exclusive");
        await AddCost(client, project.Id, 600_000, vat: "Exclusive");
        await RecordOkAsync(client, project.Id, Feb10, Entry(line.Id, 5_000));

        var net = await ReadAsync(client, project.Id, Feb10);

        Assert.Equal("ExcludingVat", net.Basis);
        Assert.Equal((1_000_000, 500_000, 600_000), (net.EarnedValue.BudgetMinor, net.EarnedValue.EarnedMinor, net.EarnedValue.SpentMinor));
        Assert.Equal(1_200_000, net.EarnedValue.ForecastMinor);                  // 1,000,000 x 600,000 / 500,000
        Assert.Equal(1_000_000, net.Divisions.Single().Lines.Single().BudgetMinor);
        // Forecast with VAT: 1,120,000 x 672,000 / 560,000 = 1,344,000. Margin = 2,000,000 - 1,344,000.
        Assert.Equal(656_000, net.EarnedValue.ForecastMarginMinor);

        await ChangeOrganizationAsync(client, varianceBasis: "IncludingVat");
        var gross = await ReadAsync(client, project.Id, Feb10);
        Assert.Equal((1_120_000, 560_000, 672_000), (gross.EarnedValue.BudgetMinor, gross.EarnedValue.EarnedMinor, gross.EarnedValue.SpentMinor));
        Assert.Equal(1_344_000, gross.EarnedValue.ForecastMinor);
        Assert.Equal(656_000, gross.EarnedValue.ForecastMarginMinor);
    }

    [SqlServerFact]
    public async Task Without_a_target_end_date_or_a_price_those_figures_are_absent_and_before_progress_the_forecast_is_the_budget()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await NewProjectAsync(client, price: null, end: null);
        await AddLine(client, project.Id, "Footings", 1_000_000);
        await AddCost(client, project.Id, 250_000);

        var report = await ReadAsync(client, project.Id);

        var ev = report.EarnedValue;
        Assert.False(report.HasProgress);
        Assert.Null(report.LastUpdatedOn);
        Assert.Null(ev.PlannedMinor);
        Assert.Null(ev.ScheduleVarianceMinor);
        Assert.Null(ev.SpiBps);
        Assert.Null(ev.PlannedPercentBps);
        Assert.Null(ev.ContractPriceMinor);
        Assert.Null(ev.ForecastMarginMinor);
        Assert.Equal(0, ev.EarnedMinor);
        Assert.Equal(0, ev.CpiBps);
        Assert.Equal(1_000_000, ev.ForecastMinor);
        Assert.Equal(750_000, ev.ToCompleteMinor);
    }

    [SqlServerFact]
    public async Task A_project_with_no_budget_lines_reads_as_empty_and_a_future_date_is_refused()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await NewProjectAsync(client);

        var report = await ReadAsync(client, project.Id);

        Assert.Empty(report.Divisions);
        Assert.False(report.HasProgress);
        Assert.Equal(0, report.EarnedValue.BudgetMinor);
        Assert.Null(report.EarnedValue.PercentCompleteBps);

        var future = await client.GetAsync($"/api/projects/{project.Id}/progress?asOf={Today.AddDays(3):yyyy-MM-dd}");
        Assert.Equal("Choose today or an earlier date.", await ErrorMessageAsync(future, "asOf"));
    }

    [SqlServerFact]
    public async Task Recording_progress_adds_only_the_forecast_to_the_dashboard_and_the_cost_summary()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);
        var dashboardBefore = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        var summaryBefore = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");
        Assert.Null(dashboardBefore.Projects.Single().ProgressForecast);
        Assert.Null(summaryBefore.ProgressForecast);

        await RecordOkAsync(client, project.Id, Feb10, Entry(footings.Id, 9_000));

        var dashboardAfter = await client.GetAsync<DashboardDto>("/api/reports/dashboard");
        var summaryAfter = await client.GetAsync<CostSummaryDto>($"/api/projects/{project.Id}/cost-summary");
        // 900,000 of work for 400,000 spent: a forecast of 1,700,000 x 400,000 / 900,000 = 755,556, under the budget, so it flags nothing.
        Assert.Equal(new ProgressForecastDto(755_556, -944_444, 5_294), dashboardAfter.Projects.Single().ProgressForecast);
        Assert.Equal(dashboardAfter.Projects.Single().ProgressForecast, summaryAfter.ProgressForecast);
        Assert.Equal(0, dashboardAfter.Totals.ForecastOverBudgetCount);

        // Every other figure and every health is what it was. The lists inside the DTOs compare by reference, so compare what the API sent.
        string Sent<T>(T value) => JsonSerializer.Serialize(value, ApiClient.Json);
        Assert.Equal(Sent(dashboardBefore with { Projects = [dashboardBefore.Projects.Single()] }),
            Sent(dashboardAfter with { Projects = [dashboardAfter.Projects.Single() with { ProgressForecast = null }] }));
        Assert.Equal(Sent(summaryBefore), Sent(summaryAfter with { ProgressForecast = null }));
    }

    // ---- Recording ----------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task A_line_that_did_not_change_is_skipped_and_nothing_changed_is_an_error()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(footings.Id, 4_000));

        // Footings repeats 40%, Columns moves: one entry is made, not two.
        var result = await RecordOkAsync(client, project.Id, "2026-02-02", Entry(footings.Id, 4_000), Entry(columns.Id, 1_000));
        Assert.Equal(1, result.LinesUpdated);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{columns.Id}/progress")).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);

        // Submitting only what already stands, or only a 0% for a line never updated, is refused.
        var nothing = await RecordAsync(client, project.Id, "2026-02-03", null, Entry(footings.Id, 4_000), Entry(columns.Id, 1_000));
        Assert.Equal("Nothing changed. Change the progress of at least one line before saving.", await ErrorMessageAsync(nothing, "lines"));
        var zero = await RecordAsync(client, project.Id, "2026-02-03", null, Entry((await AddLine(client, project.Id, "New line", 1_000)).Id, 0));
        Assert.Contains("lines", await ErrorFieldsAsync(zero));
    }

    [SqlServerFact]
    public async Task Every_refusal_names_its_field_and_nothing_is_recorded()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var other = await NewProjectAsync(client);
        var otherLine = await AddLine(client, other.Id, "Elsewhere", 100_000);
        var removed = await AddLine(client, project.Id, "Dropped", 100_000);
        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{removed.Id}/remove", new { reason = "Not needed", version = removed.Version });
        await RecordOkAsync(client, project.Id, "2026-03-01", Entry(columns.Id, 6_000));

        async Task Refused(string asOf, string field, string message, params object[] lines)
        {
            var response = await RecordAsync(client, project.Id, asOf, null, lines);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(message, await ErrorMessageAsync(response, field));
        }

        await Refused(Today.AddDays(3).ToString("yyyy-MM-dd"), "asOfDate", "Progress can't be dated in the future.", Entry(footings.Id, 1_000));
        await Refused("0001-01-01", "asOfDate", "Choose the date this progress is true on.", Entry(footings.Id, 1_000));
        await Refused(Feb10, "lines", "Change the progress of at least one line before saving.");
        await Refused(Feb10, "lines[0].percentBps", "Enter a percent between 0 and 100.", Entry(footings.Id, 10_001));
        await Refused(Feb10, "lines[0].percentBps", "Enter a percent between 0 and 100.", Entry(footings.Id, -1));
        await Refused(Feb10, "lines[0].percentBps", "Enter a percent between 0 and 100.", Entry(footings.Id, null));
        await Refused("2026-03-02", "lines[0].note", "Say why the progress went down, for example rework.", Entry(columns.Id, 5_999));
        await Refused(Feb10, "lines[0].note", "The note can be at most 300 characters.", Entry(footings.Id, 1_000, new string('x', 301)));
        await Refused(Feb10, "lines[0].budgetLineId", "That budget line isn't part of this project any more. Reload the page and try again.",
            Entry(otherLine.Id, 1_000));
        await Refused(Feb10, "lines[0].budgetLineId", "That budget line isn't part of this project any more. Reload the page and try again.",
            Entry(removed.Id, 1_000));
        await Refused(Feb10, "lines[0].budgetLineId", "That budget line isn't part of this project any more. Reload the page and try again.",
            Entry(Guid.NewGuid(), 1_000));
        await Refused(Feb10, "lines[1].budgetLineId", "This line is in the update twice. Keep one.", Entry(footings.Id, 1_000), Entry(footings.Id, 2_000));
        // Columns was last updated on 1 Mar: an update that changes it can't be dated before that.
        await Refused("2026-02-01", "asOfDate", "A line you changed was already updated on 1 Mar 2026. Use that date or a later one.",
            Entry(columns.Id, 7_000));
        await Refused(Feb10, "lines", "Update at most 200 lines at once.",
            Enumerable.Range(0, 201).Select(_ => Entry(Guid.NewGuid(), 1_000)).ToArray());

        // Several problems arrive together, and not even the valid line was recorded.
        var mixed = await RecordAsync(client, project.Id, "2026-03-02", null, Entry(footings.Id, 1_000), Entry(columns.Id, 100), Entry(footings.Id, 20_000));
        Assert.Equal(["lines[1].note", "lines[2].budgetLineId"], (await ErrorFieldsAsync(mixed)).Order());
        Assert.Equal(0, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{columns.Id}/progress")).Total);
    }

    [SqlServerFact]
    public async Task A_lower_percent_with_a_note_is_recorded_as_rework()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);
        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(footings.Id, 8_000));

        await RecordOkAsync(client, project.Id, "2026-02-05", Entry(footings.Id, 6_500, "  Two footings were redone  "));

        var history = await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress");
        Assert.Equal([6_500, 8_000], history.Items.Select(item => item.PercentBps));
        Assert.Equal("Two footings were redone", history.Items[0].Note);
        Assert.Equal(6_500, (await ReadAsync(client, project.Id)).Divisions[0].Lines.Single(line => line.BudgetLineId == footings.Id).PercentBps);
    }

    [SqlServerFact]
    public async Task A_repeated_request_id_returns_the_first_result_and_records_nothing_more()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var requestId = Guid.NewGuid().ToString();

        var firstResponse = await RecordAsync(client, project.Id, Feb10, requestId, Entry(footings.Id, 5_000), Entry(columns.Id, 2_000));
        var first = await client.ReadAsync<RecordProgressResult>(firstResponse);
        var again = await client.ReadAsync<RecordProgressResult>(await RecordAsync(client, project.Id, Feb10, requestId, Entry(footings.Id, 5_000), Entry(columns.Id, 2_000)));

        Assert.Equal(first, again);
        Assert.Equal(2, again.LinesUpdated);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{columns.Id}/progress")).Total);

        // The same id for another date, or for another project, is something else: refused.
        var otherDate = await RecordAsync(client, project.Id, "2026-02-11", requestId, Entry(footings.Id, 6_000));
        Assert.Equal(HttpStatusCode.Conflict, otherDate.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(otherDate));
        var otherProject = await NewProjectAsync(client);
        var elsewhere = await RecordAsync(client, otherProject.Id, Feb10, requestId, Entry((await AddLine(client, otherProject.Id, "Other", 1_000)).Id, 1_000));
        Assert.Equal(HttpStatusCode.Conflict, elsewhere.StatusCode);
    }

    [SqlServerFact]
    public async Task Two_copies_of_the_same_submission_at_the_same_moment_record_one_batch()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        using var second = await AnotherSessionAsync(_factory, user);
        var (project, footings, columns, _) = await WorkedProjectAsync(client);
        var requestId = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(
            RecordAsync(client, project.Id, Feb10, requestId, Entry(footings.Id, 5_000), Entry(columns.Id, 2_000)),
            RecordAsync(second, project.Id, Feb10, requestId, Entry(footings.Id, 5_000), Entry(columns.Id, 2_000)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{columns.Id}/progress")).Total);
    }

    [SqlServerFact]
    public async Task An_update_made_on_a_stale_figure_is_a_409_and_records_nothing()
    {
        var (client, user) = await AdminAsync();
        using var _c = client;
        using var colleague = await AnotherSessionAsync(_factory, user);
        var (project, footings, columns, _) = await WorkedProjectAsync(client);

        // Both people open the form at 0%. The colleague saves 50% first.
        await RecordOkAsync(colleague, project.Id, "2026-02-01", Entry(footings.Id, 5_000, expected: 0));

        var stale = await RecordAsync(client, project.Id, "2026-02-02", null, Entry(columns.Id, 2_000, expected: 0), Entry(footings.Id, 6_000, expected: 0));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        Assert.Equal(0, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{columns.Id}/progress")).Total);

        // After reloading, the figure the person saw is the one that stands, and the update is accepted.
        await RecordOkAsync(client, project.Id, "2026-02-02", Entry(columns.Id, 2_000, expected: 0), Entry(footings.Id, 6_000, expected: 5_000));
    }

    [SqlServerFact]
    public async Task An_archived_project_can_be_read_but_takes_no_progress()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);
        await RecordOkAsync(client, project.Id, Feb10, Entry(footings.Id, 5_000));
        await ArchiveAsync(client, project);

        var refused = await RecordAsync(client, project.Id, "2026-02-11", null, Entry(footings.Id, 6_000));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("rule_violation", await RuleCodeAsync(refused));
        Assert.Equal("This project is archived. Restore it before making changes.", await DetailAsync(refused));
        Assert.Equal(5_000, (await ReadAsync(client, project.Id)).Divisions[0].Lines.Single(line => line.BudgetLineId == footings.Id).PercentBps);
    }

    // ---- History ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task A_lines_history_is_newest_first_paged_and_names_who_recorded_it_even_after_the_line_is_removed()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var (project, footings, _, _) = await WorkedProjectAsync(client);
        foreach (var (date, percent, note) in new[] { ("2026-02-01", 1_000, (string?)null), ("2026-02-02", 2_000, null), ("2026-02-03", 1_500, "Rework"),
                     ("2026-02-04", 3_000, null), ("2026-02-05", 4_000, null) })
            await RecordOkAsync(client, project.Id, date, Entry(footings.Id, percent, note));

        var first = await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress?pageSize=2");
        var last = await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress?page=3&pageSize=2");

        Assert.Equal(5, first.Total);
        Assert.Equal([4_000, 3_000], first.Items.Select(item => item.PercentBps));
        Assert.Equal(DateOnly.Parse("2026-02-05"), first.Items[0].AsOfDate);
        Assert.Equal("Test Admin", first.Items[0].RecordedBy);
        Assert.Equal([1_000], last.Items.Select(item => item.PercentBps));
        Assert.Equal("Rework", (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Items[2].Note);

        await client.PostAsync<BudgetLineDto>($"/api/budget-lines/{footings.Id}/remove", new { reason = "Scope dropped", version = footings.Version });
        Assert.Equal(5, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);
        // A removed line no longer counts in the project's progress.
        Assert.DoesNotContain(footings.Id, (await ReadAsync(client, project.Id)).Divisions.SelectMany(division => division.Lines).Select(line => line.BudgetLineId));
    }

    // ---- Who may ------------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Roles_decide_who_can_read_and_who_can_record_progress()
    {
        var (admin, adminUser) = await AdminAsync();
        using var _a = admin;
        var (project, footings, _, _) = await WorkedProjectAsync(admin);
        var readUrl = $"/api/projects/{project.Id}/progress";
        var historyUrl = $"/api/budget-lines/{footings.Id}/progress";
        var body = new { asOfDate = Feb10, lines = new[] { Entry(footings.Id, 100) }, clientRequestId = (string?)null };

        // HR and the warehouse have no business with project progress.
        foreach (var role in new[] { AccessRole.HrOfficer, AccessRole.WarehouseKeeper })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _h = client;
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(readUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(historyUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(readUrl, body)).StatusCode);
        }

        // An accountant and a viewer read the figures but never record.
        foreach (var role in new[] { AccessRole.Accountant, AccessRole.Viewer })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _v = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(readUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(historyUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(readUrl, body)).StatusCode);
        }

        // The project manager and the cost engineer record it.
        foreach (var (role, percent) in new[] { (AccessRole.ProjectManager, 100), (AccessRole.CostEngineer, 200) })
        {
            var (client, _) = await SignedInAsync(_factory, role, adminUser.TenantId);
            using var _w = client;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(readUrl)).StatusCode);
            var response = await client.PostAsync(readUrl, new { asOfDate = Feb10, lines = new[] { Entry(footings.Id, percent) }, clientRequestId = (string?)null });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [SqlServerFact]
    public async Task One_company_can_never_see_or_record_progress_on_anothers_project()
    {
        var (mine, _) = await AdminAsync();
        var (theirs, _) = await AdminAsync();
        using var _a = mine;
        using var _b = theirs;
        var (project, footings, _, _) = await WorkedProjectAsync(mine);
        await RecordOkAsync(mine, project.Id, Feb10, Entry(footings.Id, 5_000));
        var readUrl = $"/api/projects/{project.Id}/progress";

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(readUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"{readUrl}?asOf={Feb10}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RecordAsync(theirs, project.Id, Feb10, null, Entry(footings.Id, 9_000))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/budget-lines/{footings.Id}/progress")).StatusCode);

        // Naming my line in their own project is refused like any line that is not theirs, and reveals nothing.
        var theirProject = await NewProjectAsync(theirs);
        var planted = await RecordAsync(theirs, theirProject.Id, Feb10, null, Entry(footings.Id, 9_000));
        Assert.Equal("That budget line isn't part of this project any more. Reload the page and try again.", await ErrorMessageAsync(planted, "lines[0].budgetLineId"));

        // Their own project shows none of mine, and mine is untouched.
        Assert.Empty((await ReadAsync(theirs, theirProject.Id)).Divisions);
        Assert.Equal(5_000, (await ReadAsync(mine, project.Id)).Divisions[0].Lines.Single(line => line.BudgetLineId == footings.Id).PercentBps);
        Assert.Equal(1, (await mine.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{footings.Id}/progress")).Total);
    }

    // ---- Performance --------------------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task The_progress_read_costs_the_same_number_of_statements_whatever_the_number_of_lines_and_entries()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await NewProjectAsync(client);
        var first = await AddLine(client, project.Id, "One", 10_000);
        await AddCost(client, project.Id, 5_000);
        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(first.Id, 1_000));
        var url = $"/api/projects/{project.Id}/progress";
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        var lines = new List<Guid> { first.Id };
        var codes = new[] { null, "3100", "4200", "5100", "9300" };
        for (var index = 0; index < 40; index++)
            lines.Add((await AddLine(client, project.Id, $"Line {index}", 1_000 + index, codes[index % codes.Length])).Id);
        for (var round = 1; round <= 4; round++)
            await RecordOkAsync(client, project.Id, $"2026-02-{round + 1:00}", lines.Select(id => Entry(id, round * 1_000)).ToArray());
        for (var index = 0; index < 20; index++) await AddCost(client, project.Id, 100 + index);

        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));
        var report = await ReadAsync(client, project.Id);

        Assert.Equal(41, report.Divisions.Sum(division => division.Lines.Count));
        Assert.Equal(small, large);
        // The project, the company, the lines with their current percent, the actual cost, and the schedule's activities (which decide
        // whether the planned value follows the schedule).
        Assert.Equal(5, large);
    }

    [SqlServerFact]
    public async Task A_lines_history_costs_the_same_number_of_statements_whatever_the_number_of_entries()
    {
        var (client, _) = await AdminAsync();
        using var _c = client;
        var project = await NewProjectAsync(client);
        var line = await AddLine(client, project.Id, "One", 10_000);
        await RecordOkAsync(client, project.Id, "2026-02-01", Entry(line.Id, 100));
        var url = $"/api/budget-lines/{line.Id}/progress";
        var small = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));

        for (var day = 2; day <= 25; day++) await RecordOkAsync(client, project.Id, $"2026-02-{day:00}", Entry(line.Id, day * 100));

        var large = await StatementsAsync(_factory, async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode));
        Assert.Equal(25, (await client.GetAsync<PagedResult<ProgressHistoryItemDto>>(url)).Total);
        Assert.Equal(small, large);
        // The line, the count and the page.
        Assert.Equal(3, large);
    }
}
