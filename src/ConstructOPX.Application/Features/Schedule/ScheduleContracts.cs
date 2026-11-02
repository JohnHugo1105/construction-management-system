using ConstructOPX.Domain.Features.Schedule;

namespace ConstructOPX.Application.Features.Schedule;

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface IScheduleRepository
{
    /// <summary>An activity with its linked lines, tracked for change. Null when it isn't in the organization.</summary>
    Task<ScheduleActivity?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The project's active activities with their linked lines, tracked for change (setting the baseline changes all of them).</summary>
    Task<IReadOnlyList<ScheduleActivity>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// In one statement: which of <paramref name="budgetLineIds"/> are active lines of the project, and which of those another active activity
    /// (not <paramref name="exceptActivityId"/>) already links.
    /// </summary>
    Task<ScheduleLineContext> LineContextAsync(Guid projectId, Guid? exceptActivityId, IReadOnlyCollection<Guid> budgetLineIds,
        CancellationToken cancellationToken);

    /// <summary>One more than the highest sort order of the project's activities (0 for the first).</summary>
    Task<int> NextSortOrderAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The activity (and its project) an earlier create with this request id made.</summary>
    Task<ScheduleRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(ScheduleActivity activity);
}

public sealed record ScheduleRequestOwner(Guid ActivityId, Guid ProjectId);

/// <summary>Reads the schedule straight into rows, a fixed number of statements however many activities, lines and entries there are.</summary>
public interface IScheduleReader
{
    /// <summary>The project's active activities in list order, each with the ids of its linked budget lines. One statement.</summary>
    Task<IReadOnlyList<ScheduleActivityRow>> ListAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The latest progress entry of each budget line of the project within each step of the curve, one statement. Step k holds the entries dated
    /// after point k-1 and on or before point k (step 0 holds everything on or before the first point). Entries after <paramref name="through"/>
    /// are left out.
    /// </summary>
    Task<IReadOnlyList<ProgressStepRow>> ProgressStepsAsync(Guid projectId, CurveSteps steps, DateOnly through, CancellationToken cancellationToken);

    /// <summary>The posted cost of the project in each step of the curve, net and gross, one statement. Cost after <paramref name="through"/> is left out.</summary>
    Task<IReadOnlyList<CostStepRow>> CostStepsAsync(Guid projectId, CurveSteps steps, DateOnly through, CancellationToken cancellationToken);
}

/// <summary>An active activity as the reader returns it.</summary>
public sealed record ScheduleActivityRow(Guid Id, string Name, DateOnly PlannedStart, DateOnly PlannedFinish, DateOnly? BaselineStart, DateOnly? BaselineFinish,
    DateTimeOffset? BaselineSetAtUtc, int SortOrder, int Version, IReadOnlyList<Guid> BudgetLineIds);

/// <summary>The points of the curve: <see cref="First"/>, then every <see cref="StepDays"/> days, <see cref="Count"/> points in all.</summary>
public sealed record CurveSteps(DateOnly First, int StepDays, int Count)
{
    public DateOnly Point(int index) => First.AddDays(index * StepDays);

    public DateOnly Last => Point(Count - 1);
}

/// <param name="Step">The step the entry belongs to (see <see cref="IScheduleReader.ProgressStepsAsync"/>); past the last point it is <c>Count</c>.</param>
public sealed record ProgressStepRow(Guid BudgetLineId, int Step, int PercentBps);

public sealed record CostStepRow(int Step, long NetMinor, long GrossMinor);

// ---- Read -------------------------------------------------------------------------------------------------------------------

/// <summary>
/// A project's schedule on today's date. Amounts are on the company's comparison basis, the same line amounts the Progress tab uses; only people
/// who read the Progress tab can read this.
/// </summary>
/// <param name="PlannedMinor">The schedule's planned value today: what the activities should have earned by the end of today.</param>
/// <param name="BaselinePlannedMinor">The same from the baseline dates; null until a baseline is set.</param>
/// <param name="PlanWeightMinor">The budget the activities cover (the sum of their weights): the planned value reaches it at the last finish.</param>
/// <param name="BudgetMinor">The whole budget of the project (every active line).</param>
/// <param name="UnplannedLineCount">Active budget lines not in any activity; their budget has no planned value.</param>
/// <param name="UsesSchedule">True when at least one activity has weight: the Progress tab's planned value then comes from this schedule.</param>
/// <param name="BaselineSetAtUtc">When the baseline was last set; null before.</param>
public sealed record ScheduleDto(Guid ProjectId, DateOnly Today, string Basis, bool ProjectArchived, bool UsesSchedule, long PlannedMinor,
    long? BaselinePlannedMinor, long PlanWeightMinor, long BudgetMinor, long UnplannedBudgetMinor, int UnplannedLineCount, DateTimeOffset? BaselineSetAtUtc,
    IReadOnlyList<ScheduleActivityDto> Activities, IReadOnlyList<ScheduleBudgetLineDto> BudgetLines);

