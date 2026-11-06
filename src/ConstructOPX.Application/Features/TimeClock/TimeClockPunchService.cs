using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>
/// Scans coming in: pushed by a machine (authenticated by its device key, never a person), imported from a machine's file, or added by a supervisor
/// because one is missing. One set of rules behind all three: the code must be an active worker's, the machine's clock must be believable, a scan sent
/// twice is stored once, and an automatic direction is the opposite of the worker's last scan that day. Nothing here creates attendance or pay.
/// </summary>
public sealed class TimeClockPunchService(ITimePunchRepository punches, ITimeClockDeviceRepository devices, ITimeClockReader reader,
    IWorkerRepository workers, IProjectRepository projects, IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    private const string UnreadableRow = "This row couldn't be read. Each row needs a badge code and a time, like ABCD-EFGH-JKMN,2026-11-02 07:02.";

    // ---- Machine push -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A batch of scans from one machine (at most 200). The company comes only from the device. A fixed number of statements per batch, however many
    /// scans it holds: the device, its last-seen mark, the company, the project, the scan ids already sent, the workers, the day's earlier scans, the
    /// postings, and the save.
    /// </summary>
    public async Task<PushPunchesResultDto> PushAsync(Guid deviceId, PushPunchesRequest request, CancellationToken cancellationToken)
    {
        var inputs = request.Punches ?? [];
        if (inputs.Count == 0) throw new ValidationFailedException("punches", "Send at least one scan.");
        if (inputs.Count > TimeClockRules.MaximumBatch) throw new ValidationFailedException("punches", "Send at most 200 scans at a time.");

        var device = await devices.GetAsync(deviceId, cancellationToken);
        if (device is not { IsActive: true }) throw new UnauthorizedAccessException("This time clock was revoked.");
        var now = clock.UtcNow;
        await devices.TouchLastSeenAsync(device.Id, now, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var project = await reader.ProjectAsync(device.ProjectId, cancellationToken) ?? throw new NotFoundException("that project");

        var results = new PunchResultDto?[inputs.Count];
        var firstOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        var repeats = new List<(int Index, int First)>();
        var parsed = new List<Incoming>();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var id = input.ClientPunchId?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > TimeClockRules.MaximumClientPunchIdLength || input.PunchedAt is not { } at ||
                !TryDirection(input.Direction, out var direction))
            {
                results[index] = Rejected(id is { Length: <= TimeClockRules.MaximumClientPunchIdLength } ? id : null, PunchRejection.Unreadable);
                continue;
            }

            if (firstOfId.TryGetValue(id, out var first))
            {
                repeats.Add((index, first));
                continue;
            }
            firstOfId[id] = index;

            if (project.IsArchived) results[index] = Rejected(id, PunchRejection.ProjectClosed);
            else if (TimeClockRules.CheckClock(at, now) is { } wrong) results[index] = Rejected(id, wrong);
            else if (PunchCode.Normalize(input.WorkerCode) is not { } code) results[index] = Rejected(id, PunchRejection.BadgeNotActive);
            else parsed.Add(new Incoming(index, id, code, at, direction));
        }

        if (parsed.Count > 0)
        {
            // A scan the machine already sent (its id is unique per machine) answers as it did, and nothing is stored again.
            var sent = (await punches.FindByClientPunchIdsAsync(device.Id, parsed.Select(item => item.ClientPunchId!).ToArray(), cancellationToken))
                .ToDictionary(item => item.ClientPunchId, StringComparer.Ordinal);
            var fresh = new List<Incoming>(parsed.Count);
            foreach (var item in parsed)
            {
                if (sent.TryGetValue(item.ClientPunchId!, out var earlier))
                    results[item.Index] = Answer(item.ClientPunchId, PunchOutcome.AlreadyRecorded, earlier.FirstName, earlier.LastName, earlier.Direction,
                        earlier.PunchedAtUtc, earlier.NotPostedHere, organization);
                else fresh.Add(item);
            }

            var accepted = await IntakeAsync(fresh, device.Id, project.Id, PunchSourceFor(device.Kind), null, organization, now, cancellationToken);
            foreach (var (index, result) in accepted) results[index] = result;
            if (accepted.Any(item => item.Result.Outcome == PunchOutcome.Accepted)) await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var (index, first) in repeats)
        {
            var original = results[first]!;
            results[index] = original.Outcome == PunchOutcome.Rejected ? original : original with { Outcome = PunchOutcome.AlreadyRecorded };
        }

        return new PushPunchesResultDto(results.Select(item => item!).ToArray());
    }

    // ---- Import -------------------------------------------------------------------------------------------------------------

    /// <summary>A machine's exported file for one project (at most 5,000 rows). Read in memory, never stored; every row is answered.</summary>
    public async Task<ImportPunchesResultDto> ImportAsync(Guid projectId, string? text, CancellationToken cancellationToken)
    {
        var rows = PunchCsv.Parse(text);
        var project = await reader.ProjectAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");
        if (project.IsArchived) throw new DomainRuleException("This project is archived. Restore it before importing scans.");
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var zone = organization.TimeClock.TimeZone;
        var now = clock.UtcNow;

        var rejected = new List<ImportRejectionDto>();
        var parsed = new List<Incoming>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (PunchCsv.ParseTime(row.Time, zone) is not { } at || !TryDirection(row.Direction, out var direction))
                rejected.Add(new ImportRejectionDto(row.Row, UnreadableRow));
            else if (TimeClockRules.CheckClock(at, now) is { } wrong) rejected.Add(new ImportRejectionDto(row.Row, ImportWords(wrong)));
            else if (PunchCode.Normalize(row.Code) is not { } code) rejected.Add(new ImportRejectionDto(row.Row, ImportWords(PunchRejection.BadgeNotActive)));
            else parsed.Add(new Incoming(index, null, code, at, direction));
        }

        var accepted = 0;
        var already = 0;
        if (parsed.Count > 0)
        {
            var outcomes = await IntakeAsync(parsed, null, project.Id, PunchSource.Import, currentUser.UserId, organization, now, cancellationToken);
            foreach (var (index, result) in outcomes)
            {
                switch (result.Outcome)
                {
                    case PunchOutcome.Accepted: accepted++; break;
                    case PunchOutcome.AlreadyRecorded: already++; break;
                    default: rejected.Add(new ImportRejectionDto(rows[index].Row, ImportWords(Enum.Parse<PunchRejection>(result.ReasonCode!)))); break;
                }
            }
        }

        if (accepted > 0)
        {
            audit.Record("TimeClock.Imported", "TimePunch", project.Id,
                $"Imported {accepted} time clock scan{(accepted == 1 ? "" : "s")} for {project.Name} ({already} already recorded, {rejected.Count} refused).");
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ImportPunchesResultDto(rows.Count, accepted, already, rejected.Count, rejected.OrderBy(item => item.Row).ToArray());
    }

    // ---- A supervisor's scan, and removing a scan ---------------------------------------------------------------------------------

    public async Task<TimeClockPunchDto> AddManualAsync(AddManualPunchRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null &&
            (await punches.FindByClientPunchIdsAsync(null, [requestId], cancellationToken)).FirstOrDefault() is { } repeated)
        {
            var original = await punches.GetAsync(repeated.Id, cancellationToken) ?? throw new NotFoundException("that scan");
            if (original.WorkerId != request.WorkerId || original.ProjectId != request.ProjectId)
                throw new DomainRuleException("That request was already used for a different scan. Reload the page and try again.");
            return ToDto(original, (await organizations.GetCurrentAsync(cancellationToken)).TimeClock.TimeZone);
        }

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var settings = organization.TimeClock;
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new NotFoundException("that project");
        var worker = await workers.GetAsync(request.WorkerId, includeHistory: false, cancellationToken) ?? throw new NotFoundException("that worker");
        var now = clock.UtcNow;

        DateTimeOffset at = default;
        if (request.At is { } wallClock)
        {
            try { at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), settings.TimeZone), TimeSpan.Zero); }
            catch (ArgumentException) { throw new ValidationFailedException("at", "That time doesn't exist in the company's time zone. Choose another."); }
        }
        PunchDirection? direction = null;
        if (TryDirection(request.Direction, out var parsed) && parsed is { } explicitDirection) direction = explicitDirection;

        // Checked before the duplicate and posting reads, so a bad request costs nothing more.
        var punch = TimePunch.AddManual(Guid.NewGuid(), currentUser.TenantId, worker, project, at, direction, request.Reason, settings, notPostedHere: false,
            currentUser.UserId, requestId, now);
        var workDate = punch.WorkDate;
        await EnsureDayOpenAsync(worker, project.Name, project.Id, workDate, cancellationToken);

        var nearby = await punches.ListAsync(project.Id, [worker.Id], workDate.AddDays(-1), workDate.AddDays(1), cancellationToken);
        if (nearby.FirstOrDefault(item => Math.Abs((item.PunchedAtUtc - punch.PunchedAtUtc).TotalSeconds) <= TimeClockRules.DuplicateWindow.TotalSeconds) is { } clash)
            throw new ValidationFailedException("at",
                $"There's already a scan at {LocalTime(clash.PunchedAtUtc, settings.TimeZone)}. Remove that one first, or choose another time.");

        // A time out that closes a shift still open belongs to the day that shift began (a night shift from 22:00 to 06:00 is one day).
        var latest = nearby.Where(item => item.PunchedAtUtc < punch.PunchedAtUtc).OrderByDescending(item => item.PunchedAtUtc)
            .Select(item => (EarlierPunch?)new EarlierPunch(item.PunchedAtUtc, item.Direction, item.WorkDate)).FirstOrDefault();
        var shiftDate = TimeClockRules.Place(direction, punch.PunchedAtUtc, workDate, latest).WorkDate;
        if (shiftDate != workDate) await EnsureDayOpenAsync(worker, project.Name, project.Id, shiftDate, cancellationToken);

        var posted = await punches.PostedAsync(project.Id, [worker.Id], shiftDate, shiftDate, cancellationToken);
        var notPosted = !posted.Contains((worker.Id, shiftDate));
        if (notPosted || shiftDate != workDate)
            punch = TimePunch.AddManual(punch.Id, currentUser.TenantId, worker, project, at, direction, request.Reason, settings, notPosted,
                currentUser.UserId, requestId, now, shiftDate);

        punches.Add(punch);
        audit.Record("TimeClock.PunchAdded", nameof(TimePunch), punch.Id,
            $"Added a missing {DirectionWords(punch.Direction)} scan for {worker.FullName} on {project.Name} at {LocalTime(punch.PunchedAtUtc, settings.TimeZone)}, {Format(punch.WorkDate)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(punch, settings.TimeZone);
    }

    public async Task<TimeClockPunchDto> VoidAsync(Guid id, VoidPunchRequest request, CancellationToken cancellationToken)
    {
        var punch = await punches.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that scan");
        ConcurrencyGuard.Ensure(punch.Version, request.Version);
        var project = await projects.GetAsync(punch.ProjectId, cancellationToken) ?? throw new NotFoundException("that project");
        var worker = await workers.GetAsync(punch.WorkerId, includeHistory: false, cancellationToken) ?? throw new NotFoundException("that worker");
        await EnsureDayOpenAsync(worker, project.Name, project.Id, punch.WorkDate, cancellationToken);

        punch.Void(project, request.Reason, currentUser.UserId, clock.UtcNow);
        var zone = (await organizations.GetCurrentAsync(cancellationToken)).TimeClock.TimeZone;
        // The reason is free text and stays on the scan, not in the audit line.
        audit.Record("TimeClock.PunchVoided", nameof(TimePunch), punch.Id,
            $"Removed the {DirectionWords(punch.Direction)} scan of {worker.FullName} on {project.Name} at {LocalTime(punch.PunchedAtUtc, zone)}, {Format(punch.WorkDate)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(punch, zone);
    }

    // ---- The shared intake ----------------------------------------------------------------------------------------------------

    private sealed record Incoming(int Index, string? ClientPunchId, string Code, DateTimeOffset At, PunchDirection? Direction);

    /// <summary>
    /// Checks the codes, drops scans already there (the same worker on the same machine within 60 seconds), resolves automatic directions in time order,
    /// flags a worker not posted to the project, and adds the scans (the caller saves). Three statements however many scans: the workers, the scans
    /// already there around those dates, and the postings.
    /// </summary>
    private async Task<IReadOnlyList<(int Index, PunchResultDto Result)>> IntakeAsync(IReadOnlyList<Incoming> incoming, Guid? deviceId, Guid projectId,
        PunchSource source, Guid? recordedBy, Organization organization, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var answers = new List<(int, PunchResultDto)>(incoming.Count);
        if (incoming.Count == 0) return answers;
        var settings = organization.TimeClock;

        var byCode = (await punches.FindWorkersByCodesAsync(incoming.Select(item => item.Code).Distinct().ToArray(), cancellationToken))
            .ToDictionary(worker => worker.Code, StringComparer.Ordinal);
        var known = new List<(Incoming Item, PunchWorker Worker, DateOnly WorkDate)>(incoming.Count);
        foreach (var item in incoming)
        {
            // An unknown code, a replaced badge and a worker who is not active answer alike, so the answers can't be used to find out which codes exist.
            if (!byCode.TryGetValue(item.Code, out var worker) || worker.Status != WorkerStatus.Active)
                answers.Add((item.Index, Rejected(item.ClientPunchId, PunchRejection.BadgeNotActive)));
            else known.Add((item, worker, TimeClockRules.WorkDateOf(item.At, settings)));
        }
        if (known.Count == 0) return answers;

        var workerIds = known.Select(item => item.Worker.Id).Distinct().ToArray();
        var from = known.Min(item => item.WorkDate).AddDays(-1);
        var to = known.Max(item => item.WorkDate).AddDays(1);
        var day = (await punches.ListAsync(projectId, workerIds, from, to, cancellationToken))
            .Select(fact => (fact.WorkerId, fact.DeviceId, fact.PunchedAtUtc, fact.WorkDate, fact.Direction, fact.Id)).ToList();
        var posted = await punches.PostedAsync(projectId, workerIds, from, to, cancellationToken);

        foreach (var (item, worker, workDate) in known.OrderBy(entry => entry.Item.At).ThenBy(entry => entry.Item.Index))
        {
            var duplicate = day.Where(fact => fact.WorkerId == worker.Id && fact.DeviceId == deviceId &&
                    Math.Abs((fact.PunchedAtUtc - item.At).TotalSeconds) <= TimeClockRules.DuplicateWindow.TotalSeconds)
                .OrderBy(fact => Math.Abs((fact.PunchedAtUtc - item.At).Ticks)).Select(fact => ((Guid, DateTimeOffset, PunchDirection)?)(fact.Id, fact.PunchedAtUtc, fact.Direction))
                .FirstOrDefault();
            var notPosted = !posted.Contains((worker.Id, workDate));
            if (duplicate is { } earlier)
            {
                answers.Add((item.Index, Answer(item.ClientPunchId, PunchOutcome.AlreadyRecorded, worker.FirstName, worker.LastName, earlier.Item3, earlier.Item2,
                    notPosted, organization)));
                continue;
            }

            // The worker's latest earlier scan decides the direction of an automatic one and the day of an Out, even across the day-start (a night
            // shift that ends after it is still one day, the day it began).
            var latest = day.Where(fact => fact.WorkerId == worker.Id && fact.PunchedAtUtc < item.At)
                .OrderByDescending(fact => fact.PunchedAtUtc).Select(fact => (EarlierPunch?)new EarlierPunch(fact.PunchedAtUtc, fact.Direction, fact.WorkDate))
                .FirstOrDefault();
            var (direction, shiftDate) = TimeClockRules.Place(item.Direction, item.At, workDate, latest);
            if (shiftDate != workDate) notPosted = !posted.Contains((worker.Id, shiftDate));
            var punch = TimePunch.Receive(Guid.NewGuid(), organization.Id, worker.Id, projectId, deviceId, item.At, shiftDate, direction,
                item.Direction is null, source, item.ClientPunchId, notPosted, recordedBy, now);
            punches.Add(punch);
            day.Add((worker.Id, deviceId, punch.PunchedAtUtc, shiftDate, direction, punch.Id));
            answers.Add((item.Index, Answer(item.ClientPunchId, PunchOutcome.Accepted, worker.FirstName, worker.LastName, direction, punch.PunchedAtUtc,
                notPosted, organization)));
        }

        return answers;
    }

    /// <summary>A day already confirmed as attendance can't take or lose a scan: the attendance entry is what changes then.</summary>
    private async Task EnsureDayOpenAsync(Worker worker, string projectName, Guid projectId, DateOnly workDate, CancellationToken cancellationToken)
    {
        if (await punches.DayRecordedAsync(worker.Id, projectId, workDate, cancellationToken))
            throw new DomainRuleException($"{worker.FullName} already has attendance on {projectName} for {Format(workDate)}. Change that attendance entry instead.");
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    /// <summary>In, Out, or automatic (Auto or nothing). False for any other word.</summary>
    private static bool TryDirection(string? text, out PunchDirection? direction)
    {
        direction = null;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "In", StringComparison.OrdinalIgnoreCase)) { direction = PunchDirection.In; return true; }
        if (string.Equals(value, "Out", StringComparison.OrdinalIgnoreCase)) { direction = PunchDirection.Out; return true; }
        return false;
    }

    private static PunchSource PunchSourceFor(TimeClockDeviceKind kind) => kind == TimeClockDeviceKind.Kiosk ? PunchSource.Kiosk : PunchSource.Terminal;

    private static PunchResultDto Rejected(string? clientPunchId, PunchRejection rejection) =>
        new(clientPunchId, PunchOutcome.Rejected, null, null, null, null, false, rejection.ToString(), TimeClockRules.Explain(rejection));

    /// <summary>What the kiosk shows: the worker's first and last name, the direction and the time. Nothing else about the worker.</summary>
    private static PunchResultDto Answer(string? clientPunchId, string outcome, string firstName, string lastName, PunchDirection direction,
        DateTimeOffset at, bool notPosted, Organization organization) =>
        new(clientPunchId, outcome, $"{firstName} {lastName}", direction.ToString(), at, LocalTime(at, organization.TimeClock.TimeZone), notPosted, null, null);

    private static string ImportWords(PunchRejection rejection) => rejection switch
    {
        PunchRejection.BadgeNotActive => "This badge code isn't active. Check the code, or issue the worker a badge.",
        PunchRejection.ClockWrong => "The time is more than 5 minutes ahead or more than 7 days old, so the row wasn't kept.",
        _ => UnreadableRow
    };

    internal static TimeClockPunchDto ToDto(TimePunch punch, TimeZoneInfo zone, string? deviceName = null) =>
        new(punch.Id, punch.PunchedAtUtc, LocalTime(punch.PunchedAtUtc, zone), punch.Direction.ToString(), punch.Source.ToString(), deviceName,
            punch.NotPostedHere, punch.Reason, punch.Version);

    /// <summary>"07:02": the company's local time of day.</summary>
    internal static string LocalTime(DateTimeOffset at, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string DirectionWords(PunchDirection direction) => direction == PunchDirection.In ? "time in" : "time out";

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
