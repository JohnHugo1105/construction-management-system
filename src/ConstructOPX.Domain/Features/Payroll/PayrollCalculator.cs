using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Domain.Features.Payroll;

public sealed record AttendanceFact(Guid EntryId, Guid WorkerId, Guid ProjectId, Guid? SubProjectId, DateOnly WorkDate,
    decimal RegularHours, decimal OvertimeHours, decimal NightHours = 0m);

/// <summary>
/// A regular holiday a worker may be paid for without working it: the worker was posted to <paramref name="ProjectId"/> that day and has
/// no attendance on it anywhere. Whether it is paid is the calculator's decision (daily rate, active that day, not the rest day).
/// </summary>
public sealed record UnworkedHolidayFact(Guid WorkerId, DateOnly Date, Guid ProjectId, Guid? SubProjectId);

/// <summary>
/// What a payroll is worked out with: the company's paid hours a day and ordinary overtime multiple, the premium factors, and the holiday
/// calendar for the period (date to type).
/// </summary>
public sealed record PaySettings(decimal HoursPerDay, int OvertimeMultiplierBps, PayFactors Factors, IReadOnlyDictionary<DateOnly, HolidayType> Holidays)
{
    /// <summary>The default factors and an empty calendar: every day is ordinary unless a worker has a rest day.</summary>
    public static PaySettings Plain(decimal hoursPerDay, int overtimeMultiplierBps) =>
        new(hoursPerDay, overtimeMultiplierBps, PayFactors.Defaults, new Dictionary<DateOnly, HolidayType>());
}

/// <param name="BasicMinor">The plain-rate pay for the regular hours on this project (no premium), the base of the 13th-month pay.</param>
public sealed record CalculatedAllocation(Guid ProjectId, Guid? SubProjectId, decimal RegularHours, decimal OvertimeHours, long GrossMinor,
    long BasicMinor = 0);

/// <summary>A regular holiday paid without being worked: one day at the daily rate, charged to the project the worker was posted to.</summary>
public sealed record CalculatedHolidayPay(DateOnly Date, Guid ProjectId, Guid? SubProjectId, long AmountMinor);

/// <summary>
/// One worker's pay. <see cref="RegularPayMinor"/> is the plain-rate pay for the regular hours (the basic pay);
/// <see cref="PremiumPayMinor"/> is what rest days and holidays add to those hours; <see cref="OvertimePayMinor"/> includes the premium on
/// overtime worked on those days; <see cref="NightPayMinor"/> is the night differential; <see cref="HolidayPayMinor"/> is regular holidays
/// paid without being worked; <see cref="ThirteenthMonthPayMinor"/> is set only on a 13th-month payroll. The gross is their sum.
/// </summary>
public sealed record CalculatedPayLine(Guid WorkerId, string WorkerName, string EmployeeNumber, string Designation, decimal RegularHours,
    decimal OvertimeHours, long RegularPayMinor, long OvertimePayMinor, IReadOnlyList<CalculatedAllocation> Allocations,
    IReadOnlyList<Guid> EntryIds)
{
    public decimal NightHours { get; init; }
    public long PremiumPayMinor { get; init; }
    public long NightPayMinor { get; init; }
    public long HolidayPayMinor { get; init; }
    public long ThirteenthMonthPayMinor { get; init; }

    /// <summary>On a 13th-month line: the year's basic pay the amount was worked out from.</summary>
    public long ThirteenthMonthBasisMinor { get; init; }

    public IReadOnlyList<CalculatedHolidayPay> HolidayPays { get; init; } = [];

    public long GrossMinor => checked(RegularPayMinor + PremiumPayMinor + OvertimePayMinor + NightPayMinor + HolidayPayMinor + ThirteenthMonthPayMinor);
}

