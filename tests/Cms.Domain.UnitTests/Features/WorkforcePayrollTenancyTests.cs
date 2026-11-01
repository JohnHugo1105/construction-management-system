using Cms.Domain.Common;
using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests.Features;

/// <summary>
/// Two things the persistence layer needs from the domain: every row of a payroll run carries its organization (so it can be filtered and
/// stamped like every other record), and a hire carries the id of the form submission so a double click can't hire twice.
/// </summary>
public sealed class WorkforcePayrollTenancyTests
{
    [Fact]
    public void Every_row_of_a_payroll_run_carries_the_runs_organization()
    {
        var worker = TestData.NewWorker();
        var lines = PayrollCalculator.Calculate(
            [new AttendanceFact(Guid.NewGuid(), worker.Id, Guid.NewGuid(), null, new DateOnly(2026, 11, 1), 8m, 2m)],
            new Dictionary<Guid, Worker> { [worker.Id] = worker }, 8m, 12_500);
        var run = PayrollRun.Draft(Guid.NewGuid(), TestData.Tenant, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), lines, 8m, 12_500, TestData.UserId,
            null, TestData.Today, TestData.Now);

        run.SetDeductions(worker.Id, [("SSS contribution", 12_000)], TestData.Now);

        var line = Assert.Single(run.Lines);
        Assert.Equal(TestData.Tenant, line.TenantId);
        Assert.Equal(TestData.Tenant, Assert.Single(line.Allocations).TenantId);
        Assert.Equal(TestData.Tenant, Assert.Single(line.Deductions).TenantId);
    }

    [Fact]
    public void A_hire_keeps_the_trimmed_request_id_and_refuses_one_that_is_too_long()
    {
        var worker = Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-100", "Juan", null, "Dela Cruz", TestData.HireDate, "Mason", 80_000, RateUnit.Daily,
            TestData.UserId, TestData.Today, TestData.Now, "  form-1  ");
        var without = TestData.NewWorker();

        Assert.Equal("form-1", worker.ClientRequestId);
        Assert.Null(without.ClientRequestId);
        var ex = Assert.Throws<ValidationFailedException>(() => Worker.Hire(Guid.NewGuid(), TestData.Tenant, "W-101", "Ana", null, "Abad", TestData.HireDate,
            "Helper", 60_000, RateUnit.Daily, TestData.UserId, TestData.Today, TestData.Now, new string('x', 81)));
        Assert.Contains("clientRequestId", ex.Errors.Keys);
    }
}
