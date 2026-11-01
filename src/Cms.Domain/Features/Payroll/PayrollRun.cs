using Cms.Domain.Common;

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
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public string Description { get; private set; } = "";
    public long AmountMinor { get; private set; }
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

    internal void SetDeductions(IEnumerable<(string Description, long AmountMinor)> deductions)
    {
        var list = deductions.ToList();
        var errors = new ValidationErrors();
        long total = 0;
        foreach (var (description, amount) in list)
        {
            if (string.IsNullOrWhiteSpace(description)) errors.Add("deductions", "Describe each deduction (for example SSS contribution).");
            else if (description.Trim().Length > 120) errors.Add("deductions", "A deduction description can be at most 120 characters.");
            if (amount <= 0) errors.Add("deductions", "Each deduction must be greater than zero.");
            else total = checked(total + amount);
        }
        if (total > GrossPayMinor) errors.Add("deductions", $"Deductions for {WorkerName} are more than the pay. Check the amounts.");
        errors.ThrowIfAny();

        _deductions.Clear();
        foreach (var (description, amount) in list) _deductions.Add(new PayrollDeduction(Guid.NewGuid(), TenantId, Id, description.Trim(), amount));
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

    public static PayrollRun Draft(Guid id, Guid tenantId, DateOnly periodStart, DateOnly periodEnd, IReadOnlyList<CalculatedPayLine> lines,
        decimal hoursPerDay, int overtimeMultiplierBps, Guid createdByUserId, string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        if (periodStart == default || periodEnd == default) errors.Add("periodStart", "Choose the first and last day of the pay period.");
        else if (periodEnd < periodStart) errors.Add("periodEnd", "The last day can't be before the first day.");
        else if (periodEnd.DayNumber - periodStart.DayNumber > 31) errors.Add("periodEnd", "A pay period is at most 31 days.");
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

    /// <summary>Replaces one worker's deductions (statutory contributions, cash advances). Draft only.</summary>
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