/// <summary>
/// Turns attendance into pay. The rules, in full:
/// <list type="bullet">
/// <item>Each attendance entry is paid at the rate in force on its work date, so a raise part-way through a period applies from its date.</item>
/// <item>A daily rate is spread over the company's paid hours per day to give the pay for the hours worked; an hourly rate is used as is.</item>
/// <item>The day type (from the holiday calendar and the worker's rest day) gives a day factor: 100% on an ordinary day.</item>
/// <item>Regular pay = hourly pay x regular hours x day factor. Its plain-rate part (factor 100%) is the basic pay; the rest is the premium.</item>
/// <item>Overtime pay = hourly pay x overtime hours x the company overtime multiple on an ordinary day, or x day factor x (100% + the
/// premium-day overtime extra) on any other day.</item>
/// <item>Night differential = hourly pay x night hours x day factor x the night rate (computed on the day's regular-hour rate, also for
/// night hours that are overtime).</item>
/// <item>A daily-rated worker posted to a project on a regular holiday, active that day, with no attendance that day and the day not their
/// rest day, is paid one day at the daily rate for it, charged to that project.</item>
/// <item>The division happens once, last, so no rounded hourly figure is ever multiplied. Each figure is rounded once, per entry, half away
/// from zero. A worker's pay and every project's share are sums of those same rounded figures, so the parts always add up to the whole.</item>
/// </list>
/// Contributions and withholding tax are worked out afterwards from the tables a person entered (<see cref="Statutory.StatutoryCalculator"/>).
/// </summary>
public static class PayrollCalculator
{
    private const decimal Bps = 10_000m;

    /// <summary>Attendance only, with the default factors and no holidays (every day is ordinary unless a worker has a rest day).</summary>
    public static IReadOnlyList<CalculatedPayLine> Calculate(IEnumerable<AttendanceFact> facts, IReadOnlyDictionary<Guid, Worker> workers,
        decimal hoursPerDay, int overtimeMultiplierBps) =>
        Calculate(facts, [], workers, PaySettings.Plain(hoursPerDay, overtimeMultiplierBps));

    public static IReadOnlyList<CalculatedPayLine> Calculate(IEnumerable<AttendanceFact> facts, IEnumerable<UnworkedHolidayFact> holidayFacts,
        IReadOnlyDictionary<Guid, Worker> workers, PaySettings settings)
    {
        if (settings.HoursPerDay <= 0m) throw new ArgumentOutOfRangeException(nameof(settings), "Paid hours a day must be greater than zero.");
        if (settings.OvertimeMultiplierBps < 10_000) throw new ArgumentOutOfRangeException(nameof(settings), "The overtime multiple is at least 100%.");

        var attendance = facts.ToList();
        var worked = attendance.Select(fact => (fact.WorkerId, fact.WorkDate)).ToHashSet();
        var holidays = holidayFacts
            .Where(fact => !worked.Contains((fact.WorkerId, fact.Date)))
            .GroupBy(fact => (fact.WorkerId, fact.Date)).Select(group => group.First())
            .ToList();

        var lines = new List<CalculatedPayLine>();
        var workerIds = attendance.Select(fact => fact.WorkerId).Concat(holidays.Select(fact => fact.WorkerId)).Distinct()
            .OrderBy(id => workers[id].LastName).ThenBy(id => workers[id].FirstName).ThenBy(id => id);
        var attendanceByWorker = attendance.ToLookup(fact => fact.WorkerId);
        var holidaysByWorker = holidays.ToLookup(fact => fact.WorkerId);
        foreach (var workerId in workerIds)
        {
            var line = CalculateWorker(workers[workerId], attendanceByWorker[workerId], holidaysByWorker[workerId], settings);
            if (line is not null) lines.Add(line);
        }

        return lines;
    }

    private sealed class Share
    {
        public decimal Regular;
        public decimal Overtime;
        public long Gross;
        public long Basic;
    }

