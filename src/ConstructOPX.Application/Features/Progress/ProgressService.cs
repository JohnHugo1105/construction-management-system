using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Progress;

/// <summary>
/// Progress and earned value. A person says how far along each budget line is (an append-only entry per change); the report turns those
/// percentages into the value of the work done and sets it against what was spent and what was planned. Recording is all or nothing for
/// the whole batch, and a repeated request id returns the first result instead of recording again. The report is a fixed number of SQL
/// statements however many lines and entries a project has.
/// </summary>
public sealed class ProgressService(IProgressRepository progress, IProgressReader reader, IProjectRepository projects, IBudgetLineRepository budgetLines,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    /// <summary>The project's progress and earned value on a date (today when none is given; never a future date).</summary>
    public async Task<ProgressDto> GetAsync(Guid projectId, DateOnly? asOf, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        if (asOf is { } requested && requested > today) throw new ValidationFailedException("asOf", "Choose today or an earlier date.");
        var date = asOf ?? today;

        var rows = await reader.ListLinesAsync(projectId, date, cancellationToken);
        var actual = await reader.SumActualAsync(projectId, date, cancellationToken);
        return ProgressReportBuilder.Build(project, organization.VarianceBasis, date, rows, actual);
    }

    /// <summary>A budget line's progress entries, newest first. Removed lines keep their history.</summary>
    public async Task<PagedResult<ProgressHistoryItemDto>> HistoryAsync(Guid budgetLineId, PageRequest page, CancellationToken cancellationToken)
    {
        _ = await budgetLines.GetAsync(budgetLineId, cancellationToken) ?? throw new NotFoundException("that budget line");
        return await reader.HistoryAsync(budgetLineId, page, cancellationToken);
    }

    /// <summary>Records the progress of several lines at once, all or nothing.</summary>
    public async Task<RecordProgressResult> RecordAsync(Guid projectId, RecordProgressRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);

        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await progress.FindByClientRequestIdAsync(requestId, cancellationToken) is { Count: > 0 } original)
            return Original(original, project, request);

        try
        {
            return await RecordNewAsync(project, request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ValidationFailedException or ConcurrencyConflictException)
        {
            // The same form submitted twice at once: the first was saved between this one's look-up and its save, so this one finds its
            // lines already at the percent it asked for, or the database refuses its duplicate entries. Answer it with what the first
            // recorded, as a repeat that arrived a moment later would be. Without such a record the refusal is a real one (an archived
            // project, a wrong percent) and is reported as it is.
            if (await progress.FindByClientRequestIdAsync(requestId, cancellationToken) is { Count: > 0 } first)
                return Original(first, project, request);
            throw;
        }
    }

    private async Task<RecordProgressResult> RecordNewAsync(Project project, RecordProgressRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;
        var today = organization.TodayLocal(now);

        // Only a batch the domain could accept loads anything: a request with thousands of lines is refused before they are looked up.
        var requested = request.Lines ?? [];
        var ids = requested.Count <= ProgressRecorder.MaximumLinesPerBatch ? requested.Select(line => line.BudgetLineId).Distinct().ToArray() : [];
        var lines = ids.Length == 0 ? new Dictionary<Guid, BudgetLine>() : await progress.GetActiveLinesAsync(project.Id, ids, cancellationToken);
        var latest = lines.Count == 0 ? new Dictionary<Guid, LatestProgress>() : await progress.GetLatestAsync(lines.Keys.ToArray(), cancellationToken);

        var changes = requested.Select(line => new ProgressChange(line.BudgetLineId, line.PercentBps ?? -1, line.Note, line.ExpectedPercentBps)).ToArray();
        var plan = ProgressRecorder.Plan(currentUser.TenantId, project, request.AsOfDate, today, changes, lines, latest, currentUser.UserId, requestId, now);

        progress.Add(plan.Entries);
        audit.Record("Progress.Recorded", nameof(Project), project.Id,
            $"Recorded progress on {plan.Entries.Count} budget line{(plan.Entries.Count == 1 ? "" : "s")} of {project.Name} as of " +
            $"{request.AsOfDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecordProgressResult(request.AsOfDate, plan.Entries.Count, now);
    }

    /// <summary>A repeated request id answers with the original result only when it was for the same project and date.</summary>
    private static RecordProgressResult Original(IReadOnlyList<ProgressEntry> entries, Project project, RecordProgressRequest request)
    {
        var first = entries[0];
        if (first.ProjectId != project.Id || first.AsOfDate != request.AsOfDate)
            throw new DomainRuleException("That request was already used for different progress. Reload the page and try again.");
        return new RecordProgressResult(first.AsOfDate, entries.Count, first.RecordedAtUtc);
    }

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
