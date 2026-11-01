using Cms.Domain.Features.Statutory;
using Cms.Infrastructure.Persistence;

namespace Cms.Infrastructure.Seeding;

/// <summary>
/// Demo contribution and tax tables so the payroll screens show calculated deductions in development. Every figure is made up and
/// deliberately round; each table's name says so. None of them is, or approximates, a legal rate: a real company enters the official
/// schedules itself. Pag-IBIG is left without a table on purpose, so the demo also shows the "needs setup" line and the manual fallback.
/// </summary>
public static class StatutoryDemo
{
    public const string DemoName = "Illustrative demo table: replace with the official schedule";

    /// <summary>Adds the demo tables (in force from the first of the month six months ago). Call before the seeder's single save.</summary>
    public static Task SeedAsync(CmsDbContext db, Guid tenantId, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-6);

        db.Add(StatutorySchedule.Create(Guid.NewGuid(), tenantId, StatutoryAgency.Sss, DemoName, from, StatutoryMethod.Brackets, StatutoryPeriodBasis.Monthly,
            null,
            [
                new StatutoryRowInput(0, 1_000_000, 10_000, 20_000, 0, 0),
                new StatutoryRowInput(1_000_000, 2_000_000, 30_000, 60_000, 0, 0),
                new StatutoryRowInput(2_000_000, 4_000_000, 50_000, 100_000, 0, 0),
                new StatutoryRowInput(4_000_000, null, 70_000, 140_000, 0, 0)
            ], null, now));

        db.Add(StatutorySchedule.Create(Guid.NewGuid(), tenantId, StatutoryAgency.PhilHealth, DemoName, from, StatutoryMethod.Percentage,
            StatutoryPeriodBasis.Monthly, new StatutoryPercentageInput(300, 300, 500_000, 5_000_000, null, null, null, null), null, null, now));

        db.Add(StatutorySchedule.Create(Guid.NewGuid(), tenantId, StatutoryAgency.WithholdingTax, DemoName, from, StatutoryMethod.TaxBrackets,
            StatutoryPeriodBasis.Monthly, null,
            [
                new StatutoryRowInput(0, 2_500_000, 0, 0, 0, 0),
                new StatutoryRowInput(2_500_000, 5_000_000, 0, 0, 0, 1_000),
                new StatutoryRowInput(5_000_000, null, 0, 0, 250_000, 2_000)
            ], null, now));

        return Task.CompletedTask;
    }
}
