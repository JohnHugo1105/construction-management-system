using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Statutory;

namespace ConstructOPX.Domain.Features.Payroll;

public enum PayrollRunStatus
{
    /// <summary>Calculated and reserved attendance; amounts and deductions can still change by recalculating.</summary>
    Draft = 0,

    /// <summary>Approved: attendance is locked and the labor cost has been posted to the projects.</summary>
    Approved = 1,

    /// <summary>Voided: attendance is released and the posted labor cost was voided.</summary>
    Voided = 2
}

public enum PayrollRunKind
{
    /// <summary>A pay period's payroll, worked out from attendance.</summary>
    Regular = 0,

    /// <summary>The 13th-month pay for a calendar year, worked out from the year's approved regular payrolls.</summary>
    ThirteenthMonth = 1
}

/// <summary>Where a deduction came from: typed by a person, or worked out from a contribution or tax table.</summary>
public enum PayrollDeductionSource
{
    Manual = 0,
    Statutory = 1
}

/// <summary>
/// Something taken from one worker's pay in a run. A manual deduction is typed by a person (an advance, a loan). A statutory one is
/// worked out from the agency's table: it keeps the table it came from, the amount the table gave (<see cref="CalculatedMinor"/>), the
/// employer's share (recorded and exported, not taken from pay and not charged to a project), and, when a person changed it, the reason.
/// </summary>
public sealed class PayrollDeduction
{
    private PayrollDeduction() { }

    internal PayrollDeduction(Guid id, Guid tenantId, Guid payrollLineId, string description, long amountMinor)
    {
        Id = id;
        TenantId = tenantId;
        PayrollLineId = payrollLineId;
        Description = description;
        AmountMinor = amountMinor;
        Source = PayrollDeductionSource.Manual;
    }

    internal static PayrollDeduction Statutory(Guid id, Guid tenantId, Guid payrollLineId, StatutoryAmount calculated, long amountMinor) =>
        new(id, tenantId, payrollLineId, StatutoryAgencies.DeductionDescription(calculated.Agency), amountMinor)
        {
            Source = PayrollDeductionSource.Statutory,
            Agency = calculated.Agency,
            ScheduleId = calculated.ScheduleId,
            CalculatedMinor = calculated.EmployeeMinor,
            EmployerShareMinor = calculated.EmployerMinor
        };

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public string Description { get; private set; } = "";
    public long AmountMinor { get; private set; }
    public PayrollDeductionSource Source { get; private set; }
    public StatutoryAgency? Agency { get; private set; }
    public Guid? ScheduleId { get; private set; }

    /// <summary>What the table gave before any limit or adjustment. Null for a manual deduction.</summary>
    public long? CalculatedMinor { get; private set; }

    /// <summary>The employer's share for the same agency. Zero for a manual deduction and for tax.</summary>
    public long EmployerShareMinor { get; private set; }

    public bool IsAdjusted { get; private set; }
    public string? AdjustmentReason { get; private set; }

    public bool IsStatutory => Source == PayrollDeductionSource.Statutory;

    /// <summary>Taken at less than the table gave because the deductions would have been more than the pay.</summary>
    public bool IsLimited => IsStatutory && !IsAdjusted && AmountMinor < CalculatedMinor;

    internal void Adjust(long amountMinor, string reason)
    {
        AmountMinor = amountMinor;
        AdjustmentReason = reason;
        IsAdjusted = true;
    }
}

public sealed class PayrollAllocation
{
    private PayrollAllocation() { }

    internal PayrollAllocation(Guid id, Guid tenantId, Guid payrollLineId, Guid projectId, Guid? subProjectId, decimal regularHours, decimal overtimeHours,
        long grossMinor, long basicMinor = 0)
    {
        Id = id;
        TenantId = tenantId;
        PayrollLineId = payrollLineId;
        ProjectId = projectId;
        SubProjectId = subProjectId;
        RegularHours = regularHours;
        OvertimeHours = overtimeHours;
        GrossMinor = grossMinor;
        BasicPayMinor = basicMinor;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public decimal RegularHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public long GrossMinor { get; private set; }

    /// <summary>The plain-rate pay for the regular hours on this project: the 13th-month pay is a twelfth of these, split by them.</summary>
    public long BasicPayMinor { get; private set; }
}

/// <summary>
/// A regular holiday paid to a worker who did not work it. <see cref="ClaimDate"/> is the holiday's date while the payroll stands and is
/// cleared when the payroll is voided: the database lets only one standing payroll pay a worker for a holiday, so nobody is paid twice.
/// </summary>
public sealed class PayrollHolidayPay
{
    private PayrollHolidayPay() { }

