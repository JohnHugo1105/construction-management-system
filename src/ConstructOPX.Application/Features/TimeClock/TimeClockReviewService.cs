using System.Security.Cryptography;
using System.Text;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>
/// Reviewing a project's day of scans and confirming it. A day is worked out from its scans with the company's day-start, break and rounding; a
/// reviewer confirms rows, approving overtime row by row, and confirming creates the attendance entries through the attendance rules. Nothing is
/// confirmed automatically, and a day that already has attendance is never overwritten.
/// </summary>
public sealed class TimeClockReviewService(ITimeClockReader reader, IOrganizationRepository organizations, AttendanceService attendance, IClock clock)
{
    private static readonly TimeClockException[] ExceptionOrder =
        [TimeClockException.MissingTimeIn, TimeClockException.MissingTimeOut, TimeClockException.NotPostedHere, TimeClockException.TooLong];

    /// <summary>
    /// One project's day (by default the earliest day before today that still waits, or today), one row per worker with scans, a page at a time. A fixed
    /// number of statements whatever the crew size: the company, the project, the count and the page of workers, their scans, and the summary
    /// (plus one to find the earliest waiting day when no date is given).
    /// </summary>
    public async Task<TimeClockDayListDto> DaysAsync(Guid projectId, DateOnly? date, PageRequest page, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var project = await reader.ProjectAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        var workDate = date ?? await reader.EarliestWaitingDateAsync(projectId, today, cancellationToken) ?? today;
        if (workDate > today) throw new ValidationFailedException("date", "Choose today or an earlier day.");

        var (total, workers) = await reader.WorkersAsync(projectId, workDate, page, cancellationToken);
        var rows = await WorkOutAsync(project, workDate, workers, organization.TimeClock, cancellationToken);
        var summary = await reader.SummaryAsync(projectId, today, cancellationToken);
        return new TimeClockDayListDto(project.Id, project.Name, project.IsArchived, workDate, rows.Select(row => row.Dto).ToArray(), total, page.SafePage,
            page.SafePageSize, summary);
    }

