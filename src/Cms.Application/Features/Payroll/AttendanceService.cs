using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Workforce;
using Cms.Domain.Common;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Application.Features.Payroll;

/// <summary>
/// Attendance use cases: the hours each worker put in on a project on a day, which payroll later pays. Beyond the domain's checks, a
/// worker's hours on one day add up across every project to at most 24, and a posting (worker, project, sub-project, day) has one entry.
/// An entry that a payroll run has taken is locked: the domain refuses to change it until the run is voided.
/// </summary>
public sealed class AttendanceService(IAttendanceRepository attendance, IAttendanceReader reader, IWorkerRepository workers,
    IProjectRepository projects, IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
    IAuditTrail audit)
{
    private const int MaximumBatchLines = 200;

    // A line's request id is the submission's id, a '#', and the worker's id without dashes (80 characters in all at most).
    private const int MaximumBatchRequestIdLength = 40;

    public Task<PagedResult<AttendanceEntryDto>> ListAsync(AttendanceListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);

    public async Task<AttendanceSheetDto> SheetAsync(Guid projectId, DateOnly? workDate, CancellationToken cancellationToken)
    {
        var date = workDate ?? (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);
        return await reader.SheetAsync(projectId, date, cancellationToken) ?? throw new NotFoundException("that project");
    }

    public async Task<AttendanceEntryDto> RecordAsync(RecordAttendanceRequest request, CancellationToken cancellationToken)
    {
        var requestId = CleanRequestId(request.ClientRequestId);
        if (requestId is not null && await attendance.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await ReadAsync(already, cancellationToken);

        try
        {
            return await RecordNewAsync(request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ValidationFailedException)
        {
            // The same form submitted twice at once: the first submission can be saved after this one looked for it and before this one
            // finished (so "this worker already has an entry" or the unique request id now refuses it). Answer it with the entry the first
            // recorded. Without such an entry the refusal is a real one and is reported as it is.
            if (await attendance.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original) return await ReadAsync(original, cancellationToken);
            throw;
        }
    }

    private async Task<AttendanceEntryDto> RecordNewAsync(RecordAttendanceRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        var worker = await workers.GetAsync(request.WorkerId, includeHistory: false, cancellationToken) ?? throw new NotFoundException("that worker");

        var entry = AttendanceEntry.Record(Guid.NewGuid(), currentUser.TenantId, worker, project, request.SubProjectId, request.WorkDate,
            request.RegularHours, request.OvertimeHours, request.Remarks, organization.HoursPerDay, currentUser.UserId, requestId, today, clock.UtcNow);
        var day = await LoadDayAsync(project.Id, request.WorkDate, [worker.Id], excludeEntryId: null, cancellationToken);
        if (CheckDay(worker, project, entry, day, excludeEntryId: null) is { } problem) throw new ValidationFailedException(problem.Field, problem.Message);

        attendance.Add(entry);
        audit.Record("Attendance.Recorded", nameof(AttendanceEntry), entry.Id, RecordedSummary(worker, project, entry));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(entry.Id, cancellationToken);
    }

    /// <summary>A crew's day on one project, recorded all together or not at all.</summary>
    public async Task<IReadOnlyList<AttendanceEntryDto>> RecordBatchAsync(RecordAttendanceBatchRequest request, CancellationToken cancellationToken)
    {
        var lines = request.Entries ?? [];
        var requestId = CleanRequestId(request.ClientRequestId);
        var errors = new ValidationErrors();
        if (lines.Count == 0) errors.Add("entries", "Add at least one worker.");
        if (lines.Count > MaximumBatchLines) errors.Add("entries", $"Record at most {MaximumBatchLines} workers at a time.");
        if (requestId is { Length: > MaximumBatchRequestIdLength })
            errors.Add("clientRequestId", $"The request id can be at most {MaximumBatchRequestIdLength} characters.");
        var seen = new HashSet<Guid>();
        for (var index = 0; index < lines.Count; index++)
            if (!seen.Add(lines[index].WorkerId)) errors.Add($"entries[{index}].workerId", "This worker is listed twice. Combine the hours into one line.");
        errors.ThrowIfAny();

        if (requestId is not null)
        {
            var recorded = await attendance.ListIdsByBatchAsync(requestId, cancellationToken);
            if (recorded.Count > 0) return await reader.GetManyAsync(recorded, cancellationToken);
        }

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        // A project or sub-project that can't take records fails every line: say so once instead of once per worker.
        project.EnsureCanReceiveRecordsFor(request.SubProjectId);

        var ids = lines.Select(line => line.WorkerId).ToArray();
        var found = (await workers.GetManyAsync(ids, includeHistory: false, cancellationToken)).ToDictionary(worker => worker.Id);
        if (found.Count != ids.Length) throw new NotFoundException("one of the workers");
        var day = await LoadDayAsync(project.Id, request.WorkDate, ids, excludeEntryId: null, cancellationToken);

        var now = clock.UtcNow;
        var entries = new List<(AttendanceEntry Entry, Worker Worker)>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var worker = found[line.WorkerId];
            try
            {
                var entry = AttendanceEntry.Record(Guid.NewGuid(), currentUser.TenantId, worker, project, request.SubProjectId, request.WorkDate,
                    line.RegularHours, line.OvertimeHours, line.Remarks, organization.HoursPerDay, currentUser.UserId,
                    requestId is null ? null : $"{requestId}#{worker.Id:N}", today, now);
                if (CheckDay(worker, project, entry, day, excludeEntryId: null) is { } problem) errors.Add($"entries[{index}].{problem.Field}", problem.Message);
                else entries.Add((entry, worker));
            }
            catch (ValidationFailedException ex)
            {
                foreach (var (field, messages) in ex.Errors)
                    foreach (var message in messages) errors.Add($"entries[{index}].{field}", message);
            }
        }

        errors.ThrowIfAny();
        foreach (var (entry, worker) in entries)
        {
            attendance.Add(entry);
            audit.Record("Attendance.Recorded", nameof(AttendanceEntry), entry.Id, RecordedSummary(worker, project, entry));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reader.GetManyAsync(entries.Select(item => item.Entry.Id).ToArray(), cancellationToken);
    }

    public async Task<AttendanceEntryDto> ReviseAsync(Guid id, ReviseAttendanceRequest request, CancellationToken cancellationToken)
    {
        var entry = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(entry.Version, request.Version);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var worker = await workers.GetAsync(entry.WorkerId, includeHistory: false, cancellationToken) ?? throw new NotFoundException("that worker");
        var project = await LoadProjectAsync(entry.ProjectId, cancellationToken);

        entry.Revise(worker, project, request.SubProjectId, request.RegularHours, request.OvertimeHours, request.Remarks, organization.HoursPerDay,
            organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        var day = await LoadDayAsync(project.Id, entry.WorkDate, [worker.Id], entry.Id, cancellationToken);
        if (CheckDay(worker, project, entry, day, excludeEntryId: entry.Id) is { } problem) throw new ValidationFailedException(problem.Field, problem.Message);

        audit.Record("Attendance.Revised", nameof(AttendanceEntry), entry.Id, $"Changed attendance for {worker.FullName} on {project.Name} for {Format(entry.WorkDate)}: " +
            $"{Hours(entry.RegularHours)} h regular, {Hours(entry.OvertimeHours)} h overtime.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(entry.Id, cancellationToken);
    }

    public async Task<AttendanceEntryDto> VoidAsync(Guid id, VoidAttendanceRequest request, CancellationToken cancellationToken)
    {
        var entry = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(entry.Version, request.Version);

        entry.Void(request.Reason, clock.UtcNow);
        audit.Record("Attendance.Voided", nameof(AttendanceEntry), entry.Id,
            $"Removed an attendance entry for {Format(entry.WorkDate)}: {entry.VoidReason}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(entry.Id, cancellationToken);
    }

    private sealed record DayFacts(IReadOnlyList<ExistingAttendance> Existing, IReadOnlyDictionary<Guid, decimal> OtherHours);

    private async Task<DayFacts> LoadDayAsync(Guid projectId, DateOnly workDate, IReadOnlyCollection<Guid> workerIds, Guid? excludeEntryId,
        CancellationToken cancellationToken) =>
        new(await attendance.ListActiveAsync(projectId, workDate, workerIds, cancellationToken),
            await attendance.TotalHoursAsync(workDate, workerIds, excludeEntryId, cancellationToken));

    /// <summary>The two rules the domain can't see because they depend on other entries. Returns the first problem as a field and a message.</summary>
    private static (string Field, string Message)? CheckDay(Worker worker, Project project, AttendanceEntry entry, DayFacts day, Guid? excludeEntryId)
    {
        if (day.Existing.Any(item => item.WorkerId == worker.Id && item.SubProjectId == entry.SubProjectId && item.EntryId != excludeEntryId))
            return ("workerId", $"{worker.FullName} already has attendance on {project.Name} for {Format(entry.WorkDate)}. Open that entry to change the hours.");

        var others = day.OtherHours.GetValueOrDefault(worker.Id);
        var total = others + entry.RegularHours + entry.OvertimeHours;
        if (total > AttendanceEntry.MaximumHoursPerDay)
            return ("overtimeHours", $"{worker.FullName} would have {Hours(total)} hours on {Format(entry.WorkDate)} across all projects, but a day has " +
                $"{Hours(AttendanceEntry.MaximumHoursPerDay)}. {Hours(others)} hours are already recorded on other entries that day.");
        return null;
    }

    private async Task<AttendanceEntry> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await attendance.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that attendance entry");

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task<AttendanceEntryDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that attendance entry");

    private static string? CleanRequestId(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RecordedSummary(Worker worker, Project project, AttendanceEntry entry) =>
        $"Recorded attendance for {worker.FullName} on {project.Name} for {Format(entry.WorkDate)}: {Hours(entry.RegularHours)} h regular, {Hours(entry.OvertimeHours)} h overtime.";

    private static string Hours(decimal hours) => hours.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
