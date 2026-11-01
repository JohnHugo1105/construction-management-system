using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;

namespace Cms.Application.Features.Costing;

/// <summary>
/// Turns the grouped sums into the dashboard and the project cost summary. Pure functions: every figure is derived here, once, from
/// <see cref="BudgetPosition"/> and the company's comparison basis, so the dashboard and a project page can never disagree.
/// </summary>
public static class CostReportBuilder
{
    public const string UnclassifiedName = "Unclassified";
    public const string WholeProjectName = "Whole project";

    // ---- Dashboard ----------------------------------------------------------------------------------------------------------

    public static DashboardDto BuildDashboard(VarianceBasis basis, IReadOnlyList<ProjectHeader> projects, IReadOnlyList<ProjectAmounts> budgets,
        IReadOnlyList<ProjectAmounts> actuals)
    {
        var budgetByProject = budgets.ToDictionary(item => item.ProjectId, item => new MeasuredAmounts(item.NetMinor, item.GrossMinor));
        var actualByProject = actuals.ToDictionary(item => item.ProjectId, item => new MeasuredAmounts(item.NetMinor, item.GrossMinor));

        var rows = new List<DashboardProjectRowDto>(projects.Count);
        long totalBudget = 0, totalActual = 0;
        long? totalPrice = null, totalMargin = null, totalBudgetMargin = null;

        foreach (var project in projects)
        {
            var budget = budgetByProject.GetValueOrDefault(project.Id);
            var actual = actualByProject.GetValueOrDefault(project.Id);
            var position = new BudgetPosition(budget.On(basis), actual.On(basis));
            var margin = Margin(project.ContractPriceMinor, actual.GrossMinor);
            var budgetMargin = Margin(project.ContractPriceMinor, budget.GrossMinor);

            rows.Add(new DashboardProjectRowDto(project.Id, project.Name, project.Status, project.ContractPriceMinor, position.BudgetMinor,
                position.ActualMinor, position.RemainingMinor, position.UsedBps, position.Health.ToString(), margin, budgetMargin));

            totalBudget = checked(totalBudget + position.BudgetMinor);
            totalActual = checked(totalActual + position.ActualMinor);
            totalPrice = AddOptional(totalPrice, project.ContractPriceMinor);
            totalMargin = AddOptional(totalMargin, margin);
            totalBudgetMargin = AddOptional(totalBudgetMargin, budgetMargin);
        }

        var overall = new BudgetPosition(totalBudget, totalActual);
        var totals = new DashboardTotalsDto(rows.Count, totalPrice, overall.BudgetMinor, overall.ActualMinor, overall.RemainingMinor,
            overall.UsedBps, overall.Health.ToString(), totalMargin, totalBudgetMargin);
        return new DashboardDto(basis.ToString(), rows, totals);
    }

    // ---- Project cost summary -----------------------------------------------------------------------------------------------

    /// <param name="subProjectNames">Every sub-project of the project by id, so rows carry names.</param>
    public static CostSummaryDto BuildSummary(ProjectHeader project, IReadOnlyDictionary<Guid, string> subProjectNames, VarianceBasis basis,
        GroupedSums budget, GroupedSums actual)
    {
        // Every line has exactly one item type, so the item-type groups add up to the whole project.
        var budgetTotal = Total(budget.ByItemType);
        var actualTotal = Total(actual.ByItemType);
        var position = new BudgetPosition(budgetTotal.On(basis), actualTotal.On(basis));

        var byItemType = Merge(budget.ByItemType, actual.ByItemType, group => group.ItemType ?? ItemType.Other)
            .Select(item =>
            {
                var itemPosition = PositionOf(item.Budget, item.Actual, basis);
                return new ItemTypeSummaryDto(item.Key.ToString(), CostingLabels.ItemTypeLabel(item.Key), itemPosition.BudgetMinor,
                    itemPosition.ActualMinor, itemPosition.RemainingMinor, itemPosition.UsedBps, itemPosition.Health.ToString());
            })
            .OrderBy(row => row.RemainingMinor).ThenBy(row => row.ItemType, StringComparer.Ordinal)
            .ToArray();

        var bySubProject = Merge(budget.BySubProject, actual.BySubProject, group => group.SubProjectId ?? Guid.Empty)
            .Select(item =>
            {
                var subPosition = PositionOf(item.Budget, item.Actual, basis);
                var subProjectId = item.Key == Guid.Empty ? (Guid?)null : item.Key;
                var name = subProjectId is null ? WholeProjectName : subProjectNames.GetValueOrDefault(item.Key, "Sub-project");
                return new SubProjectSummaryDto(subProjectId, name, subPosition.BudgetMinor, subPosition.ActualMinor, subPosition.RemainingMinor,
                    subPosition.UsedBps, subPosition.Health.ToString());
            })
            .OrderBy(row => row.RemainingMinor).ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();

        return new CostSummaryDto(project.Id, project.Name, project.Status, basis.ToString(), project.ContractPriceMinor,
            new BudgetPositionDto(position.BudgetMinor, position.ActualMinor, position.RemainingMinor, position.UsedBps, position.Health.ToString()),
            Margin(project.ContractPriceMinor, actualTotal.GrossMinor), Margin(project.ContractPriceMinor, budgetTotal.GrossMinor),
            BuildDivisions(budget, actual, basis), bySubProject, byItemType);
    }

