using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.TimeClock;

internal sealed class TimeClockDeviceRepository(ConstructOpxDbContext db) : ITimeClockDeviceRepository
{
    public Task<TimeClockDevice?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<TimeClockDevice>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ClientRequestUsedAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<TimeClockDevice>().AnyAsync(item => item.ClientRequestId == clientRequestId, cancellationToken);

    public async Task TouchLastSeenAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staleBefore = now.AddMinutes(-1);
        await db.Set<TimeClockDevice>().Where(item => item.Id == id && (item.LastSeenAtUtc == null || item.LastSeenAtUtc < staleBefore))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.LastSeenAtUtc, now), cancellationToken);
    }

    public void Add(TimeClockDevice device) => db.Set<TimeClockDevice>().Add(device);
}

/// <summary>Finds a device by its key's hash across every company (nobody is signed in yet), in one statement.</summary>
internal sealed class TimeClockDeviceKeyReader(ConstructOpxDbContext db) : ITimeClockDeviceKeyReader
{
    public Task<DeviceIdentity?> FindByKeyHashAsync(byte[] keyHash, CancellationToken cancellationToken) =>
        db.Set<TimeClockDevice>().IgnoreQueryFilters().AsNoTracking().Where(item => item.KeyHash == keyHash)
            .Select(item => new DeviceIdentity(item.Id, item.TenantId, item.Status == TimeClockDeviceStatus.Active, item.KeyHash))
            .SingleOrDefaultAsync(cancellationToken);
}

