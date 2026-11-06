using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Schedule;

/// <summary>
/// "Schedule activities past their planned finish": one row per Active project with at least one activity that is behind (its planned finish is
/// before today and a linked active budget line is below 100%, the rule of <see cref="SchedulePlan.IsBehind"/>), for the people who plan the
/// schedule (<c>schedule.write</c>). The row opens the project's Progress tab. It never carries an amount. Two statements at most: the count,
/// and the rows only when the count is above zero.
/// </summary>
internal sealed class ActivitiesBehindNeedsActionSource(ConstructOpxDbContext db) : INeedsActionSource
{
    public const string GroupKey = "activities-behind";

    public string Key => GroupKey;

    public bool CanActOn(AccessRole roles) => Permissions.IsAllowed(roles, Permissions.ScheduleWrite);

    public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken) => Projects(dates.Today).CountAsync(cancellationToken);

    public async Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
    {
        var count = await CountAsync(dates, cancellationToken);
        if (count == 0) return new NeedsActionGroupDto(GroupKey, 0, []);

        // The most activities behind first, then the one that should have finished longest ago.
        var rows = await Projects(dates.Today)
            .OrderByDescending(row => row.Behind).ThenBy(row => row.EarliestFinish).ThenBy(row => row.Name).ThenBy(row => row.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new NeedsActionGroupDto(GroupKey, count, rows.Select(row => new NeedsActionRowDto(row.Id, null, row.Name, row.Id, row.Name,
            Detail(row.Behind), null, row.EarliestFinish, true, null)).ToArray());
    }

    /// <summary>"3 activities are past their planned finish" or "1 activity is past its planned finish". No amount, ever.</summary>
    public static string Detail(int behind) =>
        behind == 1 ? "1 activity is past its planned finish" : $"{behind} activities are past their planned finish";

    private IQueryable<BehindRow> Projects(DateOnly today)
    {
        var behind = BehindActivities(today);
        return from project in db.Set<Project>().AsNoTracking()
               where project.Status == ProjectStatus.Active
               let count = behind.Count(activity => activity.ProjectId == project.Id)
               where count > 0
               select new BehindRow
               {
                   Id = project.Id,
                   Name = project.Name,
                   Behind = count,
                   EarliestFinish = behind.Where(activity => activity.ProjectId == project.Id).Min(activity => (DateOnly?)activity.PlannedFinish)
               };
    }

    /// <summary>Active activities past their planned finish with a linked active line whose current percent (its latest entry) is below 100%.</summary>
    private IQueryable<ScheduleActivity> BehindActivities(DateOnly today) =>
        db.Set<ScheduleActivity>().AsNoTracking()
            .Where(activity => activity.Status == ScheduleActivityStatus.Active && activity.PlannedFinish < today)
            .Where(activity => activity.Lines.Any(link => db.Set<BudgetLine>().Any(line =>
                line.Id == link.BudgetLineId && line.Status == BudgetLineStatus.Active &&
                (db.Set<ProgressEntry>().Where(entry => entry.BudgetLineId == line.Id)
                    .OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => (int?)entry.PercentCompleteBps).FirstOrDefault() ?? 0) < ProgressEntry.FullBps)));

    private sealed class BehindRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public int Behind { get; init; }
        public DateOnly? EarliestFinish { get; init; }
    }
}