    private static CalculatedPayLine? CalculateWorker(Worker worker, IEnumerable<AttendanceFact> facts, IEnumerable<UnworkedHolidayFact> holidayFacts,
        PaySettings settings)
    {
        long basicTotal = 0, premiumTotal = 0, overtimeTotal = 0, nightTotal = 0, holidayTotal = 0;
        decimal regularHours = 0m, overtimeHours = 0m, nightHours = 0m;
        var perProject = new Dictionary<(Guid, Guid?), Share>();
        var entryIds = new List<Guid>();
        var holidayPays = new List<CalculatedHolidayPay>();
        DateOnly? lastDate = null;

        Share ShareOf(Guid projectId, Guid? subProjectId)
        {
            if (!perProject.TryGetValue((projectId, subProjectId), out var share)) perProject[(projectId, subProjectId)] = share = new Share();
            return share;
        }

        foreach (var fact in facts.OrderBy(item => item.WorkDate))
        {
            var rate = worker.RateOn(fact.WorkDate) ??
                throw new DomainRuleException($"{worker.FullName} has no pay rate on {fact.WorkDate:d MMM yyyy}. Check the hire date and rate history.");
            var day = DayTypes.Of(fact.WorkDate, settings.Holidays.TryGetValue(fact.WorkDate, out var holiday) ? holiday : null, worker.RestDay);
            var factor = settings.Factors.DayFactorBps(day);
            // Multiply first and divide last, once: an hourly rate worked out from a daily one (1,000.00 over 7 hours) is a repeating
            // fraction, and rounding or truncating it before multiplying by the hours would shift pay by centavos.
            var divisor = rate.Unit == RateUnit.Daily ? settings.HoursPerDay : 1m;
            var basic = RoundToMinor(rate.RateMinor * fact.RegularHours / divisor);
            var premium = factor == PayFactors.OrdinaryBps
                ? 0
                : checked(RoundToMinor(rate.RateMinor * fact.RegularHours * factor / (divisor * Bps)) - basic);
            var overtime = day == DayType.Ordinary
                ? RoundToMinor(rate.RateMinor * fact.OvertimeHours * settings.OvertimeMultiplierBps / (divisor * Bps))
                : RoundToMinor(rate.RateMinor * fact.OvertimeHours * factor * (Bps + settings.Factors.PremiumDayOvertimeExtraBps) / (divisor * Bps * Bps));
            var night = fact.NightHours == 0m
                ? 0
                : RoundToMinor(rate.RateMinor * fact.NightHours * factor * settings.Factors.NightDifferentialBps / (divisor * Bps * Bps));

            basicTotal = checked(basicTotal + basic);
            premiumTotal = checked(premiumTotal + premium);
            overtimeTotal = checked(overtimeTotal + overtime);
            nightTotal = checked(nightTotal + night);
            regularHours += fact.RegularHours;
            overtimeHours += fact.OvertimeHours;
            nightHours += fact.NightHours;
            entryIds.Add(fact.EntryId);
            lastDate = fact.WorkDate;

            var share = ShareOf(fact.ProjectId, fact.SubProjectId);
            share.Regular += fact.RegularHours;
            share.Overtime += fact.OvertimeHours;
            share.Gross = checked(share.Gross + basic + premium + overtime + night);
            share.Basic = checked(share.Basic + basic);
        }

        foreach (var fact in holidayFacts.OrderBy(item => item.Date))
        {
            if (!IsPaidUnworkedHoliday(worker, fact.Date, settings, out var dailyRateMinor)) continue;
            holidayPays.Add(new CalculatedHolidayPay(fact.Date, fact.ProjectId, fact.SubProjectId, dailyRateMinor));
            holidayTotal = checked(holidayTotal + dailyRateMinor);
            var share = ShareOf(fact.ProjectId, fact.SubProjectId);
            share.Gross = checked(share.Gross + dailyRateMinor);
            if (lastDate is null || fact.Date > lastDate) lastDate = fact.Date;
        }

        if (lastDate is null) return null;

        var allocations = perProject
            .Select(pair => new CalculatedAllocation(pair.Key.Item1, pair.Key.Item2, pair.Value.Regular, pair.Value.Overtime, pair.Value.Gross, pair.Value.Basic))
            .OrderBy(allocation => allocation.ProjectId).ThenBy(allocation => allocation.SubProjectId)
            .ToArray();
        return new CalculatedPayLine(worker.Id, worker.FullName, worker.EmployeeNumber, (worker.RateOn(lastDate.Value) ?? worker.CurrentRate).Designation,
            regularHours, overtimeHours, basicTotal, overtimeTotal, allocations, entryIds)
        {
            NightHours = nightHours,
            PremiumPayMinor = premiumTotal,
            NightPayMinor = nightTotal,
            HolidayPayMinor = holidayTotal,
            HolidayPays = holidayPays
        };
    }

