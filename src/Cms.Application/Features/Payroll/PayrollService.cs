using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Workforce;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Application.Features.Payroll;

/// <summary>
/// Payroll use cases. A run is prepared from recorded, unpaid attendance (which it locks), has its deductions entered by a person,
/// and is approved, which posts each project's labor cost; voiding it releases the attendance and reverses the labor cost. Every one of
/// those steps is a single save, so the run, the locks and the project costs change together or not at all.
/// Statutory contributions (SSS, PhilHealth, Pag-IBIG, tax) are not calculated here: a person enters them as deductions.
/// Pay figures appear only in the responses of this service: never in an audit line, a log or an error message.
/// </summary>
public sealed class PayrollService(IPayrollRepository runs, IPayrollReader reader, IAttendanceRepository attendance, IWorkerRepository workers,
    IProjectRepository projects, ICostEntryRepository costs, IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    private const int MaximumDeductionsPerWorker = 25;

    // The domain refuses periods longer than 31 days. Attendance is only loaded for a period that could be accepted, so a mistyped year
    // can't make the server read every entry the company ever recorded before the domain says no.
    private const int LongestLoadedPeriodDays = 31;

    public Task<PagedResult<PayrollRunListItemDto>> ListAsync(PayrollRunListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);

    public Task<PayrollRunDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    /// <summary>Prepares a draft payroll for a period from the attendance nobody has been paid for yet, and locks that attendance to the run.</summary>
    public async Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await ReadAsync(already, cancellationToken);

        try
        {
            return await PrepareAsync(request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException)
        {
            // The same form submitted twice at once: the first submission can be saved after this one looked for it and before this one
            // finished, so this one finds nothing left to pay, or the database lets only the first lock the attendance. Answer it with the
            // payroll the first prepared. Without such a payroll the refusal is a real one and is reported as it is.
            if (await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original) return await ReadAsync(original, cancellationToken);
            throw;
        }
    }

    private async Task<PayrollRunDto> PrepareAsync(CreatePayrollRunRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var now = clock.UtcNow;

        var periodCanBeAccepted = request.PeriodEnd >= request.PeriodStart && request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber <= LongestLoadedPeriodDays;
        var unpaid = periodCanBeAccepted
            ? await attendance.ListUnpaidAsync(request.PeriodStart, request.PeriodEnd, cancellationToken)
            : [];
        var workerIds = unpaid.Select(entry => entry.WorkerId).Distinct().ToArray();
        var found = workerIds.Length == 0
            ? new Dictionary<Guid, Worker>()
            : (await workers.GetManyAsync(workerIds, includeHistory: true, cancellationToken)).ToDictionary(worker => worker.Id);

        // A worker with no pay rate on a work date stops the calculation, and the domain's message names the worker and the date.
        var lines = PayrollCalculator.Calculate(
            unpaid.Select(entry => new AttendanceFact(entry.Id, entry.WorkerId, entry.ProjectId, entry.SubProjectId, entry.WorkDate, entry.RegularHours,
                entry.OvertimeHours)),
            found, organization.HoursPerDay, organization.OvertimeMultiplierBps);
        var run = PayrollRun.Draft(Guid.NewGuid(), currentUser.TenantId, request.PeriodStart, request.PeriodEnd, lines, organization.HoursPerDay,
            organization.OvertimeMultiplierBps, currentUser.UserId, requestId, today, now);

        foreach (var entry in unpaid) entry.LockTo(run.Id, now);
        runs.Add(run);
        audit.Record("Payroll.Drafted", nameof(PayrollRun), run.Id,
            $"Prepared the payroll for {Period(run)} for {run.Lines.Count} worker{(run.Lines.Count == 1 ? "" : "s")} from {unpaid.Count} attendance entr{(unpaid.Count == 1 ? "y" : "ies")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>Replaces one worker's deductions (statutory contributions, cash advances) on a draft payroll.</summary>
    public async Task<PayrollRunDto> SetDeductionsAsync(Guid runId, Guid workerId, SetDeductionsRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(runId, cancellationToken);
        ConcurrencyGuard.Ensure(run.Version, request.Version);
        var deductions = request.Deductions ?? [];
        if (deductions.Count > MaximumDeductionsPerWorker)
            throw new ValidationFailedException("deductions", $"Enter at most {MaximumDeductionsPerWorker} deductions for one worker.");

        run.SetDeductions(workerId, deductions.Select(item => (item.Description ?? "", item.AmountMinor)), clock.UtcNow);
        var worker = run.Lines.First(line => line.WorkerId == workerId).WorkerName;
        audit.Record("Payroll.DeductionsChanged", nameof(PayrollRun), run.Id,
            $"Changed the deductions of {worker} in the payroll for {Period(run)}: {deductions.Count} deduction{(deductions.Count == 1 ? "" : "s")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>
    /// Approves the payroll and posts its labor cost: one cost entry for each project and sub-project, for the whole of what the
    /// workers earned there. The approval and every cost entry are one save, and the entries add up to the run's gross pay exactly.
    /// </summary>
    public async Task<PayrollRunDto> ApproveAsync(Guid runId, ApprovePayrollRunRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(runId, cancellationToken);
        ConcurrencyGuard.Ensure(run.Version, request.Version);
        var now = clock.UtcNow;
        run.Approve(currentUser.UserId, now);

        var shares = run.Lines.SelectMany(line => line.Allocations)
            .GroupBy(allocation => (allocation.ProjectId, allocation.SubProjectId))
            .Select(group => (group.Key.ProjectId, group.Key.SubProjectId, GrossMinor: group.Sum(allocation => allocation.GrossMinor)))
            .Where(share => share.GrossMinor > 0)
            .OrderBy(share => share.ProjectId).ThenBy(share => share.SubProjectId)
            .ToArray();

        var description = $"Payroll {Period(run)}";
        var posted = new List<CostEntry>(shares.Length);
        var loadedProjects = new Dictionary<Guid, Project>();
        foreach (var share in shares)
        {
            if (!loadedProjects.TryGetValue(share.ProjectId, out var project))
            {
                project = await projects.GetAsync(share.ProjectId, cancellationToken) ?? throw new NotFoundException("one of the projects in this payroll");
                loadedProjects[share.ProjectId] = project;
            }

            try
            {
                posted.Add(CostEntry.RecordSystem(Guid.NewGuid(), currentUser.TenantId, project, share.SubProjectId, CostOrigin.Payroll, run.Id, description,
                    ItemType.Labor, 1m, "l.s", share.GrossMinor, VatTreatment.Exempt, 0, run.PeriodEnd, currentUser.UserId, now));
            }
            catch (DomainRuleException ex)
            {
                // Say which project: the payroll covers several, and "this project is archived" alone doesn't tell the person which one.
                throw new DomainRuleException($"{project.Name}: {ex.Message}");
            }
        }

        // The money rule that matters most: what the projects are charged is exactly what the workers earned. A mismatch is a defect,
        // so it stops the save instead of posting a total that disagrees with the payroll.
        if (posted.Sum(entry => entry.GrossMinor) != run.TotalGrossMinor)
            throw new InvalidOperationException("The labor cost to post doesn't match the payroll's gross pay.");

        foreach (var entry in posted) costs.Add(entry);
        audit.Record("Payroll.Approved", nameof(PayrollRun), run.Id,
            $"Approved the payroll for {Period(run)} and posted its labor cost to {loadedProjects.Count} project{(loadedProjects.Count == 1 ? "" : "s")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>
    /// Voids the payroll: releases its attendance for a new run and, if it was approved, voids the labor cost it posted, with the
    /// same reason. A draft needs the payroll.run permission; an approved payroll needs payroll.approve.
    /// </summary>
    public async Task<PayrollRunDto> VoidAsync(Guid runId, VoidPayrollRunRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(runId, cancellationToken);
        ConcurrencyGuard.Ensure(run.Version, request.Version);
        var wasApproved = run.Status == PayrollRunStatus.Approved;
        if (wasApproved && !Permissions.IsAllowed(currentUser.Roles, Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("Only someone who can approve payroll may void an approved payroll.");

        var now = clock.UtcNow;
        run.Void(currentUser.UserId, request.Reason, now);

        foreach (var entry in await attendance.ListByPayrollRunAsync(run.Id, cancellationToken)) entry.Unlock(now);

        var voidedCosts = 0;
        if (wasApproved)
            foreach (var cost in await costs.ListByOriginAsync(CostOrigin.Payroll, run.Id, cancellationToken))
            {
                if (cost.IsVoided) continue;
                cost.Void(currentUser.UserId, run.VoidReason, now);
                voidedCosts++;
            }

        // The reason is free text a person typed and may mention an amount, so it stays on the payroll and the voided costs, not in the audit line.
        audit.Record("Payroll.Voided", nameof(PayrollRun), run.Id,
            $"Voided the {(wasApproved ? "approved" : "draft")} payroll for {Period(run)}; its attendance can be paid again" +
            (voidedCosts == 0 ? "." : $" and {voidedCosts} labor cost entr{(voidedCosts == 1 ? "y was" : "ies were")} voided."));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    public async Task<PayrollExportFile> ExportAsync(Guid runId, CancellationToken cancellationToken) =>
        PayrollRegisterCsv.Build(await ReadAsync(runId, cancellationToken));

    private async Task<PayrollRun> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await runs.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that payroll");

    private async Task<PayrollRunDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that payroll");

    /// <summary>"1 Nov - 15 Nov 2026"; both years are written when the period crosses a year end.</summary>
    internal static string Period(PayrollRun run) => Period(run.PeriodStart, run.PeriodEnd);

    internal static string Period(DateOnly start, DateOnly end) => start.Year == end.Year
        ? $"{start.ToString("d MMM", CultureInfo.InvariantCulture)} - {end.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}"
        : $"{start.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} - {end.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
}
