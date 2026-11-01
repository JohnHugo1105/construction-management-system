using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Payroll;

internal sealed class AttendanceRepository(CmsDbContext db) : IAttendanceRepository
{
    public Task<AttendanceEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<AttendanceEntry>().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<AttendanceEntry>().Where(entry => entry.ClientRequestId == clientRequestId).Select(entry => (Guid?)entry.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListIdsByBatchAsync(string batchRequestId, CancellationToken cancellationToken)
    {
        // StartsWith is escaped by EF, so a '%' or '_' in the id is taken literally.
        var prefix = batchRequestId + "#";
        return await db.Set<AttendanceEntry>().Where(entry => entry.ClientRequestId != null && entry.ClientRequestId.StartsWith(prefix))
            .Select(entry => entry.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExistingAttendance>> ListActiveAsync(Guid projectId, DateOnly workDate, IReadOnlyCollection<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        return await db.Set<AttendanceEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.WorkDate == workDate && entry.Status == AttendanceStatus.Recorded && wanted.Contains(entry.WorkerId))
            .Select(entry => new ExistingAttendance(entry.Id, entry.WorkerId, entry.SubProjectId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> TotalHoursAsync(DateOnly workDate, IReadOnlyCollection<Guid> workerIds, Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        var totals = await db.Set<AttendanceEntry>().AsNoTracking()
            .Where(entry => entry.WorkDate == workDate && entry.Status == AttendanceStatus.Recorded && wanted.Contains(entry.WorkerId) && entry.Id != excludeEntryId)
            .GroupBy(entry => entry.WorkerId)
            .Select(group => new { WorkerId = group.Key, Hours = group.Sum(entry => entry.RegularHours + entry.OvertimeHours) })
            .ToListAsync(cancellationToken);
        return totals.ToDictionary(item => item.WorkerId, item => item.Hours);
    }

    public async Task<IReadOnlyList<AttendanceEntry>> ListUnpaidAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.Set<AttendanceEntry>()
            .Where(entry => entry.Status == AttendanceStatus.Recorded && entry.PayrollRunId == null && entry.WorkDate >= from && entry.WorkDate <= to)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AttendanceEntry>> ListByPayrollRunAsync(Guid payrollRunId, CancellationToken cancellationToken) =>
        await db.Set<AttendanceEntry>().Where(entry => entry.PayrollRunId == payrollRunId).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> WorkersPaidForUnworkedHolidayAsync(DateOnly workDate, IReadOnlyCollection<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        var wanted = workerIds.ToArray();
        return (await db.Set<PayrollHolidayPay>().AsNoTracking()
                .Where(holiday => holiday.ClaimDate == workDate && wanted.Contains(holiday.WorkerId))
                .Select(holiday => holiday.WorkerId).ToListAsync(cancellationToken))
            .ToHashSet();
    }

    public void Add(AttendanceEntry entry) => db.Set<AttendanceEntry>().Add(entry);
}

internal sealed class AttendanceReader(CmsDbContext db) : IAttendanceReader
{
    public async Task<AttendanceEntryDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await GetManyAsync([id], cancellationToken)).FirstOrDefault();

    public async Task<IReadOnlyList<AttendanceEntryDto>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        var rows = await Rows(db.Set<AttendanceEntry>().AsNoTracking().Where(entry => wanted.Contains(entry.Id)))
            .OrderBy(row => row.LastName).ThenBy(row => row.FirstName).ThenBy(row => row.WorkDate).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToArray();
    }

    public async Task<AttendanceSheetDto?> SheetAsync(Guid projectId, DateOnly workDate, CancellationToken cancellationToken)
    {
        // The project's name and the company holiday on the day (if any), in one statement.
        var heading = await db.Set<Project>().AsNoTracking().Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.Name,
                Holiday = db.Set<CompanyHoliday>().Where(holiday => holiday.Date == workDate)
                    .Select(holiday => new { holiday.Name, holiday.Type }).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (heading is null) return null;
        var projectName = heading.Name;

        var rows = await Rows(db.Set<AttendanceEntry>().AsNoTracking()
                .Where(entry => entry.ProjectId == projectId && entry.WorkDate == workDate && entry.Status == AttendanceStatus.Recorded))
            .OrderBy(row => row.LastName).ThenBy(row => row.FirstName).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

        // The crew is who was posted to the project on that day, still active, and has no entry on it yet.
        var crew = await (
            from assignment in db.Set<WorkerAssignment>().AsNoTracking()
            where assignment.ProjectId == projectId && assignment.StartDate <= workDate && (assignment.EndDate == null || workDate < assignment.EndDate)
            join worker in db.Set<Worker>() on assignment.WorkerId equals worker.Id
            where worker.Status == WorkerStatus.Active &&
                !db.Set<AttendanceEntry>().Any(entry => entry.WorkerId == worker.Id && entry.ProjectId == projectId && entry.WorkDate == workDate &&
                    entry.Status == AttendanceStatus.Recorded)
            orderby worker.LastName, worker.FirstName, worker.Id
            select new { worker.Id, worker.FirstName, worker.MiddleName, worker.LastName, worker.EmployeeNumber, worker.Status }).ToListAsync(cancellationToken);

        var entries = rows.Select(ToDto).ToArray();
        return new AttendanceSheetDto(projectId, projectName, workDate, entries, entries.Sum(entry => entry.RegularHours), entries.Sum(entry => entry.OvertimeHours),
            crew.Select(item => new WorkerLookupDto(item.Id, WorkerNames.Full(item.FirstName, item.MiddleName, item.LastName), item.EmployeeNumber,
                item.Status.ToString())).ToArray(), heading.Holiday?.Name, heading.Holiday?.Type.ToString());
    }

    public async Task<PagedResult<AttendanceEntryDto>> ListAsync(AttendanceListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var entries = db.Set<AttendanceEntry>().AsNoTracking();
        if (!query.IncludeVoided) entries = entries.Where(entry => entry.Status == AttendanceStatus.Recorded);
        if (query.WorkerId is { } workerId) entries = entries.Where(entry => entry.WorkerId == workerId);
        if (query.ProjectId is { } projectId) entries = entries.Where(entry => entry.ProjectId == projectId);
        if (query.From is { } from) entries = entries.Where(entry => entry.WorkDate >= from);
        if (query.To is { } to) entries = entries.Where(entry => entry.WorkDate <= to);

        var total = await entries.CountAsync(cancellationToken);
        var rows = await Rows(entries)
            .OrderByDescending(row => row.WorkDate).ThenBy(row => row.LastName).ThenBy(row => row.FirstName).ThenBy(row => row.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(cancellationToken);
        return PagedResult<AttendanceEntryDto>.Of(rows.Select(ToDto).ToArray(), total, page);
    }

    // A class built with an object initializer, not a record built through its constructor: EF can order and filter by the members of
    // the former inside a query, but only read the latter once, as the final projection.
    private sealed class AttendanceRow
    {
        public Guid Id { get; init; }
        public Guid WorkerId { get; init; }
        public string FirstName { get; init; } = "";
        public string? MiddleName { get; init; }
        public string LastName { get; init; } = "";
        public string EmployeeNumber { get; init; } = "";
        public Guid ProjectId { get; init; }
        public string ProjectName { get; init; } = "";
        public Guid? SubProjectId { get; init; }
        public string? SubProjectName { get; init; }
        public DateOnly WorkDate { get; init; }
        public decimal RegularHours { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal NightHours { get; init; }
        public string? Remarks { get; init; }
        public AttendanceStatus Status { get; init; }
        public Guid? PayrollRunId { get; init; }
        public string? VoidReason { get; init; }
        public int Version { get; init; }
        public DayOfWeek? RestDay { get; init; }
        public HolidayType? Holiday { get; init; }
    }

    private IQueryable<AttendanceRow> Rows(IQueryable<AttendanceEntry> entries) =>
        from entry in entries
        join worker in db.Set<Worker>() on entry.WorkerId equals worker.Id
        join project in db.Set<Project>() on entry.ProjectId equals project.Id
        join sub in db.Set<SubProject>() on entry.SubProjectId equals sub.Id into subs
        from sub in subs.DefaultIfEmpty()
        select new AttendanceRow
        {
            Id = entry.Id,
            WorkerId = entry.WorkerId,
            FirstName = worker.FirstName,
            MiddleName = worker.MiddleName,
            LastName = worker.LastName,
            EmployeeNumber = worker.EmployeeNumber,
            ProjectId = entry.ProjectId,
            ProjectName = project.Name,
            SubProjectId = entry.SubProjectId,
            SubProjectName = sub.Name,
            WorkDate = entry.WorkDate,
            RegularHours = entry.RegularHours,
            OvertimeHours = entry.OvertimeHours,
            NightHours = entry.NightHours,
            Remarks = entry.Remarks,
            Status = entry.Status,
            PayrollRunId = entry.PayrollRunId,
            VoidReason = entry.VoidReason,
            Version = entry.Version,
            RestDay = worker.RestDay,
            // At most one holiday per date: read in the same statement, never once per entry.
            Holiday = db.Set<CompanyHoliday>().Where(holiday => holiday.Date == entry.WorkDate).Select(holiday => (HolidayType?)holiday.Type).FirstOrDefault()
        };

    private static AttendanceEntryDto ToDto(AttendanceRow row) => new(row.Id, row.WorkerId, WorkerNames.Full(row.FirstName, row.MiddleName, row.LastName),
        row.EmployeeNumber, row.ProjectId, row.ProjectName, row.SubProjectId, row.SubProjectName, row.WorkDate, row.RegularHours, row.OvertimeHours, row.Remarks,
        row.Status.ToString(), row.PayrollRunId is not null, row.PayrollRunId, row.VoidReason, row.Version, row.NightHours,
        DayTypes.Of(row.WorkDate, row.Holiday, row.RestDay).ToString());
}
