using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Workforce;

internal sealed class WorkerRepository(ConstructOpxDbContext db) : IWorkerRepository
{
    public Task<Worker?> GetAsync(Guid id, bool includeHistory, CancellationToken cancellationToken) =>
        Workers(includeHistory).SingleOrDefaultAsync(worker => worker.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Worker>> GetManyAsync(IReadOnlyCollection<Guid> ids, bool includeHistory, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await Workers(includeHistory).Where(worker => wanted.Contains(worker.Id)).ToListAsync(cancellationToken);
    }

    public async Task<DateOnly?> LatestWorkDateInPayrollAsync(Guid workerId, CancellationToken cancellationToken) =>
        await db.Set<AttendanceEntry>().AsNoTracking()
            .Where(entry => entry.WorkerId == workerId && entry.PayrollRunId != null)
            .Select(entry => (DateOnly?)entry.WorkDate)
            .MaxAsync(cancellationToken);

    public Task<bool> EmployeeNumberExistsAsync(string normalizedEmployeeNumber, Guid? excludeWorkerId, CancellationToken cancellationToken) =>
        db.Set<Worker>().AnyAsync(worker => worker.NormalizedEmployeeNumber == normalizedEmployeeNumber && worker.Id != excludeWorkerId, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<Worker>().Where(worker => worker.ClientRequestId == clientRequestId).Select(worker => (Guid?)worker.Id).SingleOrDefaultAsync(cancellationToken);

    public void Add(Worker worker) => db.Set<Worker>().Add(worker);

    private IQueryable<Worker> Workers(bool includeHistory)
    {
        // The status history is always loaded: whether a day can be recorded as worked depends on it. It is a handful of rows per worker.
        IQueryable<Worker> workers = db.Set<Worker>().Include(worker => worker.StatusChanges);
        // Split, so collections don't multiply each other's rows.
        return includeHistory
            ? workers.Include(worker => worker.Rates).Include(worker => worker.Assignments).AsSplitQuery()
            : workers.AsSplitQuery();
    }
}

internal sealed class WorkerReader(ConstructOpxDbContext db) : IWorkerReader
{
    private const int LookupLimit = 500;

    public async Task<PagedResult<WorkerListItemDto>> ListAsync(WorkerListQuery query, DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var workers = db.Set<Worker>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WorkerStatus>(query.Status, true, out var status) && Enum.IsDefined(status))
            workers = workers.Where(worker => worker.Status == status);
        if (query.ProjectId is { } projectId)
            workers = workers.Where(worker => worker.Assignments.Any(assignment => assignment.EndDate == null && assignment.ProjectId == projectId));
        workers = Search(workers, query.Search);

        var total = await workers.CountAsync(cancellationToken);
        var rows = await workers
            .OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(worker => new
            {
                worker.Id,
                worker.EmployeeNumber,
                worker.FirstName,
                worker.MiddleName,
                worker.LastName,
                worker.Status,
                worker.DateHired,
                worker.Version,
                // The designation in force today and the current posting come from correlated subqueries, so the whole page is one statement.
                Designation = worker.Rates.Where(rate => rate.EffectiveFrom <= today).OrderByDescending(rate => rate.EffectiveFrom)
                    .Select(rate => rate.Designation).FirstOrDefault(),
                CurrentProjectId = worker.Assignments.Where(assignment => assignment.EndDate == null).Select(assignment => (Guid?)assignment.ProjectId).FirstOrDefault(),
                CurrentProjectName = worker.Assignments.Where(assignment => assignment.EndDate == null)
                    .Select(assignment => db.Set<Project>().Where(project => project.Id == assignment.ProjectId).Select(project => project.Name).FirstOrDefault())
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new WorkerListItemDto(row.Id, row.EmployeeNumber, WorkerNames.Full(row.FirstName, row.MiddleName, row.LastName),
            row.Status.ToString(), row.Designation, row.CurrentProjectId, row.CurrentProjectName, row.DateHired, row.Version)).ToArray();
        return PagedResult<WorkerListItemDto>.Of(items, total, page);
    }

    public async Task<IReadOnlyList<WorkerLookupDto>> LookupAsync(string? search, bool includeLeft, CancellationToken cancellationToken)
    {
        var workers = db.Set<Worker>().AsNoTracking();
        if (!includeLeft)
            workers = workers.Where(worker => worker.Status != WorkerStatus.Resigned && worker.Status != WorkerStatus.Retired && worker.Status != WorkerStatus.Terminated);
        workers = Search(workers, search);

        var rows = await workers.OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id).Take(LookupLimit)
            .Select(worker => new { worker.Id, worker.FirstName, worker.MiddleName, worker.LastName, worker.EmployeeNumber, worker.Status })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new WorkerLookupDto(row.Id, WorkerNames.Full(row.FirstName, row.MiddleName, row.LastName), row.EmployeeNumber,
            row.Status.ToString())).ToArray();
    }