internal sealed class TimePunchRepository(ConstructOpxDbContext db) : ITimePunchRepository
{
    public Task<TimePunch?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<TimePunch>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PunchWorker>> FindWorkersByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var wanted = codes.ToArray();
        return await db.Set<Worker>().AsNoTracking().Where(worker => worker.PunchCode != null && wanted.Contains(worker.PunchCode))
            .Select(worker => new PunchWorker(worker.Id, worker.PunchCode!, worker.FirstName, worker.LastName, worker.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecordedPunch>> FindByClientPunchIdsAsync(Guid? deviceId, IReadOnlyCollection<string> clientPunchIds,
        CancellationToken cancellationToken)
    {
        var wanted = clientPunchIds.ToArray();
        return await (
            from punch in db.Set<TimePunch>().AsNoTracking()
            where punch.DeviceId == deviceId && punch.ClientPunchId != null && wanted.Contains(punch.ClientPunchId)
            join worker in db.Set<Worker>() on punch.WorkerId equals worker.Id
            select new RecordedPunch(punch.Id, punch.ClientPunchId!, punch.WorkerId, worker.FirstName, worker.LastName, punch.Direction, punch.PunchedAtUtc,
                punch.NotPostedHere)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PunchFact>> ListAsync(Guid projectId, IReadOnlyCollection<Guid> workerIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        return await db.Set<TimePunch>().AsNoTracking()
            .Where(punch => punch.ProjectId == projectId && punch.Status == TimePunchStatus.Recorded && punch.WorkDate >= from && punch.WorkDate <= to &&
                wanted.Contains(punch.WorkerId))
            .Select(punch => new PunchFact(punch.Id, punch.WorkerId, punch.DeviceId, punch.PunchedAtUtc, punch.WorkDate, punch.Direction))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<(Guid WorkerId, DateOnly Date)>> PostedAsync(Guid projectId, IReadOnlyCollection<Guid> workerIds, DateOnly from,
        DateOnly to, CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        // A posting covers its start date up to, but not including, its end date.
        var postings = await db.Set<WorkerAssignment>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && wanted.Contains(item.WorkerId) && item.StartDate <= to && (item.EndDate == null || item.EndDate > from))
            .Select(item => new { item.WorkerId, item.StartDate, item.EndDate })
            .ToListAsync(cancellationToken);
        var posted = new HashSet<(Guid, DateOnly)>();
        foreach (var posting in postings)
            for (var date = from; date <= to; date = date.AddDays(1))
                if (posting.StartDate <= date && (posting.EndDate is null || date < posting.EndDate)) posted.Add((posting.WorkerId, date));
        return posted;
    }

    public Task<bool> DayRecordedAsync(Guid workerId, Guid projectId, DateOnly workDate, CancellationToken cancellationToken) =>
        db.Set<AttendanceEntry>().AnyAsync(entry => entry.WorkerId == workerId && entry.ProjectId == projectId && entry.WorkDate == workDate &&
            entry.Status == AttendanceStatus.Recorded, cancellationToken);

    public async Task<IReadOnlySet<string>> CodesInUseAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var wanted = codes.ToArray();
        return (await db.Set<Worker>().AsNoTracking().Where(worker => worker.PunchCode != null && wanted.Contains(worker.PunchCode))
            .Select(worker => worker.PunchCode!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
    }

    public void Add(TimePunch punch) => db.Set<TimePunch>().Add(punch);
}

/// <summary>
/// Reads for the review page, the summaries, the devices and the badges, straight into rows. Each read is a fixed number of statements however many
/// scans, workers or devices exist. Every set carries the company filter.
/// </summary>
internal sealed class TimeClockReader(ConstructOpxDbContext db) : ITimeClockReader
{
    public Task<TimeClockProject?> ProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<Project>().AsNoTracking().Where(project => project.Id == projectId)
            .Select(project => new TimeClockProject(project.Id, project.Name, project.Status == ProjectStatus.Archived))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<DateOnly?> EarliestWaitingDateAsync(Guid projectId, DateOnly before, CancellationToken cancellationToken) =>
        Waiting(before).Where(punch => punch.ProjectId == projectId).Select(punch => (DateOnly?)punch.WorkDate).MinAsync(cancellationToken);

    public async Task<(int Total, IReadOnlyList<ReviewWorker> Rows)> WorkersAsync(Guid projectId, DateOnly workDate, PageRequest? page,
        CancellationToken cancellationToken)
    {
        var punches = db.Set<TimePunch>();
        var workers = db.Set<Worker>().AsNoTracking().Where(worker => punches.Any(punch => punch.WorkerId == worker.Id && punch.ProjectId == projectId &&
            punch.WorkDate == workDate && punch.Status == TimePunchStatus.Recorded));
        var ordered = workers.OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id);
        var total = page is null ? 0 : await workers.CountAsync(cancellationToken);
        var paged = page is null ? ordered : ordered.Skip(page.Skip).Take(page.SafePageSize);
        var rows = await paged.Select(worker => new ReviewWorker(worker.Id, worker.FirstName, worker.MiddleName, worker.LastName,
                db.Set<AttendanceEntry>().Any(entry => entry.WorkerId == worker.Id && entry.ProjectId == projectId && entry.WorkDate == workDate &&
                    entry.Status == AttendanceStatus.Recorded),
                db.Set<WorkerAssignment>().Any(posting => posting.WorkerId == worker.Id && posting.ProjectId == projectId && posting.StartDate <= workDate &&
                    (posting.EndDate == null || workDate < posting.EndDate))))
            .ToListAsync(cancellationToken);
        return (page is null ? rows.Count : total, rows);
    }

    public async Task<IReadOnlyList<ReviewPunch>> PunchesAsync(Guid projectId, DateOnly workDate, IReadOnlyCollection<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        return await (
            from punch in db.Set<TimePunch>().AsNoTracking()
            where punch.ProjectId == projectId && punch.WorkDate == workDate && punch.Status == TimePunchStatus.Recorded && wanted.Contains(punch.WorkerId)
            join device in db.Set<TimeClockDevice>() on punch.DeviceId equals device.Id into devices
            from device in devices.DefaultIfEmpty()
            orderby punch.PunchedAtUtc, punch.Id
            select new ReviewPunch(punch.Id, punch.WorkerId, punch.PunchedAtUtc, punch.Direction, punch.Source, device.Name, punch.NotPostedHere,
                punch.Reason, punch.Version)).ToListAsync(cancellationToken);
    }

    public async Task<TimeClockSummaryDto> SummaryAsync(Guid projectId, DateOnly today, CancellationToken cancellationToken)
    {
        var summary = await WaitingDays(today).Where(day => day.ProjectId == projectId)
            .GroupBy(day => day.ProjectId)
            .Select(group => new
            {
                Days = group.Select(day => day.WorkDate).Distinct().Count(),
                Rows = group.Count(),
                Exceptions = group.Count(day => day.HasException),
                Earliest = group.Min(day => day.WorkDate)
            }).FirstOrDefaultAsync(cancellationToken);
        return summary is null
            ? new TimeClockSummaryDto(projectId, 0, 0, 0, null)
            : new TimeClockSummaryDto(projectId, summary.Days, summary.Rows, summary.Exceptions, summary.Earliest);
    }

    public async Task<IReadOnlyList<TimeClockDeviceDto>> DevicesAsync(CancellationToken cancellationToken) =>
        await (
            from device in db.Set<TimeClockDevice>().AsNoTracking()
            join project in db.Set<Project>() on device.ProjectId equals project.Id
            orderby device.Status, device.Name, device.Id
            select new TimeClockDeviceDto(device.Id, device.Name, device.ProjectId, project.Name, device.Kind.ToString(), device.Status.ToString(),
                device.LastSeenAtUtc, device.CreatedAtUtc, device.Version)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BadgeDto>> BadgesAsync(IReadOnlyCollection<Guid> workerIds, DateOnly today, CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        // Only the designation is read from the pay history, never an amount.
        var rows = await db.Set<Worker>().AsNoTracking().Where(worker => wanted.Contains(worker.Id))
            .OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id)
            .Select(worker => new
            {
                worker.Id,
                worker.FirstName,
                worker.MiddleName,
                worker.LastName,
                worker.EmployeeNumber,
                worker.PunchCode,
                JobTitle = db.Set<WorkerRate>().Where(rate => rate.WorkerId == worker.Id && rate.EffectiveFrom <= today)
                    .OrderByDescending(rate => rate.EffectiveFrom).Select(rate => rate.Designation).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return rows.Select(row => new BadgeDto(row.Id, WorkerNames.Full(row.FirstName, row.MiddleName, row.LastName), row.EmployeeNumber, row.JobTitle,
            row.PunchCode)).ToArray();
    }

    public async Task<(int Count, IReadOnlyList<Guid> FirstIds)> ActiveWithoutCodeAsync(int take, CancellationToken cancellationToken)
    {
        var workers = db.Set<Worker>().AsNoTracking().Where(worker => worker.Status == WorkerStatus.Active && worker.PunchCode == null);
        var count = await workers.CountAsync(cancellationToken);
        if (take <= 0 || count == 0) return (count, []);
        var ids = await workers.OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id)
            .Select(worker => worker.Id).Take(take).ToListAsync(cancellationToken);
        return (count, ids);
    }

    /// <summary>Scans (not removed) before <paramref name="before"/> whose worker has no attendance on the project that day.</summary>
    private IQueryable<TimePunch> Waiting(DateOnly before) =>
        db.Set<TimePunch>().AsNoTracking().Where(punch => punch.Status == TimePunchStatus.Recorded && punch.WorkDate < before &&
            !db.Set<AttendanceEntry>().Any(entry => entry.WorkerId == punch.WorkerId && entry.ProjectId == punch.ProjectId && entry.WorkDate == punch.WorkDate &&
                entry.Status == AttendanceStatus.Recorded));

    /// <summary>One row per worker, project and day that waits, flagged when its scans don't pair up or the worker wasn't posted there.</summary>
    internal IQueryable<WaitingDay> WaitingDays(DateOnly before) =>
        Waiting(before).GroupBy(punch => new { punch.ProjectId, punch.WorkerId, punch.WorkDate })
            .Select(group => new WaitingDay
            {
                ProjectId = group.Key.ProjectId,
                WorkerId = group.Key.WorkerId,
                WorkDate = group.Key.WorkDate,
                HasException = group.Count(punch => punch.Direction == PunchDirection.In) != group.Count(punch => punch.Direction == PunchDirection.Out) ||
                    group.Count(punch => punch.NotPostedHere) > 0
            });
}

/// <summary>A worker's waiting day on a project. A class with initializers so it composes into further SQL.</summary>
internal sealed class WaitingDay
{
    public Guid ProjectId { get; init; }
    public Guid WorkerId { get; init; }
    public DateOnly WorkDate { get; init; }
    public bool HasException { get; init; }
}
