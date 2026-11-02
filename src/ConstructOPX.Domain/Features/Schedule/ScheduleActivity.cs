using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Schedule;

public enum ScheduleActivityStatus
{
    Active,

    /// <summary>Taken out of the plan with a reason. It stays on record but counts in nothing and links no budget line.</summary>
    Removed
}

/// <summary>What a person types about an activity: its name, its planned dates and the budget lines whose work it is.</summary>
public sealed record ScheduleActivityDetails(string? Name, DateOnly PlannedStart, DateOnly PlannedFinish, IReadOnlyList<Guid>? BudgetLineIds);

/// <summary>
/// What the database says about the budget lines an activity asks to link, looked up before the domain decides.
/// </summary>
/// <param name="ActiveLines">The project's active budget lines among those asked for, by id, with their description.</param>
/// <param name="LinkedElsewhere">Lines among those asked for that another active activity of the project already links, with that activity's name.</param>
public sealed record ScheduleLineContext(IReadOnlyDictionary<Guid, string> ActiveLines, IReadOnlyDictionary<Guid, string> LinkedElsewhere)
{
    public static readonly ScheduleLineContext Empty = new(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
}

/// <summary>
/// One planned activity of a project: a name, the dates it is planned to start and finish, and the budget lines whose work it covers. Its weight
/// in the plan is the budget of those lines, so the schedule says when the money is planned to be earned. A budget line belongs to at most one
/// activity. Setting the baseline copies the planned dates to the baseline dates, so later changes show as slip. This is planning data, not a
/// financial record: an activity is removed with a reason (kept on record, audited), not voided, and an archived project's schedule is read-only.
/// There are no dependencies between activities and no critical path.
/// </summary>
public sealed class ScheduleActivity : AggregateRoot
{
    public const int MaximumNameLength = 120;
    public const int MaximumReasonLength = 300;
    public const int MaximumLinesPerActivity = 500;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);
    private static readonly DateOnly LatestDate = new(2100, 12, 31);

    private readonly List<ScheduleActivityLine> _lines = [];

    private ScheduleActivity() { }

    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedFinish { get; private set; }

    /// <summary>The planned dates as they stood when the baseline was last set; null until then (or for an activity added after it).</summary>
    public DateOnly? BaselineStart { get; private set; }

    public DateOnly? BaselineFinish { get; private set; }
    public DateTimeOffset? BaselineSetAtUtc { get; private set; }

    /// <summary>Where the activity sits in the list; a new one goes last.</summary>
    public int SortOrder { get; private set; }

