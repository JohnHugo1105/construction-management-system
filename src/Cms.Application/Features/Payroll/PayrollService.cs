using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Statutory;
using Cms.Application.Features.Workforce;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Statutory;
using Cms.Domain.Features.Workforce;

namespace Cms.Application.Features.Payroll;

/// <summary>
/// Payroll use cases. A run is prepared from recorded, unpaid attendance (which it locks), has its deductions entered by a person,
/// and is approved, which posts each project's labor cost; voiding it releases the attendance and reverses the labor cost. Every one of
/// those steps is a single save, so the run, the locks and the project costs change together or not at all.
/// Contributions (SSS, PhilHealth, Pag-IBIG) and withholding tax are worked out when a draft is prepared, from the tables in force at
/// the period's end; an agency without a table is left for the person to enter as a deduction. The employee's share reduces net pay; the
/// employer's share is recorded but never posted to a project, so the posted labor cost stays the gross pay.
/// Pay figures appear only in the responses of this service: never in an audit line, a log or an error message.
/// </summary>
public sealed class PayrollService(IPayrollRepository runs, IPayrollReader reader, IAttendanceRepository attendance, IWorkerRepository workers,
    IProjectRepository projects, ICostEntryRepository costs, IOrganizationRepository organizations, IStatutoryScheduleRepository statutory,
    IStatutoryReader statutoryReader, IPayRulesRepository payRules, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    /// <summary>The source name on the project cost a 13th-month payroll posts.</summary>
    public const string ThirteenthMonthSourceName = "13th-month pay";

    private const int MaximumDeductionsPerWorker = 25;

    // The domain refuses periods longer than 31 days (counting both ends). Attendance is only loaded for a period that could be accepted,
    // so a mistyped year can't make the server read every entry the company ever recorded before the domain says no.
    private const int LongestLoadedPeriodDays = PayrollRun.MaximumPeriodDaysApart;

    public Task<PagedResult<PayrollRunListItemDto>> ListAsync(PayrollRunListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);

    public Task<PayrollRunDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    /// <summary>Prepares a draft payroll for a period from the attendance nobody has been paid for yet, and locks that attendance to the run.</summary>
    public async Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return EnsureSameRequest(await ReadAsync(already, cancellationToken), request);

        try
        {
            return await PrepareAsync(request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException)
        {
            // The same form submitted twice at once: the first submission can be saved after this one looked for it and before this one
            // finished, so this one finds nothing left to pay, or the database lets only the first lock the attendance. Answer it with the
            // payroll the first prepared. Without such a payroll the refusal is a real one and is reported as it is.
            if (await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original)
                return EnsureSameRequest(await ReadAsync(original, cancellationToken), request);
            throw;
        }
    }

    /// <summary>A repeated request id answers with the original payroll only when it was for the same pay period.</summary>
    private static PayrollRunDto EnsureSameRequest(PayrollRunDto original, CreatePayrollRunRequest request) =>
        original.Kind == nameof(PayrollRunKind.Regular) && original.PeriodStart == request.PeriodStart && original.PeriodEnd == request.PeriodEnd
            ? original
            : throw new DomainRuleException("That request id was already used for a different pay period. Reload the page and try again.");

    private async Task<PayrollRunDto> PrepareAsync(CreatePayrollRunRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var now = clock.UtcNow;

        var periodCanBeAccepted = request.PeriodEnd >= request.PeriodStart && request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber <= LongestLoadedPeriodDays;
        var unpaid = periodCanBeAccepted
            ? await attendance.ListUnpaidAsync(request.PeriodStart, request.PeriodEnd, cancellationToken)
            : [];

        // A worker with no pay rate on a work date stops the calculation, and the domain's message names the worker and the date.
        var calculation = periodCanBeAccepted
            ? await CalculateAsync(unpaid, request.PeriodStart, request.PeriodEnd, organization.HoursPerDay, organization.OvertimeMultiplierBps, null,
                cancellationToken)
            : new PayCalculation([], new Dictionary<Guid, Worker>());
        var lines = calculation.Lines;
        var found = calculation.Workers;
        var run = PayrollRun.Draft(Guid.NewGuid(), currentUser.TenantId, request.PeriodStart, request.PeriodEnd, lines, organization.HoursPerDay,
            organization.OvertimeMultiplierBps, currentUser.UserId, requestId, today, now);

        // Contributions and tax from the tables in force at the period's end: one read for the tables, none per worker (the workers'
        // flags came with the workers above).
        var tables = await statutory.ListInForceAsync(run.PeriodEnd, cancellationToken);
        run.ApplyStatutory(StatutoryFor(run, tables, found.ToDictionary(pair => pair.Key, pair => pair.Value.Statutory), organization.PayrollRunsPerMonth),
            organization.PayrollRunsPerMonth, now, StatutoryBasisOf(tables));

        foreach (var entry in unpaid) entry.LockTo(run.Id, now);
        runs.Add(run);
        audit.Record("Payroll.Drafted", nameof(PayrollRun), run.Id,
            $"Prepared the payroll for {Period(run)} for {run.Lines.Count} worker{(run.Lines.Count == 1 ? "" : "s")} from {unpaid.Count} attendance entr{(unpaid.Count == 1 ? "y" : "ies")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>
    /// Works a draft out again from what is current: the pay (the pay rules, holidays, rest days, postings and the company's paid hours
    /// and overtime multiple), then the contributions and tax (the tables, worker details and runs per month). Adjustments are reset;
    /// manual deductions are kept. A 13th-month draft is worked out again from the approved payrolls of its year.
    /// </summary>
    public async Task<PayrollRunDto> RecalculateStatutoryAsync(Guid runId, VersionRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(runId, cancellationToken);
        ConcurrencyGuard.Ensure(run.Version, request.Version);
        if (run.Status != PayrollRunStatus.Draft)
            throw new DomainRuleException("Contributions and tax can only be recalculated while the payroll is a draft.");
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;

        if (run.IsThirteenthMonth)
        {
            var thirteenthLines = await ThirteenthMonthLinesAsync(run.Year!.Value, run.Id, cancellationToken);
            await EnsureNoArchivedPlacesAsync(thirteenthLines, cancellationToken);
            run.Recalculate(thirteenthLines, organization.HoursPerDay, organization.OvertimeMultiplierBps, now);
            audit.Record("Payroll.Recalculated", nameof(PayrollRun), run.Id, $"Recalculated the 13th-month pay for {run.Year}.");
        }
        else
        {
            var calculation = await CalculateAsync(await attendance.ListByPayrollRunAsync(run.Id, cancellationToken), run.PeriodStart, run.PeriodEnd,
                organization.HoursPerDay, organization.OvertimeMultiplierBps, run.Id, cancellationToken);
            run.Recalculate(calculation.Lines, organization.HoursPerDay, organization.OvertimeMultiplierBps, now);
            var tables = await statutory.ListInForceAsync(run.PeriodEnd, cancellationToken);
            run.ApplyStatutory(StatutoryFor(run, tables, calculation.Workers.ToDictionary(pair => pair.Key, pair => pair.Value.Statutory),
                organization.PayrollRunsPerMonth), organization.PayrollRunsPerMonth, now, StatutoryBasisOf(tables));
            audit.Record("Payroll.StatutoryRecalculated", nameof(PayrollRun), run.Id,
                $"Recalculated the pay, contributions and tax in the payroll for {Period(run)}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>
    /// Prepares the 13th-month pay for a calendar year: a twelfth of each worker's basic pay from the year's approved payrolls, for every
    /// worker not already in a standing 13th-month payroll for that year. No contributions or tax are taken. A repeated request id returns
    /// the original.
    /// </summary>
    public async Task<PayrollRunDto> CreateThirteenthMonthAsync(CreateThirteenthMonthRunRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return EnsureSameRequest(await ReadAsync(already, cancellationToken), request);

        try
        {
            var organization = await organizations.GetCurrentAsync(cancellationToken);
            var today = organization.TodayLocal(clock.UtcNow);
            var lines = request.Year > today.Year || request.Year < PayrollRun.EarliestThirteenthMonthYear
                ? []
                : await ThirteenthMonthLinesAsync(request.Year, null, cancellationToken);
            await EnsureNoArchivedPlacesAsync(lines, cancellationToken);
            var run = PayrollRun.DraftThirteenthMonth(Guid.NewGuid(), currentUser.TenantId, request.Year, lines, organization.HoursPerDay,
                organization.OvertimeMultiplierBps, currentUser.UserId, requestId, today, clock.UtcNow);
            runs.Add(run);
            audit.Record("Payroll.Drafted", nameof(PayrollRun), run.Id,
                $"Prepared the 13th-month pay for {run.Year} for {run.Lines.Count} worker{(run.Lines.Count == 1 ? "" : "s")}.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await ReadAsync(run.Id, cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form submitted twice at once: the database lets only the first claim the workers' year. Answer with that payroll.
            if (await runs.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original)
                return EnsureSameRequest(await ReadAsync(original, cancellationToken), request);
            throw;
        }
    }

    private static PayrollRunDto EnsureSameRequest(PayrollRunDto original, CreateThirteenthMonthRunRequest request) =>
        original.Kind == nameof(PayrollRunKind.ThirteenthMonth) && original.Year == request.Year
            ? original
            : throw new DomainRuleException("That request id was already used for a different payroll. Reload the page and try again.");

    private sealed record PayCalculation(IReadOnlyList<CalculatedPayLine> Lines, IReadOnlyDictionary<Guid, Worker> Workers);

    /// <summary>
    /// Works out a pay period's pay from its attendance, the pay rules and holidays, and the unworked regular holidays of the workers posted
    /// on them. A fixed number of statements however many workers and days: the rules, the holidays, (when a regular holiday falls in the
    /// period) the postings, attendance and payrolls of those days, and the workers with their history.
    /// </summary>
    private async Task<PayCalculation> CalculateAsync(IReadOnlyList<AttendanceEntry> entries, DateOnly periodStart, DateOnly periodEnd, decimal hoursPerDay,
        int overtimeMultiplierBps, Guid? runId, CancellationToken cancellationToken)
    {
        var factors = (await payRules.GetRulesAsync(cancellationToken))?.Factors ?? PayFactors.Defaults;
        var holidays = await payRules.HolidaysBetweenAsync(periodStart, periodEnd, cancellationToken);
        var settings = new PaySettings(hoursPerDay, overtimeMultiplierBps, factors, holidays);
        var regularHolidays = holidays.Where(pair => pair.Value == HolidayType.Regular).Select(pair => pair.Key).ToArray();
        var candidates = regularHolidays.Length == 0
            ? []
            : await runs.ListUnworkedHolidayCandidatesAsync(regularHolidays, runId, cancellationToken);

        var workerIds = entries.Select(entry => entry.WorkerId).Concat(candidates.Select(candidate => candidate.WorkerId)).Distinct().ToArray();
        var found = workerIds.Length == 0
            ? new Dictionary<Guid, Worker>()
            : (await workers.GetManyAsync(workerIds, includeHistory: true, cancellationToken)).ToDictionary(worker => worker.Id);
        var known = candidates.Where(candidate => found.ContainsKey(candidate.WorkerId)).ToArray();
        var lines = PayrollCalculator.Calculate(
            entries.Select(entry => new AttendanceFact(entry.Id, entry.WorkerId, entry.ProjectId, entry.SubProjectId, entry.WorkDate, entry.RegularHours,
                entry.OvertimeHours, entry.NightHours)),
            known, found, settings);
        return new PayCalculation(lines, found);
    }

    /// <summary>The 13th-month lines for a year, from the approved payrolls; three statements however many workers and payrolls.</summary>
    private async Task<IReadOnlyList<CalculatedPayLine>> ThirteenthMonthLinesAsync(int year, Guid? runId, CancellationToken cancellationToken) =>
        ThirteenthMonthCalculator.Calculate(await runs.ThirteenthMonthBasisAsync(year, runId, cancellationToken));

    /// <summary>
    /// A 13th-month payroll charges each worker's share to the projects (and sub-projects) where the basic pay was earned, and an archived
    /// one takes no new cost. Rather than prepare a payroll that could never be approved, it is refused up front, naming every archived
    /// place at once. The archive rule itself is unchanged: restore, approve, archive again.
    /// </summary>
    private async Task EnsureNoArchivedPlacesAsync(IReadOnlyList<CalculatedPayLine> lines, CancellationToken cancellationToken)
    {
        var places = lines.SelectMany(line => line.Allocations).Where(share => share.GrossMinor > 0)
            .Select(share => (share.ProjectId, share.SubProjectId)).Distinct().ToArray();
        if (places.Length == 0) return;
        var archived = await runs.ArchivedPlacesAsync(places, cancellationToken);
        if (archived.Count > 0) throw new DomainRuleException(ArchivedPlacesMessage(archived));
    }

    /// <summary>"Tower A is archived. Restore it ...", or "Tower A and Warehouse B are archived. Restore them ...".</summary>
    internal static string ArchivedPlacesMessage(IReadOnlyList<string> places)
    {
        var list = places.Count == 1 ? places[0] : $"{string.Join(", ", places.Take(places.Count - 1))} and {places[^1]}";
        list = char.ToUpperInvariant(list[0]) + list[1..];
        return places.Count == 1
            ? $"{list} is archived. Restore it to charge its share of the 13th-month pay, then archive it again."
            : $"{list} are archived. Restore them to charge their share of the 13th-month pay, then archive them again.";
    }

    /// <summary>Changes one worker's calculated contribution or tax on a draft, with a reason (kept on the payroll, not in the activity log).</summary>
    public async Task<PayrollRunDto> AdjustStatutoryAsync(Guid runId, Guid workerId, string agencyText, AdjustStatutoryRequest request,
        CancellationToken cancellationToken)
    {
        var agency = StatutoryText.Agency(agencyText) ?? throw new NotFoundException("that contribution");
        var run = await LoadAsync(runId, cancellationToken);
        ConcurrencyGuard.Ensure(run.Version, request.Version);
        run.AdjustStatutory(workerId, agency, request.AmountMinor, request.Reason, clock.UtcNow);
        var worker = run.Lines.First(line => line.WorkerId == workerId).WorkerName;
        audit.Record("Payroll.StatutoryAdjusted", nameof(PayrollRun), run.Id,
            $"Adjusted the {(agency == StatutoryAgency.WithholdingTax ? "withholding tax" : $"{StatutoryAgencies.Label(agency)} contribution")} of {worker} in the payroll for {Period(run)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>A payroll's contributions and tax: what each agency is owed (the workers' and the employer's shares) and each worker's part.</summary>
    public async Task<PayrollStatutoryDto> GetStatutoryAsync(Guid runId, CancellationToken cancellationToken) =>
        await statutoryReader.RunStatutoryAsync(runId, cancellationToken) ?? throw new NotFoundException("that payroll");

    /// <summary>Each worker's amounts from the tables in force, worked out on the run's gross pay. A worker with no flags on record is treated as new.</summary>
    private static Dictionary<Guid, IReadOnlyList<StatutoryAmount>> StatutoryFor(PayrollRun run, IReadOnlyList<StatutorySchedule> tables,
        IReadOnlyDictionary<Guid, StatutoryParticipation> participation, int runsPerMonth)
    {
        var result = new Dictionary<Guid, IReadOnlyList<StatutoryAmount>>();
        if (tables.Count == 0) return result;
        foreach (var line in run.Lines)
            result[line.WorkerId] = StatutoryCalculator.Calculate(line.GrossPayMinor,
                participation.TryGetValue(line.WorkerId, out var who) ? who : StatutoryParticipation.Default, tables, runsPerMonth);
        return result;
    }

    /// <summary>Replaces one worker's manual deductions (cash advances, loans) on a draft payroll. Calculated contributions and tax are kept.</summary>
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
        if (run.Status == PayrollRunStatus.Draft)
        {
            // The year's 13th-month pay counts only the payrolls approved before it, so a payroll approved after it would be left out of it.
            if (!run.IsThirteenthMonth && await ThirteenthMonthClaimsAsync(run, cancellationToken) is { Count: > 0 } claims)
            {
                var year = run.PeriodEnd.Year;
                throw new DomainRuleException(claims.Any(claim => claim.Approved)
                    ? $"The 13th-month pay for {year} was already approved for {claims.Count} of this payroll's workers, so it would not count this payroll. Void the 13th-month pay first, then approve this payroll."
                    : $"The 13th-month pay for {year} is being prepared for {claims.Count} of this payroll's workers. Void the 13th-month pay first, then approve this payroll.");
            }
            await EnsureStillCalculatedAsync(run, cancellationToken);
        }
        var now = clock.UtcNow;
        run.Approve(currentUser.UserId, now);
        // A 13th-month payroll's cost is dated the year's last day, or today when it is paid early in the year itself.
        var costDate = run.IsThirteenthMonth
            ? DateOnlyMin((await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(now), run.PeriodEnd)
            : run.PeriodEnd;

        var shares = run.Lines.SelectMany(line => line.Allocations)
            .GroupBy(allocation => (allocation.ProjectId, allocation.SubProjectId))
            .Select(group => (group.Key.ProjectId, group.Key.SubProjectId, GrossMinor: group.Sum(allocation => allocation.GrossMinor)))
            .Where(share => share.GrossMinor > 0)
            .OrderBy(share => share.ProjectId).ThenBy(share => share.SubProjectId)
            .ToArray();

        var description = run.IsThirteenthMonth ? $"13th-month pay {run.Year}" : $"Payroll {Period(run)}";
        var sourceName = run.IsThirteenthMonth ? ThirteenthMonthSourceName : null;
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
                    ItemType.Labor, 1m, "l.s", share.GrossMinor, VatTreatment.Exempt, 0, costDate, currentUser.UserId, now, sourceName));
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
            $"Approved the {Title(run)} and posted its labor cost to {loadedProjects.Count} project{(loadedProjects.Count == 1 ? "" : "s")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(run.Id, cancellationToken);
    }

    /// <summary>
    /// A draft is approved only as it would be worked out now, so an approved payroll can always be explained from its tables, pay rules,
    /// holidays and the workers' rest days and postings. A regular payroll is worked out again with the paid hours and overtime multiple it
    /// was made with (a change of company settings alone is not a reason to refuse).
    /// </summary>
    private async Task EnsureStillCalculatedAsync(PayrollRun run, CancellationToken cancellationToken)
    {
        if (run.IsThirteenthMonth)
        {
            if (!run.MatchesCalculation(await ThirteenthMonthLinesAsync(run.Year!.Value, run.Id, cancellationToken)))
                throw new DomainRuleException($"The approved payrolls of {run.Year} changed after this 13th-month pay was worked out. Recalculate it, then approve.");
            return;
        }

        // The deductions must still match the tables they were worked out from: a table added, corrected or retired after the draft would make
        // the approved payroll impossible to explain from its table.
        if (StatutoryBasisOf(await statutory.ListInForceAsync(run.PeriodEnd, cancellationToken)) != (run.StatutoryBasis ?? ""))
            throw new DomainRuleException("A contribution or tax table was added or changed after this payroll was worked out. Recalculate the contributions and tax, then approve.");
        var calculation = await CalculateAsync(await attendance.ListByPayrollRunAsync(run.Id, cancellationToken), run.PeriodStart, run.PeriodEnd,
            run.HoursPerDay, run.OvertimeMultiplierBps, run.Id, cancellationToken);
        if (!run.MatchesCalculation(calculation.Lines))
            throw new DomainRuleException("The pay rules, holidays, or a worker's pay rate, status, rest day or posting changed after this payroll was worked out. Recalculate the payroll, then approve.");
    }

    /// <summary>The workers of a regular payroll already in a standing 13th-month payroll for the year its period ends in (one statement).</summary>
    private Task<IReadOnlyList<ThirteenthMonthClaim>> ThirteenthMonthClaimsAsync(PayrollRun run, CancellationToken cancellationToken) =>
        runs.ThirteenthMonthClaimsAsync(run.Id, run.PeriodEnd.Year, cancellationToken);

    private static DateOnly DateOnlyMin(DateOnly first, DateOnly second) => first < second ? first : second;

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

        // A standing 13th-month pay for the year was worked out from this payroll's basic pay: voiding this one would leave it paying for
        // pay that no longer stands. Its 13th-month pay is voided first, then prepared again after the correction.
        if (wasApproved && !run.IsThirteenthMonth && await ThirteenthMonthClaimsAsync(run, cancellationToken) is { Count: > 0 } claims)
            throw new DomainRuleException(
                $"The 13th-month pay for {run.PeriodEnd.Year} already includes {claims.Count} of this payroll's workers. Void the 13th-month pay first, then void this payroll.");

        // Voiding changes the cost of every project the payroll charged. An archived project is read-only (restore it first), so a
        // finished project's figures can't change unnoticed because of a payroll void made weeks later. Checked before anything changes.
        var costsToVoid = wasApproved
            ? (await costs.ListByOriginAsync(CostOrigin.Payroll, run.Id, cancellationToken)).Where(cost => !cost.IsVoided).ToList()
            : [];
        foreach (var projectId in costsToVoid.Select(cost => cost.ProjectId).Distinct())
        {
            var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("one of the projects in this payroll");
            if (project.IsArchived)
                throw new DomainRuleException($"{project.Name} is archived. Restore it before voiding this payroll, so the finished project's figures don't change unnoticed.");
        }

        var now = clock.UtcNow;
        run.Void(currentUser.UserId, request.Reason, now);

        foreach (var entry in await attendance.ListByPayrollRunAsync(run.Id, cancellationToken)) entry.Unlock(now);

        var voidedCosts = 0;
        foreach (var cost in costsToVoid)
        {
            cost.Void(currentUser.UserId, run.VoidReason, now);
            voidedCosts++;
        }

        // The reason is free text a person typed and may mention an amount, so it stays on the payroll and the voided costs, not in the audit line.
        audit.Record("Payroll.Voided", nameof(PayrollRun), run.Id,
            $"Voided the {(wasApproved ? "approved" : "draft")} {Title(run)}; " +
            (run.IsThirteenthMonth ? "its workers can be included in another 13th-month payroll" : "its attendance can be paid again") +
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

    /// <summary>The tables the deductions came from (agency, table and its version), as one short text; empty when none is in force.</summary>
    internal static string StatutoryBasisOf(IReadOnlyList<StatutorySchedule> tables) =>
        string.Join(';', tables.OrderBy(table => table.Agency).Select(table => $"{(int)table.Agency}:{table.Id:N}:{table.Version}"));

    /// <summary>"payroll for 1 Nov - 15 Nov 2026", or "13th-month pay for 2026".</summary>
    internal static string Title(PayrollRun run) => run.IsThirteenthMonth ? $"13th-month pay for {run.Year}" : $"payroll for {Period(run)}";

    /// <summary>"1 Nov - 15 Nov 2026"; both years are written when the period crosses a year end.</summary>
    internal static string Period(PayrollRun run) => Period(run.PeriodStart, run.PeriodEnd);

    internal static string Period(DateOnly start, DateOnly end) => start.Year == end.Year
        ? $"{start.ToString("d MMM", CultureInfo.InvariantCulture)} - {end.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}"
        : $"{start.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} - {end.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
}
