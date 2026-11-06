using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// A demo schedule for Riverside Residences (fictional): five activities linked to its budget lines (found by work-breakdown code), with a
/// baseline set and then two activities moved later, so the timeline shows slip, the S-curve shows a baseline, and the masonry walls are past
/// their planned finish while still open (one "Needs your action" row). Call it after the seeder's budget lines and progress have been saved;
/// it skips itself when the project already has a schedule, and links only the lines it finds. Saves what it adds.
/// </summary>
public static class ScheduleDemo
{
    // Name, planned start and finish (days from today), and the budget lines (by work-breakdown code) whose work it covers.
    private static readonly (string Name, int StartDays, int FinishDays, string[] Codes)[] Plan =
    [
        ("Foundations and columns", -150, -40, ["3100", "3300"]),
        ("Rebar and slabs", -120, 20, ["3200"]),
        ("Masonry walls", -60, -5, ["4200"]),
        ("Electrical and plumbing rough-in", -30, 90, ["16200", "15400"]),
        ("Floor tiles and finishes", 10, 150, ["9300"])
    ];

    /// <summary>
    /// Adds the Riverside demo schedule and saves it; does nothing when Riverside already has a schedule. Call it from the development seeder
    /// once Riverside's budget lines (and its progress, for a realistic S-curve) are saved.
    /// </summary>
    /// <param name="db">The seeder's context; the method reads past the tenant filter, so it works before a person is signed in.</param>
    /// <param name="tenantId">The demo company the activities belong to.</param>
    /// <param name="riverside">The Riverside Residences project, not archived.</param>
    /// <param name="userId">Who the activities are recorded as created by (the demo administrator).</param>
    /// <param name="today">Today in the company's time zone: the planned dates are set around it.</param>
    /// <param name="now">The current instant, for the created, baseline and changed times.</param>
    public static async Task SeedAsync(ConstructOpxDbContext db, Guid tenantId, Project riverside, Guid userId, DateOnly today, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (await db.Set<ScheduleActivity>().IgnoreQueryFilters().AnyAsync(item => item.ProjectId == riverside.Id, cancellationToken)) return;

        var lines = await db.Set<BudgetLine>().IgnoreQueryFilters()
            .Where(line => line.TenantId == tenantId && line.ProjectId == riverside.Id && line.Status == BudgetLineStatus.Active && line.WbsCode != null)
            .Select(line => new { line.Id, line.WbsCode, line.Description })
            .ToListAsync(cancellationToken);
        var byCode = lines.GroupBy(line => line.WbsCode!).ToDictionary(group => group.Key, group => group.First());

        var activities = new List<ScheduleActivity>();
        var baselineAt = now.AddDays(-45);
        foreach (var (name, startDays, finishDays, codes) in Plan)
        {
            var found = codes.Where(byCode.ContainsKey).Select(code => byCode[code]).ToArray();
            var context = new ScheduleLineContext(found.ToDictionary(line => line.Id, line => line.Description), new Dictionary<Guid, string>());
            var activity = ScheduleActivity.Create(Guid.NewGuid(), tenantId, riverside,
                new ScheduleActivityDetails(name, today.AddDays(startDays), today.AddDays(finishDays), found.Select(line => line.Id).ToArray()), context,
                activities.Count, userId, null, now.AddDays(-60));
            activity.SetBaseline(riverside, baselineAt);
            activities.Add(activity);
        }

        // After the baseline: the rebar slipped two weeks and the rough-in a week, which the timeline shows as slip.
        Slip(activities[1], riverside, 14, now);
        Slip(activities[3], riverside, 7, now);
        db.AddRange(activities);
        await db.SaveChangesAsync(cancellationToken);

        static void Slip(ScheduleActivity activity, Project project, int days, DateTimeOffset now)
        {
            // The activity keeps its own lines, so they are the only lines the check needs to know about.
            var ids = activity.LinkedLineIds().ToArray();
            var own = new ScheduleLineContext(ids.ToDictionary(id => id, _ => ""), new Dictionary<Guid, string>());
            activity.Update(project, new ScheduleActivityDetails(activity.Name, activity.PlannedStart, activity.PlannedFinish.AddDays(days), ids), own, null,
                now);
        }
    }
}