    /// <summary>
    /// Whether an unworked day is paid as a regular holiday: the date is a regular holiday, the worker was hired and active that day, it
    /// is not their rest day, and their rate that day is a daily rate (an hourly-rated worker is paid for hours worked only).
    /// </summary>
    public static bool IsPaidUnworkedHoliday(Worker worker, DateOnly date, PaySettings settings, out long dailyRateMinor)
    {
        dailyRateMinor = 0;
        if (!settings.Holidays.TryGetValue(date, out var holiday) || holiday != HolidayType.Regular) return false;
        if (!worker.CanBeRecordedOn(date)) return false;
        if (worker.RestDay is { } rest && date.DayOfWeek == rest) return false;
        if (worker.RateOn(date) is not { Unit: RateUnit.Daily } rate) return false;
        dailyRateMinor = rate.RateMinor;
        return true;
    }

    internal static long RoundToMinor(decimal value) => checked((long)decimal.Round(value, 0, MidpointRounding.AwayFromZero));
}

/// <summary>One project's part of a worker's basic pay for the year.</summary>
public sealed record BasicPayShare(Guid ProjectId, Guid? SubProjectId, long BasicMinor);

/// <summary>A worker's basic pay for a year, from the approved regular payrolls, by project.</summary>
public sealed record ThirteenthMonthBasis(Guid WorkerId, string WorkerName, string EmployeeNumber, string Designation, IReadOnlyList<BasicPayShare> Shares);

/// <summary>
/// The 13th-month pay: one twelfth of the basic pay a worker earned in the calendar year (the plain-rate pay for the regular hours of the
/// approved payrolls; overtime, premiums, night differential and holiday pay are not basic pay). It is rounded once, half away from zero,
/// and charged to the projects in proportion to the basic pay earned on each, by the largest remainder, so the shares add up exactly.
/// </summary>
public static class ThirteenthMonthCalculator
{
    public const int MonthsInYear = 12;

    public static IReadOnlyList<CalculatedPayLine> Calculate(IEnumerable<ThirteenthMonthBasis> workers)
    {
        var lines = new List<CalculatedPayLine>();
        foreach (var worker in workers)
        {
            var shares = worker.Shares.Where(share => share.BasicMinor > 0).OrderBy(share => share.ProjectId).ThenBy(share => share.SubProjectId).ToArray();
            var basic = shares.Aggregate(0L, (sum, share) => checked(sum + share.BasicMinor));
            if (basic <= 0) continue;
            var amount = PayrollCalculator.RoundToMinor(basic / (decimal)MonthsInYear);
            if (amount <= 0) continue;
            var split = LargestRemainder(amount, shares.Select(share => share.BasicMinor).ToArray());
            var allocations = shares.Select((share, index) => new CalculatedAllocation(share.ProjectId, share.SubProjectId, 0m, 0m, split[index]))
                .Where(allocation => allocation.GrossMinor > 0)
                .ToArray();
            lines.Add(new CalculatedPayLine(worker.WorkerId, worker.WorkerName, worker.EmployeeNumber, worker.Designation, 0m, 0m, 0, 0, allocations, [])
            {
                ThirteenthMonthPayMinor = amount,
                ThirteenthMonthBasisMinor = basic
            });
        }

        return lines;
    }

    /// <summary>
    /// Splits <paramref name="total"/> in proportion to <paramref name="weights"/>: each part is rounded down, then the centavos left over
    /// go one at a time to the parts with the largest remainders (the earlier part first on a tie). The parts add up to the total exactly.
    /// </summary>
    public static long[] LargestRemainder(long total, IReadOnlyList<long> weights)
    {
        if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
        if (weights.Any(weight => weight < 0)) throw new ArgumentOutOfRangeException(nameof(weights));
        var parts = new long[weights.Count];
        Int128 sum = 0;
        foreach (var weight in weights) sum += weight;
        if (weights.Count == 0) return parts;
        if (sum == 0) throw new ArgumentException("At least one weight must be greater than zero.", nameof(weights));

        var remainders = new Int128[weights.Count];
        long assigned = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            var product = (Int128)total * weights[index];
            parts[index] = (long)(product / sum);
            remainders[index] = product % sum;
            assigned += parts[index];
        }

        var left = total - assigned;
        foreach (var index in Enumerable.Range(0, weights.Count).OrderByDescending(index => remainders[index]).ThenBy(index => index))
        {
            if (left == 0) break;
            parts[index]++;
            left--;
        }

        return parts;
    }
}
