using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.SiteDiary;

namespace ConstructOPX.Application.Features.SiteDiary;

/// <summary>
/// Site diary use cases: write the day's report, correct it, void it, and read a project's reports with the workers and deliveries of the
/// day filled in. A change loads the report and its project, checks the version the person opened, lets the domain judge the author window
/// and the archived project, and saves once with its audit line. The audit line names the fields a correction changed and never the text
/// a person typed.
/// </summary>
public sealed class SiteDiaryService(ISiteDiaryRepository entries, ISiteDiaryReader reader, IProjectRepository projects, IOrganizationRepository organizations,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    public async Task<PagedResult<DiaryListItemDto>> ListAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        // A project of another company answers 404, not an empty list.
        await LoadProjectAsync(projectId, cancellationToken);
        return await reader.ListAsync(projectId, page, cancellationToken);
    }

    public async Task<DiaryEntryDto> GetAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, cancellationToken);
        return await ReadAsync(projectId, reportDate, cancellationToken);
    }

    /// <summary>Who was on site and what was delivered on a day, for the form that writes that day's report.</summary>
    public async Task<DiaryFilledInDto> FilledInAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, cancellationToken);
        return await reader.FilledInAsync(projectId, reportDate, CanSeeWorkerNames, cancellationToken);
    }

    // ---- Changes ----------------------------------------------------------------------------------------------------------------

    public async Task<DiaryEntryDto> CreateAsync(Guid projectId, CreateDiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await entries.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, projectId, request, cancellationToken);

        var project = await LoadProjectAsync(projectId, cancellationToken);
        var today = await TodayAsync(cancellationToken);
        var date = request.ReportDate ?? default;
        var entry = SiteDiaryEntry.Create(Guid.NewGuid(), currentUser.TenantId, project, date,
            Details(request.Weather, request.WeatherNote, request.WorkDone, request.IssuesAndDelays, request.VisitorsAndInspections, request.SafetyNotes,
                request.SubcontractorWorkers), currentUser.UserId, requestId, today, clock.UtcNow);
        if (await entries.LiveExistsAsync(projectId, date, cancellationToken)) throw new DomainRuleException(AlreadyReported(date));

        entries.Add(entry);
        audit.Record("SiteDiary.Posted", nameof(SiteDiaryEntry), entry.Id, $"Wrote the site report for {Format(date)} on {project.Name}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made.
            if (await entries.FindByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SameCreateAsync(first, projectId, request, cancellationToken);
            throw;
        }
        catch (DomainRuleException)
        {
            // Two people wrote the same day at once: the unique date let one through.
            throw new DomainRuleException(AlreadyReported(date));
        }
        return await ReadByIdAsync(entry.Id, cancellationToken);
    }

    public async Task<DiaryEntryDto> UpdateAsync(Guid projectId, DateOnly reportDate, UpdateDiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var (entry, project) = await LoadForChangeAsync(projectId, reportDate, request.Version, cancellationToken);
        var changed = entry.Correct(project, Details(request.Weather, request.WeatherNote, request.WorkDone, request.IssuesAndDelays,
            request.VisitorsAndInspections, request.SafetyNotes, request.SubcontractorWorkers), currentUser.UserId, CanManage, await TodayAsync(cancellationToken),
            clock.UtcNow);
        if (changed.Count == 0) return await ReadAsync(projectId, reportDate, cancellationToken);

        // Field names only: what a person typed stays on the report, never in the audit line.
        audit.Record("SiteDiary.Corrected", nameof(SiteDiaryEntry), entry.Id,
            $"Corrected the site report for {Format(reportDate)} on {project.Name}: changed {string.Join(", ", changed)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(projectId, reportDate, cancellationToken);
    }

    public async Task<DiaryEntryDto> VoidAsync(Guid projectId, DateOnly reportDate, VoidDiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var (entry, project) = await LoadForChangeAsync(projectId, reportDate, request.Version, cancellationToken);
        entry.Void(project, request.Reason, currentUser.UserId, CanManage, await TodayAsync(cancellationToken), clock.UtcNow);
        // The reason is free text and stays on the report, not in the audit line.
        audit.Record("SiteDiary.Voided", nameof(SiteDiaryEntry), entry.Id, $"Voided the site report for {Format(reportDate)} on {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // The voided report is no longer the live one for its date, so it is read by its own id.
        return await ReadByIdAsync(entry.Id, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private bool CanManage => Permissions.IsAllowed(currentUser.Roles, Permissions.ProjectsManage);

    private bool CanSeeWorkerNames => Permissions.IsAllowed(currentUser.Roles, Permissions.WorkforceRead);

    private DiaryReadScope Scope(DateOnly today) => new(currentUser.UserId, CanManage, Permissions.IsAllowed(currentUser.Roles, Permissions.DiaryWrite),
        CanSeeWorkerNames, today);

    private async Task<DiaryEntryDto> ReadAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken) =>
        await reader.GetAsync(projectId, reportDate, Scope(await TodayAsync(cancellationToken)), cancellationToken) ?? throw new NotFoundException("a report for that day");

    private async Task<DiaryEntryDto> ReadByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetByIdAsync(id, Scope(await TodayAsync(cancellationToken)), cancellationToken) ?? throw new NotFoundException("that report");

    /// <summary>A repeated create answers with the original only when it was for the same project and day.</summary>
    private async Task<DiaryEntryDto> SameCreateAsync(DiaryRequestOwner owner, Guid projectId, CreateDiaryEntryRequest request, CancellationToken cancellationToken) =>
        owner.ProjectId == projectId && owner.ReportDate == (request.ReportDate ?? default)
            ? await ReadByIdAsync(owner.EntryId, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different report. Reload the page and try again.");

    /// <summary>Loads the live report and its project for a change: the version must be the one the person opened.</summary>
    private async Task<(SiteDiaryEntry Entry, Project Project)> LoadForChangeAsync(Guid projectId, DateOnly reportDate, int version,
        CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        var entry = await entries.GetLiveAsync(projectId, reportDate, cancellationToken) ?? throw new NotFoundException("a report for that day");
        ConcurrencyGuard.Ensure(entry.Version, version);
        return (entry, project);
    }

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    /// <summary>The company's calendar day: "not in the future" and the author window are judged on it, never on the UTC day.</summary>
    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private static SiteDiaryDetails Details(string? weather, string? weatherNote, string? workDone, string? issues, string? visitors, string? safety, int workers) =>
        new(ParseWeather(weather), weatherNote, workDone, issues, visitors, safety, workers);

    /// <summary>A word from the request as the weather, or an undefined value (which the domain reports as "choose the weather"). Numbers are not words.</summary>
    private static DiaryWeather ParseWeather(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) && Enum.TryParse<DiaryWeather>(text.Trim(), ignoreCase: true, out var value) &&
        Enum.IsDefined(value)
            ? value
            : (DiaryWeather)(-1);

    private static string AlreadyReported(DateOnly date) =>
        $"There is already a site report for {Format(date)} on this project. Open it to correct it, or void it to write it again.";

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
