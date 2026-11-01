using Cms.Application.Common;
using Cms.Application.Features.Statutory;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Statutory;
using Cms.Domain.Features.Workforce;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Statutory;

internal sealed class StatutoryScheduleRepository(CmsDbContext db) : IStatutoryScheduleRepository
{
    public Task<StatutorySchedule?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<StatutorySchedule>().Include(schedule => schedule.Rows).SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<StatutorySchedule>().Where(schedule => schedule.ClientRequestId == clientRequestId).Select(schedule => (Guid?)schedule.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> EffectiveDateTakenAsync(StatutoryAgency agency, DateOnly effectiveFrom, Guid? excludeId, CancellationToken cancellationToken) =>
        db.Set<StatutorySchedule>().AnyAsync(schedule => schedule.Agency == agency && schedule.EffectiveFrom == effectiveFrom &&
            schedule.Status == StatutoryScheduleStatus.Active && schedule.Id != excludeId, cancellationToken);

    public Task<bool> IsUsedByApprovedPayrollAsync(Guid id, CancellationToken cancellationToken) =>
        UsedByApprovedPayroll(db, id).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<StatutorySchedule>> ListInForceAsync(DateOnly onDate, CancellationToken cancellationToken) =>
        await InForce(db, onDate).Include(schedule => schedule.Rows).AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, StatutoryParticipation>> ParticipationAsync(IReadOnlyCollection<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        if (workerIds.Count == 0) return new Dictionary<Guid, StatutoryParticipation>();
        var rows = await db.Set<Worker>().AsNoTracking().Where(worker => workerIds.Contains(worker.Id))
            .Select(worker => new { worker.Id, worker.IsSssMember, worker.IsPhilHealthMember, worker.IsPagIbigMember, worker.IsTaxExempt })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => new StatutoryParticipation(row.IsSssMember, row.IsPhilHealthMember, row.IsPagIbigMember, row.IsTaxExempt));
    }

    public void Add(StatutorySchedule schedule) => db.Set<StatutorySchedule>().Add(schedule);

    /// <summary>
    /// The active tables with the latest effective date on or before the day, one per agency: a table counts unless another active table of
    /// its agency starts later but still on or before the day. One statement.
    /// </summary>
    internal static IQueryable<StatutorySchedule> InForce(CmsDbContext db, DateOnly onDate) =>
        db.Set<StatutorySchedule>().Where(schedule => schedule.Status == StatutoryScheduleStatus.Active && schedule.EffectiveFrom <= onDate &&
            !db.Set<StatutorySchedule>().Any(later => later.Agency == schedule.Agency && later.Status == StatutoryScheduleStatus.Active &&
                later.EffectiveFrom <= onDate && later.EffectiveFrom > schedule.EffectiveFrom));

    /// <summary>Deductions taken from the table by a payroll that was approved (including one voided since: it was paid out of that table).</summary>
    internal static IQueryable<PayrollDeduction> UsedByApprovedPayroll(CmsDbContext db, Guid scheduleId) =>
        from deduction in db.Set<PayrollDeduction>()
        where deduction.ScheduleId == scheduleId
        join line in db.Set<PayrollLine>() on deduction.PayrollLineId equals line.Id
        join run in db.Set<PayrollRun>() on line.PayrollRunId equals run.Id
        where run.ApprovedAtUtc != null
        select deduction;
}

internal sealed class StatutoryReader(CmsDbContext db) : IStatutoryReader
{
    public async Task<PagedResult<StatutoryScheduleListItemDto>> ListAsync(StatutoryScheduleListQuery query, DateOnly today, PageRequest page,
        CancellationToken cancellationToken)
    {
        var schedules = db.Set<StatutorySchedule>().AsNoTracking();
        if (!query.IncludeRetired) schedules = schedules.Where(schedule => schedule.Status == StatutoryScheduleStatus.Active);
        if (StatutoryText.Agency(query.Agency) is { } agency) schedules = schedules.Where(schedule => schedule.Agency == agency);

        var inForce = StatutoryScheduleRepository.InForce(db, today).Select(schedule => schedule.Id);
        var total = await schedules.CountAsync(cancellationToken);
        var rows = await schedules.OrderBy(schedule => schedule.Agency == StatutoryAgency.Sss ? 0
                : schedule.Agency == StatutoryAgency.PhilHealth ? 1
                : schedule.Agency == StatutoryAgency.PagIbig ? 2 : 3)
            .ThenByDescending(schedule => schedule.EffectiveFrom).ThenBy(schedule => schedule.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(schedule => new
            {
                schedule.Id,
                schedule.Agency,
                schedule.Name,
                schedule.EffectiveFrom,
                schedule.Status,
                schedule.Method,
                schedule.PeriodBasis,
                RowCount = schedule.Rows.Count(),
                InForce = inForce.Contains(schedule.Id),
                schedule.Version
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new StatutoryScheduleListItemDto(row.Id, row.Agency.ToString(), StatutoryAgencies.Label(row.Agency), row.Name,
            row.EffectiveFrom, row.Status.ToString(), row.Method.ToString(), row.PeriodBasis.ToString(), row.RowCount, row.InForce, row.Version)).ToArray();
        return PagedResult<StatutoryScheduleListItemDto>.Of(items, total, page);
    }

    public async Task<StatutoryScheduleDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var inForce = StatutoryScheduleRepository.InForce(db, today).Select(schedule => schedule.Id);
        var used = StatutoryScheduleRepository.UsedByApprovedPayroll(db, id);
        var schedule = await db.Set<StatutorySchedule>().AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            Schedule = item,
            InForce = inForce.Contains(item.Id),
            Used = used.Any()
        }).SingleOrDefaultAsync(cancellationToken);
        if (schedule is null) return null;

