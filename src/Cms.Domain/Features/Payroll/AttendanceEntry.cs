using Cms.Domain.Common;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.Features.Payroll;

public enum AttendanceStatus
{
    Recorded = 0,
    Voided = 1
}

/// <summary>
/// The hours one worker put in on one project on one day. Attendance is the input to payroll (the desktop app had no attendance and no
/// payroll at all). Once a payroll run has taken an entry it is locked: it can't be edited or voided until the run is voided, so
/// pay can never be changed behind a payroll's back.
/// </summary>
public sealed class AttendanceEntry : AggregateRoot
{
    public const decimal MaximumHoursPerDay = 24m;

    private AttendanceEntry() { }

    public Guid WorkerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SubProjectId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public decimal RegularHours { get; private set; }
    public decimal OvertimeHours { get; private set; }

    /// <summary>
    /// How many of the day's hours (regular or overtime) fell in the night, typed by the person who knows the shift; they earn the night
    /// differential. The system does not work shifts out.
    /// </summary>
    public decimal NightHours { get; private set; }

    public string? Remarks { get; private set; }
    public AttendanceStatus Status { get; private set; } = AttendanceStatus.Recorded;

    /// <summary>The payroll run that took this entry. While set, the entry is locked.</summary>
    public Guid? PayrollRunId { get; private set; }

    public Guid RecordedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }

    public bool IsVoided => Status == AttendanceStatus.Voided;
    public bool IsLocked => PayrollRunId is not null;

    public static AttendanceEntry Record(Guid id, Guid tenantId, Worker worker, Project project, Guid? subProjectId, DateOnly workDate,
        decimal regularHours, decimal overtimeHours, string? remarks, decimal hoursPerDay, Guid recordedByUserId, string? clientRequestId,
        DateOnly today, DateTimeOffset now, decimal nightHours = 0m)
    {
        project.EnsureCanReceiveRecordsFor(subProjectId);
        Validate(worker, workDate, regularHours, overtimeHours, nightHours, hoursPerDay, today);

        var entry = new AttendanceEntry
        {
            WorkerId = worker.Id,
            ProjectId = project.Id,
            SubProjectId = subProjectId,
            WorkDate = workDate,
            RegularHours = regularHours,
            OvertimeHours = overtimeHours,
            NightHours = nightHours,
            Remarks = Guard.OptionalText(remarks, "remarks", "the remarks", 300),
            RecordedByUserId = recordedByUserId,
            ClientRequestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80)
        };
        entry.Initialize(id, tenantId, now);
        return entry;
    }

    public void Revise(Worker worker, Project project, Guid? subProjectId, decimal regularHours, decimal overtimeHours, string? remarks,
        decimal hoursPerDay, DateOnly today, DateTimeOffset now, decimal nightHours = 0m)
    {
        EnsureEditable();
        if (worker.Id != WorkerId || project.Id != ProjectId) throw new ArgumentException("The worker or project doesn't match this entry.");
        project.EnsureCanReceiveRecordsFor(subProjectId);
        Validate(worker, WorkDate, regularHours, overtimeHours, nightHours, hoursPerDay, today);
        SubProjectId = subProjectId;
        RegularHours = regularHours;
        OvertimeHours = overtimeHours;
        NightHours = nightHours;
        Remarks = Guard.OptionalText(remarks, "remarks", "the remarks", 300);
        Touch(now);
    }

    public void Void(string? reason, DateTimeOffset now)
    {
        EnsureEditable();
        VoidReason = Guard.RequiredText(reason, "reason", "the reason for removing this entry", 300);
        Status = AttendanceStatus.Voided;
        VoidedAtUtc = now;
        Touch(now);
    }

    public void LockTo(Guid payrollRunId, DateTimeOffset now)
    {
        if (IsVoided) throw new DomainRuleException("A removed attendance entry can't be paid.");
        if (IsLocked && PayrollRunId != payrollRunId) throw new DomainRuleException("This attendance is already in another payroll run.");
        PayrollRunId = payrollRunId;
        Touch(now);
    }

    public void Unlock(DateTimeOffset now)
    {
        PayrollRunId = null;
        Touch(now);
    }

    private void EnsureEditable()
    {
        if (IsVoided) throw new DomainRuleException("This attendance entry was removed.");
        if (IsLocked) throw new DomainRuleException("This attendance is part of a payroll run. Void the payroll run first to change it.");
    }

    private static void Validate(Worker worker, DateOnly workDate, decimal regularHours, decimal overtimeHours, decimal nightHours,
        decimal hoursPerDay, DateOnly today)
    {
        var errors = new ValidationErrors();
        if (workDate == default) errors.Add("workDate", "Choose the work date.");
        else if (workDate > today) errors.Add("workDate", "The work date can't be in the future.");
        else if (!worker.CanBeRecordedOn(workDate)) errors.Add("workDate", $"{worker.FullName} can't be recorded for that date (not hired yet, on leave, suspended, or already left).");
        if (regularHours < 0m || decimal.Round(regularHours, 2) != regularHours) errors.Add("regularHours", "Enter regular hours of zero or more, up to 2 decimals.");
        else if (regularHours > hoursPerDay) errors.Add("regularHours", $"Regular hours are at most {hoursPerDay:0.##} a day. Enter the rest as overtime.");
        if (overtimeHours < 0m || decimal.Round(overtimeHours, 2) != overtimeHours) errors.Add("overtimeHours", "Enter overtime hours of zero or more, up to 2 decimals.");
        if (regularHours + overtimeHours <= 0m) errors.Add("regularHours", "Enter the hours worked.");
        if (regularHours + overtimeHours > MaximumHoursPerDay) errors.Add("overtimeHours", $"A day has {MaximumHoursPerDay:0} hours. Check the hours entered.");
        if (nightHours < 0m || decimal.Round(nightHours, 2) != nightHours) errors.Add("nightHours", "Enter night hours of zero or more, up to 2 decimals.");
        else if (nightHours > regularHours + overtimeHours) errors.Add("nightHours", "Night hours can't be more than the hours worked that day.");
        errors.ThrowIfAny();
    }
}
