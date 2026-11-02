using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Progress;

/// <summary>
/// What the earned-value forecast on the dashboard and the project cost summary is worked out from, read the way the Progress tab reads it:
/// each active budget line's current percent (its latest entry on or before the date) and the posted cost dated on or before the date. At
/// most two statements however many projects, lines and entries there are: one for the lines of the projects that have progress, one for
/// their posted cost (skipped when there is no progress anywhere). Each line's worth is rounded by <see cref="EarnedValueCalculator"/>, the
/// same call the Progress tab makes, so the two can never differ by a centavo.
/// </summary>
internal sealed class ProgressForecastSource(ConstructOpxDbContext db) : IProgressForecastSource
{
    public async Task<IReadOnlyList<ProgressForecastInputs>> ForecastInputsAsync(Guid? projectId, bool includeArchived, DateOnly asOf,
        CancellationToken cancellationToken)
    {
        // Only projects that have any progress at all: the rest are neither read nor summed.
        var projects = db.Set<Project>().AsNoTracking();
        if (projectId is { } only) projects = projects.Where(project => project.Id == only);
        if (!includeArchived) projects = projects.Where(project => project.Status != ProjectStatus.Archived);
        var withProgress = projects.Where(project => db.Set<ProgressEntry>().Any(entry => entry.ProjectId == project.Id)).Select(project => project.Id);

        // A line's current percent is a sub-select per line inside the one statement, as on the Progress tab.
        var lines = await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.Status == BudgetLineStatus.Active && withProgress.Contains(line.ProjectId))
            .Select(line => new
            {
                line.ProjectId,
                line.NetMinor,
                line.GrossMinor,
                Percent = db.Set<ProgressEntry>().Where(entry => entry.BudgetLineId == line.Id && entry.AsOfDate <= asOf)
                    .OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => (int?)entry.PercentCompleteBps).FirstOrDefault()
            })
            .Where(row => row.Percent > 0)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0) return [];

        var earned = lines.GroupBy(row => row.ProjectId).ToDictionary(group => group.Key, group => new MeasuredAmounts(
            group.Sum(row => EarnedValueCalculator.LineEarnedValue(row.NetMinor, row.Percent!.Value)),
            group.Sum(row => EarnedValueCalculator.LineEarnedValue(row.GrossMinor, row.Percent!.Value))));

        var actuals = (await db.Set<CostEntry>().AsNoTracking()
                .Where(entry => entry.Status == CostEntryStatus.Posted && entry.EntryDate <= asOf && withProgress.Contains(entry.ProjectId))
                .GroupBy(entry => entry.ProjectId)
                .Select(group => new ProjectAmounts(group.Key, group.Sum(entry => entry.NetMinor), group.Sum(entry => entry.GrossMinor)))
                .ToListAsync(cancellationToken))
            .ToDictionary(item => item.ProjectId, item => new MeasuredAmounts(item.NetMinor, item.GrossMinor));

        return earned.Select(item => new ProgressForecastInputs(item.Key, item.Value, actuals.GetValueOrDefault(item.Key))).ToArray();
    }
}