        var rows = await db.Set<StatutoryScheduleRow>().AsNoTracking().Where(row => row.ScheduleId == id).OrderBy(row => row.RowNumber)
            .Select(row => new StatutoryRowDto(row.FromMinor, row.ToMinor, row.EmployeeMinor, row.EmployerMinor, row.BaseTaxMinor, row.RateBps))
            .ToListAsync(cancellationToken);

        var s = schedule.Schedule;
        return new StatutoryScheduleDto(s.Id, s.Agency.ToString(), StatutoryAgencies.Label(s.Agency), s.Name, s.EffectiveFrom, s.Status.ToString(),
            s.Method.ToString(), s.PeriodBasis.ToString(), s.EmployeeRateBps, s.EmployerRateBps, s.BaseFloorMinor, s.BaseCeilingMinor, s.EmployeeMinimumMinor,
            s.EmployeeMaximumMinor, s.EmployerMinimumMinor, s.EmployerMaximumMinor, rows, schedule.Used, schedule.InForce, s.RetiredAtUtc, s.Version);
    }

    public async Task<IReadOnlyList<StatutoryScheduleSummaryDto>> InForceAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await StatutoryScheduleRepository.InForce(db, today).AsNoTracking()
            .Select(schedule => new { schedule.Id, schedule.Agency, schedule.Name, schedule.EffectiveFrom, schedule.Method, schedule.PeriodBasis })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new StatutoryScheduleSummaryDto(row.Id, row.Agency.ToString(), row.Name, row.EffectiveFrom, row.Method.ToString(),
            row.PeriodBasis.ToString())).ToArray();
    }

    public async Task<PayrollStatutoryDto?> RunStatutoryAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.Set<PayrollRun>().AsNoTracking().Where(item => item.Id == runId)
            .Select(item => new { item.Id, item.Status, item.RunsPerMonth }).SingleOrDefaultAsync(cancellationToken);
        if (run is null) return null;

        // Every calculated deduction of the run with its worker and table, in one statement however many workers there are.
        var shares = await (
            from deduction in db.Set<PayrollDeduction>().AsNoTracking()
            where deduction.Source == PayrollDeductionSource.Statutory
            join line in db.Set<PayrollLine>() on deduction.PayrollLineId equals line.Id
            where line.PayrollRunId == runId
            join schedule in db.Set<StatutorySchedule>() on deduction.ScheduleId equals schedule.Id into tables
            from schedule in tables.DefaultIfEmpty()
            orderby line.WorkerName, line.Id
            select new
            {
                line.WorkerId,
                line.WorkerName,
                line.EmployeeNumber,
                deduction.Agency,
                deduction.AmountMinor,
                deduction.EmployerShareMinor,
                deduction.CalculatedMinor,
                deduction.IsAdjusted,
                ScheduleName = schedule == null ? null : schedule.Name
            }).ToListAsync(cancellationToken);

        var agencies = StatutoryAgencies.InOrder
            .Select(agency => (agency, items: shares.Where(share => share.Agency == agency).ToList()))
            .Where(group => group.items.Count > 0)
            .Select(group => new StatutoryAgencyTotalDto(group.agency.ToString(), StatutoryAgencies.Label(group.agency),
                group.items.Select(item => item.ScheduleName).OfType<string>().Distinct().ToArray(), group.items.Select(item => item.WorkerId).Distinct().Count(),
                group.items.Sum(item => item.AmountMinor), group.items.Sum(item => item.EmployerShareMinor),
                group.items.Sum(item => item.AmountMinor + item.EmployerShareMinor), group.items.Count(item => item.IsAdjusted),
                group.items.Count(item => !item.IsAdjusted && item.AmountMinor < item.CalculatedMinor)))
            .ToArray();

        var workers = shares.GroupBy(share => share.WorkerId).Select(group => new StatutoryWorkerDto(group.Key, group.First().WorkerName,
            group.First().EmployeeNumber,
            group.OrderBy(item => item.Agency).Select(item => new StatutoryWorkerShareDto(item.Agency!.Value.ToString(), item.AmountMinor, item.EmployerShareMinor,
                item.CalculatedMinor, item.IsAdjusted, !item.IsAdjusted && item.AmountMinor < item.CalculatedMinor)).ToArray())).ToArray();

        var employee = agencies.Sum(agency => agency.EmployeeMinor);
        var employer = agencies.Sum(agency => agency.EmployerMinor);
        return new PayrollStatutoryDto(run.Id, run.Status.ToString(), run.RunsPerMonth, employee, employer, employee + employer, agencies, workers);
    }
}
