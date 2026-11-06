using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// A demo time clock for Riverside Residences (fictional): one kiosk at the main gate, a badge code for each of the crew, and the scans of the most recent
/// Sunday before today (a rest day, so the seeded attendance has none) for four of them: three clean days of 07:00 to 16:00 (8 hours after the break)
/// and one worker who scanned in and never out, so the review page and "Needs your action" show an exception. Nothing is confirmed: the day waits for a
/// person, as every day of scans does. Saves what it adds.
/// </summary>
public static class TimeClockDemo
{
    /// <summary>
    /// The demo kiosk's device key, for development only: enter it on the kiosk page (<c>/kiosk</c>) of a local database seeded with the demo. It is
    /// fixed so a developer can use it; a real device key is random and shown once.
    /// </summary>
    public const string DemoDeviceKey = "ZGVtby1raW9zay1rZXktZm9yLWRldmVsb3BtZW50LTE";

    public static async Task SeedAsync(ConstructOpxDbContext db, Organization organization, Project riverside, IReadOnlyList<Worker> crew, Guid userId,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        var kiosk = TimeClockDevice.Register(Guid.NewGuid(), tenant, "Main gate kiosk", riverside, TimeClockDeviceKind.Kiosk,
            DeviceKeys.HashOf(DemoDeviceKey)!, userId, null, now.AddDays(-14));
        db.Add(kiosk);

        foreach (var worker in crew.Where(item => item.Status == WorkerStatus.Active)) worker.IssuePunchCode(PunchCodes.New(), now);

        // The most recent Sunday before today: the crew rests on Sundays, so the seeded attendance leaves it empty.
        var sunday = today.AddDays(-1);
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(-1);
        var zone = organization.TimeClock.TimeZone;
        DateTimeOffset At(int hour, int minute) =>
            new(TimeZoneInfo.ConvertTimeToUtc(sunday.ToDateTime(new TimeOnly(hour, minute)), zone), TimeSpan.Zero);

        var scanners = crew.Take(4).ToArray();
        for (var index = 0; index < scanners.Length; index++)
        {
            var worker = scanners[index];
            // Arrivals a minute or two apart, as at a real gate.
            var arrived = At(6, 55 + index);
            AddScan(db, tenant, worker, riverside, kiosk, arrived, sunday, PunchDirection.In, now, index);
            // The last one forgot to scan out: a missing time-out for the reviewer.
            if (index < scanners.Length - 1) AddScan(db, tenant, worker, riverside, kiosk, At(16, 2 + index), sunday, PunchDirection.Out, now, index + 10);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddScan(ConstructOpxDbContext db, Guid tenant, Worker worker, Project project, TimeClockDevice kiosk, DateTimeOffset at,
        DateOnly workDate, PunchDirection direction, DateTimeOffset now, int sequence) =>
        db.Add(TimePunch.Receive(Guid.NewGuid(), tenant, worker.Id, project.Id, kiosk.Id, at, workDate, direction, directionWasAutomatic: true,
            PunchSource.Kiosk, $"demo-{workDate:yyyyMMdd}-{sequence:00}-{worker.EmployeeNumber}", notPostedHere: false, recordedByUserId: null, now));
}
