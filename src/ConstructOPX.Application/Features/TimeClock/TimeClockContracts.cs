using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Application.Features.TimeClock;

// The time clock: workers scan their own badge at a machine when they arrive and leave; a person reviews the day and confirms it, which
// creates the attendance entry through the attendance rules. A scan never reaches pay by itself. See docs/api/timeclock.md and
// docs/business-rules.md section 17.

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface ITimeClockDeviceRepository
{
    /// <summary>A device tracked for change. Null when it isn't in the organization.</summary>
    Task<TimeClockDevice?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ClientRequestUsedAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the device as seen now, in one statement that changes nothing when it was already seen within the last minute. It does not touch the
    /// device's version, so an administrator's open form is not made stale by a busy machine.
    /// </summary>
    Task TouchLastSeenAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(TimeClockDevice device);
}

/// <summary>
/// Finds the device a key belongs to, in any company: it runs before anyone is signed in, to establish which company a machine's request
/// belongs to. Only the hash is looked up; the key itself never reaches the database.
/// </summary>
public interface ITimeClockDeviceKeyReader
{
    Task<DeviceIdentity?> FindByKeyHashAsync(byte[] keyHash, CancellationToken cancellationToken);
}

public sealed record DeviceIdentity(Guid DeviceId, Guid TenantId, bool IsActive, byte[] KeyHash);

public interface ITimePunchRepository
{
    /// <summary>A scan tracked for change. Null when it isn't in the organization.</summary>
    Task<TimePunch?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The workers whose badge carries one of these codes (any status), in one statement.</summary>
    Task<IReadOnlyList<PunchWorker>> FindWorkersByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    /// <summary>The scans this device already sent with these ids, with the worker's name, in one statement.</summary>
    Task<IReadOnlyList<RecordedPunch>> FindByClientPunchIdsAsync(Guid? deviceId, IReadOnlyCollection<string> clientPunchIds, CancellationToken cancellationToken);