    public ScheduleActivityStatus Status { get; private set; } = ScheduleActivityStatus.Active;
    public string? RemovedReason { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<ScheduleActivityLine> Lines => _lines;

    public bool IsActive => Status == ScheduleActivityStatus.Active;

    public static ScheduleActivity Create(Guid id, Guid tenantId, Project project, ScheduleActivityDetails details, ScheduleLineContext lines,
        int sortOrder, Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        var activity = new ScheduleActivity { ProjectId = project.Id, SortOrder = sortOrder, CreatedByUserId = createdByUserId };
        var errors = new ValidationErrors();
        activity.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));
        activity.Initialize(id, tenantId, now);
        var lineIds = activity.Apply(details, lines, errors);
        errors.ThrowIfAny();
        activity.ReplaceLines(lineIds);
        return activity;
    }

    /// <summary>Changes the name, dates and linked lines. Returns false, and changes nothing, when they are the same as they were.</summary>
    public bool Update(Project project, ScheduleActivityDetails details, ScheduleLineContext lines, int? sortOrder, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive();

        var before = (Name, PlannedStart, PlannedFinish, SortOrder, Lines: LinkedLineIds().Order().ToArray());
        var errors = new ValidationErrors();
        if (sortOrder is < 0) errors.Add("sortOrder", "Choose a position of zero or more.");
        var lineIds = Apply(details, lines, errors);
        errors.ThrowIfAny();

        if (sortOrder is { } order) SortOrder = order;
        var changed = before.Name != Name || before.PlannedStart != PlannedStart || before.PlannedFinish != PlannedFinish || before.SortOrder != SortOrder ||
            !before.Lines.SequenceEqual(lineIds.Order());
        if (!changed) return false;
        ReplaceLines(lineIds);
        Touch(now);
        return true;
    }

    /// <summary>Copies the planned dates to the baseline. Repeatable: each time the baseline becomes today's plan.</summary>
    public void SetBaseline(Project project, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive();
        BaselineStart = PlannedStart;
        BaselineFinish = PlannedFinish;
        BaselineSetAtUtc = now;
        Touch(now);
    }

    /// <summary>Takes the activity out of the plan. Its budget lines are freed for another activity; the reason stays on the record.</summary>
    public void Remove(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive();
        RemovedReason = Guard.RequiredText(reason, "reason", "the reason for removing it", MaximumReasonLength);
        Status = ScheduleActivityStatus.Removed;
        RemovedAtUtc = now;
        RemovedByUserId = userId;
        _lines.Clear();
        Touch(now);
    }

    public IEnumerable<Guid> LinkedLineIds() => _lines.Select(line => line.BudgetLineId);

    // ---- Checks -----------------------------------------------------------------------------------------------------------------

    /// <summary>Checks and applies the name and dates; returns the line ids to link. Nothing changes when there is an error.</summary>
    private IReadOnlyList<Guid> Apply(ScheduleActivityDetails details, ScheduleLineContext context, ValidationErrors errors)
    {
        var name = Field(errors, () => Guard.RequiredText(details.Name, "name", "the activity name", MaximumNameLength));
        var startOk = CheckDate(errors, details.PlannedStart, "plannedStart", "the planned start");
        var finishOk = CheckDate(errors, details.PlannedFinish, "plannedFinish", "the planned finish");
        if (startOk && finishOk && details.PlannedFinish < details.PlannedStart)
            errors.Add("plannedFinish", "The planned finish can't be before the planned start.");

        var requested = (details.BudgetLineIds ?? []).Distinct().ToArray();
        if (requested.Length > MaximumLinesPerActivity)
            errors.Add("budgetLineIds", $"An activity can link at most {MaximumLinesPerActivity} budget lines. Split it into smaller activities.");
        else
            for (var index = 0; index < requested.Length; index++)
            {
                var lineId = requested[index];
                if (!context.ActiveLines.TryGetValue(lineId, out var description))
                    errors.Add($"budgetLineIds[{index}]", "That budget line isn't part of this project any more. Reload the page and try again.");
                else if (context.LinkedElsewhere.TryGetValue(lineId, out var other))
                    errors.Add($"budgetLineIds[{index}]",
                        $"\"{description}\" is already in the activity \"{other}\". A budget line can be in one activity only: take it out of that activity first.");
            }

        if (errors.Any) return [];
        Name = name!;
        PlannedStart = details.PlannedStart;
        PlannedFinish = details.PlannedFinish;
        return requested;
    }

    private void ReplaceLines(IReadOnlyList<Guid> lineIds)
    {
        _lines.RemoveAll(line => !lineIds.Contains(line.BudgetLineId));
        foreach (var lineId in lineIds.Where(lineId => _lines.All(line => line.BudgetLineId != lineId)))
            _lines.Add(new ScheduleActivityLine(Guid.NewGuid(), TenantId, Id, lineId));
    }

    private static bool CheckDate(ValidationErrors errors, DateOnly date, string field, string label)
    {
        if (date == default) errors.Add(field, $"Choose {label}.");
        else if (date < EarliestDate || date > LatestDate) errors.Add(field, "Choose a valid date.");
        else return true;
        return false;
    }

    private void EnsureActive()
    {
        if (!IsActive) throw new DomainRuleException("This activity was removed from the schedule, so it can't be changed.");
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this activity.", nameof(project));
    }

    /// <summary>"3 Dec 2026", for messages.</summary>
    public static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}

/// <summary>A budget line whose work an activity covers. A line is linked to at most one activity (a unique index holds it).</summary>
public sealed class ScheduleActivityLine
{
    private ScheduleActivityLine() { }

    internal ScheduleActivityLine(Guid id, Guid tenantId, Guid activityId, Guid budgetLineId)
    {
        Id = id;
        TenantId = tenantId;
        ActivityId = activityId;
        BudgetLineId = budgetLineId;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActivityId { get; private set; }
    public Guid BudgetLineId { get; private set; }
}
