using ConstructOPX.Application.Features.Schedule;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Schedule;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Schedule;

internal sealed class ScheduleRepository(ConstructOpxDbContext db) : IScheduleRepository
{
    public Task<ScheduleActivity?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ScheduleActivity>().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduleActivity>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Set<ScheduleActivity>().Include(item => item.Lines)
            .Where(item => item.ProjectId == projectId && item.Status == ScheduleActivityStatus.Active)
            .ToListAsync(cancellationToken);

    public async Task<ScheduleLineContext> LineContextAsync(Guid projectId, Guid? exceptActivityId, IReadOnlyCollection<Guid> budgetLineIds,
        CancellationToken cancellationToken)
    {
        // One statement: each asked-for line that is an active line of the project, with the name of the other active activity that links it.
        var rows = await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.ProjectId == projectId && line.Status == BudgetLineStatus.Active && budgetLineIds.Contains(line.Id))
            .Select(line => new
            {
                line.Id,
                line.Description,
                Other = (from link in db.Set<ScheduleActivityLine>()
                         join activity in db.Set<ScheduleActivity>() on link.ActivityId equals activity.Id
                         where link.BudgetLineId == line.Id && activity.Status == ScheduleActivityStatus.Active &&
                               (exceptActivityId == null || activity.Id != exceptActivityId)
                         select activity.Name).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return new ScheduleLineContext(rows.ToDictionary(row => row.Id, row => row.Description),
            rows.Where(row => row.Other is not null).ToDictionary(row => row.Id, row => row.Other!));
    }

    public async Task<int> NextSortOrderAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await db.Set<ScheduleActivity>().Where(item => item.ProjectId == projectId).MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? -1) + 1;

    public Task<ScheduleRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<ScheduleActivity>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new ScheduleRequestOwner(item.Id, item.ProjectId)).SingleOrDefaultAsync(cancellationToken);

    public void Add(ScheduleActivity activity) => db.Set<ScheduleActivity>().Add(activity);
}

/// <summary>
/// Reads the schedule straight into rows. The activities are one statement (their linked line ids come in the same statement); the S-curve's
/// history is one statement for progress and one for cost, grouped into the curve's steps in SQL, so the rows returned grow with the lines and
/// the steps, never with the number of entries.
/// </summary>
internal sealed class ScheduleReader(ConstructOpxDbContext db) : IScheduleReader
{
    public async Task<IReadOnlyList<ScheduleActivityRow>> ListAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await db.Set<ScheduleActivity>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status == ScheduleActivityStatus.Active)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.PlannedStart).ThenBy(item => item.Name).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id, item.Name, item.PlannedStart, item.PlannedFinish, item.BaselineStart, item.BaselineFinish, item.BaselineSetAtUtc, item.SortOrder,
                item.Version, Lines = item.Lines.Select(line => line.BudgetLineId).ToList()
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ScheduleActivityRow(row.Id, row.Name, row.PlannedStart, row.PlannedFinish, row.BaselineStart, row.BaselineFinish,
            row.BaselineSetAtUtc, row.SortOrder, row.Version, row.Lines)).ToArray();
    }

    public async Task<IReadOnlyList<ProgressStepRow>> ProgressStepsAsync(Guid projectId, CurveSteps steps, DateOnly through,
        CancellationToken cancellationToken)
    {
        var (first, last, stepDays, count) = (steps.First, steps.Last, steps.StepDays, steps.Count);
        // Within each step, a line's latest entry (latest date, then latest saved) is what the line stood at on the step's point.
        var rows = await db.Set<ProgressEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.AsOfDate <= through)
            .Select(entry => new
            {
                entry.BudgetLineId, entry.AsOfDate, entry.RecordedAtUtc, entry.Id, entry.PercentCompleteBps,
                Step = entry.AsOfDate <= first ? 0
                    : entry.AsOfDate > last ? count
                    : (EF.Functions.DateDiffDay(first, entry.AsOfDate) + stepDays - 1) / stepDays
            })
            .GroupBy(entry => new { entry.BudgetLineId, entry.Step })
            .Select(group => new
            {
                group.Key.BudgetLineId,
                group.Key.Step,
                Percent = group.OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => entry.PercentCompleteBps).First()
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ProgressStepRow(row.BudgetLineId, row.Step, row.Percent)).ToArray();
    }

    public async Task<IReadOnlyList<CostStepRow>> CostStepsAsync(Guid projectId, CurveSteps steps, DateOnly through, CancellationToken cancellationToken)
    {
        var (first, last, stepDays, count) = (steps.First, steps.Last, steps.StepDays, steps.Count);
        // The same posted cost the Progress tab and the dashboard add up, summed per step.
        return await db.Set<CostEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.Status == CostEntryStatus.Posted && entry.EntryDate <= through)
            .Select(entry => new
            {
                entry.NetMinor, entry.GrossMinor,
                Step = entry.EntryDate <= first ? 0
                    : entry.EntryDate > last ? count
                    : (EF.Functions.DateDiffDay(first, entry.EntryDate) + stepDays - 1) / stepDays
            })
            .GroupBy(entry => entry.Step)
            .Select(group => new CostStepRow(group.Key, group.Sum(entry => entry.NetMinor), group.Sum(entry => entry.GrossMinor)))
            .ToListAsync(cancellationToken);
    }
}
