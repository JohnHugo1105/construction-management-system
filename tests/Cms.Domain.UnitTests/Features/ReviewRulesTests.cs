using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests.Features;

/// <summary>Rules an independent review found missing; each test is the scenario that review described.</summary>
public sealed class PostingOverlapTests
{
    [Fact]
    public void A_released_worker_cannot_be_posted_to_a_day_before_the_old_posting_ended()
    {
        var worker = TestData.NewWorker();
        var a = TestData.NewProject("A");
        var b = TestData.NewProject("B");
        worker.AssignTo(a, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now);
        worker.Release(new DateOnly(2026, 9, 1), TestData.Now);

        var ex = Assert.Throws<ValidationFailedException>(() => worker.AssignTo(b, null, new DateOnly(2026, 3, 1), TestData.UserId, TestData.Now));
        Assert.Contains("startDate", ex.Errors.Keys);

        // From the day the old posting ended it is fine.
        worker.AssignTo(b, null, new DateOnly(2026, 9, 1), TestData.UserId, TestData.Now);
        Assert.Equal(b.Id, worker.CurrentAssignment!.ProjectId);
    }

    [Fact]
    public void A_worker_who_left_and_was_reinstated_cannot_be_posted_into_the_old_posting_either()
    {
        var worker = TestData.NewWorker();
        var a = TestData.NewProject("A");
        worker.AssignTo(a, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now);
        worker.ChangeStatus(WorkerStatus.Resigned, new DateOnly(2026, 6, 1), null, TestData.Today, TestData.Now);
        worker.Reinstate(new DateOnly(2026, 8, 1), "Rehired", TestData.Today, TestData.Now);

        Assert.Throws<ValidationFailedException>(() => worker.AssignTo(a, null, new DateOnly(2026, 4, 1), TestData.UserId, TestData.Now));
        worker.AssignTo(a, null, new DateOnly(2026, 8, 1), TestData.UserId, TestData.Now);
    }
}

public sealed class SubProjectRestoreTests
{
    [Fact]
    public void Restoring_a_project_leaves_a_sub_project_that_was_archived_on_its_own_archived()
    {
        var project = TestData.NewProject();
        var phase1 = project.AddSubProject(Guid.NewGuid(), "Phase 1", new DateOnly(2026, 1, 5), TestData.Now);
        var phase2 = project.AddSubProject(Guid.NewGuid(), "Phase 2", new DateOnly(2026, 1, 5), TestData.Now);
        project.ArchiveSubProject(phase1.Id, TestData.Now);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.All(project.SubProjects, sub => Assert.Equal(SubProjectStatus.Archived, sub.Status));

        project.Restore(TestData.Now);

        Assert.Equal(SubProjectStatus.Archived, phase1.Status);
        Assert.Equal(SubProjectStatus.Active, phase2.Status);
        Assert.Throws<DomainRuleException>(() => project.EnsureCanReceiveRecordsFor(phase1.Id));
        project.EnsureCanReceiveRecordsFor(phase2.Id);
    }
}

public sealed class WorkerStatusHistoryTests
{
    [Fact]
    public void Days_of_suspension_stay_closed_after_the_worker_returns()
    {
        var worker = TestData.NewWorker();
        worker.ChangeStatus(WorkerStatus.Suspended, new DateOnly(2026, 10, 10), "Safety breach", TestData.Today, TestData.Now);
        worker.ChangeStatus(WorkerStatus.Active, new DateOnly(2026, 10, 20), "Suspension served", TestData.Today, TestData.Now);

        Assert.True(worker.CanBeRecordedOn(new DateOnly(2026, 10, 9)));
        Assert.False(worker.CanBeRecordedOn(new DateOnly(2026, 10, 10)));
        Assert.False(worker.CanBeRecordedOn(new DateOnly(2026, 10, 15)));
        Assert.True(worker.CanBeRecordedOn(new DateOnly(2026, 10, 20)));
        Assert.Equal(WorkerStatus.Suspended, worker.StatusOn(new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void A_worker_who_left_can_be_paid_up_to_the_last_day_worked_and_not_after()
    {
        var worker = TestData.NewWorker();
        worker.ChangeStatus(WorkerStatus.Resigned, new DateOnly(2026, 10, 30), null, TestData.Today, TestData.Now);

        Assert.True(worker.CanBeRecordedOn(new DateOnly(2026, 10, 30)));
        Assert.False(worker.CanBeRecordedOn(new DateOnly(2026, 10, 31)));
        Assert.False(worker.CanBeRecordedOn(TestData.HireDate.AddDays(-1)));
    }
}

public sealed class PayPeriodAndRoundingTests
{
    private static IReadOnlyList<CalculatedPayLine> OneLine(Worker worker) =>
        PayrollCalculator.Calculate([new AttendanceFact(Guid.NewGuid(), worker.Id, Guid.NewGuid(), null, new DateOnly(2026, 10, 5), 8m, 0m)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, 8m, 12_500);

    [Theory]
    [InlineData(30, true)]   // 1 to 31 October: 31 days counting both ends
    [InlineData(31, false)]  // 1 October to 1 November: 32 days
    public void A_pay_period_is_at_most_31_days_counting_both_ends(int daysApart, bool accepted)
    {
        var worker = TestData.NewWorker();
        var start = new DateOnly(2026, 10, 1);
        Func<PayrollRun> draft = () => PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, start, start.AddDays(daysApart), OneLine(worker), 8m, 12_500,
            TestData.UserId, null, TestData.Today, TestData.Now);

        if (accepted) Assert.Equal(start.AddDays(daysApart), draft().PeriodEnd);
        else Assert.Contains("periodEnd", Assert.Throws<ValidationFailedException>(() => draft()).Errors.Keys);
    }

    [Fact]
    public void An_hourly_rate_from_a_daily_one_is_divided_once_and_last()
    {
        // 600.25 a day over 7.5 paid hours, 0.75 hours worked: exactly 60.025, which is 6,002.5 centavos and rounds half away from zero to 6,003.
        // Rounding the hourly rate (80.0333...) before multiplying by 0.75 gives 6,002.
        var worker = TestData.NewWorker(rateMinor: 60_025);

        var line = Assert.Single(PayrollCalculator.Calculate(
            [new AttendanceFact(Guid.NewGuid(), worker.Id, Guid.NewGuid(), null, new DateOnly(2026, 11, 3), 0.75m, 0m)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, 7.5m, 12_500));

        Assert.Equal(6_003, line.RegularPayMinor);
    }
}

public sealed class BudgetPositionLimitTests
{
    [Fact]
    public void A_share_too_large_for_an_int_is_the_largest_value_not_a_failure()
    {
        var position = new BudgetPosition(BudgetMinor: 1, ActualMinor: 1_000_000_000_000);

        Assert.Equal(int.MaxValue, position.UsedBps);
        Assert.Equal(BudgetHealth.OverBudget, position.Health);
    }
}