    /// <summary>The scans (not removed) of these workers on a project with a work date in the range, in one statement.</summary>
    Task<IReadOnlyList<PunchFact>> ListAsync(Guid projectId, IReadOnlyCollection<Guid> workerIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Which of these workers were posted to the project on which of the dates, from their postings, in one statement.</summary>
    Task<IReadOnlySet<(Guid WorkerId, DateOnly Date)>> PostedAsync(Guid projectId, IReadOnlyCollection<Guid> workerIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>True when the worker already has attendance (not removed) on the project for the day, on any sub-project.</summary>
    Task<bool> DayRecordedAsync(Guid workerId, Guid projectId, DateOnly workDate, CancellationToken cancellationToken);

    /// <summary>Which of these codes some worker of the company already carries.</summary>
    Task<IReadOnlySet<string>> CodesInUseAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    void Add(TimePunch punch);
}

public sealed record PunchWorker(Guid Id, string Code, string FirstName, string LastName, WorkerStatus Status);

public sealed record RecordedPunch(Guid Id, string ClientPunchId, Guid WorkerId, string FirstName, string LastName, PunchDirection Direction,
    DateTimeOffset PunchedAtUtc, bool NotPostedHere);

public sealed record PunchFact(Guid Id, Guid WorkerId, Guid? DeviceId, DateTimeOffset PunchedAtUtc, DateOnly WorkDate, PunchDirection Direction);

/// <summary>Reads for the review page, the badges, the devices and the summaries, straight into rows in a fixed number of statements.</summary>
public interface ITimeClockReader
{
    /// <summary>The project's name and whether it is archived. Null when it isn't in the organization.</summary>
    Task<TimeClockProject?> ProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The earliest work date before <paramref name="before"/> with scans not yet recorded as attendance, if any.</summary>
    Task<DateOnly?> EarliestWaitingDateAsync(Guid projectId, DateOnly before, CancellationToken cancellationToken);

    /// <summary>The workers with scans on the project and day, in name order: the total and one page (two statements).</summary>
    Task<(int Total, IReadOnlyList<ReviewWorker> Rows)> WorkersAsync(Guid projectId, DateOnly workDate, PageRequest? page,
        CancellationToken cancellationToken);

    /// <summary>The scans (not removed) of these workers on the project and day, with the machine's name, in one statement.</summary>
    Task<IReadOnlyList<ReviewPunch>> PunchesAsync(Guid projectId, DateOnly workDate, IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken);

    /// <summary>What waits on one project before today, in one statement.</summary>
    Task<TimeClockSummaryDto> SummaryAsync(Guid projectId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimeClockDeviceDto>> DevicesAsync(CancellationToken cancellationToken);

    /// <summary>Badge cards for these workers (at most 100): names, employee number, job title and code. Never a pay figure.</summary>
    Task<IReadOnlyList<BadgeDto>> BadgesAsync(IReadOnlyCollection<Guid> workerIds, DateOnly today, CancellationToken cancellationToken);

    /// <summary>The active workers without a badge code: how many in all, and the first <paramref name="take"/> ids in name order.</summary>
    Task<(int Count, IReadOnlyList<Guid> FirstIds)> ActiveWithoutCodeAsync(int take, CancellationToken cancellationToken);
}

public sealed record TimeClockProject(Guid Id, string Name, bool IsArchived);

public sealed record ReviewWorker(Guid WorkerId, string FirstName, string? MiddleName, string LastName, bool AlreadyRecorded, bool Posted);

public sealed record ReviewPunch(Guid Id, Guid WorkerId, DateTimeOffset PunchedAtUtc, PunchDirection Direction, PunchSource Source, string? DeviceName,
    bool NotPostedHere, string? Reason, int Version);

// ---- Machine push ------------------------------------------------------------------------------------------------------------

/// <param name="ClientPunchId">The machine's own id for the scan (up to 80 characters), unique per machine: sending it again stores nothing.</param>
/// <param name="WorkerCode">What the badge says.</param>
/// <param name="PunchedAt">The time the machine says, with its offset (for example 2026-11-02T07:02:10+08:00).</param>
/// <param name="Direction">In, Out, or Auto (or left out): the opposite of the worker's last scan that day.</param>
public sealed record PunchInput(string? ClientPunchId, string? WorkerCode, DateTimeOffset? PunchedAt, string? Direction);

public sealed record PushPunchesRequest(IReadOnlyList<PunchInput>? Punches);

/// <summary>
/// The answer to one scan. <c>Outcome</c> is Accepted, AlreadyRecorded or Rejected. An accepted (or already recorded) scan carries only what the
/// kiosk shows: the worker's name, the direction, the time and the company's local time of day ("07:02"), and whether the worker is posted to this
/// project. A rejected one carries a plain reason and a stable <c>ReasonCode</c> (BadgeNotActive, ClockWrong, Unreadable, ProjectClosed); an
/// unknown code, a replaced badge and a worker who is not active all answer BadgeNotActive with the same words.
/// </summary>
public sealed record PunchResultDto(string? ClientPunchId, string Outcome, string? DisplayName, string? Direction, DateTimeOffset? PunchedAt,
    string? LocalTime, bool NotPostedHere, string? ReasonCode, string? Reason);

public sealed record PushPunchesResultDto(IReadOnlyList<PunchResultDto> Results);

public static class PunchOutcome
{
    public const string Accepted = "Accepted";
    public const string AlreadyRecorded = "AlreadyRecorded";
    public const string Rejected = "Rejected";
}

// ---- Import ------------------------------------------------------------------------------------------------------------------

/// <summary>A machine's exported file: its text, rows of <c>code,timestamp[,direction]</c> with an optional header row.</summary>
public sealed record ImportPunchesRequest(string? Csv);

/// <summary>What an import did: how many rows were read, accepted, already there, and refused (with the row number and a plain reason).</summary>
public sealed record ImportPunchesResultDto(int Rows, int Accepted, int AlreadyRecorded, int Rejected, IReadOnlyList<ImportRejectionDto> RejectedRows);

/// <summary>A refused row: its number in the file (the header is row 1 when there is one) and why. The row's content is not echoed back.</summary>
public sealed record ImportRejectionDto(int Row, string Reason);

// ---- Review ------------------------------------------------------------------------------------------------------------------

/// <summary>
/// One project's day of scans, one row per worker (paged, in name order), with what still waits on the project before today (<c>Summary</c>), so the
/// page can lead to the earliest day that waits.
/// </summary>
public sealed record TimeClockDayListDto(Guid ProjectId, string ProjectName, bool ProjectArchived, DateOnly WorkDate, IReadOnlyList<TimeClockDayRowDto> Items,
    int Total, int Page, int PageSize, TimeClockSummaryDto Summary);

/// <summary>
/// A worker's day: the first in and last out (instants, and the company's local time of day), the break taken, the hours worked out from the scans,
/// the overtime that waits for approval, the night hours and the exceptions (MissingTimeOut, MissingTimeIn, NotPostedHere, TooLong).
/// <c>AlreadyRecorded</c>: the worker already has attendance on this project that day; the row is never confirmed over it.
/// <c>CanConfirm</c>: no missing scan, hours above zero, not already recorded and the project open. <c>IsClean</c>: no exception and no overtime
/// waiting, so "Confirm all clean rows" takes it. <c>ScanStamp</c>: a short stamp of the scans the row was worked out from; sent back when
/// confirming so a row whose scans changed meanwhile is skipped rather than confirmed. Hours only: never a pay figure.
/// </summary>
public sealed record TimeClockDayRowDto(Guid WorkerId, string WorkerName, DateTimeOffset? FirstIn, string? FirstInLocal, DateTimeOffset? LastOut,
    string? LastOutLocal, int BreakMinutes, decimal RegularHours, decimal ProposedOvertimeHours, decimal NightHours, IReadOnlyList<string> Exceptions,
    bool AlreadyRecorded, bool CanConfirm, bool IsClean, IReadOnlyList<TimeClockPunchDto> Punches, string ScanStamp);

/// <summary>One scan on the review page. <c>Source</c> is Kiosk, Terminal, Import or Manual; <c>Reason</c> is a supervisor's reason for a manual scan.</summary>
public sealed record TimeClockPunchDto(Guid Id, DateTimeOffset PunchedAtUtc, string LocalTime, string Direction, string Source, string? DeviceName,
    bool NotPostedHere, string? Reason, int Version);

/// <summary>What waits on a project: work days before today with scans not yet confirmed.</summary>
/// <param name="DaysWaiting">Distinct work dates.</param>
/// <param name="RowsWaiting">Worker-days (a worker on a date).</param>
/// <param name="RowsWithExceptions">Worker-days with an unpaired scan or a worker not posted there.</param>
/// <param name="EarliestDate">The first of them, or null when nothing waits.</param>
public sealed record TimeClockSummaryDto(Guid ProjectId, int DaysWaiting, int RowsWaiting, int RowsWithExceptions, DateOnly? EarliestDate);

/// <summary>One row to confirm. <c>ScanStamp</c> is the stamp the review row carried when the person read it; when it is sent and the scans have changed since, the row is skipped.</summary>
public sealed record ConfirmTimeClockRow(Guid WorkerId, bool ApproveOvertime, string? ScanStamp = null);

/// <summary>
/// Confirms rows of one project's day. With <c>AllClean</c> every row of the whole day (not only a page) with no exception and no unapproved overtime is confirmed (overtime approvals
/// in <c>Rows</c> count); without it, exactly the rows listed are confirmed, and a row's overtime is recorded only when approved.
/// </summary>
public sealed record ConfirmTimeClockDaysRequest(Guid ProjectId, DateOnly WorkDate, IReadOnlyList<ConfirmTimeClockRow>? Rows, bool AllClean = false);

/// <summary>Which workers were confirmed (their attendance entries now exist), and which were not, with the reason.</summary>
public sealed record ConfirmTimeClockResultDto(IReadOnlyList<Guid> Confirmed, IReadOnlyList<ConfirmSkipDto> Skipped);

public sealed record ConfirmSkipDto(Guid WorkerId, string WorkerName, string Reason);

/// <param name="At">The time of the scan as the company's wall clock (for example 2026-11-01T07:02), in the company's time zone.</param>
/// <param name="Direction">In or Out.</param>
public sealed record AddManualPunchRequest(Guid ProjectId, Guid WorkerId, DateTime? At, string? Direction, string? Reason, string? ClientRequestId);

public sealed record VoidPunchRequest(string? Reason, int Version);

// ---- Devices -----------------------------------------------------------------------------------------------------------------

/// <param name="Kind">Kiosk (a tablet at the gate with a scanner) or Terminal (a time clock machine).</param>
public sealed record RegisterDeviceRequest(string? Name, Guid ProjectId, string? Kind, string? ClientRequestId);

/// <summary>A registered time clock. Never its key or the key's hash.</summary>
public sealed record TimeClockDeviceDto(Guid Id, string Name, Guid ProjectId, string ProjectName, string Kind, string Status,
    DateTimeOffset? LastSeenAtUtc, DateTimeOffset CreatedAtUtc, int Version);

/// <summary>A device with its key. The key is in this answer only, once: it is never stored or shown again.</summary>
public sealed record DeviceKeyDto(TimeClockDeviceDto Device, string Key);

/// <summary>Whether the time clock can be used: Available once an active device has connected, NeedsSetup before.</summary>
public sealed record TimeClockCapabilityDto(string Status, string Message);

public sealed record TimeClockDeviceListDto(IReadOnlyList<TimeClockDeviceDto> Items, TimeClockCapabilityDto Capability);

// ---- Badges ------------------------------------------------------------------------------------------------------------------

public sealed record IssuePunchCodeRequest(int Version);

/// <summary>The new code, for the badge. <c>Reissued</c>: a code was replaced, and the old badge stopped working.</summary>
public sealed record PunchCodeIssuedDto(Guid WorkerId, string Code, bool Reissued, int Version);

/// <summary>One badge card: the worker's name, employee number, job title and code. Never a pay figure.</summary>
public sealed record BadgeDto(Guid WorkerId, string FullName, string EmployeeNumber, string? JobTitle, string? Code);

/// <param name="ActiveWithoutCode">Active workers with no badge code yet (they get one with "Issue codes for everyone without one").</param>
public sealed record BadgeSheetDto(string CompanyName, IReadOnlyList<BadgeDto> Items, int ActiveWithoutCode);

public sealed record IssueMissingCodesResultDto(int Issued, IReadOnlyList<Guid> WorkerIds, int StillWithoutCode);
