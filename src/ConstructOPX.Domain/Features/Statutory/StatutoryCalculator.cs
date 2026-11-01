namespace ConstructOPX.Domain.Features.Statutory;

/// <summary>Which contributions a worker pays and whether withholding tax applies to them.</summary>
public sealed record StatutoryParticipation(bool Sss, bool PhilHealth, bool PagIbig, bool TaxExempt)
{
    /// <summary>A new worker: a member of all three funds, and taxed.</summary>
    public static readonly StatutoryParticipation Default = new(true, true, true, false);

    public bool Covers(StatutoryAgency agency) => agency switch
    {
        StatutoryAgency.Sss => Sss,
        StatutoryAgency.PhilHealth => PhilHealth,
        StatutoryAgency.PagIbig => PagIbig,
        StatutoryAgency.WithholdingTax => !TaxExempt,
        _ => false
    };
}

/// <summary>What one agency takes from one worker's pay in one run, and what the employer adds.</summary>
public sealed record StatutoryAmount(StatutoryAgency Agency, Guid ScheduleId, long EmployeeMinor, long EmployerMinor);

/// <summary>
/// Works out the government contributions and withholding tax of one worker for one pay run, from the tables a person entered. The
/// rules, in full:
/// <list type="bullet">
/// <item>A <b>monthly</b> table is applied to a monthly base: the run's gross pay times the company's runs per month (an estimate of the
/// month's pay). The month's amount is divided by the runs per month and rounded half away from zero, each run on its own.</item>
/// <item>A <b>per-run</b> table is applied to the run's gross pay as it is.</item>
/// <item>Percentage: the base is held between the table's floor and ceiling, each share is the base times its rate (rounded once),
/// then held between that share's minimum and maximum.</item>
/// <item>Brackets: the shares of the row the base falls in.</item>
/// <item>Tax: the taxable amount is the base less the employee's SSS, PhilHealth and Pag-IBIG contributions on the same basis (month or
/// run); the tax is the row's base tax plus its rate on the excess over the row's start; it is never negative and nothing for a worker
/// who is exempt.</item>
/// <item>A worker who is not a member of a fund pays nothing to it and the employer adds nothing.</item>
/// </list>
/// Nothing here knows any legal rate: every figure comes from the tables.
/// </summary>
public static class StatutoryCalculator
{
    public const int MinimumRunsPerMonth = 1;
    public const int MaximumRunsPerMonth = 4;

    /// <summary>The table in force for an agency on a date: the active table with the latest effective date on or before it.</summary>
    public static StatutorySchedule? InForce(IEnumerable<StatutorySchedule> schedules, StatutoryAgency agency, DateOnly onDate) =>
        schedules.Where(schedule => schedule.Agency == agency && schedule.IsActive && schedule.EffectiveFrom <= onDate)
            .OrderByDescending(schedule => schedule.EffectiveFrom).FirstOrDefault();

    /// <summary>
    /// The amounts for one worker, in agency order. <paramref name="schedules"/> holds at most one table per agency (the one in force);
    /// an agency without a table is left out, so the person enters it by hand.
    /// </summary>
    public static IReadOnlyList<StatutoryAmount> Calculate(long grossMinor, StatutoryParticipation participation,
        IReadOnlyCollection<StatutorySchedule> schedules, int runsPerMonth)
    {
        if (grossMinor < 0) throw new ArgumentOutOfRangeException(nameof(grossMinor));
        if (runsPerMonth is < MinimumRunsPerMonth or > MaximumRunsPerMonth) throw new ArgumentOutOfRangeException(nameof(runsPerMonth));
        var byAgency = schedules.GroupBy(schedule => schedule.Agency)
            .ToDictionary(group => group.Key, group => group.Count() == 1
                ? group.Single()
                : throw new ArgumentException("Give at most one table per agency.", nameof(schedules)));

        var monthlyBase = checked(grossMinor * runsPerMonth);
        long monthlyContributions = 0, perRunContributions = 0;
        var amounts = new List<StatutoryAmount>(4);

        foreach (var agency in StatutoryAgencies.InOrder.Where(StatutoryAgencies.IsContribution))
        {
            if (!participation.Covers(agency) || !byAgency.TryGetValue(agency, out var schedule)) continue;
            long employee, employer, employeeMonthly;
            if (schedule.PeriodBasis == StatutoryPeriodBasis.Monthly)
            {
                var (monthEmployee, monthEmployer) = schedule.Contribution(monthlyBase);
                employee = PerRun(monthEmployee, runsPerMonth);
                employer = PerRun(monthEmployer, runsPerMonth);
                employeeMonthly = monthEmployee;
            }
            else
            {
                (employee, employer) = schedule.Contribution(grossMinor);
                employeeMonthly = checked(employee * runsPerMonth);
            }

            monthlyContributions = checked(monthlyContributions + employeeMonthly);
            perRunContributions = checked(perRunContributions + employee);
            amounts.Add(new StatutoryAmount(agency, schedule.Id, employee, employer));
        }

        if (participation.Covers(StatutoryAgency.WithholdingTax) && byAgency.TryGetValue(StatutoryAgency.WithholdingTax, out var tax))
        {
            var perRunTax = tax.PeriodBasis == StatutoryPeriodBasis.Monthly
                ? PerRun(tax.Tax(monthlyBase - monthlyContributions), runsPerMonth)
                : tax.Tax(grossMinor - perRunContributions);
            amounts.Add(new StatutoryAmount(StatutoryAgency.WithholdingTax, tax.Id, perRunTax, 0));
        }

        return amounts;
    }

    /// <summary>
    /// Holds employee amounts, taken in order, within the pay that is left (<paramref name="roomMinor"/>): each amount takes what it can
    /// until the room is used up, so when the amounts would be more than the pay, their total equals it exactly and the later ones are
    /// reduced first.
    /// </summary>
    public static IReadOnlyList<long> LimitToPay(IReadOnlyList<long> amounts, long roomMinor)
    {
        var left = Math.Max(0, roomMinor);
        var limited = new long[amounts.Count];
        for (var index = 0; index < amounts.Count; index++)
        {
            limited[index] = Math.Min(Math.Max(0, amounts[index]), left);
            left -= limited[index];
        }

        return limited;
    }

    /// <summary>A month's amount for one run: divided by the runs per month, rounded half away from zero.</summary>
    private static long PerRun(long monthlyMinor, int runsPerMonth) =>
        (long)decimal.Round(monthlyMinor / (decimal)runsPerMonth, 0, MidpointRounding.AwayFromZero);
}
