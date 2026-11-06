using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Domain.UnitTests.Features;

public sealed class WorkerTests
{
    [Fact]
    public void Hiring_records_the_first_pay_line()
    {
        var worker = TestData.NewWorker();

        var rate = Assert.Single(worker.Rates);
        Assert.Equal(RateChangeCategory.Initial, rate.Category);
        Assert.Equal(80_000, rate.RateMinor);
        Assert.Equal(TestData.HireDate, rate.EffectiveFrom);
        Assert.Equal("W-001", worker.EmployeeNumber);
        Assert.Equal("Juan Dela Cruz", worker.FullName);
    }

    [Fact]
    public void Hiring_reports_every_problem_at_once()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Worker.Hire(Guid.NewGuid(), TestData.Tenant, "", " ", null, "", default, "",
            0, RateUnit.Daily, TestData.UserId, TestData.Today, TestData.Now));

        foreach (var field in new[] { "employeeNumber", "firstName", "lastName", "designation", "dateHired", "rateMinor" })
            Assert.Contains(field, ex.Errors.Keys);
    }

    [Fact]
    public void A_future_hire_date_is_rejected() =>
        Assert.Contains("dateHired", Assert.Throws<ValidationFailedException>(() => Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-9", "A", null, "B",
            TestData.Today.AddDays(1), "Mason", 1, RateUnit.Daily, TestData.UserId, TestData.Today, TestData.Now)).Errors.Keys);

    [Fact]
    public void A_raise_is_added_to_the_history_and_the_rate_on_each_date_is_answerable()
    {
        var worker = TestData.NewWorker();

        worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 96_000, RateUnit.Daily, new DateOnly(2026, 11, 10), "Annual review", TestData.UserId, TestData.Now);

        Assert.Equal(2, worker.Rates.Count);
        Assert.Equal(80_000, worker.RateOn(new DateOnly(2026, 11, 9))!.RateMinor);
        Assert.Equal(96_000, worker.RateOn(new DateOnly(2026, 11, 10))!.RateMinor);
        Assert.Equal(96_000, worker.CurrentRate.RateMinor);
        Assert.Null(worker.RateOn(TestData.HireDate.AddDays(-1)));
    }

    [Fact]
    public void Pay_history_is_never_rewritten()
    {
        var worker = TestData.NewWorker();
        worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 96_000, RateUnit.Daily, new DateOnly(2026, 11, 10), null, TestData.UserId, TestData.Now);

        var sameDay = Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 99_000, RateUnit.Daily,
            new DateOnly(2026, 11, 10), null, TestData.UserId, TestData.Now));
        var earlier = Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 99_000, RateUnit.Daily,
            new DateOnly(2026, 6, 1), null, TestData.UserId, TestData.Now));

        Assert.Contains("effectiveFrom", sameDay.Errors.Keys);
        Assert.Contains("effectiveFrom", earlier.Errors.Keys);
        Assert.Equal(2, worker.Rates.Count);
    }

    [Fact]
    public void The_category_must_match_what_changed()
    {
        var worker = TestData.NewWorker();
        var date = new DateOnly(2026, 11, 10);

        Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.Promotion, "Mason", 96_000, RateUnit.Daily, date, null, TestData.UserId, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.RateAdjustment, "Foreman", 96_000, RateUnit.Daily, date, null, TestData.UserId, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 80_000, RateUnit.Daily, date, null, TestData.UserId, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.ChangeRate(RateChangeCategory.Initial, "Mason", 96_000, RateUnit.Daily, date, null, TestData.UserId, TestData.Now));

        worker.ChangeRate(RateChangeCategory.Promotion, "Foreman", 120_000, RateUnit.Daily, date, "Leads the formwork crew", TestData.UserId, TestData.Now);
        Assert.Equal("Foreman", worker.CurrentRate.Designation);
    }

    [Fact]
    public void Assigning_a_worker_ends_the_previous_posting_without_overlap()
    {
        var worker = TestData.NewWorker();
        var first = TestData.NewProject("Tower A");
        var second = TestData.NewProject("Warehouse B");

        worker.AssignTo(first, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now);
        worker.AssignTo(second, null, new DateOnly(2026, 6, 1), TestData.UserId, TestData.Now);

        Assert.Equal(2, worker.Assignments.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), worker.Assignments[0].EndDate);
        Assert.Equal(second.Id, worker.CurrentAssignment!.ProjectId);
        Assert.Single(worker.Assignments, assignment => assignment.IsCurrent);
    }

    [Fact]
    public void A_posting_must_move_forward_and_not_repeat_the_current_one()
    {
        var worker = TestData.NewWorker();
        var first = TestData.NewProject("Tower A");
        var second = TestData.NewProject("Warehouse B");
        worker.AssignTo(first, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now);

        Assert.Throws<DomainRuleException>(() => worker.AssignTo(first, null, new DateOnly(2026, 3, 1), TestData.UserId, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.AssignTo(second, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.AssignTo(second, null, TestData.HireDate.AddDays(-1), TestData.UserId, TestData.Now));
    }

    [Fact]
    public void A_worker_can_not_be_posted_to_an_archived_project_or_while_suspended()
    {
        var worker = TestData.NewWorker();
        var archived = TestData.NewProject();
        archived.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() => worker.AssignTo(archived, null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now));

        worker.ChangeStatus(WorkerStatus.Suspended, new DateOnly(2026, 5, 1), "Safety breach", TestData.Today, TestData.Now);
        Assert.Throws<DomainRuleException>(() => worker.AssignTo(TestData.NewProject(), null, new DateOnly(2026, 6, 1), TestData.UserId, TestData.Now));
    }

    [Fact]
    public void Leaving_ends_the_current_posting_and_a_left_worker_has_no_free_moves()
    {
        var worker = TestData.NewWorker();
        worker.AssignTo(TestData.NewProject(), null, new DateOnly(2026, 2, 1), TestData.UserId, TestData.Now);

        worker.ChangeStatus(WorkerStatus.Resigned, new DateOnly(2026, 10, 31), null, TestData.Today, TestData.Now);

        Assert.Null(worker.CurrentAssignment);
        Assert.Equal(new DateOnly(2026, 10, 31), worker.Assignments[0].EndDate);
        Assert.Throws<DomainRuleException>(() => worker.ChangeStatus(WorkerStatus.Active, TestData.Today, null, TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => worker.ChangeRate(RateChangeCategory.RateAdjustment, "Mason", 99_000, RateUnit.Daily,
            new DateOnly(2026, 11, 1), null, TestData.UserId, TestData.Now));
    }

    [Fact]
    public void Suspending_or_terminating_needs_a_reason_and_reinstating_brings_the_worker_back()
    {
        var worker = TestData.NewWorker();
        Assert.Throws<ValidationFailedException>(() => worker.ChangeStatus(WorkerStatus.Suspended, new DateOnly(2026, 5, 1), null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.ChangeStatus(WorkerStatus.Terminated, new DateOnly(2026, 5, 1), " ", TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => worker.ChangeStatus(WorkerStatus.Active, new DateOnly(2026, 5, 1), null, TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => worker.Reinstate(TestData.Today, "no", TestData.Today, TestData.Now));

        worker.ChangeStatus(WorkerStatus.Terminated, new DateOnly(2026, 5, 1), "Repeated absences", TestData.Today, TestData.Now);
        Assert.Throws<ValidationFailedException>(() => worker.Reinstate(new DateOnly(2026, 6, 1), "", TestData.Today, TestData.Now));
        worker.Reinstate(new DateOnly(2026, 6, 1), "Appeal upheld", TestData.Today, TestData.Now);

        Assert.Equal(WorkerStatus.Active, worker.Status);
    }

    [Fact]
    public void Attendance_eligibility_follows_hire_date_and_status()
    {
        var worker = TestData.NewWorker();
        Assert.False(worker.CanBeRecordedOn(TestData.HireDate.AddDays(-1)));
        Assert.True(worker.CanBeRecordedOn(TestData.Today));

        worker.ChangeStatus(WorkerStatus.Resigned, new DateOnly(2026, 10, 30), null, TestData.Today, TestData.Now);
        Assert.True(worker.CanBeRecordedOn(new DateOnly(2026, 10, 30)));
        Assert.False(worker.CanBeRecordedOn(new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Cases_are_appended_and_validated()
    {
        var worker = TestData.NewWorker();

        Assert.Throws<ValidationFailedException>(() => worker.RecordCase("", null, TestData.Today, null, TestData.UserId, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => worker.RecordCase("Late", null, TestData.HireDate.AddDays(-1), null, TestData.UserId, TestData.Today, TestData.Now));
        worker.RecordCase("Absent without notice", "Warned verbally", new DateOnly(2026, 10, 12), null, TestData.UserId, TestData.Today, TestData.Now);

        Assert.Single(worker.Cases);
    }
}
