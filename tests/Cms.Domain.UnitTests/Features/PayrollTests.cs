using Cms.Domain.Common;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests.Features;

public sealed class PayrollCalculatorTests
{
    private const decimal HoursPerDay = 8m;
    private const int OvertimeBps = 12_500;

    private static AttendanceFact Fact(Guid worker, Guid project, DateOnly date, decimal regular, decimal overtime = 0m, Guid? sub = null) =>
        new(Guid.NewGuid(), worker, project, sub, date, regular, overtime);

    private static IReadOnlyDictionary<Guid, Worker> Workers(params Worker[] workers) => workers.ToDictionary(worker => worker.Id);

    [Fact]
    public void A_full_day_is_the_daily_rate_and_overtime_is_paid_at_the_multiple_of_the_hourly_rate()
    {
        // 800.00 a day over 8 hours is 100.00 an hour. 8 h regular = 800.00. 2 h overtime = 100.00 x 2 x 1.25 = 250.00.
        var worker = TestData.NewWorker();
        var project = Guid.NewGuid();

        var line = Assert.Single(PayrollCalculator.Calculate([Fact(worker.Id, project, new DateOnly(2026, 11, 3), 8m, 2m)], Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Equal(80_000, line.RegularPayMinor);
        Assert.Equal(25_000, line.OvertimePayMinor);
        Assert.Equal(105_000, line.GrossMinor);
        Assert.Equal(8m, line.RegularHours);
        Assert.Equal(2m, line.OvertimeHours);
    }

    [Fact]
    public void A_part_day_is_paid_by_the_hour()
    {
        var worker = TestData.NewWorker();

        var line = Assert.Single(PayrollCalculator.Calculate([Fact(worker.Id, Guid.NewGuid(), new DateOnly(2026, 11, 3), 4m)], Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Equal(40_000, line.GrossMinor);
    }

    [Fact]
    public void An_hourly_rate_is_used_as_it_is()
    {
        var worker = TestData.NewWorker(rateMinor: 12_500, unit: RateUnit.Hourly);

        var line = Assert.Single(PayrollCalculator.Calculate([Fact(worker.Id, Guid.NewGuid(), new DateOnly(2026, 11, 3), 6m, 1m)], Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Equal(75_000, line.RegularPayMinor);
        Assert.Equal(15_625, line.OvertimePayMinor);
    }

    [Fact]
    public void A_raise_part_way_through_the_period_applies_from_its_date()
    {
        var worker = TestData.NewWorker();
        worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 96_000, RateUnit.Daily, new DateOnly(2026, 11, 10), null, TestData.UserId, TestData.Now);
        var project = Guid.NewGuid();

        var line = Assert.Single(PayrollCalculator.Calculate([
            Fact(worker.Id, project, new DateOnly(2026, 11, 9), 8m),
            Fact(worker.Id, project, new DateOnly(2026, 11, 10), 8m)], Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Equal(80_000 + 96_000, line.GrossMinor);
    }

    [Fact]
    public void Each_projects_share_adds_up_to_the_workers_gross_pay()
    {
        // A rate that does not divide evenly makes the rounding visible: 833.33 a day is 104.16625 an hour.
        var worker = TestData.NewWorker(rateMinor: 83_333);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var facts = new[]
        {
            Fact(worker.Id, a, new DateOnly(2026, 11, 3), 3m, 1m),
            Fact(worker.Id, b, new DateOnly(2026, 11, 3), 5m),
            Fact(worker.Id, a, new DateOnly(2026, 11, 4), 7m, 2.5m),
            Fact(worker.Id, b, new DateOnly(2026, 11, 5), 8m, 0.75m)
        };

        var line = Assert.Single(PayrollCalculator.Calculate(facts, Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Equal(2, line.Allocations.Count);
        Assert.Equal(line.GrossMinor, line.Allocations.Sum(allocation => allocation.GrossMinor));
        Assert.Equal(line.RegularHours, line.Allocations.Sum(allocation => allocation.RegularHours));
        Assert.Equal(4, line.EntryIds.Count);
    }

    [Fact]
    public void Pay_is_calculated_per_worker_in_name_order()
    {
        var juan = TestData.NewWorker("W-001");
        var ana = Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-002", "Ana", null, "Abad", TestData.HireDate, "Helper", 60_000, RateUnit.Daily,
            TestData.UserId, TestData.Today, TestData.Now);

        var lines = PayrollCalculator.Calculate([
            Fact(juan.Id, Guid.NewGuid(), new DateOnly(2026, 11, 3), 8m),
            Fact(ana.Id, Guid.NewGuid(), new DateOnly(2026, 11, 3), 8m)], Workers(juan, ana), HoursPerDay, OvertimeBps);

        Assert.Equal(["Ana Abad", "Juan Dela Cruz"], lines.Select(line => line.WorkerName));
    }

    [Fact]
    public void Attendance_before_the_first_rate_stops_the_calculation_with_a_clear_message()
    {
        var worker = TestData.NewWorker();

        var ex = Assert.Throws<DomainRuleException>(() => PayrollCalculator.Calculate(
            [Fact(worker.Id, Guid.NewGuid(), TestData.HireDate.AddDays(-3), 8m)], Workers(worker), HoursPerDay, OvertimeBps));

        Assert.Contains("Juan Dela Cruz has no pay rate", ex.Message);
    }

    [Fact]
    public void Nothing_to_pay_gives_no_lines() =>
        Assert.Empty(PayrollCalculator.Calculate([], Workers(), HoursPerDay, OvertimeBps));
}

public sealed class AttendanceEntryTests
{
    private static AttendanceEntry Record(Worker worker, Project project, DateOnly? date = null, decimal regular = 8m, decimal overtime = 0m) =>
        AttendanceEntry.Record(Guid.NewGuid(), TestData.Tenant, worker, project, null, date ?? new DateOnly(2026, 10, 30), regular, overtime, null, 8m,
            TestData.UserId, null, TestData.Today, TestData.Now);

    [Fact]
    public void Records_hours_for_a_worker_on_a_project()
    {
        var entry = Record(TestData.NewWorker(), TestData.NewProject(), regular: 8m, overtime: 2m);

        Assert.Equal(8m, entry.RegularHours);
        Assert.Equal(2m, entry.OvertimeHours);
        Assert.False(entry.IsLocked);
    }

    [Fact]
    public void Hours_are_checked_against_the_company_day()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();

        Assert.Contains("regularHours", Assert.Throws<ValidationFailedException>(() => Record(worker, project, regular: 9m)).Errors.Keys);
        Assert.Contains("regularHours", Assert.Throws<ValidationFailedException>(() => Record(worker, project, regular: 0m)).Errors.Keys);
        Assert.Contains("overtimeHours", Assert.Throws<ValidationFailedException>(() => Record(worker, project, regular: 8m, overtime: 17m)).Errors.Keys);
        Assert.Throws<ValidationFailedException>(() => Record(worker, project, regular: 7.555m));
    }

    [Fact]
    public void The_date_must_be_a_day_the_worker_can_be_recorded()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();

        Assert.Contains("workDate", Assert.Throws<ValidationFailedException>(() => Record(worker, project, date: TestData.Today.AddDays(1))).Errors.Keys);
        Assert.Contains("workDate", Assert.Throws<ValidationFailedException>(() => Record(worker, project, date: TestData.HireDate.AddDays(-1))).Errors.Keys);
    }

    [Fact]
    public void An_archived_project_takes_no_attendance()
    {
        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Throws<DomainRuleException>(() => Record(TestData.NewWorker(), project));
    }

    [Fact]
    public void A_locked_entry_can_not_be_edited_or_removed_until_unlocked()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();
        var entry = Record(worker, project);

        entry.LockTo(Guid.NewGuid(), TestData.Now);

        Assert.True(entry.IsLocked);
        Assert.Throws<DomainRuleException>(() => entry.Revise(worker, project, null, 4m, 0m, null, 8m, TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => entry.Void("mistake", TestData.Now));
        Assert.Throws<DomainRuleException>(() => entry.LockTo(Guid.NewGuid(), TestData.Now));

        entry.Unlock(TestData.Now);
        entry.Revise(worker, project, null, 4m, 0m, null, 8m, TestData.Today, TestData.Now);
        Assert.Equal(4m, entry.RegularHours);
    }

    [Fact]
    public void A_removed_entry_needs_a_reason_and_can_not_be_paid()
    {
        var entry = Record(TestData.NewWorker(), TestData.NewProject());

        Assert.Throws<ValidationFailedException>(() => entry.Void(" ", TestData.Now));
        entry.Void("Entered twice", TestData.Now);

        Assert.True(entry.IsVoided);
        Assert.Throws<DomainRuleException>(() => entry.LockTo(Guid.NewGuid(), TestData.Now));
    }
}

public sealed class PayrollRunTests
{
    private static PayrollRun NewRun(out Worker worker)
    {
        worker = TestData.NewWorker();
        var lines = PayrollCalculator.Calculate(
            [new AttendanceFact(Guid.NewGuid(), worker.Id, Guid.NewGuid(), null, new DateOnly(2026, 11, 3), 8m, 2m)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, 8m, 12_500);
        return PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1).AddDays(1), lines, 8m, 12_500,
            TestData.UserId, null, TestData.Today, TestData.Now);
    }

    [Fact]
    public void A_draft_copies_the_workers_name_and_designation_and_totals_the_pay()
    {
        var run = NewRun(out _);

        var line = Assert.Single(run.Lines);
        Assert.Equal("Juan Dela Cruz", line.WorkerName);
        Assert.Equal("Mason", line.Designation);
        Assert.Equal(PayrollRunStatus.Draft, run.Status);
        Assert.Equal(105_000, run.TotalGrossMinor);
        Assert.Equal(105_000, run.TotalNetMinor);
    }

    [Fact]
    public void Deductions_reduce_net_pay_and_can_not_exceed_gross()
    {
        var run = NewRun(out var worker);

        run.SetDeductions(worker.Id, [("SSS contribution", 12_000), ("Cash advance", 20_000)], TestData.Now);
        Assert.Equal(32_000, run.TotalDeductionsMinor);
        Assert.Equal(73_000, run.TotalNetMinor);

        Assert.Throws<ValidationFailedException>(() => run.SetDeductions(worker.Id, [("Too much", 105_001)], TestData.Now));
        Assert.Throws<ValidationFailedException>(() => run.SetDeductions(worker.Id, [("", 100)], TestData.Now));
        Assert.Throws<ValidationFailedException>(() => run.SetDeductions(worker.Id, [("Zero", 0)], TestData.Now));
        Assert.Throws<NotFoundException>(() => run.SetDeductions(Guid.NewGuid(), [], TestData.Now));
        Assert.Equal(73_000, run.TotalNetMinor);
    }

    [Fact]
    public void Approving_locks_the_deductions_and_can_not_be_repeated()
    {
        var run = NewRun(out var worker);

        run.Approve(TestData.UserId, TestData.Now);

        Assert.Equal(PayrollRunStatus.Approved, run.Status);
        Assert.Throws<DomainRuleException>(() => run.Approve(TestData.UserId, TestData.Now));
        Assert.Throws<DomainRuleException>(() => run.SetDeductions(worker.Id, [("Late", 100)], TestData.Now));
    }

    [Fact]
    public void Voiding_needs_a_reason_and_a_voided_payroll_can_not_come_back()
    {
        var run = NewRun(out _);
        run.Approve(TestData.UserId, TestData.Now);

        Assert.Throws<ValidationFailedException>(() => run.Void(TestData.UserId, "", TestData.Now));
        run.Void(TestData.UserId, "Wrong period", TestData.Now);

        Assert.Equal(PayrollRunStatus.Voided, run.Status);
        Assert.Throws<DomainRuleException>(() => run.Void(TestData.UserId, "again", TestData.Now));
        Assert.Throws<DomainRuleException>(() => run.Approve(TestData.UserId, TestData.Now));
    }

    [Fact]
    public void The_period_is_validated()
    {
        Assert.Contains("periodEnd", Assert.Throws<ValidationFailedException>(() => PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 11, 5),
            new DateOnly(2026, 11, 1), [], 8m, 12_500, TestData.UserId, null, TestData.Today, TestData.Now)).Errors.Keys);
        Assert.Contains("periodEnd", Assert.Throws<ValidationFailedException>(() => PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 15), [], 8m, 12_500, TestData.UserId, null, TestData.Today, TestData.Now)).Errors.Keys);
        Assert.Throws<DomainRuleException>(() => PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), [],
            8m, 12_500, TestData.UserId, null, TestData.Today, TestData.Now));
    }
}
