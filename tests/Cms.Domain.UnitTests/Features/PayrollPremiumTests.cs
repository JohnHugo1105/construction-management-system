using Cms.Domain.Common;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Statutory;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests.Features;

/// <summary>
/// Rest-day, holiday and night premiums, unworked regular holidays, and the 13th-month pay. Every worker is paid 800.00 a day over 8 paid
/// hours (100.00 an hour) unless the test says otherwise; the default factors apply (rest day 130%, special day 130%, special day on a
/// rest day 150%, regular holiday 200%, regular holiday on a rest day 260%, overtime on those days x 130%, night differential 10%).
/// </summary>
public sealed class PayrollPremiumTests
{
    private const decimal HoursPerDay = 8m;
    private const int OvertimeBps = 12_500;

    // Sunday 4 Oct 2026, Wednesday 7 Oct 2026, Friday 25 Dec 2026, Wednesday 30 Dec 2026.
    private static readonly DateOnly Sunday = new(2026, 10, 4);
    private static readonly DateOnly Wednesday = new(2026, 10, 7);

    private static AttendanceFact Fact(Guid worker, Guid project, DateOnly date, decimal regular, decimal overtime = 0m, decimal night = 0m) =>
        new(Guid.NewGuid(), worker, project, null, date, regular, overtime, night);

    private static PaySettings Settings(params (DateOnly Date, HolidayType Type)[] holidays) =>
        new(HoursPerDay, OvertimeBps, PayFactors.Defaults, holidays.ToDictionary(item => item.Date, item => item.Type));

    private static Worker WorkerResting(DayOfWeek? restDay, long rateMinor = 80_000, RateUnit unit = RateUnit.Daily)
    {
        var worker = TestData.NewWorker(rateMinor: rateMinor, unit: unit);
        worker.SetRestDay(restDay, TestData.Now);
        return worker;
    }

    private static CalculatedPayLine One(Worker worker, IEnumerable<AttendanceFact> facts, PaySettings settings,
        IEnumerable<UnworkedHolidayFact>? holidays = null) =>
        Assert.Single(PayrollCalculator.Calculate(facts, holidays ?? [], new Dictionary<Guid, Worker> { [worker.Id] = worker }, settings));

