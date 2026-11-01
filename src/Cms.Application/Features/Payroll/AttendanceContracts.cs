using Cms.Application.Common;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Payroll;

namespace Cms.Application.Features.Payroll;

public interface IAttendanceRepository
{
    Task<AttendanceEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The entry a form submission already recorded, so a double click returns it instead of recording the hours twice.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>The entries a crew submission already recorded (each carries the submission's id followed by the worker).</summary>
    Task<IReadOnlyList<Guid>> ListIdsByBatchAsync(string batchRequestId, CancellationToken cancellationToken);

    /// <summary>Entries not removed for these workers on one project and day, whatever the sub-project, to refuse a second entry for the same posting.</summary>
    Task<IReadOnlyList<ExistingAttendance>> ListActiveAsync(Guid projectId, DateOnly workDate, IReadOnlyCollection<Guid> workerIds,
        CancellationToken cancellationToken);

    /// <summary>Hours (regular plus overtime) already recorded for each worker on a day across every project, not counting one entry being revised.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> TotalHoursAsync(DateOnly workDate, IReadOnlyCollection<Guid> workerIds, Guid? excludeEntryId,
        CancellationToken cancellationToken);

    /// <summary>Recorded attendance in a period that no payroll run has taken yet, tracked for change so a run can lock it.</summary>
    Task<IReadOnlyList<AttendanceEntry>> ListUnpaidAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>The attendance a payroll run locked, tracked for change so voiding the run can release it.</summary>
    Task<IReadOnlyList<AttendanceEntry>> ListByPayrollRunAsync(Guid payrollRunId, CancellationToken cancellationToken);

    void Add(AttendanceEntry entry);
}

public sealed record ExistingAttendance(Guid EntryId, Guid WorkerId, Guid? SubProjectId);

public interface IAttendanceReader
{
    Task<AttendanceEntryDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The entries with these ids, workers in name order.</summary>
    Task<IReadOnlyList<AttendanceEntryDto>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>One project's sheet for one day. Null when the project isn't in the organization.</summary>
    Task<AttendanceSheetDto?> SheetAsync(Guid projectId, DateOnly workDate, CancellationToken cancellationToken);

    Task<PagedResult<AttendanceEntryDto>> ListAsync(AttendanceListQuery query, PageRequest page, CancellationToken cancellationToken);
}

public sealed record AttendanceListQuery(Guid? WorkerId, Guid? ProjectId, DateOnly? From, DateOnly? To, bool IncludeVoided);

/// <summary>Hours only: attendance never carries pay. <c>IsLocked</c> means a payroll run has taken the entry.</summary>
public sealed record AttendanceEntryDto(Guid Id, Guid WorkerId, string WorkerName, string EmployeeNumber, Guid ProjectId, string ProjectName,
    Guid? SubProjectId, string? SubProjectName, DateOnly WorkDate, decimal RegularHours, decimal OvertimeHours, string? Remarks, string Status,
    bool IsLocked, Guid? PayrollRunId, string? VoidReason, int Version);

/// <summary>
/// A project's day: the hours recorded, and the crew posted to the project that day who have no entry yet, so a foreman can fill in
/// the rest in one step.
/// </summary>
public sealed record AttendanceSheetDto(Guid ProjectId, string ProjectName, DateOnly WorkDate, IReadOnlyList<AttendanceEntryDto> Entries,
    decimal TotalRegularHours, decimal TotalOvertimeHours, IReadOnlyList<WorkerLookupDto> CrewWithoutEntry);

public sealed record RecordAttendanceRequest(Guid WorkerId, Guid ProjectId, Guid? SubProjectId, DateOnly WorkDate, decimal RegularHours,
    decimal OvertimeHours, string? Remarks, string? ClientRequestId);

public sealed record AttendanceBatchLine(Guid WorkerId, decimal RegularHours, decimal OvertimeHours, string? Remarks);

/// <summary>A crew's day on one project. All lines are recorded or none are; <c>ClientRequestId</c> covers the whole submission.</summary>
public sealed record RecordAttendanceBatchRequest(Guid ProjectId, Guid? SubProjectId, DateOnly WorkDate, IReadOnlyList<AttendanceBatchLine>? Entries,
    string? ClientRequestId);

public sealed record ReviseAttendanceRequest(Guid? SubProjectId, decimal RegularHours, decimal OvertimeHours, string? Remarks, int Version);

public sealed record VoidAttendanceRequest(string? Reason, int Version);
