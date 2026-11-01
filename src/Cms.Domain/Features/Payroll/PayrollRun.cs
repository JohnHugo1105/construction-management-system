using Cms.Domain.Common;
using Cms.Domain.Features.Statutory;

namespace Cms.Domain.Features.Payroll;

public enum PayrollRunStatus
{
    /// <summary>Calculated and reserved attendance; amounts and deductions can still change by recalculating.</summary>
    Draft = 0,

    /// <summary>Approved: attendance is locked and the labor cost has been posted to the projects.</summary>
    Approved = 1,

    /// <summary>Voided: attendance is released and the posted labor cost was voided.</summary>
    Voided = 2
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
        long grossMinor)
    {
        Id = id;
        TenantId = tenantId;
        PayrollLineId = payrollLineId;
        ProjectId = projectId;
        SubProjectId = subProjectId;
        RegularHours = regularHours;
        OvertimeHours = overtimeHours;
        GrossMinor = grossMinor;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public decimal RegularHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public long GrossMinor { get; private set; }
}

/// <summary>One worker's pay in a run. Name, number and designation are copied in so a printed payroll never changes later.</summary>
public sealed class PayrollLine
{
    private readonly List<PayrollDeduction> _deductions = [];
    private readonly List<PayrollAllocation> _allocations = [];

    private PayrollLine() { }

    internal PayrollLine(Guid id, Guid tenantId, Guid payrollRunId, CalculatedPayLine calculated)
    {
        Id = id;
        TenantId = tenantId;
        PayrollRunId = payrollRunId;
        WorkerId = calculated.WorkerId;
        WorkerName = calculated.WorkerName;
        EmployeeNumber = calculated.EmployeeNumber;
        Designation = calculated.Designation;
        RegularHours = calculated.RegularHours;
        OvertimeHours = calculated.OvertimeHours;
        RegularPayMinor = calculated.RegularPayMinor;
        OvertimePayMinor = calculated.OvertimePayMinor;
        foreach (var allocation in calculated.Allocations)
            _allocations.Add(new PayrollAllocation(Guid.NewGuid(), tenantId, id, allocation.ProjectId, allocation.SubProjectId, allocation.RegularHours,
                allocation.OvertimeHours, allocation.GrossMinor));
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public Guid WorkerId { get; private set; }
    public string WorkerName { get; private set; } = "";
    public string EmployeeNumber { get; private set; } = "";
    public string Designation { get; private set; } = "";
    public decimal RegularHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public long RegularPayMinor { get; private set; }
    public long OvertimePayMinor { get; private set; }

    public IReadOnlyList<PayrollDeduction> Deductions => _deductions;
    public IReadOnlyList<PayrollAllocation> Allocations => _allocations;

    public long GrossPayMinor => RegularPayMinor + OvertimePayMinor;
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

    /// <summary>The company settings the run was calculated with, kept so it can be explained later.</summary>
    public decimal HoursPerDay { get; private set; }

    public int OvertimeMultiplierBps { get; private set; }

    /// <summary>The company's pay runs per month when the contributions and tax were last worked out (a monthly table's base is the run's pay times this).</summary>
    public int RunsPerMonth { get; private set; } = 2;

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
        foreach (var line in lines) run._lines.Add(new PayrollLine(Guid.NewGuid(), tenantId, id, line));
        return run;
    }

    /// <summary>
    /// Replaces every worker's calculated contributions and tax with fresh amounts (adjustments are reset). A worker missing from
    /// <paramref name="amountsByWorker"/> has none. Draft only: an approved payroll keeps the amounts it was approved with.
    /// </summary>
    public void ApplyStatutory(IReadOnlyDictionary<Guid, IReadOnlyList<StatutoryAmount>> amountsByWorker, int runsPerMonth, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new DomainRuleException("Contributions and tax can only be recalculated while the payroll is a draft.");
        if (runsPerMonth is < StatutoryCalculator.MinimumRunsPerMonth or > StatutoryCalculator.MaximumRunsPerMonth)
            throw new ArgumentOutOfRangeException(nameof(runsPerMonth));
        RunsPerMonth = runsPerMonth;
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
        Touch(now);
    }
}
