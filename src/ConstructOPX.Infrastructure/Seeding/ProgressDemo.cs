using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo progress for the "Riverside Residences" project the development seeder creates: six weeks of site updates on its budget lines, so the
/// Progress tab shows earned value against what was spent and planned (the work is over budget and behind schedule, with one line of rework
/// in the history). Fictional and small. Call it once, after the seeder's own data has been saved: it reads the project, its budget lines
/// (found by work-breakdown code) and the demo users, and skips itself when the project already has progress.
/// </summary>
public static class ProgressDemo
{
    private const string ProjectName = "Riverside Residences";

    // One update per row: how many days ago, who recorded it, and the percent of each line (by work-breakdown code) that changed.
    private static readonly (int DaysAgo, string User, (string Wbs, int PercentBps, string? Note)[] Lines)[] Updates =
    [
        (42, "manager", [("3300", 3_000, null), ("3200", 3_500, null)]),
        (28, "manager", [("3300", 6_000, null), ("3200", 7_000, null), ("4200", 1_500, null)]),
        (21, "cost", [("3200", 6_500, "Two columns were re-tied after the inspection")]),
        (14, "manager", [("3300", 8_000, null), ("3200", 8_500, null), ("4200", 3_000, null), ("9300", 1_000, null)]),
        (2, "manager", [("3300", 8_500, null), ("3200", 9_000, null), ("4200", 4_000, null), ("9300", 2_000, null)])
    ];

    public static async Task SeedAsync(ConstructOpxDbContext db, Guid tenantId, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var project = await db.Set<Project>().IgnoreQueryFilters().Include(item => item.SubProjects)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Name == ProjectName, cancellationToken);
        if (project is null) return;
        if (await db.Set<ProgressEntry>().IgnoreQueryFilters().AnyAsync(entry => entry.ProjectId == project.Id, cancellationToken)) return;

        var linesByCode = (await db.Set<BudgetLine>().IgnoreQueryFilters()
                .Where(line => line.TenantId == tenantId && line.ProjectId == project.Id && line.Status == BudgetLineStatus.Active && line.WbsCode != null)
                .ToListAsync(cancellationToken))
            .ToDictionary(line => line.WbsCode!);
        var activeLines = linesByCode.Values.ToDictionary(line => line.Id);
        var userIds = await db.Set<User>().IgnoreQueryFilters().Where(user => user.TenantId == tenantId)
            .ToDictionaryAsync(user => user.Username, user => user.Id, cancellationToken);

        var latest = new Dictionary<Guid, LatestProgress>();
        foreach (var (daysAgo, username, lines) in Updates)
        {
            var asOf = today.AddDays(-daysAgo);
            var changes = lines.Where(line => linesByCode.ContainsKey(line.Wbs))
                .Select(line => new ProgressChange(linesByCode[line.Wbs].Id, line.PercentBps, line.Note, null)).ToArray();
            if (changes.Length == 0 || !userIds.TryGetValue(username, out var userId)) continue;

            var plan = ProgressRecorder.Plan(tenantId, project, asOf, today, changes, activeLines, latest, userId, null, now.AddDays(-daysAgo));
            db.Set<ProgressEntry>().AddRange(plan.Entries);
            foreach (var entry in plan.Entries) latest[entry.BudgetLineId] = new LatestProgress(entry.PercentCompleteBps, entry.AsOfDate);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