    public async Task<WorkerDetailDto?> GetAsync(Guid id, bool includePay, DateOnly today, CancellationToken cancellationToken)
    {
        var worker = await db.Set<Worker>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (worker is null) return null;

        // Pay history is the only place a pay figure is read. Without permission only the designations are read, never the amounts.
        string? designation;
        long? currentRateMinor = null;
        string? currentRateUnit = null;
        IReadOnlyList<WorkerRateDto> rates = [];
        if (includePay)
        {
            var history = await db.Set<WorkerRate>().AsNoTracking().Where(rate => rate.WorkerId == id).OrderByDescending(rate => rate.EffectiveFrom)
                .ToListAsync(cancellationToken);
            var inForce = history.FirstOrDefault(rate => rate.EffectiveFrom <= today);
            designation = inForce?.Designation;
            currentRateMinor = inForce?.RateMinor;
            currentRateUnit = inForce?.Unit.ToString();
            rates = history.Select(rate => new WorkerRateDto(rate.Id, rate.Designation, rate.RateMinor, rate.Unit.ToString(), rate.Category.ToString(),
                rate.EffectiveFrom, rate.Reason, rate.RecordedAtUtc)).ToArray();
        }
        else
        {
            designation = await db.Set<WorkerRate>().AsNoTracking().Where(rate => rate.WorkerId == id && rate.EffectiveFrom <= today)
                .OrderByDescending(rate => rate.EffectiveFrom).Select(rate => rate.Designation).FirstOrDefaultAsync(cancellationToken);
        }

        var assignments = await (
            from assignment in db.Set<WorkerAssignment>().AsNoTracking()
            where assignment.WorkerId == id
            join project in db.Set<Project>() on assignment.ProjectId equals project.Id
            join sub in db.Set<SubProject>() on assignment.SubProjectId equals sub.Id into subs
            from sub in subs.DefaultIfEmpty()
            orderby assignment.StartDate descending
            select new WorkerAssignmentDto(assignment.Id, assignment.ProjectId, project.Name, assignment.SubProjectId, sub.Name, assignment.StartDate,
                assignment.EndDate, assignment.EndDate == null)).ToListAsync(cancellationToken);

        var cases = await (
            from workerCase in db.Set<WorkerCase>().AsNoTracking()
            where workerCase.WorkerId == id
            join project in db.Set<Project>() on workerCase.ProjectId equals project.Id into projects
            from project in projects.DefaultIfEmpty()
            orderby workerCase.CaseDate descending, workerCase.RecordedAtUtc descending
            select new WorkerCaseDto(workerCase.Id, workerCase.CaseDate, workerCase.Title, workerCase.Remarks, workerCase.ProjectId, project.Name,
                workerCase.RecordedAtUtc)).ToListAsync(cancellationToken);

        return new WorkerDetailDto(worker.Id, worker.EmployeeNumber, worker.FirstName, worker.MiddleName, worker.LastName, worker.FullName, worker.DateOfBirth,
            worker.DateHired, worker.MaritalStatus, worker.SpouseName, worker.Religion, worker.EducationalAttainment, worker.PresentAddress,
            worker.ProvincialAddress, worker.ContactNumber, worker.Reference, worker.Status.ToString(), worker.StatusChangedOn, worker.StatusReason,
            designation, currentRateMinor, currentRateUnit, assignments.FirstOrDefault(item => item.IsCurrent), assignments, rates, cases, worker.Version,
            includePay ? new WorkerStatutoryDto(worker.IsSssMember, worker.IsPhilHealthMember, worker.IsPagIbigMember, worker.IsTaxExempt) : null,
            RestDayText.Format(worker.RestDay));
    }

    private static IQueryable<Worker> Search(IQueryable<Worker> workers, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return workers;
        var pattern = Sql.Contains(search);
        return workers.Where(worker => EF.Functions.Like(worker.EmployeeNumber, pattern, Sql.LikeEscape) ||
            EF.Functions.Like(worker.FirstName, pattern, Sql.LikeEscape) ||
            (worker.MiddleName != null && EF.Functions.Like(worker.MiddleName, pattern, Sql.LikeEscape)) ||
            EF.Functions.Like(worker.LastName, pattern, Sql.LikeEscape) ||
            EF.Functions.Like(worker.FirstName + " " + worker.LastName, pattern, Sql.LikeEscape) ||
            EF.Functions.Like(worker.LastName + " " + worker.FirstName, pattern, Sql.LikeEscape));
    }
}
