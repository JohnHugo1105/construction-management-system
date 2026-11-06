using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.SiteDiary;
using ConstructOPX.Infrastructure.Persistence;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo site reports for Riverside Residences (fictional): a week of reports ending the day before yesterday, so "Write today's report" is
/// offered and the "Site diary not written yesterday" row appears for people who write reports. Each report was written by the author the
/// caller gives (the Project Manager in the demo) on its own day. A report carries no money. Saves what it adds.
/// </summary>
public static class SiteDiaryDemo
{
    private static readonly (DiaryWeather Weather, string? WeatherNote, string WorkDone, string? Issues, string? Visitors, string? Safety, int Subcontractors)[] Week =
    [
        (DiaryWeather.Fair, null, "Formwork and rebar for the second-floor slab, grid A to D.", null, "Client's site representative walked the second floor.",
            "Toolbox talk on working at height. Everyone wore harnesses on the slab edge.", 6),
        (DiaryWeather.Cloudy, "Light wind in the afternoon.", "Continued rebar tying on the second-floor slab. Electrical conduit laid in the slab.", null, null, null, 6),
        (DiaryWeather.Rain, "Rain from 1 pm to 4 pm.", "Concrete pour for the second-floor slab, grid A to D, finished before the rain.",
            "Pour stopped for an hour when the rain started. Mixer truck waited on site.", "Building official inspected the formwork before the pour.",
            "Wet floor near the mixer. Mud cleared and the access road covered with gravel.", 8),
        (DiaryWeather.HeavyRain, null, "No work on site in the morning because of the heavy rain. Curing of the slab checked in the afternoon.",
            "Half a day lost to rain.", null, "Site drainage cleared.", 0),
        (DiaryWeather.Fair, null, "Removed side forms from the slab. Started masonry for the second-floor walls, grid A.", null, null, null, 5),
        (DiaryWeather.Fair, null, "Masonry for the second-floor walls, grids A and B. Plumbing rough-in for the ground-floor bathrooms.", "Delivery of hollow blocks came late.",
            null, "First-aid kit restocked.", 7),
        (DiaryWeather.Cloudy, null, "Masonry for the second-floor walls, grids C and D. Plastering started on the ground floor.", null,
            "Supplier's representative checked the block samples.", null, 7)
    ];

    /// <summary>Adds and saves the demo week of reports for Riverside Residences. Call it once, after the project and the Project Manager exist.</summary>
    /// <param name="db">The database to add the reports to.</param>
    /// <param name="tenantId">The organization the reports belong to.</param>
    /// <param name="riverside">The demo project the reports are for.</param>
    /// <param name="authorUserId">The person each report is written by (the Project Manager in the demo).</param>
    /// <param name="today">Today on the company's calendar; the reports end the day before yesterday.</param>
    /// <param name="now">The moment the reports are recorded as written.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    public static async Task SeedAsync(ConstructOpxDbContext db, Guid tenantId, Project riverside, Guid authorUserId, DateOnly today, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // The last report is for the day before yesterday: yesterday's is missing and today's is still to write.
        var last = today.AddDays(-2);
        for (var index = 0; index < Week.Length; index++)
        {
            var day = last.AddDays(index - (Week.Length - 1));
            var report = Week[index];
            db.Add(SiteDiaryEntry.Create(Guid.NewGuid(), tenantId, riverside, day,
                new SiteDiaryDetails(report.Weather, report.WeatherNote, report.WorkDone, report.Issues, report.Visitors, report.Safety, report.Subcontractors),
                authorUserId, null, today, now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
