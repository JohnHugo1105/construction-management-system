using Cms.Application.Features.Costing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Progress;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Progress;

/// <summary>
/// Turns the rows the reader returned into the progress report: each line's earned value, the roll-ups by division, and the project's earned
/// value on the company's comparison basis. Nothing here reads the database, so every figure comes from one calculation however it is
/// shown.
/// </summary>
public static class ProgressReportBuilder
{
    public static ProgressDto Build(Project project, VarianceBasis basis, DateOnly asOf, IReadOnlyList<ProgressLineRow> rows, MeasuredAmounts actual)
    {
        var lines = rows.Select(row =>
        {
            var budget = BudgetOn(row, basis);
            var percent = row.PercentBps ?? 0;
            return new ProgressLineDto(row.BudgetLineId, row.Description, row.WbsCode, budget, percent, row.LastUpdatedOn,
                EarnedValueCalculator.LineEarnedValue(budget, percent));
        }).ToArray();

        var divisions = rows.Zip(lines)
            .GroupBy(pair => pair.First.Division)
            .OrderBy(group => group.Key is null).ThenBy(group => group.Key)
            .Select(group => ToDivision(group.Key, group.Select(pair => pair.Second).ToArray()))
            .ToArray();

        var planned = PlannedProgress.From(project.StartDate, project.TargetEndDate, asOf);
        var budgetTotal = lines.Sum(line => line.BudgetMinor);
        var earnedTotal = lines.Sum(line => line.EarnedMinor);
        var figures = EarnedValueCalculator.Calculate(budgetTotal, earnedTotal, actual.On(basis), planned);

        // The contract price includes VAT, so the margin is worked out on amounts with VAT whatever basis the screen compares on.
        var grossFigures = basis == VarianceBasis.IncludingVat
            ? figures
            : EarnedValueCalculator.Calculate(
                rows.Sum(row => row.GrossMinor),
                rows.Sum(row => EarnedValueCalculator.LineEarnedValue(row.GrossMinor, row.PercentBps ?? 0)),
                actual.GrossMinor, planned);
        var margin = EarnedValueCalculator.ForecastMarginMinor(project.ContractPriceMinor, grossFigures.EstimateAtCompletionMinor);

        var lastUpdated = rows.Max(row => row.LastUpdatedOn);
        return new ProgressDto(project.Id, asOf, basis.ToString(), lastUpdated is not null, lastUpdated,
            new EarnedValueDto(figures.BudgetAtCompletionMinor, figures.EarnedMinor, figures.ActualMinor, figures.PlannedMinor, figures.CostVarianceMinor,
                figures.ScheduleVarianceMinor, figures.CostIndexBps, figures.ScheduleIndexBps, figures.EstimateAtCompletionMinor,
                figures.EstimateToCompleteMinor, figures.VarianceAtCompletionMinor, figures.PercentCompleteBps, figures.PlannedPercentBps,
                project.ContractPriceMinor, margin),
            divisions);
    }

    private static ProgressDivisionDto ToDivision(int? division, IReadOnlyList<ProgressLineDto> lines)
    {
        var budget = lines.Sum(line => line.BudgetMinor);
        var earned = lines.Sum(line => line.EarnedMinor);
        var name = division is null ? CostReportBuilder.UnclassifiedName : WbsCatalog.MainScopeOf(division.Value) ?? $"Division {division}";
        return new ProgressDivisionDto(division, name, budget, earned, budget == 0 ? null : Share(earned, budget), lines);
    }

    private static long BudgetOn(ProgressLineRow row, VarianceBasis basis) => basis == VarianceBasis.ExcludingVat ? row.NetMinor : row.GrossMinor;

    private static int Share(long earned, long budget) =>
        (int)Math.Min(decimal.Round(earned * 10_000m / budget, 0, MidpointRounding.AwayFromZero), int.MaxValue);
}
