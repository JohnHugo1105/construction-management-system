using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Payroll;

internal sealed class PayrollRepository(CmsDbContext db) : IPayrollRepository
{
    public Task<PayrollRun?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<PayrollRun>()
            .Include(run => run.Lines).ThenInclude(line => line.Deductions)
            .Include(run => run.Lines).ThenInclude(line => line.Allocations)
            .Include(run => run.Lines).ThenInclude(line => line.HolidayPays)
            .AsSplitQuery()
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<PayrollRun>().Where(run => run.ClientRequestId == clientRequestId).Select(run => (Guid?)run.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UnworkedHolidayFact>> ListUnworkedHolidayCandidatesAsync(IReadOnlyCollection<DateOnly> dates, Guid? excludingRunId,
        CancellationToken cancellationToken)
    {
        if (dates.Count == 0) return [];
        var wanted = dates.Distinct().ToArray();
        var first = wanted.Min();
        var last = wanted.Max();

        // Three statements however many workers and days: the postings that touch the holidays, the attendance on them, and the holidays
        // other standing payrolls already paid. A posting on an archived project or sub-project is left out: the holiday's cost could not be
        // charged there, so it would stop the payroll's approval (6.12). The run page says how many workers that left out.
        var postings = await db.Set<WorkerAssignment>().AsNoTracking()
            .Where(assignment => assignment.StartDate <= last && (assignment.EndDate == null || assignment.EndDate > first))
            .Where(assignment => db.Set<Project>().Any(project => project.Id == assignment.ProjectId && project.Status != ProjectStatus.Archived) &&
                (assignment.SubProjectId == null ||
                 db.Set<SubProject>().Any(sub => sub.Id == assignment.SubProjectId && sub.Status != SubProjectStatus.Archived)))
            .Select(assignment => new { assignment.WorkerId, assignment.ProjectId, assignment.SubProjectId, assignment.StartDate, assignment.EndDate })
            .ToListAsync(cancellationToken);
        if (postings.Count == 0) return [];

        var worked = (await db.Set<AttendanceEntry>().AsNoTracking()
                .Where(entry => entry.Status == AttendanceStatus.Recorded && wanted.Contains(entry.WorkDate))
                .Select(entry => new { entry.WorkerId, entry.WorkDate })
                .Distinct()
                .ToListAsync(cancellationToken))
            .Select(item => (item.WorkerId, item.WorkDate)).ToHashSet();

        var paid = (await (
                from holiday in db.Set<PayrollHolidayPay>().AsNoTracking()
                join line in db.Set<PayrollLine>() on holiday.PayrollLineId equals line.Id
                where holiday.ClaimDate != null && wanted.Contains(holiday.ClaimDate.Value) && line.PayrollRunId != excludingRunId
                select new { holiday.WorkerId, Date = holiday.ClaimDate!.Value })
                .ToListAsync(cancellationToken))
            .Select(item => (item.WorkerId, item.Date)).ToHashSet();

        var candidates = new List<UnworkedHolidayFact>();
        foreach (var date in wanted.Order())
            foreach (var posting in postings.Where(item => item.StartDate <= date && (item.EndDate == null || date < item.EndDate)))
                if (!worked.Contains((posting.WorkerId, date)) && !paid.Contains((posting.WorkerId, date)))
                    candidates.Add(new UnworkedHolidayFact(posting.WorkerId, date, posting.ProjectId, posting.SubProjectId));
        return candidates;
    }

    public async Task<IReadOnlyList<ThirteenthMonthBasis>> ThirteenthMonthBasisAsync(int year, Guid? excludingRunId, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);

        // The basic pay is stored on each project share of each approved regular payroll, so the year's sum is one grouped statement.
        var basic = await (
            from allocation in db.Set<PayrollAllocation>().AsNoTracking()
            join line in db.Set<PayrollLine>() on allocation.PayrollLineId equals line.Id
            join run in db.Set<PayrollRun>() on line.PayrollRunId equals run.Id
            where run.Kind == PayrollRunKind.Regular && run.Status == PayrollRunStatus.Approved && run.PeriodEnd >= first && run.PeriodEnd <= last
            group allocation by new { line.WorkerId, allocation.ProjectId, allocation.SubProjectId } into shares
            select new { shares.Key.WorkerId, shares.Key.ProjectId, shares.Key.SubProjectId, BasicMinor = shares.Sum(item => item.BasicPayMinor) })
            .ToListAsync(cancellationToken);
        if (basic.Count == 0) return [];

        var claimed = (await db.Set<PayrollLine>().AsNoTracking()
                .Where(line => line.ThirteenthMonthYear == year && line.PayrollRunId != excludingRunId)
                .Select(line => line.WorkerId).ToListAsync(cancellationToken))
            .ToHashSet();
        var workerIds = basic.Select(item => item.WorkerId).Where(id => !claimed.Contains(id)).Distinct().ToArray();
        if (workerIds.Length == 0) return [];

        // The worker's name as it is now and the designation of their latest pay line.
        var people = await db.Set<Worker>().AsNoTracking()
            .Where(worker => workerIds.Contains(worker.Id))
            .Select(worker => new
            {
                worker.Id,
                worker.FirstName,
                worker.MiddleName,
                worker.LastName,
                worker.EmployeeNumber,
                Designation = db.Set<WorkerRate>().Where(rate => rate.WorkerId == worker.Id && rate.EffectiveFrom <= last)
                    .OrderByDescending(rate => rate.EffectiveFrom).Select(rate => rate.Designation).FirstOrDefault()
            })
            .OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id)
            .ToListAsync(cancellationToken);

