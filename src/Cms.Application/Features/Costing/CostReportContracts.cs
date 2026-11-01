using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;

namespace Cms.Application.Features.Costing;

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

public sealed record ProjectHeader(Guid Id, string Name, string Status, long? ContractPriceMinor);

public sealed record ProjectAmounts(Guid ProjectId, long NetMinor, long GrossMinor);

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
public sealed record DashboardProjectRowDto(Guid Id, string Name, string Status, long? ContractPriceMinor, long BudgetMinor, long ActualMinor,
    long RemainingMinor, int? UsedBps, string Health, long? MarginAfterActualMinor, long? BudgetMarginMinor);

/// <summary>
/// The overall row. Budget, actual and remaining add up every project in the list. The contract price and the two margins add up only the
/// projects that have a contract price (null when none has), so a project with no price does not distort the margin.
/// </summary>
public sealed record DashboardTotalsDto(int ProjectCount, long? ContractPriceMinor, long BudgetMinor, long ActualMinor, long RemainingMinor,
    int? UsedBps, string Health, long? MarginAfterActualMinor, long? BudgetMarginMinor);

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
/// A project's position plus where the money goes. Inside each breakdown the rows run from the biggest overrun to the most money left, so
/// problems come first. A row with actual cost but no budget is Unbudgeted.
/// </summary>
public sealed record CostSummaryDto(Guid ProjectId, string ProjectName, string ProjectStatus, string Basis, long? ContractPriceMinor,
    BudgetPositionDto Position, long? MarginAfterActualMinor, long? BudgetMarginMinor, IReadOnlyList<DivisionSummaryDto> ByDivision,
    IReadOnlyList<SubProjectSummaryDto> BySubProject, IReadOnlyList<ItemTypeSummaryDto> ByItemType);
