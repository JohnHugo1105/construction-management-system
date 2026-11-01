using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.Features.Payroll;

public sealed record AttendanceFact(Guid EntryId, Guid WorkerId, Guid ProjectId, Guid? SubProjectId, DateOnly WorkDate,
    decimal RegularHours, decimal OvertimeHours);

public sealed record CalculatedAllocation(Guid ProjectId, Guid? SubProjectId, decimal RegularHours, decimal OvertimeHours, long GrossMinor);

public sealed record CalculatedPayLine(Guid WorkerId, string WorkerName, string EmployeeNumber, string Designation, decimal RegularHours,
    decimal OvertimeHours, long RegularPayMinor, long OvertimePayMinor, IReadOnlyList<CalculatedAllocation> Allocations,
    IReadOnlyList<Guid> EntryIds)
{
    public long GrossMinor => RegularPayMinor + OvertimePayMinor;
}

/// <summary>
/// Turns attendance into pay. The rules, in full:
/// <list type="bullet">
/// <item>Each attendance entry is paid at the rate in force on its work date, so a raise part-way through a period applies from its date.</item>
/// <item>A daily rate is divided by the company's paid hours per day to give an hourly rate; an hourly rate is used as is.</item>
/// <item>Regular pay = hourly rate x regular hours. Overtime pay = hourly rate x overtime hours x overtime multiplier.</item>
/// <item>Each figure is rounded once, per entry, half away from zero. A worker's pay and every project's share are sums of those same rounded figures, so the parts always add up to the whole.</item>
/// </list>
/// Holiday and rest-day premiums and statutory contributions (SSS, PhilHealth, Pag-IBIG, withholding tax) are deliberately not
/// computed: they change by law and by company policy. They are entered as deductions, or as extra hours, by the HR officer.
/// </summary>
public static class PayrollCalculator
{
    public static IReadOnlyList<CalculatedPayLine> Calculate(IEnumerable<AttendanceFact> facts, IReadOnlyDictionary<Guid, Worker> workers,
        decimal hoursPerDay, int overtimeMultiplierBps)
    {
        if (hoursPerDay <= 0m) throw new ArgumentOutOfRangeException(nameof(hoursPerDay));
        if (overtimeMultiplierBps < 10_000) throw new ArgumentOutOfRangeException(nameof(overtimeMultiplierBps));

        var lines = new List<CalculatedPayLine>();
        foreach (var group in facts.GroupBy(fact => fact.WorkerId).OrderBy(group => workers[group.Key].LastName).ThenBy(group => workers[group.Key].FirstName))
        {
            var worker = workers[group.Key];
            long regularTotal = 0, overtimeTotal = 0;
            decimal regularHours = 0m, overtimeHours = 0m;
            var perProject = new Dictionary<(Guid, Guid?), (decimal Regular, decimal Overtime, long Gross)>();

            foreach (var fact in group.OrderBy(item => item.WorkDate))
            {
                var rate = worker.RateOn(fact.WorkDate) ??
                    throw new DomainRuleException($"{worker.FullName} has no pay rate on {fact.WorkDate:d MMM yyyy}. Check the hire date and rate history.");
                var hourly = rate.Unit == RateUnit.Daily ? (decimal)rate.RateMinor / hoursPerDay : rate.RateMinor;
                var regularPay = RoundToMinor(hourly * fact.RegularHours);
                var overtimePay = RoundToMinor(hourly * fact.OvertimeHours * overtimeMultiplierBps / 10_000m);

                regularTotal = checked(regularTotal + regularPay);
                overtimeTotal = checked(overtimeTotal + overtimePay);
                regularHours += fact.RegularHours;
                overtimeHours += fact.OvertimeHours;

                var key = (fact.ProjectId, fact.SubProjectId);
                perProject.TryGetValue(key, out var existing);
                perProject[key] = (existing.Regular + fact.RegularHours, existing.Overtime + fact.OvertimeHours,
                    checked(existing.Gross + regularPay + overtimePay));
            }

            var allocations = perProject
                .Select(pair => new CalculatedAllocation(pair.Key.Item1, pair.Key.Item2, pair.Value.Regular, pair.Value.Overtime, pair.Value.Gross))
                .OrderBy(allocation => allocation.ProjectId).ThenBy(allocation => allocation.SubProjectId)
                .ToArray();
            var lastWorkDate = group.Max(item => item.WorkDate);
            lines.Add(new CalculatedPayLine(worker.Id, worker.FullName, worker.EmployeeNumber,
                (worker.RateOn(lastWorkDate) ?? worker.CurrentRate).Designation, regularHours, overtimeHours, regularTotal, overtimeTotal,
                allocations, group.Select(item => item.EntryId).ToArray()));
        }

        return lines;
    }

    private static long RoundToMinor(decimal value) => checked((long)decimal.Round(value, 0, MidpointRounding.AwayFromZero));
}