        var sharesByWorker = basic.ToLookup(item => item.WorkerId);
        return people.Select(person => new ThirteenthMonthBasis(person.Id, WorkerNames.Full(person.FirstName, person.MiddleName, person.LastName),
                person.EmployeeNumber, person.Designation ?? "",
                sharesByWorker[person.Id].Select(item => new BasicPayShare(item.ProjectId, item.SubProjectId, item.BasicMinor)).ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<ThirteenthMonthClaim>> ThirteenthMonthClaimsAsync(Guid runId, int year, CancellationToken cancellationToken) =>
        // The 13th-month year is cleared when its payroll is voided, so a line that still carries it belongs to a standing payroll.
        await (
            from claim in db.Set<PayrollLine>().AsNoTracking()
            join thirteenth in db.Set<PayrollRun>() on claim.PayrollRunId equals thirteenth.Id
            where claim.ThirteenthMonthYear == year && claim.PayrollRunId != runId &&
                db.Set<PayrollLine>().Any(line => line.PayrollRunId == runId && line.WorkerId == claim.WorkerId)
            select new ThirteenthMonthClaim(claim.WorkerId, thirteenth.Status == PayrollRunStatus.Approved))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ArchivedPlacesAsync(IReadOnlyCollection<(Guid ProjectId, Guid? SubProjectId)> places,
        CancellationToken cancellationToken)
    {
        var projectIds = places.Select(place => place.ProjectId).Distinct().ToArray();
        if (projectIds.Length == 0) return [];
        var wanted = places.ToHashSet();
        var found = await db.Set<Project>().AsNoTracking().Include(project => project.SubProjects)
            .Where(project => projectIds.Contains(project.Id))
            .ToListAsync(cancellationToken);

        var names = new List<string>();
        foreach (var project in found.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (project.IsArchived) names.Add(project.Name);
            // A sub-project archived along with its project comes back when the project is restored, so only the project is named.
            foreach (var sub in project.SubProjects
                         .Where(sub => sub.Status == SubProjectStatus.Archived && !(project.IsArchived && sub.ArchivedWithProject) &&
                             wanted.Contains((project.Id, sub.Id)))
                         .OrderBy(sub => sub.Name, StringComparer.CurrentCultureIgnoreCase))
                names.Add($"the {sub.Name} sub-project of {project.Name}");
        }
        return names;
    }

    public void Add(PayrollRun run) => db.Set<PayrollRun>().Add(run);
}

internal sealed class PayRulesRepository(CmsDbContext db) : IPayRulesRepository
{
    public Task<PayRules?> GetRulesAsync(CancellationToken cancellationToken) => db.Set<PayRules>().SingleOrDefaultAsync(cancellationToken);

    public void AddRules(PayRules rules) => db.Set<PayRules>().Add(rules);

    public Task<CompanyHoliday?> GetHolidayAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<CompanyHoliday>().SingleOrDefaultAsync(holiday => holiday.Id == id, cancellationToken);

    public Task<bool> HolidayExistsOnAsync(DateOnly date, CancellationToken cancellationToken) =>
        db.Set<CompanyHoliday>().AnyAsync(holiday => holiday.Date == date, cancellationToken);

    public async Task<IReadOnlyDictionary<DateOnly, HolidayType>> HolidaysBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.Set<CompanyHoliday>().AsNoTracking().Where(holiday => holiday.Date >= from && holiday.Date <= to)
            .ToDictionaryAsync(holiday => holiday.Date, holiday => holiday.Type, cancellationToken);

    public Task<bool> ApprovedPayrollCoversAsync(DateOnly date, CancellationToken cancellationToken) =>
        db.Set<PayrollRun>().AnyAsync(run => run.Kind == PayrollRunKind.Regular && run.Status == PayrollRunStatus.Approved &&
            run.PeriodStart <= date && run.PeriodEnd >= date, cancellationToken);

    public void AddHoliday(CompanyHoliday holiday) => db.Set<CompanyHoliday>().Add(holiday);

    public void RemoveHoliday(CompanyHoliday holiday) => db.Set<CompanyHoliday>().Remove(holiday);
}

internal sealed class PayRulesReader(CmsDbContext db) : IPayRulesReader
{
    public async Task<IReadOnlyList<CompanyHolidayDto>> HolidaysAsync(int year, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);
        var rows = await db.Set<CompanyHoliday>().AsNoTracking()
            .Where(holiday => holiday.Date >= first && holiday.Date <= last)
            .OrderBy(holiday => holiday.Date)
            .Select(holiday => new
            {
                holiday.Id,
                holiday.Date,
                holiday.Name,
                holiday.Type,
                holiday.Version,
                Covered = db.Set<PayrollRun>().Any(run => run.Kind == PayrollRunKind.Regular && run.Status == PayrollRunStatus.Approved &&
                    run.PeriodStart <= holiday.Date && run.PeriodEnd >= holiday.Date)
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new CompanyHolidayDto(row.Id, row.Date, row.Name, row.Type.ToString(), row.Covered, row.Version)).ToArray();
    }
}

internal sealed class PayrollReader(CmsDbContext db) : IPayrollReader
{
    public async Task<PagedResult<PayrollRunListItemDto>> ListAsync(PayrollRunListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var runs = db.Set<PayrollRun>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PayrollRunStatus>(query.Status, true, out var status) && Enum.IsDefined(status))
            runs = runs.Where(run => run.Status == status);

        var total = await runs.CountAsync(cancellationToken);
        // The totals are summed in SQL for the page's runs, so the list never loads a line.
        var rows = await runs.OrderByDescending(run => run.CreatedAtUtc).ThenByDescending(run => run.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(run => new
            {
                run.Id,
                run.PeriodStart,
                run.PeriodEnd,
                run.Status,
                run.CreatedAtUtc,
                run.ApprovedAtUtc,
                run.Version,
                run.Kind,
                run.Year,
                WorkerCount = run.Lines.Count(),
                Gross = run.Lines.Sum(line => line.RegularPayMinor + line.PremiumPayMinor + line.OvertimePayMinor + line.NightPayMinor +
                    line.HolidayPayMinor + line.ThirteenthMonthPayMinor),
                Deductions = run.Lines.SelectMany(line => line.Deductions).Sum(deduction => deduction.AmountMinor)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new PayrollRunListItemDto(row.Id, row.PeriodStart, row.PeriodEnd, row.Status.ToString(), row.WorkerCount, row.Gross,
            row.Deductions, row.Gross - row.Deductions, row.CreatedAtUtc, row.ApprovedAtUtc, row.Version, row.Kind.ToString(), row.Year)).ToArray();
        return PagedResult<PayrollRunListItemDto>.Of(items, total, page);
    }

    public async Task<PayrollRunDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await db.Set<PayrollRun>().AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id,
            item.PeriodStart,
            item.PeriodEnd,
            item.Status,
            item.HoursPerDay,
            item.OvertimeMultiplierBps,
            item.CreatedAtUtc,
            item.ApprovedAtUtc,
            item.VoidedAtUtc,
            item.VoidReason,
            item.Version,
            item.RunsPerMonth,
            item.Kind,
            item.Year,
            // The needs-setup note: a regular payroll for a year in which the company entered no holiday at all.
            NoHolidaysThatYear = item.Kind == PayrollRunKind.Regular &&
                !db.Set<CompanyHoliday>().Any(holiday => holiday.Date.Year == item.PeriodEnd.Year),
            // The workers a draft left out of holiday pay because they were posted to an archived project or sub-project (6.12): paid by the
            // day, posted there on a regular holiday in the period, with no attendance that day and not paid for it by another standing
            // payroll. Part of this statement, so the read stays at its statement count.
            LeftOutOnArchived = item.Kind == PayrollRunKind.Regular && item.Status == PayrollRunStatus.Draft
                ? (from holiday in db.Set<CompanyHoliday>()
                   where holiday.Type == HolidayType.Regular && holiday.Date >= item.PeriodStart && holiday.Date <= item.PeriodEnd
                   from posting in db.Set<WorkerAssignment>()
                   where posting.StartDate <= holiday.Date && (posting.EndDate == null || posting.EndDate > holiday.Date) &&
                       (db.Set<Project>().Any(project => project.Id == posting.ProjectId && project.Status == ProjectStatus.Archived) ||
                        db.Set<SubProject>().Any(sub => sub.Id == posting.SubProjectId && sub.Status == SubProjectStatus.Archived)) &&
                       !db.Set<AttendanceEntry>().Any(entry => entry.WorkerId == posting.WorkerId && entry.WorkDate == holiday.Date &&
                           entry.Status == AttendanceStatus.Recorded) &&
                       !(from paid in db.Set<PayrollHolidayPay>()
                         join line in db.Set<PayrollLine>() on paid.PayrollLineId equals line.Id
                         where paid.WorkerId == posting.WorkerId && paid.ClaimDate == holiday.Date && line.PayrollRunId != item.Id
                         select paid.Id).Any() &&
                       db.Set<WorkerRate>().Where(rate => rate.WorkerId == posting.WorkerId && rate.EffectiveFrom <= holiday.Date)
                           .OrderByDescending(rate => rate.EffectiveFrom).Select(rate => (RateUnit?)rate.Unit).FirstOrDefault() == RateUnit.Daily
                   select posting.WorkerId).Distinct().Count()
                : 0,
            CreatedBy = db.Set<User>().Where(user => user.Id == item.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            ApprovedBy = db.Set<User>().Where(user => user.Id == item.ApprovedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            VoidedBy = db.Set<User>().Where(user => user.Id == item.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
        }).SingleOrDefaultAsync(cancellationToken);
        if (run is null) return null;

        // The holiday days come with the lines (one joined statement; few lines have any).
        var lines = await db.Set<PayrollLine>().AsNoTracking().Include(line => line.HolidayPays).Where(line => line.PayrollRunId == id)
            .OrderBy(line => line.WorkerName).ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

        var deductions = await (
            from deduction in db.Set<PayrollDeduction>().AsNoTracking()
            join line in db.Set<PayrollLine>() on deduction.PayrollLineId equals line.Id
            where line.PayrollRunId == id
            orderby deduction.Description, deduction.AmountMinor
            select new
            {
                deduction.PayrollLineId,
                deduction.Id,
                deduction.Description,
                deduction.AmountMinor,
                deduction.Source,
                deduction.Agency,
                deduction.EmployerShareMinor,
                deduction.CalculatedMinor,
                deduction.IsAdjusted,
                deduction.AdjustmentReason
            }).ToListAsync(cancellationToken);

        var allocations = await (
            from allocation in db.Set<PayrollAllocation>().AsNoTracking()
            join line in db.Set<PayrollLine>() on allocation.PayrollLineId equals line.Id
            where line.PayrollRunId == id
            join project in db.Set<Project>() on allocation.ProjectId equals project.Id
            join sub in db.Set<SubProject>() on allocation.SubProjectId equals sub.Id into subs
            from sub in subs.DefaultIfEmpty()
            orderby project.Name, sub.Name
            select new
            {
                allocation.PayrollLineId,
                allocation.ProjectId,
                ProjectName = project.Name,
                allocation.SubProjectId,
                SubProjectName = sub.Name,
                allocation.RegularHours,
                allocation.OvertimeHours,
                allocation.GrossMinor
            }).ToListAsync(cancellationToken);

        var deductionsByLine = deductions.ToLookup(item => item.PayrollLineId);
        var allocationsByLine = allocations.ToLookup(item => item.PayrollLineId);
        var lineDtos = lines.Select(line =>
        {
            // Calculated contributions and tax first, in agency order, then what a person typed (by description).
            var lineDeductions = deductionsByLine[line.Id]
                .OrderBy(item => item.Source == PayrollDeductionSource.Statutory ? 0 : 1).ThenBy(item => item.Agency.HasValue ? (int)item.Agency.Value : 0)
                .Select(item =>
            {
                var statutory = item.Source == PayrollDeductionSource.Statutory;
                return new PayrollDeductionDto(item.Id, item.Description, item.AmountMinor, item.Source.ToString(), item.Agency?.ToString(),
                    item.EmployerShareMinor, item.CalculatedMinor, item.IsAdjusted, statutory && !item.IsAdjusted && item.AmountMinor < item.CalculatedMinor,
                    item.AdjustmentReason);
            }).ToArray();
            var deducted = lineDeductions.Sum(item => item.AmountMinor);
            var gross = line.GrossPayMinor;
            return new PayrollLineDto(line.WorkerId, line.WorkerName, line.EmployeeNumber, line.Designation, line.RegularHours, line.OvertimeHours,
                line.RegularPayMinor, line.OvertimePayMinor, gross, deducted, gross - deducted, lineDeductions,
                allocationsByLine[line.Id].Select(item => new PayrollAllocationDto(item.ProjectId, item.ProjectName, item.SubProjectId, item.SubProjectName,
                    item.RegularHours, item.OvertimeHours, item.GrossMinor)).ToArray(),
                lineDeductions.Sum(item => item.EmployerShareMinor), lineDeductions.Any(item => item.IsLimited), line.NightHours, line.PremiumPayMinor,
                line.NightPayMinor, line.HolidayPayMinor, line.ThirteenthMonthPayMinor, line.ThirteenthMonthBasisMinor,
                line.HolidayPays.Select(item => item.HolidayDate).Order().ToArray());
        }).ToArray();

        var totalGross = lineDtos.Sum(line => line.GrossPayMinor);
        var totalDeductions = lineDtos.Sum(line => line.DeductionsMinor);
        return new PayrollRunDto(run.Id, run.PeriodStart, run.PeriodEnd, run.Status.ToString(), lineDtos.Length, totalGross, totalDeductions,
            totalGross - totalDeductions, run.HoursPerDay, run.OvertimeMultiplierBps, run.CreatedBy, run.CreatedAtUtc, run.ApprovedBy, run.ApprovedAtUtc,
            run.VoidedBy, run.VoidedAtUtc, run.VoidReason, lineDtos, run.Version, run.RunsPerMonth, lineDtos.Sum(line => line.EmployerSharesMinor),
            run.Kind.ToString(), run.Year, run.NoHolidaysThatYear ? run.PeriodEnd.Year : null, run.LeftOutOnArchived);
    }
}
