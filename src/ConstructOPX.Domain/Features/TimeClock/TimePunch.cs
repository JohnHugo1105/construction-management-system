using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Domain.Features.TimeClock;

public enum PunchDirection
{
    In = 0,
    Out = 1
}

public enum PunchSource
{
    Kiosk = 0,
    Terminal = 1,
    Import = 2,

    /// <summary>A supervisor added a scan that is missing, with a reason.</summary>
    Manual = 3
}

public enum TimePunchStatus
{
    Recorded = 0,
    Voided = 1
}

/// <summary>
/// One scan of a worker's badge: the worker, the project, the machine (none for an import or a supervisor's entry), the time the machine says
/// and the time the server received it, the direction, and the work date it belongs to (worked out on receipt from the company's day-start).
/// Scans are kept as they came: never changed, never deleted; a wrong one is voided with a reason. A scan never reaches pay by itself:
/// a person confirms the day, which creates the attendance entry.
/// </summary>
public sealed class TimePunch : AggregateRoot
{
    public const int MaximumReasonLength = 300;

    private TimePunch() { }

    public Guid WorkerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public DateTimeOffset PunchedAtUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public PunchDirection Direction { get; private set; }

    /// <summary>The machine sent no direction, so it was taken as the opposite of the worker's last scan that day.</summary>
    public bool DirectionWasAutomatic { get; private set; }

    public PunchSource Source { get; private set; }

    /// <summary>The machine's own id for the scan; unique per machine, so a scan sent twice is stored once.</summary>
    public string? ClientPunchId { get; private set; }

    /// <summary>The worker was not posted to this project that day. The scan is kept; the day is flagged for review.</summary>
    public bool NotPostedHere { get; private set; }

    /// <summary>Why a supervisor added this scan (manual scans only).</summary>
    public string? Reason { get; private set; }

    /// <summary>The person who imported or added the scan; none for a machine.</summary>
    public Guid? RecordedByUserId { get; private set; }

    public TimePunchStatus Status { get; private set; } = TimePunchStatus.Recorded;
    public string? VoidReason { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }

    public bool IsVoided => Status == TimePunchStatus.Voided;

    /// <summary>A scan from a machine or an import. The caller has already checked the code, the clock and the duplicates.</summary>
    public static TimePunch Receive(Guid id, Guid tenantId, Guid workerId, Guid projectId, Guid? deviceId, DateTimeOffset punchedAt,
        DateOnly workDate, PunchDirection direction, bool directionWasAutomatic, PunchSource source, string? clientPunchId, bool notPostedHere,
        Guid? recordedByUserId, DateTimeOffset now)
    {
        if (source == PunchSource.Manual) throw new ArgumentException("A supervisor's scan is added with AddManual.", nameof(source));
        if (clientPunchId is { Length: > TimeClockRules.MaximumClientPunchIdLength })
            throw new ArgumentException("A scan id is at most 80 characters.", nameof(clientPunchId));
        var punch = new TimePunch
        {
            WorkerId = workerId,
            ProjectId = projectId,
            DeviceId = deviceId,
            PunchedAtUtc = punchedAt.ToUniversalTime(),
            ReceivedAtUtc = now,
            WorkDate = workDate,
            Direction = direction,
            DirectionWasAutomatic = directionWasAutomatic,
            Source = source,
            ClientPunchId = clientPunchId,
            NotPostedHere = notPostedHere,
            RecordedByUserId = recordedByUserId
        };
        punch.Initialize(id, tenantId, now);
        return punch;
    }

    /// <summary>
    /// A scan a supervisor adds because one is missing (a badge left at home, a machine that was down). The direction is stated, the time can't
    /// be in the future, and the reason is required.
    /// </summary>
    public static TimePunch AddManual(Guid id, Guid tenantId, Worker worker, Project project, DateTimeOffset punchedAt, PunchDirection? direction,
        string? reason, TimeClockSettings settings, bool notPostedHere, Guid recordedByUserId, string? clientRequestId, DateTimeOffset now,
        DateOnly? shiftWorkDate = null)
    {
        project.EnsureCanReceiveRecordsFor(null);
        var errors = new ValidationErrors();
        if (direction is not { } chosen || !Enum.IsDefined(chosen)) errors.Add("direction", "Choose time in or time out.");
        if (punchedAt == default) errors.Add("at", "Enter the time of the scan.");
        else if (punchedAt - now > TimeClockRules.MaximumAhead) errors.Add("at", "The time can't be in the future.");
        string cleanReason = "";
        try { cleanReason = Guard.RequiredText(reason, "reason", "why the scan is being added", MaximumReasonLength); }
        catch (ValidationFailedException ex) { errors.Add("reason", ex.Errors["reason"][0]); }
        string? requestId = null;
        try { requestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", TimeClockRules.MaximumClientPunchIdLength); }
        catch (ValidationFailedException ex) { errors.Add("clientRequestId", ex.Errors["clientRequestId"][0]); }
        errors.ThrowIfAny();

        // An Out that closes a shift still open belongs to the day that shift began (TimeClockRules.Place); otherwise the scan's own work date.
        var workDate = shiftWorkDate ?? TimeClockRules.WorkDateOf(punchedAt, settings);
        if (!worker.CanBeRecordedOn(workDate))
            throw new ValidationFailedException("at", $"{worker.FullName} can't be recorded for that date (not hired yet, on leave, suspended, or already left).");

        var punch = new TimePunch
        {
            WorkerId = worker.Id,
            ProjectId = project.Id,
            PunchedAtUtc = punchedAt.ToUniversalTime(),
            ReceivedAtUtc = now,
            WorkDate = workDate,
            Direction = direction!.Value,
            Source = PunchSource.Manual,
            ClientPunchId = requestId,
            NotPostedHere = notPostedHere,
            Reason = cleanReason,
            RecordedByUserId = recordedByUserId
        };
        punch.Initialize(id, tenantId, now);
        return punch;
    }

    public void Void(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        if (IsVoided) throw new DomainRuleException("This scan was already removed.");
        VoidReason = Guard.RequiredText(reason, "reason", "why the scan is being removed", MaximumReasonLength);
        Status = TimePunchStatus.Voided;
        VoidedAtUtc = now;
        VoidedByUserId = userId;
        Touch(now);
    }
}
