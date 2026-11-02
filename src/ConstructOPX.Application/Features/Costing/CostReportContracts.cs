using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;

namespace ConstructOPX.Application.Features.Costing;

// ---- Port -------------------------------------------------------------------------------------------------------------------

public enum CostMeasure
{
    /// <summary>Active budget lines (removed lines never count).</summary>
    Budget = 0,

    /// <summary>Posted cost entries (voided entries never count).</summary>
    Actual = 1
}

/// <summary>
/// Reads the sums the reports are built from. Each call is a grouped SQL statement: the statement count does not grow with the number
/// of projects or lines. Both net and gross are returned so the service can apply the company's comparison basis.
/// </summary>
public interface ICostReportReader
{
    /// <summary>Every project (archived ones only when asked), in name order.</summary>
    Task<IReadOnlyList<ProjectHeader>> ListProjectsAsync(bool includeArchived, CancellationToken cancellationToken);

    /// <summary>One sum per project that has any lines of the measure.</summary>
    Task<IReadOnlyList<ProjectAmounts>> SumByProjectAsync(CostMeasure measure, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>The sums of one project, grouped by work breakdown, sub-project and item type.</summary>
    Task<GroupedSums> SumProjectAsync(Guid projectId, CostMeasure measure, CancellationToken cancellationToken);
}

/// <summary>
/// A source of money the company has promised to spend but has not yet been invoiced for or certified: open purchase orders, subcontracts
/// not yet claimed. Reports add every source's remainder to the actual cost to give the forecast. A source never counts what has already
/// become actual cost, so nothing is counted twice. Implementations are classes named <c>*CommitmentSource</c> in Infrastructure.
/// </summary>
public interface ICommitmentSource
{
    /// <summary>
    /// The open remainder per project, net and gross. <paramref name="projectId"/> narrows it to one project; otherwise every project that
    /// is visible (archived ones only when asked). A project with nothing open is simply absent.
    /// </summary>
    Task<IReadOnlyList<ProjectAmounts>> OpenCommitmentsAsync(Guid? projectId, bool includeArchived, CancellationToken cancellationToken);
}

/// <summary>
/// What the earned-value forecast needs of each project, read the same way the Progress tab reads it, so the dashboard and the project
/// pages show one figure. Implementations are classes named <c>*ForecastSource</c> in Infrastructure.
/// </summary>
public interface IProgressForecastSource
{
    /// <summary>
    /// For every project that has progress recorded (archived ones only when asked, or just <paramref name="projectId"/>): the value of the
    /// work done and the posted cost, both as they stood on <paramref name="asOf"/>. A line's work is worth its budget times the percent of
    /// its latest entry on or before the date; removed lines never count. A project with no progress, or none above 0%, is simply absent.
    /// The statement count does not grow with the number of projects, lines or entries.
    /// </summary>
    Task<IReadOnlyList<ProgressForecastInputs>> ForecastInputsAsync(Guid? projectId, bool includeArchived, DateOnly asOf, CancellationToken cancellationToken);
}

public sealed record ProjectHeader(Guid Id, string Name, string Status, long? ContractPriceMinor);

public sealed record ProjectAmounts(Guid ProjectId, long NetMinor, long GrossMinor);

/// <param name="Earned">What the work done is worth: the sum over lines of budget x percent complete, each line rounded once.</param>
/// <param name="Actual">Posted cost dated on or before the date the inputs are for: the Progress tab's "spent".</param>
public sealed record ProgressForecastInputs(Guid ProjectId, MeasuredAmounts Earned, MeasuredAmounts Actual);

/// <summary>One group of a project's sums. Only the key fields of the grouping it came from are filled in.</summary>
/// <param name="Division">Work breakdown grouping: the division, or null for lines with no classification.</param>
/// <param name="WbsCode">Work breakdown grouping: the code.</param>
/// <param name="SubScope">Work breakdown grouping: the name stored on the lines of the code.</param>
/// <param name="SubProjectId">Sub-project grouping: the sub-project, or null for the whole project.</param>
/// <param name="ItemType">Item type grouping.</param>
public sealed record GroupedAmounts(int? Division, string? WbsCode, string? SubScope, Guid? SubProjectId, ItemType? ItemType, long NetMinor,
    long GrossMinor);

public sealed record GroupedSums(IReadOnlyList<GroupedAmounts> ByWbs, IReadOnlyList<GroupedAmounts> BySubProject, IReadOnlyList<GroupedAmounts> ByItemType)
{
    public static GroupedSums Empty { get; } = new([], [], []);
}

/// <summary>Net and gross sums of one slice. The comparison basis (<see cref="VarianceBasis"/>) picks which one a report shows.</summary>
public readonly record struct MeasuredAmounts(long NetMinor, long GrossMinor)
{
    public long On(VarianceBasis basis) => basis == VarianceBasis.ExcludingVat ? NetMinor : GrossMinor;

    public static MeasuredAmounts operator +(MeasuredAmounts left, MeasuredAmounts right) =>
        new(checked(left.NetMinor + right.NetMinor), checked(left.GrossMinor + right.GrossMinor));
}

// ---- Dashboard --------------------------------------------------------------------------------------------------------------

/// <summary>
/// One project's budget against actual cost, both on the company's comparison basis. The margin fields always use VAT-inclusive (gross)
/// amounts, because the contract price includes VAT. They are null when the project has no contract price.
/// </summary>
/// <param name="RemainingMinor">Budget minus actual; negative means over budget.</param>
/// <param name="UsedBps">Actual as a share of budget in basis points (10,000 is 100%); null when nothing is budgeted.</param>
/// <param name="Health">OnTrack, NearLimit, OverBudget, Unbudgeted or NoActivity.</param>
/// <param name="MarginAfterActualMinor">Contract price minus actual gross cost so far.</param>
/// <param name="BudgetMarginMinor">Contract price minus budgeted gross cost: the margin if the project is delivered on budget.</param>
/// <param name="Commitments">What is promised but not yet spent, and the forecast it implies. Null when nothing is open, so a project without
/// purchase orders or subcontracts shows no empty figures.</param>
/// <param name="ProgressForecast">Where the work done says the project will finish. Null until progress is recorded, so a project nobody has
/// measured shows nothing. A separate signal from <c>Health</c>, which only compares money spent with the budget.</param>
public sealed record DashboardProjectRowDto(Guid Id, string Name, string Status, long? ContractPriceMinor, long BudgetMinor, long ActualMinor,
    long RemainingMinor, int? UsedBps, string Health, long? MarginAfterActualMinor, long? BudgetMarginMinor,
    CommitmentPositionDto? Commitments = null, ProgressForecastDto? ProgressForecast = null);

/// <summary>
/// The overall row. Budget, actual and remaining add up every project in the list. The contract price and the two margins add up only the
/// projects that have a contract price (null when none has), so a project with no price does not distort the margin.
/// </summary>
/// <param name="ForecastOverBudgetCount">How many of the listed projects are forecast to finish over budget (their row has a progress forecast
/// with a positive <c>OverBudgetMinor</c>); 0 when none is.</param>
public sealed record DashboardTotalsDto(int ProjectCount, long? ContractPriceMinor, long BudgetMinor, long ActualMinor, long RemainingMinor,
    int? UsedBps, string Health, long? MarginAfterActualMinor, long? BudgetMarginMinor, CommitmentPositionDto? Commitments = null,
    int ForecastOverBudgetCount = 0);

/// <param name="Basis">IncludingVat or ExcludingVat: which amounts budget and actual are compared on.</param>
public sealed record DashboardDto(string Basis, IReadOnlyList<DashboardProjectRowDto> Projects, DashboardTotalsDto Totals);

// ---- Project cost summary ---------------------------------------------------------------------------------------------------

public sealed record WbsSummaryDto(string? WbsCode, string Name, long BudgetMinor, long ActualMinor, long RemainingMinor, int? UsedBps, string Health);

/// <param name="Division">The division number, or null for lines with no classification (named "Unclassified").</param>
public sealed record DivisionSummaryDto(int? Division, string Name, long BudgetMinor, long ActualMinor, long RemainingMinor, int? UsedBps,
    string Health, IReadOnlyList<WbsSummaryDto> Wbs);

/// <param name="SubProjectId">The sub-project, or null for budget and cost that belong to the whole project.</param>
public sealed record SubProjectSummaryDto(Guid? SubProjectId, string Name, long BudgetMinor, long ActualMinor, long RemainingMinor, int? UsedBps,
    string Health);

public sealed record ItemTypeSummaryDto(string ItemType, string Label, long BudgetMinor, long ActualMinor, long RemainingMinor, int? UsedBps,
    string Health);

public sealed record BudgetPositionDto(long BudgetMinor, long ActualMinor, long RemainingMinor, int? UsedBps, string Health);

/// <summary>
/// Money promised but not yet invoiced or certified, and the forecast it gives: <c>Forecast = actual + committed</c>, on the same basis as
/// the budget. <c>ForecastHealth</c> judges the forecast against the budget the way <c>Health</c> judges actual cost, so a project can read
/// "on track" today and "forecast over budget" once its orders are counted.
/// </summary>
public sealed record CommitmentPositionDto(long CommittedMinor, long ForecastMinor, long ForecastRemainingMinor, string ForecastHealth);

/// <summary>
/// The earned-value forecast of a project: what it is expected to cost at completion if the work so far keeps costing what it has
/// (<c>EAC = budget x spent / work done</c>), on the company's comparison basis. It is the figure the project's Progress tab shows, from the
/// same calculation. Carries no contract price or margin.
/// </summary>
/// <param name="EstimateAtCompletionMinor">The expected cost at completion.</param>
/// <param name="OverBudgetMinor">The forecast minus the budget; zero or negative means the project is forecast to finish within its budget.</param>
/// <param name="PercentCompleteBps">The work done as a share of the budget, in basis points (10,000 is 100%).</param>
public sealed record ProgressForecastDto(long EstimateAtCompletionMinor, long OverBudgetMinor, int PercentCompleteBps);

/// <summary>
/// A project's position plus where the money goes. Inside each breakdown the rows run from the biggest overrun to the most money left, so
/// problems come first. A row with actual cost but no budget is Unbudgeted.
/// </summary>
public sealed record CostSummaryDto(Guid ProjectId, string ProjectName, string ProjectStatus, string Basis, long? ContractPriceMinor,
    BudgetPositionDto Position, long? MarginAfterActualMinor, long? BudgetMarginMinor, IReadOnlyList<DivisionSummaryDto> ByDivision,
    IReadOnlyList<SubProjectSummaryDto> BySubProject, IReadOnlyList<ItemTypeSummaryDto> ByItemType, CommitmentPositionDto? Commitments = null,
    ProgressForecastDto? ProgressForecast = null);
