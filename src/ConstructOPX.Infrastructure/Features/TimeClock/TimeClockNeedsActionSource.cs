using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.TimeClock;

/// <summary>
/// "Time clock scans to review": one row per project (not archived) with work days before today whose scans are not yet confirmed as attendance, for the
/// people who confirm them (timeclock.review). Projects with a day whose scans don't pair up, or a worker not posted there, come first and are urgent.
/// No amounts, ever. Two statements: the count, and the rows only when the count is above zero.
/// </summary>
internal sealed class TimeClockNeedsActionSource(ConstructOpxDbContext db) : INeedsActionSource
{
    public const string GroupKey = "time-clock-to-review";

    public string Key => GroupKey;

    public bool CanActOn(AccessRole roles) => Permissions.IsAllowed(roles, Permissions.TimeClockReview);

    public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken) =>
        Projects(dates).CountAsync(cancellationToken);

    public async Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
    {
        var count = await CountAsync(dates, cancellationToken);
        if (count == 0) return new NeedsActionGroupDto(Key, 0, []);

        var rows = await Projects(dates)
            .OrderByDescending(row => row.Exceptions > 0).ThenBy(row => row.Earliest).ThenBy(row => row.ProjectName).ThenBy(row => row.ProjectId)
            .Take(take).ToListAsync(cancellationToken);
        return new NeedsActionGroupDto(Key, count, rows.Select(row => new NeedsActionRowDto(row.ProjectId, null, row.ProjectName, row.ProjectId,
            row.ProjectName, Detail(row), null, row.Earliest, row.Exceptions > 0, null)).ToArray());
    }

    /// <summary>"12 workers to review for Sun 1 Nov", or across several days "15 workers' days to review, Sun 1 Nov to Tue 3 Nov", and the exceptions.</summary>
    private static string Detail(ProjectRow row)
    {
        var first = Day(row.Earliest);
        var what = row.Days == 1
            ? $"{Count(row.Rows, "worker", "workers")} to review for {first}"
            : $"{Count(row.Rows, "worker's day", "workers' days")} to review, {first} to {Day(row.Latest)}";
        return row.Exceptions == 0 ? what : $"{what}; {Count(row.Exceptions, "needs", "need")} a closer look";
    }

    private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

    private static string Day(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    private IQueryable<ProjectRow> Projects(NeedsActionDates dates) =>
        from day in new TimeClockReader(db).WaitingDays(dates.Today)
        join project in db.Set<Project>() on day.ProjectId equals project.Id
        where project.Status != ProjectStatus.Archived
        group day by new { project.Id, project.Name } into perProject
        select new ProjectRow
        {
            ProjectId = perProject.Key.Id,
            ProjectName = perProject.Key.Name,
            Earliest = perProject.Min(day => day.WorkDate),
            Latest = perProject.Max(day => day.WorkDate),
            Days = perProject.Select(day => day.WorkDate).Distinct().Count(),
            Rows = perProject.Count(),
            Exceptions = perProject.Count(day => day.HasException)
        };

    private sealed class ProjectRow
    {
        public Guid ProjectId { get; init; }
        public string ProjectName { get; init; } = "";
        public DateOnly Earliest { get; init; }
        public DateOnly Latest { get; init; }
        public int Days { get; init; }
        public int Rows { get; init; }
        public int Exceptions { get; init; }
    }
}
