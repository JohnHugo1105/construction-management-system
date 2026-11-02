using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Schedule;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;

namespace ConstructOPX.Application.Features.Progress;

/// <summary>
/// Turns the rows the reader returned into the progress report: each line's earned value, the roll-ups by division, and the project's earned
/// value on the company's comparison basis. Nothing here reads the database, so every figure comes from one calculation however it is
/// shown.
/// <para>
/// The planned value follows the project's schedule when it has one: when at least one activity has budget lines linked (weight above zero),
/// the planned value is the schedule's (each activity's weight times the share of its planned days gone, see <see cref="SchedulePlan"/>), and the
/// schedule index and variance are worked out from it. Otherwise it stays the straight line over the project's start and target end dates.
/// Forecasts use the cost index and never the planned value, so a schedule changes none of them.
/// </para>
/// </summary>
public static class ProgressReportBuilder
{
    public const string PlannedFromSchedule = "Schedule";
    public const string PlannedFromProjectDates = "ProjectDates";

    /// <param name="schedule">The project's active schedule activities; null or empty keeps the straight-line planned value.</param>
    public static ProgressDto Build(Project project, VarianceBasis basis, DateOnly asOf, IReadOnlyList<ProgressLineRow> rows, MeasuredAmounts actual,
        IReadOnlyList<ScheduleActivityRow>? schedule = null)
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
        var plannedFrom = planned is null ? null : PlannedFromProjectDates;
        if (schedule is { Count: > 0 } && ScheduleWeights.From(schedule, rows, basis) is { UsesSchedule: true } weights)
        {
            figures = WithSchedule(figures, SchedulePlan.PlannedValue(weights.Planned, asOf));
            plannedFrom = PlannedFromSchedule;
        }

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
            divisions, plannedFrom);
    }

    /// <summary>
    /// The figures with the schedule's planned value in place of the straight line: the schedule variance is earned minus planned, the schedule
    /// index earned over planned (none while nothing is planned yet) and the planned percent the planned value as a share of the budget. Every
    /// other figure (the forecasts included) does not use the planned value and stays as calculated.
    /// </summary>
    private static EarnedValueFigures WithSchedule(EarnedValueFigures figures, long plannedMinor) => figures with
    {
        PlannedMinor = plannedMinor,
        ScheduleVarianceMinor = figures.EarnedMinor - plannedMinor,
        ScheduleIndexBps = plannedMinor > 0 ? Share(figures.EarnedMinor, plannedMinor) : null,
        PlannedPercentBps = figures.BudgetAtCompletionMinor > 0 ? Share(plannedMinor, figures.BudgetAtCompletionMinor) : null
    };

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
