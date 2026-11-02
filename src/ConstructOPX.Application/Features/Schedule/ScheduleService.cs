using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;

namespace ConstructOPX.Application.Features.Schedule;

/// <summary>
/// The project schedule: planned activities with dates, each covering budget lines, a baseline, and the S-curve. Weights, percent complete
/// and planned value are worked out from the Progress tab's own line rows (<see cref="IProgressReader"/>), so the two never disagree. Every
/// change loads the activity, checks the version the person opened and that the project is not archived, and saves once with its audit line.
/// Reads are a fixed number of SQL statements however many activities, lines and entries there are.
/// </summary>
public sealed class ScheduleService(IScheduleRepository activities, IScheduleReader reader, IProgressReader progress, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    /// <summary>The schedule as of today. Four statements: the project, the company, the activities, and the budget lines with today's percent.</summary>
    public async Task<ScheduleDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var rows = await reader.ListAsync(projectId, cancellationToken);
        var lines = await progress.ListLinesAsync(projectId, today, cancellationToken);
        return ScheduleFigures.Build(project, organization.VarianceBasis, today, rows, lines);
    }

    /// <summary>
    /// The S-curve. Six statements however many activities, lines and entries: the project, the company, the activities, the budget lines with
    /// today's percent, the progress history in steps and the posted cost in steps (the last three are skipped without activities).
    /// </summary>
    public async Task<ScheduleCurveDto> CurveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var rows = await reader.ListAsync(projectId, cancellationToken);
        var steps = ScheduleFigures.Steps(rows);
        if (steps is null) return ScheduleFigures.Curve(projectId, organization.VarianceBasis, today, rows, [], null, [], []);

        var lines = await progress.ListLinesAsync(projectId, today, cancellationToken);
        var progressSteps = await reader.ProgressStepsAsync(projectId, steps, today, cancellationToken);
        var costSteps = await reader.CostStepsAsync(projectId, steps, today, cancellationToken);
        return ScheduleFigures.Curve(projectId, organization.VarianceBasis, today, rows, lines, steps, progressSteps, costSteps);
    }

    // ---- Changes ----------------------------------------------------------------------------------------------------------------

    public async Task<ScheduleDto> CreateAsync(Guid projectId, CreateScheduleActivityRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await activities.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, projectId, cancellationToken);

        var project = await LoadProjectAsync(projectId, cancellationToken);
        project.EnsureNotArchived();
        var details = Details(request.Name, request.PlannedStart, request.PlannedFinish, request.BudgetLineIds);
        var context = await LineContextAsync(projectId, null, details.BudgetLineIds, cancellationToken);
        var activity = ScheduleActivity.Create(Guid.NewGuid(), currentUser.TenantId, project, details, context,
            await activities.NextSortOrderAsync(projectId, cancellationToken), currentUser.UserId, requestId, clock.UtcNow);

        activities.Add(activity);
        audit.Record("Schedule.ActivityAdded", nameof(ScheduleActivity), activity.Id,
            $"Added the schedule activity \"{activity.Name}\" ({Describe(activity)}) to {project.Name}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made.
            if (await activities.FindByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SameCreateAsync(first, projectId, cancellationToken);
            throw;
        }

        return await GetAsync(projectId, cancellationToken);
    }

    public async Task<ScheduleDto> UpdateAsync(Guid projectId, Guid id, UpdateScheduleActivityRequest request, CancellationToken cancellationToken)
    {
        var (activity, project) = await LoadForChangeAsync(projectId, id, request.Version, cancellationToken);
        var details = Details(request.Name, request.PlannedStart, request.PlannedFinish, request.BudgetLineIds);
        var context = await LineContextAsync(projectId, activity.Id, details.BudgetLineIds, cancellationToken);
        if (!activity.Update(project, details, context, request.SortOrder, clock.UtcNow)) return await GetAsync(projectId, cancellationToken);

        audit.Record("Schedule.ActivityChanged", nameof(ScheduleActivity), activity.Id,
            $"Changed the schedule activity \"{activity.Name}\" ({Describe(activity)}) on {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    public async Task<ScheduleDto> RemoveAsync(Guid projectId, Guid id, RemoveScheduleActivityRequest request, CancellationToken cancellationToken)
    {
        var (activity, project) = await LoadForChangeAsync(projectId, id, request.Version, cancellationToken);
        activity.Remove(project, request.Reason, currentUser.UserId, clock.UtcNow);
        // The reason is free text and stays on the record, not in the audit line.
        audit.Record("Schedule.ActivityRemoved", nameof(ScheduleActivity), activity.Id,
            $"Removed the schedule activity \"{activity.Name}\" from {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>
    /// Copies every active activity's planned dates to its baseline. The person names the activities and versions they saw; a schedule that
    /// changed since (an activity added, changed or removed) is refused with a 409 so they confirm what they are fixing.
    /// </summary>
    public async Task<ScheduleDto> SetBaselineAsync(Guid projectId, SetScheduleBaselineRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        project.EnsureNotArchived();
        if (request.Activities is null) throw new ValidationFailedException("activities", "Reload the schedule and try again.");

        var live = await activities.ListActiveAsync(projectId, cancellationToken);
        if (live.Count == 0) throw new DomainRuleException("Add an activity to the schedule before setting the baseline.");
        var seen = request.Activities.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First().Version);
        if (seen.Count != request.Activities.Count || seen.Count != live.Count ||
            live.Any(activity => !seen.TryGetValue(activity.Id, out var version) || version != activity.Version))
            throw new ConcurrencyConflictException();

        var now = clock.UtcNow;
        foreach (var activity in live) activity.SetBaseline(project, now);
        audit.Record("Schedule.BaselineSet", nameof(Project), project.Id,
            $"Set the schedule baseline of {project.Name} from {live.Count} activit{(live.Count == 1 ? "y" : "ies")}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private async Task<ScheduleDto> SameCreateAsync(ScheduleRequestOwner owner, Guid projectId, CancellationToken cancellationToken) =>
        owner.ProjectId == projectId
            ? await GetAsync(projectId, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different activity. Reload the page and try again.");

    /// <summary>Only a list the domain could accept is looked up: more lines than an activity may link are refused before any query.</summary>
    private async Task<ScheduleLineContext> LineContextAsync(Guid projectId, Guid? activityId, IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        var distinct = (ids ?? []).Distinct().ToArray();
        return distinct.Length is 0 or > ScheduleActivity.MaximumLinesPerActivity
            ? ScheduleLineContext.Empty
            : await activities.LineContextAsync(projectId, activityId, distinct, cancellationToken);
    }

    /// <summary>Loads the activity and its project for a change. An activity of another project (or company) is a 404; the version must match.</summary>
    private async Task<(ScheduleActivity Activity, Project Project)> LoadForChangeAsync(Guid projectId, Guid id, int version,
        CancellationToken cancellationToken)
    {
        var activity = await activities.GetAsync(id, cancellationToken);
        if (activity is null || activity.ProjectId != projectId) throw new NotFoundException("that schedule activity");
        ConcurrencyGuard.Ensure(activity.Version, version);
        return (activity, await LoadProjectAsync(projectId, cancellationToken));
    }

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private static ScheduleActivityDetails Details(string? name, DateOnly? start, DateOnly? finish, IReadOnlyList<Guid>? lineIds) =>
        new(name, start ?? default, finish ?? default, lineIds);

    /// <summary>"3 Dec 2026 to 9 Jan 2027, 4 budget lines": the dates and how many lines, never an amount.</summary>
    private static string Describe(ScheduleActivity activity)
    {
        var count = activity.Lines.Count;
        return $"{Format(activity.PlannedStart)} to {Format(activity.PlannedFinish)}, {count} budget line{(count == 1 ? "" : "s")}";
    }

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