    public async Task<TimeClockSummaryDto> SummaryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        _ = await reader.ProjectAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        return await reader.SummaryAsync(projectId, organization.TodayLocal(clock.UtcNow), cancellationToken);
    }

    /// <summary>
    /// Confirms rows of a day. A row with a missing scan is never confirmed (its hours would be a guess); one already recorded is left as it is;
    /// overtime is recorded only where approved. "Confirm all clean rows" (<c>AllClean</c>) takes only rows with no exception and no unapproved overtime.
    /// Confirming twice records nothing new.
    /// </summary>
    public async Task<ConfirmTimeClockResultDto> ConfirmAsync(ConfirmTimeClockDaysRequest request, CancellationToken cancellationToken)
    {
        var chosen = request.Rows ?? [];
        var errors = new ValidationErrors();
        if (!request.AllClean && chosen.Count == 0) errors.Add("rows", "Choose at least one worker to confirm.");
        if (chosen.Select(row => row.WorkerId).Distinct().Count() != chosen.Count) errors.Add("rows", "A worker is listed twice.");
        if (request.WorkDate == default) errors.Add("workDate", "Choose the day.");
        errors.ThrowIfAny();

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        if (request.WorkDate > today) throw new ValidationFailedException("workDate", "Choose today or an earlier day.");
        var project = await reader.ProjectAsync(request.ProjectId, cancellationToken) ?? throw new NotFoundException("that project");
        if (project.IsArchived) throw new DomainRuleException("This project is archived. Restore it before confirming attendance.");

        var (_, workers) = await reader.WorkersAsync(project.Id, request.WorkDate, null, cancellationToken);
        var approvals = chosen.ToDictionary(row => row.WorkerId, row => row.ApproveOvertime);
        var seen = chosen.Where(row => !string.IsNullOrEmpty(row.ScanStamp)).ToDictionary(row => row.WorkerId, row => row.ScanStamp!);
        var wanted = request.AllClean ? workers : workers.Where(worker => approvals.ContainsKey(worker.WorkerId)).ToArray();
        var skipped = new List<ConfirmSkipDto>();
        if (!request.AllClean)
            foreach (var missing in chosen.Where(row => workers.All(worker => worker.WorkerId != row.WorkerId)))
                skipped.Add(new ConfirmSkipDto(missing.WorkerId, "", "There are no scans for this worker on that day."));

        var lines = new List<TimeClockAttendanceLine>();
        var names = new Dictionary<Guid, string>();
        foreach (var row in await WorkOutAsync(project, request.WorkDate, wanted, organization.TimeClock, cancellationToken))
        {
            var dto = row.Dto;
            names[dto.WorkerId] = dto.WorkerName;
            var approved = approvals.GetValueOrDefault(dto.WorkerId);
            var reason = dto.AlreadyRecorded ? "Already recorded."
                : seen.TryGetValue(dto.WorkerId, out var stamp) && stamp != dto.ScanStamp
                    ? "The scans changed after you opened this day. Reload it and look at this worker again."
                : row.Day.HasMissingScan ? "A scan is missing. Add it, or remove the extra one, then confirm."
                : row.Day.TotalHours <= 0m ? "The scans add up to no time worked."
                : request.AllClean && row.Day.Exceptions != TimeClockException.None ? "Needs a look before confirming: " + ExceptionWords(row.Day.Exceptions) + "."
                : request.AllClean && !row.Day.IsCleanWithout(approved) ? "The overtime waits for your approval."
                : null;
            if (reason is not null)
            {
                skipped.Add(new ConfirmSkipDto(dto.WorkerId, dto.WorkerName, reason));
                continue;
            }

            var (regular, overtime, night) = row.Day.ToAttendance(approved);
            lines.Add(new TimeClockAttendanceLine(dto.WorkerId, regular, overtime, night,
                new TimeClockEvidence(row.Day.FirstIn, row.Day.LastOut, row.Day.BreakMinutes)));
        }

        var outcome = await attendance.RecordFromTimeClockAsync(project.Id, request.WorkDate, lines, cancellationToken);
        foreach (var (workerId, message) in outcome.Refused)
            skipped.Add(new ConfirmSkipDto(workerId, names.GetValueOrDefault(workerId, ""), message));
        return new ConfirmTimeClockResultDto(outcome.Recorded, skipped);
    }

    private sealed record WorkedRow(TimeClockDayRowDto Dto, TimeClockDayResult Day);

    private async Task<IReadOnlyList<WorkedRow>> WorkOutAsync(TimeClockProject project, DateOnly workDate, IReadOnlyList<ReviewWorker> workers,
        TimeClockSettings settings, CancellationToken cancellationToken)
    {
        if (workers.Count == 0) return [];
        var punches = (await reader.PunchesAsync(project.Id, workDate, workers.Select(worker => worker.WorkerId).ToArray(), cancellationToken))
            .ToLookup(punch => punch.WorkerId);
        var zone = settings.TimeZone;
        var rows = new List<WorkedRow>(workers.Count);
        foreach (var worker in workers)
        {
            var scans = punches[worker.WorkerId].OrderBy(punch => punch.PunchedAtUtc).ToArray();
            var day = TimeClockDay.Work(scans.Select(punch => new PunchMoment(punch.PunchedAtUtc, punch.Direction)), settings, notPostedHere: !worker.Posted);
            var canConfirm = !worker.AlreadyRecorded && !day.HasMissingScan && day.TotalHours > 0m && !project.IsArchived;
            var dto = new TimeClockDayRowDto(worker.WorkerId, WorkerNames.Full(worker.FirstName, worker.MiddleName, worker.LastName),
                day.FirstIn, day.FirstIn is { } firstIn ? TimeClockPunchService.LocalTime(firstIn, zone) : null,
                day.LastOut, day.LastOut is { } lastOut ? TimeClockPunchService.LocalTime(lastOut, zone) : null,
                day.BreakMinutes, day.RegularHours, day.ProposedOvertimeHours, day.NightHours,
                ExceptionOrder.Where(flag => day.Exceptions.HasFlag(flag)).Select(flag => flag.ToString()).ToArray(),
                worker.AlreadyRecorded, canConfirm, canConfirm && day.IsCleanWithout(false),
                scans.Select(punch => new TimeClockPunchDto(punch.Id, punch.PunchedAtUtc, TimeClockPunchService.LocalTime(punch.PunchedAtUtc, zone),
                    punch.Direction.ToString(), punch.Source.ToString(), punch.DeviceName, punch.NotPostedHere, punch.Reason, punch.Version)).ToArray(),
                ScanStamp(scans));
            rows.Add(new WorkedRow(dto, day));
        }
        return rows;
    }

    /// <summary>
    /// A short stamp of the scans a row was worked out from (their ids, times and directions). The review page sends it back when confirming, so a
    /// scan added, removed or sent late between reading the day and confirming it makes the row wait for another look instead of being confirmed
    /// with hours the reviewer never saw.
    /// </summary>
    private static string ScanStamp(IEnumerable<ReviewPunch> scans)
    {
        var text = string.Join(';', scans.OrderBy(punch => punch.PunchedAtUtc).ThenBy(punch => punch.Id)
            .Select(punch => $"{punch.Id:N}.{punch.PunchedAtUtc.UtcTicks}.{(int)punch.Direction}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static string ExceptionWords(TimeClockException exceptions) =>
        string.Join(", ", ExceptionOrder.Where(flag => exceptions.HasFlag(flag)).Select(flag => flag switch
        {
            TimeClockException.MissingTimeIn => "a time out with no time in",
            TimeClockException.MissingTimeOut => "a time in with no time out",
            TimeClockException.NotPostedHere => "not posted to this project that day",
            _ => "more than 16 hours in the day"
        }));
}