    private static DivisionSummaryDto[] BuildDivisions(GroupedSums budget, GroupedSums actual, VarianceBasis basis)
    {
        // Lines with no division are one "Unclassified" bucket whatever else they carry.
        var codes = Merge(budget.ByWbs, actual.ByWbs, group => (Division: group.Division ?? 0, Code: group.Division is null ? "" : group.WbsCode ?? ""));

        return codes
            .GroupBy(item => item.Key.Division)
            .Select(division =>
            {
                var number = division.Key == 0 ? (int?)null : division.Key;
                var divisionPosition = PositionOf(Sum(division.Select(item => item.Budget)), Sum(division.Select(item => item.Actual)), basis);
                var wbsRows = number is null
                    ? []
                    : division.Select(item => WbsRow(item.Key.Code, item.First, item.Budget, item.Actual, basis))
                        .OrderBy(row => row.RemainingMinor).ThenBy(row => row.WbsCode, StringComparer.Ordinal)
                        .ToArray();
                var name = number is null ? UnclassifiedName : WbsCatalog.MainScopeOf(number.Value) ?? $"Division {number}";
                return new DivisionSummaryDto(number, name, divisionPosition.BudgetMinor, divisionPosition.ActualMinor, divisionPosition.RemainingMinor,
                    divisionPosition.UsedBps, divisionPosition.Health.ToString(), wbsRows);
            })
            .OrderBy(row => row.RemainingMinor).ThenBy(row => row.Division ?? int.MaxValue)
            .ToArray();
    }

    private static WbsSummaryDto WbsRow(string code, GroupedAmounts first, MeasuredAmounts budget, MeasuredAmounts actual, VarianceBasis basis)
    {
        var position = PositionOf(budget, actual, basis);
        var name = WbsCatalog.FindByCode(code)?.SubScope ?? first.SubScope ?? "Other work";
        return new WbsSummaryDto(code.Length == 0 ? null : code, name, position.BudgetMinor, position.ActualMinor, position.RemainingMinor,
            position.UsedBps, position.Health.ToString());
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------------------

    private static BudgetPosition PositionOf(MeasuredAmounts budget, MeasuredAmounts actual, VarianceBasis basis) =>
        new(budget.On(basis), actual.On(basis));

    private static MeasuredAmounts Total(IEnumerable<GroupedAmounts> groups) =>
        Sum(groups.Select(group => new MeasuredAmounts(group.NetMinor, group.GrossMinor)));

    private static MeasuredAmounts Sum(IEnumerable<MeasuredAmounts> amounts) =>
        amounts.Aggregate(default(MeasuredAmounts), (total, next) => total + next);

    private static long? Margin(long? contractPriceMinor, long grossMinor) => contractPriceMinor is { } price ? checked(price - grossMinor) : null;

    private static long? AddOptional(long? total, long? value) => value is null ? total : checked((total ?? 0) + value.Value);

    /// <summary>Joins the budget groups and the actual groups on their key. A key present on one side only gets zero on the other.</summary>
    private static List<Merged<TKey>> Merge<TKey>(IEnumerable<GroupedAmounts> budget, IEnumerable<GroupedAmounts> actual,
        Func<GroupedAmounts, TKey> key) where TKey : notnull
    {
        var merged = new Dictionary<TKey, Merged<TKey>>();
        foreach (var group in budget)
        {
            var current = merged.GetValueOrDefault(key(group)) ?? new Merged<TKey>(key(group), group);
            current.Budget += new MeasuredAmounts(group.NetMinor, group.GrossMinor);
            merged[current.Key] = current;
        }

        foreach (var group in actual)
        {
            var current = merged.GetValueOrDefault(key(group)) ?? new Merged<TKey>(key(group), group);
            current.Actual += new MeasuredAmounts(group.NetMinor, group.GrossMinor);
            merged[current.Key] = current;
        }

        return [.. merged.Values];
    }

    private sealed class Merged<TKey>(TKey key, GroupedAmounts first)
    {
        public TKey Key { get; } = key;

        /// <summary>The first group seen for the key, which carries the names (for example a work-breakdown sub-scope).</summary>
        public GroupedAmounts First { get; } = first;

        public MeasuredAmounts Budget { get; set; }
        public MeasuredAmounts Actual { get; set; }
    }
}