/// <param name="PlannedDays">Both ends counted.</param>
/// <param name="SlipDays">Planned finish minus baseline finish (positive: later than the baseline); null without a baseline.</param>
/// <param name="WeightMinor">The linked lines' budget.</param>
/// <param name="PercentCompleteBps">The budget-weighted percent of the linked lines; null with none linked.</param>
/// <param name="IsBehind">The planned finish is before today and it is below 100%.</param>
/// <param name="NeedsLines">No budget line is linked: it is listed but counts for nothing in the plan.</param>
/// <param name="BudgetLineIds">The linked active budget lines.</param>
public sealed record ScheduleActivityDto(Guid Id, string Name, DateOnly PlannedStart, DateOnly PlannedFinish, int PlannedDays, DateOnly? BaselineStart,
    DateOnly? BaselineFinish, int? SlipDays, int SortOrder, long WeightMinor, int? PercentCompleteBps, bool IsBehind, bool NeedsLines,
    IReadOnlyList<Guid> BudgetLineIds, int Version);

/// <summary>An active budget line, for the line picker (grouped by division) and to name an activity's lines.</summary>
/// <param name="ActivityId">The activity that links it, or null.</param>
public sealed record ScheduleBudgetLineDto(Guid Id, string Description, string? WbsCode, int? Division, string DivisionName, long BudgetMinor, int PercentBps,
    Guid? ActivityId);

/// <summary>
/// The S-curve: cumulative planned value, baseline planned value, earned value and actual cost at weekly points from the first activity start to
/// the last finish (at most 120 points; longer plans use wider steps), plus today when it falls between two points. Earned value and actual cost
/// stop at today. Amounts are on the company's comparison basis, from the same line amounts as the Progress tab, so the point for today equals
/// the Progress tab's planned value, earned value and actual cost.
/// </summary>
/// <param name="StepDays">The days between points: 7, or a multiple of 7 for a long plan.</param>
public sealed record ScheduleCurveDto(Guid ProjectId, DateOnly Today, string Basis, bool HasBaseline, int StepDays, IReadOnlyList<ScheduleCurvePointDto> Points);

/// <param name="IsToday">The point for today (between two weekly points, or a weekly point that falls on today).</param>
/// <param name="BaselinePlannedMinor">Null until a baseline is set.</param>
/// <param name="EarnedMinor">Null after today.</param>
/// <param name="ActualMinor">Posted cost; null after today.</param>
public sealed record ScheduleCurvePointDto(DateOnly Date, bool IsToday, long PlannedMinor, long? BaselinePlannedMinor, long? EarnedMinor, long? ActualMinor);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <param name="BudgetLineIds">The budget lines whose work the activity covers. A line already in another activity is refused.</param>
public sealed record CreateScheduleActivityRequest(string? Name, DateOnly? PlannedStart, DateOnly? PlannedFinish, IReadOnlyList<Guid>? BudgetLineIds,
    string? ClientRequestId);

/// <param name="SortOrder">A new position in the list; null keeps it.</param>
public sealed record UpdateScheduleActivityRequest(string? Name, DateOnly? PlannedStart, DateOnly? PlannedFinish, IReadOnlyList<Guid>? BudgetLineIds,
    int? SortOrder, int Version);

public sealed record RemoveScheduleActivityRequest(string? Reason, int Version);

/// <summary>The activities the person saw when they chose to set the baseline. A schedule changed since then is refused (409).</summary>
public sealed record SetScheduleBaselineRequest(IReadOnlyList<ScheduleActivityVersion>? Activities);

public sealed record ScheduleActivityVersion(Guid Id, int Version);
