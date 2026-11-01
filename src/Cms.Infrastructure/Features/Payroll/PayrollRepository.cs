using Cms.Application.Common;
using Cms.Application.Features.Payroll;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Payroll;

internal sealed class PayrollRepository(CmsDbContext db) : IPayrollRepository
{
    public Task<PayrollRun?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<PayrollRun>()
            .Include(run => run.Lines).ThenInclude(line => line.Deductions)
            .Include(run => run.Lines).ThenInclude(line => line.Allocations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<PayrollRun>().Where(run => run.ClientRequestId == clientRequestId).Select(run => (Guid?)run.Id).SingleOrDefaultAsync(cancellationToken);

    public void Add(PayrollRun run) => db.Set<PayrollRun>().Add(run);
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
                WorkerCount = run.Lines.Count(),
                Gross = run.Lines.Sum(line => line.RegularPayMinor + line.OvertimePayMinor),
                Deductions = run.Lines.SelectMany(line => line.Deductions).Sum(deduction => deduction.AmountMinor)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new PayrollRunListItemDto(row.Id, row.PeriodStart, row.PeriodEnd, row.Status.ToString(), row.WorkerCount, row.Gross,
            row.Deductions, row.Gross - row.Deductions, row.CreatedAtUtc, row.ApprovedAtUtc, row.Version)).ToArray();
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
            CreatedBy = db.Set<User>().Where(user => user.Id == item.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            ApprovedBy = db.Set<User>().Where(user => user.Id == item.ApprovedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            VoidedBy = db.Set<User>().Where(user => user.Id == item.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
        }).SingleOrDefaultAsync(cancellationToken);
        if (run is null) return null;

        var lines = await db.Set<PayrollLine>().AsNoTracking().Where(line => line.PayrollRunId == id).OrderBy(line => line.WorkerName).ThenBy(line => line.Id)
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
            var gross = line.RegularPayMinor + line.OvertimePayMinor;
            return new PayrollLineDto(line.WorkerId, line.WorkerName, line.EmployeeNumber, line.Designation, line.RegularHours, line.OvertimeHours,
                line.RegularPayMinor, line.OvertimePayMinor, gross, deducted, gross - deducted, lineDeductions,
                allocationsByLine[line.Id].Select(item => new PayrollAllocationDto(item.ProjectId, item.ProjectName, item.SubProjectId, item.SubProjectName,
                    item.RegularHours, item.OvertimeHours, item.GrossMinor)).ToArray(),
                lineDeductions.Sum(item => item.EmployerShareMinor), lineDeductions.Any(item => item.IsLimited));
        }).ToArray();

        var totalGross = lineDtos.Sum(line => line.GrossPayMinor);
        var totalDeductions = lineDtos.Sum(line => line.DeductionsMinor);
        return new PayrollRunDto(run.Id, run.PeriodStart, run.PeriodEnd, run.Status.ToString(), lineDtos.Length, totalGross, totalDeductions,
            totalGross - totalDeductions, run.HoursPerDay, run.OvertimeMultiplierBps, run.CreatedBy, run.CreatedAtUtc, run.ApprovedBy, run.ApprovedAtUtc,
            run.VoidedBy, run.VoidedAtUtc, run.VoidReason, lineDtos, run.Version, run.RunsPerMonth, lineDtos.Sum(line => line.EmployerSharesMinor));
    }
}
