using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.SiteDiary;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.SiteDiary;

/// <summary>
/// "Site diary not written yesterday": one row per Active project whose report for yesterday (on the company's calendar) is missing while a
/// Posted report exists for it within the seven days before, so a project that has never kept a diary is never nagged. Nothing is listed when
/// yesterday was a company holiday. A person who may write reports sees the group; a row carries no amount. The count and the rows share one
/// filter, and the page reads at most two statements for the group (the count, and the rows only when the count is above zero).
/// </summary>
internal sealed class DiaryNotWrittenNeedsActionSource(ConstructOpxDbContext db) : INeedsActionSource
{
    /// <summary>A project is expected to report yesterday when it reported on one of the seven days before.</summary>
    public const int LookbackDays = 7;

    public string Key => "diary-not-written";

    public bool CanActOn(AccessRole roles) => Permissions.IsAllowed(roles, Permissions.DiaryWrite);

    public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken) => Missing(Yesterday(dates)).CountAsync(cancellationToken);

    public async Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
    {
        var day = Yesterday(dates);
        var missing = Missing(day);
        var count = await missing.CountAsync(cancellationToken);
        if (count == 0) return new NeedsActionGroupDto(Key, 0, []);

        // The first project by name, so the same eight show each time.
        var projects = await missing.OrderBy(project => project.Name).ThenBy(project => project.Id).Take(take)
            .Select(project => new { project.Id, project.Name }).ToListAsync(cancellationToken);
        var detail = $"No report for {day.ToString("ddd d MMM", CultureInfo.InvariantCulture)}";
        return new NeedsActionGroupDto(Key, count,
            projects.Select(project => new NeedsActionRowDto(project.Id, null, project.Name, project.Id, project.Name, detail, null, day, false, null)).ToArray());
    }

    /// <summary>The day a report was due: yesterday on the company's calendar.</summary>
    private static DateOnly Yesterday(NeedsActionDates dates) => dates.Today.AddDays(-1);

    private IQueryable<Project> Missing(DateOnly day)
    {
        var from = day.AddDays(-LookbackDays);
        var before = day.AddDays(-1);
        var entries = db.Set<SiteDiaryEntry>();
        return db.Set<Project>().AsNoTracking().Where(project => project.Status == ProjectStatus.Active &&
            !db.Set<CompanyHoliday>().Any(holiday => holiday.Date == day) &&
            !entries.Any(entry => entry.ProjectId == project.Id && entry.Status == DiaryStatus.Posted && entry.ReportDate == day) &&
            entries.Any(entry => entry.ProjectId == project.Id && entry.Status == DiaryStatus.Posted && entry.ReportDate >= from && entry.ReportDate <= before));
    }
}