    [Theory]
    [InlineData(null, false, 80_000, 0)] // ordinary day: 800.00
    [InlineData(DayOfWeek.Sunday, false, 80_000, 24_000)] // rest day 130%: 1,040.00
    [InlineData(null, true, 80_000, 24_000)] // special day 130%: 1,040.00
    [InlineData(DayOfWeek.Sunday, true, 80_000, 40_000)] // special day on the rest day 150%: 1,200.00
    public void A_full_day_on_a_rest_day_or_special_day_earns_the_day_factor(DayOfWeek? restDay, bool special, long basic, long premium)
    {
        var worker = WorkerResting(restDay);
        var settings = special ? Settings((Sunday, HolidayType.Special)) : Settings();

        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 8m)], settings);

        Assert.Equal(basic, line.RegularPayMinor);
        Assert.Equal(premium, line.PremiumPayMinor);
        Assert.Equal(basic + premium, line.GrossMinor);
    }

    [Theory]
    [InlineData(null, 160_000)] // regular holiday 200%: 1,600.00
    [InlineData(DayOfWeek.Sunday, 208_000)] // regular holiday on the rest day 260%: 2,080.00
    public void A_full_day_on_a_regular_holiday_earns_the_holiday_factor(DayOfWeek? restDay, long gross)
    {
        var worker = WorkerResting(restDay);

        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 8m)], Settings((Sunday, HolidayType.Regular)));

        Assert.Equal(80_000, line.RegularPayMinor);
        Assert.Equal(gross - 80_000, line.PremiumPayMinor);
        Assert.Equal(gross, line.GrossMinor);
    }

    [Theory]
    [InlineData(null, false, 25_000)] // ordinary: 100.00 x 2 x 1.25 (company overtime multiple)
    [InlineData(DayOfWeek.Sunday, false, 33_800)] // rest day: 100.00 x 2 x 1.30 x 1.30
    [InlineData(null, true, 52_000)] // regular holiday: 100.00 x 2 x 2.00 x 1.30
    [InlineData(DayOfWeek.Sunday, true, 67_600)] // regular holiday on the rest day: 100.00 x 2 x 2.60 x 1.30
    public void Overtime_on_a_premium_day_is_the_day_factor_times_the_premium_overtime_extra(DayOfWeek? restDay, bool holiday, long overtime)
    {
        var worker = WorkerResting(restDay);
        var settings = holiday ? Settings((Sunday, HolidayType.Regular)) : Settings();

        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 8m, 2m)], settings);

        Assert.Equal(overtime, line.OvertimePayMinor);
    }

    [Fact]
    public void The_night_differential_is_ten_percent_of_the_days_regular_hour_rate_for_each_night_hour()
    {
        var worker = WorkerResting(null);
        var project = Guid.NewGuid();

        // Ordinary day, 3 night hours: 100.00 x 3 x 10% = 30.00. Regular holiday, 3 night hours: 100.00 x 3 x 200% x 10% = 60.00.
        var ordinary = One(worker, [Fact(worker.Id, project, Wednesday, 8m, 0m, 3m)], Settings());
        var holiday = One(worker, [Fact(worker.Id, project, Wednesday, 8m, 0m, 3m)], Settings((Wednesday, HolidayType.Regular)));

        Assert.Equal(3_000, ordinary.NightPayMinor);
        Assert.Equal(83_000, ordinary.GrossMinor);
        Assert.Equal(6_000, holiday.NightPayMinor);
        Assert.Equal(166_000, holiday.GrossMinor);
        Assert.Equal(3m, holiday.NightHours);
    }

    [Fact]
    public void Night_hours_that_are_overtime_still_earn_the_differential_on_the_regular_hour_rate()
    {
        var worker = WorkerResting(null);

        // 8 h + 2 h overtime, both overtime hours at night: 100.00 x 2 x 10% = 20.00 (not on the overtime rate: the documented default).
        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Wednesday, 8m, 2m, 2m)], Settings());

        Assert.Equal(2_000, line.NightPayMinor);
        Assert.Equal(80_000 + 25_000 + 2_000, line.GrossMinor);
    }

    [Fact]
    public void Awkward_amounts_are_rounded_once_each_after_multiplying_everything()
    {
        // 1,000.01 a day, 0.75 h regular + 0.75 h overtime + 0.75 night hours on the rest day.
        // basic 100,001 x 0.75 / 8 = 9,375.09 -> 9,375; at 130% 12,187.62 -> 12,188, so the premium is 2,813;
        // overtime 100,001 x 0.75 x 1.30 x 1.30 / 8 = 15,843.91 -> 15,844; night 100,001 x 0.75 x 1.30 x 10% / 8 = 1,218.76 -> 1,219.
        var worker = WorkerResting(DayOfWeek.Sunday, rateMinor: 100_001);

        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 0.75m, 0.75m, 0.75m)], Settings());

        Assert.Equal(9_375, line.RegularPayMinor);
        Assert.Equal(2_813, line.PremiumPayMinor);
        Assert.Equal(15_844, line.OvertimePayMinor);
        Assert.Equal(1_219, line.NightPayMinor);
        Assert.Equal(9_375 + 2_813 + 15_844 + 1_219, line.GrossMinor);
    }

    [Fact]
    public void Project_shares_add_up_to_the_pay_exactly_and_carry_the_basic_pay()
    {
        var worker = WorkerResting(DayOfWeek.Sunday, rateMinor: 100_001);
        var tower = Guid.NewGuid();
        var warehouse = Guid.NewGuid();

        var line = One(worker,
        [
            Fact(worker.Id, tower, Sunday, 0.75m, 0.75m, 0.75m),
            Fact(worker.Id, warehouse, Wednesday, 7.25m, 1.5m, 1m),
            Fact(worker.Id, tower, new DateOnly(2026, 10, 8), 3.33m)
        ], Settings((Wednesday, HolidayType.Special)));

        Assert.Equal(line.GrossMinor, line.Allocations.Sum(share => share.GrossMinor));
        Assert.Equal(line.RegularPayMinor, line.Allocations.Sum(share => share.BasicMinor));
    }

    [Fact]
    public void A_worker_without_a_rest_day_is_paid_a_sunday_as_an_ordinary_day()
    {
        var worker = WorkerResting(null);

        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 8m, 2m)], Settings());

        Assert.Equal((80_000L, 0L, 25_000L), (line.RegularPayMinor, line.PremiumPayMinor, line.OvertimePayMinor));
    }

    [Fact]
    public void Changed_factors_are_used_for_the_day()
    {
        var worker = WorkerResting(DayOfWeek.Sunday);
        var factors = PayFactors.Defaults with { RestDayBps = 15_000, PremiumDayOvertimeExtraBps = 0 };

        // Rest day at 150%: 1,200.00; 1 h overtime at 150% x 100%: 150.00.
        var line = One(worker, [Fact(worker.Id, Guid.NewGuid(), Sunday, 8m, 1m)], new PaySettings(HoursPerDay, OvertimeBps, factors,
            new Dictionary<DateOnly, HolidayType>()));

        Assert.Equal((80_000L, 40_000L, 15_000L), (line.RegularPayMinor, line.PremiumPayMinor, line.OvertimePayMinor));
    }

    // ---- Unworked regular holidays

    [Fact]
    public void A_daily_rated_worker_posted_on_a_regular_holiday_without_attendance_is_paid_one_day_charged_to_the_project()
    {
        var worker = WorkerResting(DayOfWeek.Sunday);
        var tower = Guid.NewGuid();

        var line = One(worker, [Fact(worker.Id, tower, new DateOnly(2026, 10, 6), 8m)], Settings((Wednesday, HolidayType.Regular)),
            [new UnworkedHolidayFact(worker.Id, Wednesday, tower, null)]);

        Assert.Equal(80_000, line.HolidayPayMinor);
        Assert.Equal(160_000, line.GrossMinor);
        Assert.Equal(80_000, line.RegularPayMinor); // holiday pay is not basic pay
        var share = Assert.Single(line.Allocations);
        Assert.Equal((160_000L, 80_000L), (share.GrossMinor, share.BasicMinor));
        Assert.Equal(new CalculatedHolidayPay(Wednesday, tower, null, 80_000), Assert.Single(line.HolidayPays));
    }

    [Fact]
    public void A_worker_with_only_an_unworked_holiday_in_the_period_still_gets_a_line()
    {
        var worker = WorkerResting(null);

        var line = One(worker, [], Settings((Wednesday, HolidayType.Regular)), [new UnworkedHolidayFact(worker.Id, Wednesday, Guid.NewGuid(), null)]);

        Assert.Equal((0m, 80_000L, 80_000L), (line.RegularHours, line.HolidayPayMinor, line.GrossMinor));
        Assert.Empty(line.EntryIds);
    }

    [Fact]
    public void An_unworked_holiday_is_not_paid_when_any_condition_fails()
    {
        var project = Guid.NewGuid();
        var hourly = WorkerResting(null, 12_500, RateUnit.Hourly);
        var restsThatDay = WorkerResting(DayOfWeek.Wednesday);
        var onLeave = WorkerResting(null);
        onLeave.ChangeStatus(WorkerStatus.OnLeave, new DateOnly(2026, 10, 1), null, TestData.Today, TestData.Now);
        var worked = WorkerResting(null);
        var notYetHired = Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-009", "Ana", null, "Abad", new DateOnly(2026, 10, 20), "Helper", 60_000,
            RateUnit.Daily, TestData.UserId, TestData.Today, TestData.Now);
        var workers = new[] { hourly, restsThatDay, onLeave, worked, notYetHired }.ToDictionary(worker => worker.Id);
        var regular = Settings((Wednesday, HolidayType.Regular));

        var lines = PayrollCalculator.Calculate([Fact(worked.Id, project, Wednesday, 8m)],
            workers.Values.Select(worker => new UnworkedHolidayFact(worker.Id, Wednesday, project, null)), workers, regular);

        // Only the worker who worked is paid: 8 h on a regular holiday at 200% = 1,600.00, and no extra holiday day.
        var line = Assert.Single(lines);
        Assert.Equal(worked.Id, line.WorkerId);
        Assert.Equal((0L, 160_000L), (line.HolidayPayMinor, line.GrossMinor));

        // A special day is never paid when not worked.
        var special = Settings((Wednesday, HolidayType.Special));
        var idle = WorkerResting(null);
        Assert.Empty(PayrollCalculator.Calculate([], [new UnworkedHolidayFact(idle.Id, Wednesday, project, null)],
            new Dictionary<Guid, Worker> { [idle.Id] = idle }, special));
    }

    [Theory]
    [InlineData(null, null, DayType.Ordinary)]
    [InlineData(DayOfWeek.Sunday, null, DayType.RestDay)]
    [InlineData(DayOfWeek.Monday, null, DayType.Ordinary)]
    [InlineData(null, HolidayType.Special, DayType.Special)]
    [InlineData(DayOfWeek.Sunday, HolidayType.Special, DayType.SpecialRestDay)]
    [InlineData(null, HolidayType.Regular, DayType.RegularHoliday)]
    [InlineData(DayOfWeek.Sunday, HolidayType.Regular, DayType.RegularHolidayRestDay)]
    public void The_day_type_comes_from_the_holiday_and_the_workers_rest_day(DayOfWeek? restDay, HolidayType? holiday, DayType expected) =>
        Assert.Equal(expected, DayTypes.Of(Sunday, holiday, restDay));

    // ---- Pay rules and holidays

    [Fact]
    public void Pay_rules_are_kept_within_their_bounds_and_every_problem_is_reported_at_once()
    {
        var bad = new PayFactors(9_999, 50_001, 13_000, 20_000, 26_000, -1, 10_001);

        var error = Assert.Throws<ValidationFailedException>(() => PayRules.Create(Guid.NewGuid(), TestData.Tenant, bad, TestData.Now));

        Assert.Equal(new[] { "nightDifferentialBps", "premiumDayOvertimeExtraBps", "restDayBps", "specialDayBps" },
            error.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Enter the pay for a rest day between 100% and 500% of the ordinary rate.", error.Errors["restDayBps"][0]);
    }

    [Fact]
    public void Pay_rules_accept_the_boundaries_and_keep_the_change()
    {
        var rules = PayRules.Create(Guid.NewGuid(), TestData.Tenant, PayFactors.Defaults, TestData.Now);
        var edge = new PayFactors(10_000, 50_000, 10_000, 50_000, 50_000, 0, 10_000);

        rules.Change(edge, TestData.Now);

        Assert.Equal(edge, rules.Factors);
        Assert.Equal(13_000, PayFactors.Defaults.DayFactorBps(DayType.RestDay));
        Assert.Equal(26_000, PayFactors.Defaults.DayFactorBps(DayType.RegularHolidayRestDay));
        Assert.Equal(10_000, PayFactors.Defaults.DayFactorBps(DayType.Ordinary));
    }

    [Fact]
    public void A_holiday_needs_a_name_of_at_most_80_characters_and_a_type()
    {
        var error = Assert.Throws<ValidationFailedException>(() =>
            CompanyHoliday.Create(Guid.NewGuid(), TestData.Tenant, default, new string('x', 81), (HolidayType)7, TestData.Now));

        Assert.Equal("Choose the date of the holiday.", error.Errors["date"][0]);
        Assert.Equal("The name can be at most 80 characters.", error.Errors["name"][0]);
        Assert.Equal("Choose regular holiday or special day.", error.Errors["type"][0]);

        var holiday = CompanyHoliday.Create(Guid.NewGuid(), TestData.Tenant, Wednesday, "  Company foundation day ", HolidayType.Special, TestData.Now);
        Assert.Equal("Company foundation day", holiday.Name);
        holiday.Change("Foundation day", HolidayType.Regular, TestData.Now);
        Assert.Equal((HolidayType.Regular, "Foundation day"), (holiday.Type, holiday.Name));
    }

    [Fact]
    public void Night_hours_are_bounded_by_the_hours_worked_that_day()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();

        var tooMany = Assert.Throws<ValidationFailedException>(() => AttendanceEntry.Record(Guid.NewGuid(), TestData.Tenant, worker, project, null, Wednesday,
            4m, 1m, null, HoursPerDay, TestData.UserId, null, TestData.Today, TestData.Now, 5.5m));
        Assert.Equal("Night hours can't be more than the hours worked that day.", tooMany.Errors["nightHours"][0]);

        var negative = Assert.Throws<ValidationFailedException>(() => AttendanceEntry.Record(Guid.NewGuid(), TestData.Tenant, worker, project, null, Wednesday,
            4m, 0m, null, HoursPerDay, TestData.UserId, null, TestData.Today, TestData.Now, -1m));
        Assert.Equal("Enter night hours of zero or more, up to 2 decimals.", negative.Errors["nightHours"][0]);

        var entry = AttendanceEntry.Record(Guid.NewGuid(), TestData.Tenant, worker, project, null, Wednesday, 4m, 1m, null, HoursPerDay, TestData.UserId,
            null, TestData.Today, TestData.Now, 5m);
        Assert.Equal(5m, entry.NightHours);
    }

    [Fact]
    public void A_rest_day_is_set_and_cleared_and_an_unknown_day_is_refused()
    {
        var worker = TestData.NewWorker();
        Assert.Null(worker.RestDay);

        worker.SetRestDay(DayOfWeek.Sunday, TestData.Now);
        Assert.Equal(DayOfWeek.Sunday, worker.RestDay);
        worker.SetRestDay(null, TestData.Now);
        Assert.Null(worker.RestDay);

        var error = Assert.Throws<ValidationFailedException>(() => worker.SetRestDay((DayOfWeek)9, TestData.Now));
        Assert.Equal("Choose a day of the week, or no fixed rest day.", error.Errors["restDay"][0]);
    }

    // ---- Recalculating and approving a draft

    private static PayrollRun DraftOf(params CalculatedPayLine[] lines) =>
        PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), lines, HoursPerDay, OvertimeBps,
            TestData.UserId, null, TestData.Today, TestData.Now);

    [Fact]
    public void Recalculating_a_draft_takes_the_new_pay_keeps_typed_deductions_and_drops_the_calculated_ones()
    {
        var worker = WorkerResting(null);
        var project = Guid.NewGuid();
        var facts = new[] { Fact(worker.Id, project, Sunday, 8m) };
        var workers = new Dictionary<Guid, Worker> { [worker.Id] = worker };
        var run = DraftOf([.. PayrollCalculator.Calculate(facts, [], workers, Settings())]);
        run.SetDeductions(worker.Id, [("Cash advance", 10_000)], TestData.Now);
        run.ApplyStatutory(new Dictionary<Guid, IReadOnlyList<StatutoryAmount>>
        {
            [worker.Id] = [new StatutoryAmount(StatutoryAgency.Sss, Guid.NewGuid(), 4_000, 8_000)]
        }, 2, TestData.Now, "basis");

        worker.SetRestDay(DayOfWeek.Sunday, TestData.Now);
        var fresh = PayrollCalculator.Calculate(facts, [], workers, Settings());
        Assert.False(run.MatchesCalculation(fresh));

        run.Recalculate(fresh, HoursPerDay, OvertimeBps, TestData.Now);

        var line = Assert.Single(run.Lines);
        Assert.Equal(104_000, line.GrossPayMinor);
        Assert.Equal(10_000, line.DeductionsMinor);
        Assert.Equal(0, line.StatutoryDeductionsMinor);
        Assert.Null(run.StatutoryBasis);
        Assert.True(run.MatchesCalculation(fresh));
    }

    [Fact]
    public void Recalculating_refuses_when_typed_deductions_would_be_more_than_the_pay_or_lost_with_a_worker()
    {
        var keeps = WorkerResting(null);
        var leaves = WorkerResting(null);
        var project = Guid.NewGuid();
        var workers = new Dictionary<Guid, Worker> { [keeps.Id] = keeps, [leaves.Id] = leaves };
        var run = DraftOf([.. PayrollCalculator.Calculate([Fact(keeps.Id, project, Wednesday, 8m), Fact(leaves.Id, project, Wednesday, 8m)], [], workers,
            Settings())]);
        run.SetDeductions(keeps.Id, [("Loan", 70_000)], TestData.Now);
        run.SetDeductions(leaves.Id, [("Loan", 1_000)], TestData.Now);

        var smaller = PayrollCalculator.Calculate([Fact(keeps.Id, project, Wednesday, 4m), Fact(leaves.Id, project, Wednesday, 8m)], [], workers, Settings());
        var tooMuch = Assert.Throws<DomainRuleException>(() => run.Recalculate(smaller, HoursPerDay, OvertimeBps, TestData.Now));
        Assert.Equal("After recalculating, the deductions for Juan Dela Cruz would be more than the pay. Change the deductions first.", tooMuch.Message);

        var withoutOne = PayrollCalculator.Calculate([Fact(keeps.Id, project, Wednesday, 8m)], [], workers, Settings());
        var lost = Assert.Throws<DomainRuleException>(() => run.Recalculate(withoutOne, HoursPerDay, OvertimeBps, TestData.Now));
        Assert.Equal("Juan Dela Cruz would no longer be paid in this payroll, but has deductions entered. Remove them first.", lost.Message);
        Assert.Equal(2, run.Lines.Count);

        run.SetDeductions(leaves.Id, [], TestData.Now);
        run.Recalculate(withoutOne, HoursPerDay, OvertimeBps, TestData.Now);
        Assert.Equal(keeps.Id, Assert.Single(run.Lines).WorkerId);
    }

    [Fact]
    public void Voiding_releases_the_holidays_and_the_13th_month_year_for_another_payroll()
    {
        var worker = WorkerResting(null);
        var project = Guid.NewGuid();
        var regular = DraftOf([.. PayrollCalculator.Calculate([], [new UnworkedHolidayFact(worker.Id, Wednesday, project, null)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, Settings((Wednesday, HolidayType.Regular)))]);
        var holiday = Assert.Single(Assert.Single(regular.Lines).HolidayPays);
        Assert.Equal(Wednesday, holiday.ClaimDate);

        regular.Void(TestData.UserId, "Wrong period", TestData.Now);

        Assert.Null(holiday.ClaimDate);
        Assert.Equal(Wednesday, holiday.HolidayDate);

        var thirteenth = ThirteenthMonth(2026, Basis(worker.Id, (project, 120_000)));
        Assert.Equal(2026, Assert.Single(thirteenth.Lines).ThirteenthMonthYear);
        thirteenth.Void(TestData.UserId, "Prepared twice", TestData.Now);
        Assert.Null(Assert.Single(thirteenth.Lines).ThirteenthMonthYear);
        Assert.Equal(2026, thirteenth.Year);
    }

    // ---- 13th-month pay

    private static ThirteenthMonthBasis Basis(Guid workerId, params (Guid Project, long Basic)[] shares) =>
        new(workerId, "Juan Dela Cruz", "W-001", "Mason", shares.Select(share => new BasicPayShare(share.Project, null, share.Basic)).ToArray());

    private static PayrollRun ThirteenthMonth(int year, params ThirteenthMonthBasis[] workers) =>
        PayrollRun.DraftThirteenthMonth(Guid.NewGuid(), TestData.Tenant, year, ThirteenthMonthCalculator.Calculate(workers), HoursPerDay, OvertimeBps,
            TestData.UserId, null, TestData.Today, TestData.Now);

    [Fact]
    public void The_13th_month_pay_is_a_twelfth_of_the_years_basic_pay_rounded_once()
    {
        // 10,000.06 / 12 = 833.338... -> 833.34; 0.18 / 12 = 0.015 -> 0.02 (half away from zero).
        var lines = ThirteenthMonthCalculator.Calculate([Basis(Guid.NewGuid(), (Guid.NewGuid(), 1_000_006)), Basis(Guid.NewGuid(), (Guid.NewGuid(), 18))]);

        Assert.Equal([83_334L, 2L], lines.Select(line => line.ThirteenthMonthPayMinor));
        Assert.Equal([1_000_006L, 18L], lines.Select(line => line.ThirteenthMonthBasisMinor));
        Assert.All(lines, line => Assert.Equal(line.ThirteenthMonthPayMinor, line.GrossMinor));
        Assert.All(lines, line => Assert.Equal(0, line.RegularPayMinor));
    }

    [Fact]
    public void The_13th_month_pay_is_split_by_basic_pay_per_project_and_the_shares_add_up_exactly()
    {
        var a = new Guid("00000000-0000-0000-0000-00000000000a");
        var b = new Guid("00000000-0000-0000-0000-00000000000b");
        var c = new Guid("00000000-0000-0000-0000-00000000000c");

        // 300.00 of basic pay, 100.00 on each project: 25.00 -> 8.33 + 8.33 + 8.33 = 24.99, the last centavo to the first on a tie.
        var even = Assert.Single(ThirteenthMonthCalculator.Calculate([Basis(Guid.NewGuid(), (a, 100), (b, 100), (c, 100))]));
        Assert.Equal(25, even.ThirteenthMonthPayMinor);
        Assert.Equal([9L, 8L, 8L], even.Allocations.Select(share => share.GrossMinor));

        // 1,234,567.89 of basic pay split 1 : 2 : 4 (largest remainder): the shares add up to the 13th-month pay exactly.
        var uneven = Assert.Single(ThirteenthMonthCalculator.Calculate([Basis(Guid.NewGuid(), (a, 17_636_684), (b, 35_273_369), (c, 70_546_736))]));
        Assert.Equal(10_288_066, uneven.ThirteenthMonthPayMinor);
        Assert.Equal(uneven.ThirteenthMonthPayMinor, uneven.Allocations.Sum(share => share.GrossMinor));
        Assert.All(uneven.Allocations, share => Assert.Equal(0, share.BasicMinor));
    }

    [Theory]
    [InlineData(10L, new long[] { 1, 2 }, new long[] { 3, 7 })]
    [InlineData(1L, new long[] { 5, 5, 5 }, new long[] { 1, 0, 0 })]
    [InlineData(0L, new long[] { 3, 9 }, new long[] { 0, 0 })]
    [InlineData(100L, new long[] { 0, 1 }, new long[] { 0, 100 })]
    public void The_largest_remainder_split_gives_left_over_centavos_to_the_largest_fractions(long total, long[] weights, long[] expected) =>
        Assert.Equal(expected, ThirteenthMonthCalculator.LargestRemainder(total, weights));

    [Fact]
    public void A_worker_with_no_basic_pay_in_the_year_is_left_out()
    {
        Assert.Empty(ThirteenthMonthCalculator.Calculate([Basis(Guid.NewGuid(), (Guid.NewGuid(), 0))]));
        Assert.Empty(ThirteenthMonthCalculator.Calculate([Basis(Guid.NewGuid())]));
    }

    [Fact]
    public void A_13th_month_payroll_covers_the_calendar_year_refuses_a_future_year_and_takes_no_contributions()
    {
        var run = ThirteenthMonth(2026, Basis(Guid.NewGuid(), (Guid.NewGuid(), 120_000)));
        Assert.Equal((PayrollRunKind.ThirteenthMonth, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            (run.Kind, run.Year, run.PeriodStart, run.PeriodEnd));
        Assert.Equal(10_000, run.TotalGrossMinor);

        var future = Assert.Throws<ValidationFailedException>(() => ThirteenthMonth(2027, Basis(Guid.NewGuid(), (Guid.NewGuid(), 120_000))));
        Assert.Equal("A 13th-month payroll can't be for a future year.", future.Errors["year"][0]);

        var nobody = Assert.Throws<DomainRuleException>(() => ThirteenthMonth(2025));
        Assert.Equal("No worker has approved pay in 2025 that isn't already in a 13th-month payroll for that year.", nobody.Message);

        var statutory = Assert.Throws<DomainRuleException>(() => run.ApplyStatutory(new Dictionary<Guid, IReadOnlyList<StatutoryAmount>>(), 2, TestData.Now));
        Assert.Equal("No contributions or tax are taken from 13th-month pay.", statutory.Message);

        // Manual deductions still work.
        run.SetDeductions(run.Lines[0].WorkerId, [("Tax on the excess", 500)], TestData.Now);
        Assert.Equal(9_500, run.TotalNetMinor);
    }
}