    internal PayrollHolidayPay(Guid id, Guid tenantId, Guid payrollLineId, Guid workerId, CalculatedHolidayPay calculated)
    {
        Id = id;
        TenantId = tenantId;
        PayrollLineId = payrollLineId;
        WorkerId = workerId;
        HolidayDate = calculated.Date;
        ClaimDate = calculated.Date;
        ProjectId = calculated.ProjectId;
        SubProjectId = calculated.SubProjectId;
        AmountMinor = calculated.AmountMinor;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public Guid WorkerId { get; private set; }
    public DateOnly HolidayDate { get; private set; }
    public DateOnly? ClaimDate { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public long AmountMinor { get; private set; }

    internal void Release() => ClaimDate = null;
}

/// <summary>One worker's pay in a run. Name, number and designation are copied in so a printed payroll never changes later.</summary>
public sealed class PayrollLine
{
    private readonly List<PayrollDeduction> _deductions = [];
    private readonly List<PayrollAllocation> _allocations = [];
    private readonly List<PayrollHolidayPay> _holidayPays = [];

    private PayrollLine() { }

    internal PayrollLine(Guid id, Guid tenantId, Guid payrollRunId, CalculatedPayLine calculated, int? thirteenthMonthYear)
    {
        Id = id;
        TenantId = tenantId;
        PayrollRunId = payrollRunId;
        WorkerId = calculated.WorkerId;
        ThirteenthMonthYear = thirteenthMonthYear;
        Refresh(calculated);
    }

    /// <summary>Takes the amounts, project shares and holiday days of a fresh calculation (a draft being recalculated).</summary>
    internal void Refresh(CalculatedPayLine calculated)
    {
        if (calculated.WorkerId != WorkerId) throw new ArgumentException("The calculation is for another worker.", nameof(calculated));
        WorkerName = calculated.WorkerName;
        EmployeeNumber = calculated.EmployeeNumber;
        Designation = calculated.Designation;
        RegularHours = calculated.RegularHours;
        OvertimeHours = calculated.OvertimeHours;
        NightHours = calculated.NightHours;
        RegularPayMinor = calculated.RegularPayMinor;
        PremiumPayMinor = calculated.PremiumPayMinor;
        OvertimePayMinor = calculated.OvertimePayMinor;
        NightPayMinor = calculated.NightPayMinor;
        HolidayPayMinor = calculated.HolidayPayMinor;
        ThirteenthMonthPayMinor = calculated.ThirteenthMonthPayMinor;
        ThirteenthMonthBasisMinor = calculated.ThirteenthMonthBasisMinor;
        _allocations.Clear();
        foreach (var allocation in calculated.Allocations)
            _allocations.Add(new PayrollAllocation(Guid.NewGuid(), TenantId, Id, allocation.ProjectId, allocation.SubProjectId, allocation.RegularHours,
                allocation.OvertimeHours, allocation.GrossMinor, allocation.BasicMinor));
        _holidayPays.Clear();
        foreach (var holiday in calculated.HolidayPays) _holidayPays.Add(new PayrollHolidayPay(Guid.NewGuid(), TenantId, Id, WorkerId, holiday));
    }

    /// <summary>Whether this line holds exactly what a fresh calculation gives: the same amounts, project shares and holiday days.</summary>
    internal bool Matches(CalculatedPayLine calculated) =>
        RegularPayMinor == calculated.RegularPayMinor && PremiumPayMinor == calculated.PremiumPayMinor &&
        OvertimePayMinor == calculated.OvertimePayMinor && NightPayMinor == calculated.NightPayMinor &&
        HolidayPayMinor == calculated.HolidayPayMinor && ThirteenthMonthPayMinor == calculated.ThirteenthMonthPayMinor &&
        ThirteenthMonthBasisMinor == calculated.ThirteenthMonthBasisMinor &&
        _allocations.Select(item => (item.ProjectId, item.SubProjectId, item.GrossMinor, item.BasicPayMinor)).OrderBy(item => item.ProjectId)
            .ThenBy(item => item.SubProjectId)
            .SequenceEqual(calculated.Allocations.Select(item => (item.ProjectId, item.SubProjectId, item.GrossMinor, item.BasicMinor))
                .OrderBy(item => item.ProjectId).ThenBy(item => item.SubProjectId)) &&
        _holidayPays.Select(item => (item.HolidayDate, item.ProjectId, item.SubProjectId, item.AmountMinor)).OrderBy(item => item.HolidayDate)
            .SequenceEqual(calculated.HolidayPays.Select(item => (item.Date, item.ProjectId, item.SubProjectId, item.AmountMinor)).OrderBy(item => item.Item1));

    /// <summary>The payroll was voided: the worker's holidays and 13th-month year can be paid by another payroll.</summary>
    internal void ReleaseClaims()
    {
        ThirteenthMonthYear = null;
        foreach (var holiday in _holidayPays) holiday.Release();
    }

    /// <summary>Calculated contributions and tax are worked out on the gross, so a recalculation of the pay drops them until they are worked out again.</summary>
    internal void ClearStatutory() => _deductions.RemoveAll(deduction => deduction.IsStatutory);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public Guid WorkerId { get; private set; }
    public string WorkerName { get; private set; } = "";
    public string EmployeeNumber { get; private set; } = "";
    public string Designation { get; private set; } = "";
    public decimal RegularHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public decimal NightHours { get; private set; }

    /// <summary>The plain-rate pay for the regular hours (the basic pay, without premiums).</summary>
    public long RegularPayMinor { get; private set; }

    /// <summary>What rest days and holidays add to the regular hours.</summary>
    public long PremiumPayMinor { get; private set; }

    /// <summary>Overtime, including the premium on overtime worked on a rest day or holiday.</summary>
    public long OvertimePayMinor { get; private set; }

    public long NightPayMinor { get; private set; }

    /// <summary>Regular holidays paid without being worked.</summary>
    public long HolidayPayMinor { get; private set; }

    public long ThirteenthMonthPayMinor { get; private set; }

    /// <summary>On a 13th-month line: the year's basic pay the amount was worked out from.</summary>
    public long ThirteenthMonthBasisMinor { get; private set; }

    /// <summary>
    /// On a 13th-month line: the year it pays, while the payroll stands (cleared when it is voided). The database lets only one standing
    /// 13th-month line per worker and year.
    /// </summary>
    public int? ThirteenthMonthYear { get; private set; }

    public IReadOnlyList<PayrollDeduction> Deductions => _deductions;
    public IReadOnlyList<PayrollAllocation> Allocations => _allocations;
    public IReadOnlyList<PayrollHolidayPay> HolidayPays => _holidayPays;

    public long GrossPayMinor => RegularPayMinor + PremiumPayMinor + OvertimePayMinor + NightPayMinor + HolidayPayMinor + ThirteenthMonthPayMinor;
    public long DeductionsMinor => _deductions.Sum(deduction => deduction.AmountMinor);
    public long NetPayMinor => GrossPayMinor - DeductionsMinor;

    public long ManualDeductionsMinor => _deductions.Where(deduction => !deduction.IsStatutory).Sum(deduction => deduction.AmountMinor);
    public long StatutoryDeductionsMinor => _deductions.Where(deduction => deduction.IsStatutory).Sum(deduction => deduction.AmountMinor);
    public long EmployerSharesMinor => _deductions.Sum(deduction => deduction.EmployerShareMinor);

    /// <summary>A statutory amount was taken at less than its table gave, because the deductions would have been more than the pay.</summary>
    public bool StatutoryLimited => _deductions.Any(deduction => deduction.IsLimited);

    /// <summary>
    /// Replaces the deductions a person typed (advances, loans, or a contribution entered by hand). The calculated contributions and tax
    /// are left as they are, and all deductions together can't be more than the pay.
    /// </summary>
    internal void SetDeductions(IEnumerable<(string Description, long AmountMinor)> deductions)
    {
        var list = deductions.ToList();
        var errors = new ValidationErrors();
        var statutory = StatutoryDeductionsMinor;
        var total = statutory;
        foreach (var (description, amount) in list)
        {
            if (string.IsNullOrWhiteSpace(description)) errors.Add("deductions", "Describe each deduction (for example SSS contribution).");
            else if (description.Trim().Length > 120) errors.Add("deductions", "A deduction description can be at most 120 characters.");
            if (amount <= 0) errors.Add("deductions", "Each deduction must be greater than zero.");
            else if (amount > MoneyLimits.MaximumAmountMinor) errors.Add("deductions", "A deduction can be at most ₱100,000,000,000.00.");
            else total = checked(total + amount);
        }
        if (total > GrossPayMinor)
            errors.Add("deductions", statutory > 0
                ? $"Deductions for {WorkerName}, together with the calculated contributions and tax, are more than the pay. Check the amounts."
                : $"Deductions for {WorkerName} are more than the pay. Check the amounts.");
        errors.ThrowIfAny();

        _deductions.RemoveAll(deduction => !deduction.IsStatutory);
        foreach (var (description, amount) in list) _deductions.Add(new PayrollDeduction(Guid.NewGuid(), TenantId, Id, description.Trim(), amount));
    }

    /// <summary>
    /// Replaces the calculated contributions and tax (adjustments are reset). When they would take more than the pay left after the
    /// manual deductions, each is limited in agency order so the deductions add up to the pay exactly, and the line shows it.
    /// </summary>
    internal void ApplyStatutory(IReadOnlyList<StatutoryAmount> amounts)
    {
        _deductions.RemoveAll(deduction => deduction.IsStatutory);
        var limited = StatutoryCalculator.LimitToPay(amounts.Select(amount => amount.EmployeeMinor).ToArray(), GrossPayMinor - ManualDeductionsMinor);
        for (var index = 0; index < amounts.Count; index++)
            _deductions.Add(PayrollDeduction.Statutory(Guid.NewGuid(), TenantId, Id, amounts[index], limited[index]));
    }

    /// <summary>A person changes a calculated amount, with a reason. The deductions together still can't be more than the pay.</summary>
    internal void AdjustStatutory(StatutoryAgency agency, long amountMinor, string? reason)
    {
        var deduction = _deductions.FirstOrDefault(item => item.IsStatutory && item.Agency == agency)
            ?? throw new DomainRuleException(
                $"{StatutoryAgencies.Label(agency)} wasn't calculated for {WorkerName}, so there is nothing to adjust. Enter it as a deduction instead.");
        var errors = new ValidationErrors();
        var cleanReason = reason?.Trim() ?? "";
        if (cleanReason.Length == 0) errors.Add("reason", "Say why the amount is changed.");
        else if (cleanReason.Length > 300) errors.Add("reason", "The reason can be at most 300 characters.");
        if (amountMinor < 0) errors.Add("amountMinor", "The amount can't be negative. Enter 0 to take nothing.");
        else if (amountMinor > MoneyLimits.MaximumAmountMinor) errors.Add("amountMinor", "The amount can be at most ₱100,000,000,000.00.");
        else if (checked(DeductionsMinor - deduction.AmountMinor + amountMinor) > GrossPayMinor)
            errors.Add("amountMinor", $"That would make the deductions for {WorkerName} more than the pay. Enter a smaller amount.");
        errors.ThrowIfAny();
        deduction.Adjust(amountMinor, cleanReason);
    }
}

/// <summary>
/// A pay period's payroll. It moves Draft, then Approved (attendance locked, labor cost posted to the projects), and can be voided
/// with a reason. The desktop app had no payroll: wages never reached a project's cost.
/// </summary>
public sealed class PayrollRun : AggregateRoot
{
    private readonly List<PayrollLine> _lines = [];

    private PayrollRun() { }

    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public PayrollRunStatus Status { get; private set; } = PayrollRunStatus.Draft;

    /// <summary>A pay period's payroll, or a 13th-month payroll for a calendar year.</summary>
    public PayrollRunKind Kind { get; private set; } = PayrollRunKind.Regular;

    /// <summary>The calendar year a 13th-month payroll pays; null for a pay period's payroll.</summary>
    public int? Year { get; private set; }

    public bool IsThirteenthMonth => Kind == PayrollRunKind.ThirteenthMonth;

    /// <summary>The company settings the run was calculated with, kept so it can be explained later.</summary>
    public decimal HoursPerDay { get; private set; }

    public int OvertimeMultiplierBps { get; private set; }

    /// <summary>The company's pay runs per month when the contributions and tax were last worked out (a monthly table's base is the run's pay times this).</summary>
    public int RunsPerMonth { get; private set; } = 2;

    /// <summary>
    /// Which contribution and tax tables (and which versions of them) the statutory deductions were worked out from, as one short text.
    /// Approving compares it with the tables in force now: a table added, changed or retired since the draft was worked out means the
    /// deductions no longer match the tables, so the person recalculates first.
    /// </summary>
    public string? StatutoryBasis { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<PayrollLine> Lines => _lines;

    public long TotalGrossMinor => _lines.Sum(line => line.GrossPayMinor);
    public long TotalDeductionsMinor => _lines.Sum(line => line.DeductionsMinor);
    public long TotalNetMinor => _lines.Sum(line => line.NetPayMinor);

    /// <summary>A pay period of at most 31 days counting both ends, so the last day is at most 30 days after the first (1 to 31 January is fine).</summary>
    public const int MaximumPeriodDaysApart = 30;

    public static PayrollRun Draft(Guid id, Guid tenantId, DateOnly periodStart, DateOnly periodEnd, IReadOnlyList<CalculatedPayLine> lines,
        decimal hoursPerDay, int overtimeMultiplierBps, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        if (periodStart == default || periodEnd == default) errors.Add("periodStart", "Choose the first and last day of the pay period.");
        else if (periodEnd < periodStart) errors.Add("periodEnd", "The last day can't be before the first day.");
        else if (periodEnd.DayNumber - periodStart.DayNumber > MaximumPeriodDaysApart) errors.Add("periodEnd", "A pay period is at most 31 days.");
        else if (periodEnd > today) errors.Add("periodEnd", "The pay period can't end in the future.");
        errors.ThrowIfAny();
        if (lines.Count == 0) throw new DomainRuleException("There is no unpaid attendance in that period.");

        var run = new PayrollRun
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            HoursPerDay = hoursPerDay,
            OvertimeMultiplierBps = overtimeMultiplierBps,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        run.Initialize(id, tenantId, now);
        foreach (var line in lines) run._lines.Add(new PayrollLine(Guid.NewGuid(), tenantId, id, line, null));
        return run;
    }

    /// <summary>The earliest year a 13th-month payroll can be prepared for.</summary>
    public const int EarliestThirteenthMonthYear = 2000;

    /// <summary>
    /// A 13th-month payroll for a calendar year: one line per worker with basic pay that year who is not in another standing 13th-month
    /// payroll for it. The period is the whole year; the current year is allowed at any time (an early payout when a worker leaves), a
    /// future year is not. It carries no contributions and no tax.
    /// </summary>
    public static PayrollRun DraftThirteenthMonth(Guid id, Guid tenantId, int year, IReadOnlyList<CalculatedPayLine> lines, decimal hoursPerDay,
        int overtimeMultiplierBps, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        if (year > today.Year) throw new ValidationFailedException("year", "A 13th-month payroll can't be for a future year.");
        if (year < EarliestThirteenthMonthYear) throw new ValidationFailedException("year", $"Choose a year from {EarliestThirteenthMonthYear}.");
        if (lines.Count == 0)
            throw new DomainRuleException($"No worker has approved pay in {year} that isn't already in a 13th-month payroll for that year.");
        if (lines.Any(line => line.ThirteenthMonthPayMinor <= 0 || line.EntryIds.Count > 0))
            throw new ArgumentException("A 13th-month payroll is made of 13th-month lines only.", nameof(lines));

        var run = new PayrollRun
        {
            Kind = PayrollRunKind.ThirteenthMonth,
            Year = year,
            PeriodStart = new DateOnly(year, 1, 1),
            PeriodEnd = new DateOnly(year, 12, 31),
            HoursPerDay = hoursPerDay,
            OvertimeMultiplierBps = overtimeMultiplierBps,
            CreatedByUserId = createdByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        run.Initialize(id, tenantId, now);
        foreach (var line in lines) run._lines.Add(new PayrollLine(Guid.NewGuid(), tenantId, id, line, year));
        return run;
    }

    /// <summary>
    /// Works a draft's pay out again (the pay rules, holidays, rest days or company settings changed). Each worker's line takes the fresh
    /// amounts; a worker who is no longer paid leaves the payroll and a newly paid one joins it. Typed deductions are kept, so the
    /// recalculation is refused when they would be more than the new pay or would be lost with a worker who leaves. The calculated
    /// contributions and tax are dropped: work them out again (<see cref="ApplyStatutory"/>) straight after.
    /// </summary>
    public void Recalculate(IReadOnlyList<CalculatedPayLine> lines, decimal hoursPerDay, int overtimeMultiplierBps, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft) throw new DomainRuleException("Pay can only be recalculated while the payroll is a draft.");
        if (lines.Count == 0) throw new DomainRuleException("Nothing is left to pay in this payroll. Void it instead.");
        var fresh = lines.ToDictionary(line => line.WorkerId);
        foreach (var line in _lines)
        {
            if (!fresh.TryGetValue(line.WorkerId, out var calculated))
            {
                if (line.ManualDeductionsMinor > 0)
                    throw new DomainRuleException($"{line.WorkerName} would no longer be paid in this payroll, but has deductions entered. Remove them first.");
            }
            else if (line.ManualDeductionsMinor > calculated.GrossMinor)
            {
                throw new DomainRuleException($"After recalculating, the deductions for {line.WorkerName} would be more than the pay. Change the deductions first.");
            }
        }

        _lines.RemoveAll(line => !fresh.ContainsKey(line.WorkerId));
        foreach (var line in _lines)
        {
            line.Refresh(fresh[line.WorkerId]);
            line.ClearStatutory();
        }

        var present = _lines.Select(line => line.WorkerId).ToHashSet();
        foreach (var calculated in lines.Where(line => !present.Contains(line.WorkerId)))
            _lines.Add(new PayrollLine(Guid.NewGuid(), TenantId, Id, calculated, Year));

        HoursPerDay = hoursPerDay;
        OvertimeMultiplierBps = overtimeMultiplierBps;
        StatutoryBasis = null;
        Touch(now);
    }

    /// <summary>
    /// Whether the payroll holds exactly what a fresh calculation gives (same workers, amounts, project shares and holiday days). Approving
    /// checks it: a draft worked out before the pay rules, holidays or a worker's rest day changed is recalculated first.
    /// </summary>
    public bool MatchesCalculation(IReadOnlyList<CalculatedPayLine> lines)
    {
        if (lines.Count != _lines.Count) return false;
        var fresh = lines.ToDictionary(line => line.WorkerId);
        return _lines.All(line => fresh.TryGetValue(line.WorkerId, out var calculated) && line.Matches(calculated));
    }

    /// <summary>
    /// Replaces every worker's calculated contributions and tax with fresh amounts (adjustments are reset). A worker missing from
    /// <paramref name="amountsByWorker"/> has none. Draft only: an approved payroll keeps the amounts it was approved with.
    /// </summary>
    public void ApplyStatutory(IReadOnlyDictionary<Guid, IReadOnlyList<StatutoryAmount>> amountsByWorker, int runsPerMonth, DateTimeOffset now,
        string? statutoryBasis = null)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new DomainRuleException("Contributions and tax can only be recalculated while the payroll is a draft.");
        if (IsThirteenthMonth) throw new DomainRuleException("No contributions or tax are taken from 13th-month pay.");
        if (runsPerMonth is < StatutoryCalculator.MinimumRunsPerMonth or > StatutoryCalculator.MaximumRunsPerMonth)
            throw new ArgumentOutOfRangeException(nameof(runsPerMonth));
        RunsPerMonth = runsPerMonth;
        StatutoryBasis = statutoryBasis;
        foreach (var line in _lines)
            line.ApplyStatutory(amountsByWorker.TryGetValue(line.WorkerId, out var amounts) ? amounts : []);
        Touch(now);
    }

    /// <summary>Changes one worker's calculated contribution or tax, with a reason. Draft only.</summary>
    public void AdjustStatutory(Guid workerId, StatutoryAgency agency, long amountMinor, string? reason, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft) throw new DomainRuleException("Deductions can only be changed while the payroll is a draft.");
        var line = _lines.FirstOrDefault(item => item.WorkerId == workerId) ?? throw new NotFoundException("that worker in this payroll");
        line.AdjustStatutory(agency, amountMinor, reason);
        Touch(now);
    }

    /// <summary>Replaces one worker's manual deductions (cash advances, loans). Calculated contributions and tax are kept. Draft only.</summary>
    public void SetDeductions(Guid workerId, IEnumerable<(string Description, long AmountMinor)> deductions, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft) throw new DomainRuleException("Deductions can only be changed while the payroll is a draft.");
        var line = _lines.FirstOrDefault(item => item.WorkerId == workerId) ?? throw new NotFoundException("that worker in this payroll");
        line.SetDeductions(deductions);
        Touch(now);
    }

    public void Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft) throw new DomainRuleException(Status == PayrollRunStatus.Approved
            ? "This payroll is already approved."
            : "A voided payroll can't be approved. Create a new one.");
        Status = PayrollRunStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = now;
        Touch(now);
    }

    public void Void(Guid voidedByUserId, string? reason, DateTimeOffset now)
    {
        if (Status == PayrollRunStatus.Voided) throw new DomainRuleException("This payroll was already voided.");
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for voiding this payroll", 300);
        Status = PayrollRunStatus.Voided;
        VoidedByUserId = voidedByUserId;
        VoidedAtUtc = now;
        // The holidays and the 13th-month year this payroll paid can be paid again by another payroll.
        foreach (var line in _lines) line.ReleaseClaims();
        Touch(now);
    }
}
