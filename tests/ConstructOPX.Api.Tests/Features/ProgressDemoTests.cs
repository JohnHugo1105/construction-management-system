using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.Infrastructure.Seeding;
using ConstructOPX.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// The demo progress for Riverside Residences. This class holds one test on purpose: the development seeder fills only an empty database,
/// and every test class has a database of its own.
/// </summary>
public sealed class ProgressDemoTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public ProgressDemoTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task The_demo_shows_Riverside_over_budget_and_behind_schedule_with_one_line_of_rework_and_seeds_only_once()
    {
        Guid tenantId;
        DateOnly today;
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
            var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var organization = db.Set<Organization>().IgnoreQueryFilters().Single();
            tenantId = organization.Id;
            today = organization.TodayLocal(clock.UtcNow);
            await ProgressDemo.SeedAsync(db, tenantId, today, clock.UtcNow);
            await ProgressDemo.SeedAsync(db, tenantId, today, clock.UtcNow);   // a second call changes nothing
        }

        using var client = new ApiClient(_factory);
        await client.SignInAsync("manager", DevelopmentSeeder.DemoPassword);
        var projects = await client.GetAsync<PagedResult<ProjectListItemDto>>("/api/projects");
        var riverside = projects.Items.Single(project => project.Name == "Riverside Residences");

        var report = await client.GetAsync<ProgressDto>($"/api/projects/{riverside.Id}/progress");

        Assert.True(report.HasProgress);
        Assert.Equal(today.AddDays(-2), report.LastUpdatedOn);
        var ev = report.EarnedValue;
        Assert.True(ev.EarnedMinor > 0 && ev.EarnedMinor < ev.BudgetMinor);
        Assert.True(ev.CpiBps < 10_000, "Riverside is over budget for the work done.");
        Assert.True(ev.SpiBps < 10_000, "Riverside is behind schedule.");
        Assert.True(ev.ForecastMinor > ev.BudgetMinor);
        Assert.NotNull(ev.ForecastMarginMinor);

        // The dashboard may not read calmer than the project page: Riverside is flagged there with the Progress tab's own figure.
        var dashboard = await client.GetAsync<ConstructOPX.Application.Features.Costing.DashboardDto>("/api/reports/dashboard");
        var row = dashboard.Projects.Single(item => item.Id == riverside.Id);
        Assert.NotNull(row.ProgressForecast);
        Assert.Equal(ev.ForecastMinor, row.ProgressForecast.EstimateAtCompletionMinor);
        Assert.Equal(ev.ForecastMinor - ev.BudgetMinor, row.ProgressForecast.OverBudgetMinor);
        Assert.True(dashboard.Totals.ForecastOverBudgetCount >= 1);

        var rebar = report.Divisions.SelectMany(division => division.Lines).Single(line => line.WbsCode == "3200");
        Assert.Equal(9_000, rebar.PercentBps);
        var history = await client.GetAsync<PagedResult<ProgressHistoryItemDto>>($"/api/budget-lines/{rebar.BudgetLineId}/progress");
        Assert.Equal([9_000, 8_500, 6_500, 7_000, 3_500], history.Items.Select(item => item.PercentBps));
        Assert.Equal("Two columns were re-tied after the inspection", history.Items[2].Note);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/projects/{riverside.Id}/progress")).StatusCode);
    }
}
