using System.Data.Common;
using System.Net;
using System.Text;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Payroll;
using Cms.Application.Features.Statutory;
using Cms.Application.Features.Workforce;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.WorkforceTestKit;

namespace Cms.Api.Tests.Features;

/// <summary>
/// Statutory deductions end to end on the real SQL Server host. Every table here is artificial (33.33, 3.21%) so nothing reads as a legal
/// rate; each expected centavo is worked out in the comment beside it.
/// </summary>
public sealed class StatutoryApiTests : IClassFixture<CmsApiFactory>
{
    private const string Schedules = "/api/statutory/schedules";
    private readonly CmsApiFactory _factory;

    public StatutoryApiTests(CmsApiFactory factory) => _factory = factory;

    /// <summary>Monthly brackets: under 1,000.00 is 11.11/22.22; under 5,000.00 is 33.33/44.45; above that 55.55/66.66.</summary>
    private static object SssTable(string effectiveFrom = "2026-01-01", string? clientRequestId = null, int version = 0) => new
    {
        agency = "Sss", name = "Artificial test brackets", effectiveFrom, method = "Brackets", periodBasis = "Monthly", clientRequestId, version,
        rows = new object[]
        {
            new { fromMinor = 0L, toMinor = (long?)100_000, employeeMinor = 1_111L, employerMinor = 2_222L, baseTaxMinor = 0L, rateBps = 0 },
            new { fromMinor = 100_000L, toMinor = (long?)500_000, employeeMinor = 3_333L, employerMinor = 4_445L, baseTaxMinor = 0L, rateBps = 0 },
            new { fromMinor = 500_000L, toMinor = (long?)null, employeeMinor = 5_555L, employerMinor = 6_666L, baseTaxMinor = 0L, rateBps = 0 }
        }
    };

    /// <summary>Per run: 3.21% employee and 4.32% employer of the run's pay, no floor or ceiling.</summary>
    private static object PhilHealthTable() => new
    {
        agency = "PhilHealth", name = "Artificial test percentage", effectiveFrom = "2026-01-01", method = "Percentage", periodBasis = "PerRun",
        employeeRateBps = 321, employerRateBps = 432, baseFloorMinor = 0L, version = 0
    };

    /// <summary>Monthly tax: nothing under 1,000.00, then 10% of the excess over 1,000.00.</summary>
    private static object TaxTable() => new
    {
        agency = "WithholdingTax", name = "Artificial test tax", effectiveFrom = "2026-01-01", method = "TaxBrackets", periodBasis = "Monthly", version = 0,
        rows = new object[]
        {
            new { fromMinor = 0L, toMinor = (long?)100_000, employeeMinor = 0L, employerMinor = 0L, baseTaxMinor = 0L, rateBps = 0 },
            new { fromMinor = 100_000L, toMinor = (long?)null, employeeMinor = 0L, employerMinor = 0L, baseTaxMinor = 0L, rateBps = 1_000 }
        }
    };

    private static PayrollDeductionDto Statutory(PayrollLineDto line, string agency) =>
        line.Deductions.Single(item => item.Source == "Statutory" && item.Agency == agency);

    [SqlServerFact]
    public async Task Without_tables_payroll_says_what_needs_setup_and_manual_deductions_still_work()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var status = await client.GetAsync<StatutoryStatusDto>("/api/statutory/status");
        Assert.Equal(["Sss", "PhilHealth", "PagIbig", "WithholdingTax"], status.Agencies.Select(item => item.Agency));
        Assert.All(status.Agencies, item => Assert.Equal(("NeedsSetup", "Enter them as deductions, or ask an accountant to set the table up."), (item.State, item.Fallback)));
        Assert.Equal("SSS contributions were not calculated because no SSS table is set up.", status.Agencies[0].Message);
        Assert.Equal("Withholding tax was not calculated because no withholding tax table is set up.", status.Agencies[3].Message);

        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001"), project);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");
        var line = Assert.Single(draft.Lines);
        Assert.Empty(line.Deductions);

        var manual = await client.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "SSS contribution", amountMinor = 12_000L } } });
        Assert.Equal(("Manual", 12_000L), (Assert.Single(manual.Lines[0].Deductions).Source, manual.TotalDeductionsMinor));

        var empty = await client.GetAsync<PayrollStatutoryDto>($"/api/payroll-runs/{draft.Id}/statutory");
        Assert.Empty(empty.Agencies);
        Assert.Equal(0, empty.TotalToRemitMinor);
    }

    [SqlServerFact]
    public async Task An_accountant_sets_up_tables_and_a_new_run_calculates_adjusts_recalculates_approves_and_remits_to_the_centavo()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var (accountant, _) = await SignedInAsync(_factory, AccessRole.Accountant, user.TenantId);
        using var _b = accountant;
        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, user.TenantId);
        using var _h = hr;

        var sss = await accountant.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        await accountant.PostAsync<StatutoryScheduleDto>(Schedules, PhilHealthTable());
        await accountant.PostAsync<StatutoryScheduleDto>(Schedules, TaxTable());
        Assert.Equal((3, true, false), (sss.Rows.Count, sss.InForce, sss.UsedByApprovedPayroll));

        var status = await hr.GetAsync<StatutoryStatusDto>("/api/statutory/status");
        Assert.Equal(["Available", "Available", "NeedsSetup", "Available"], status.Agencies.Select(item => item.State));
        Assert.Equal("Pag-IBIG contributions were not calculated because no Pag-IBIG table is set up.", status.Agencies[2].Message);

        var project = await NewProjectAsync(admin);
        var juan = await AssignAsync(admin, await HireAsync(admin, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), project);
        // Ana pays PhilHealth only: not an SSS member and exempt from tax.
        var ana = await AssignAsync(admin, await admin.PostAsync<WorkerDetailDto>("/api/workers", new
        {
            employeeNumber = "W-002", firstName = "Ana", lastName = "Abad", dateHired = HireDate, designation = "Helper", rateMinor = 60_000L, rateUnit = "Daily",
            sssMember = false, taxExempt = true
        }), project);
        Assert.Equal(new WorkerStatutoryDto(false, true, true, true), ana.Statutory);
        await RecordAsync(admin, juan.Id, project.Id, "2026-10-05", 8m);
        await RecordAsync(admin, ana.Id, project.Id, "2026-10-05", 8m);

        // Juan earns 800.00 in the run; 2 runs a month, so the monthly base is 1,600.00.
        // SSS (monthly, 1,000.00 to 5,000.00 row): 33.33 / 2 = 16.665 -> 16.67; employer 44.45 / 2 = 22.225 -> 22.23.
        // PhilHealth (per run): 3.21% of 800.00 = 25.68; employer 4.32% = 34.56.
        // Tax (monthly): 1,600.00 - 33.33 - (25.68 x 2) = 1,515.31; 10% of 515.31 = 51.531 -> 51.53 a month -> 25.765 -> 25.77 a run.
        // Ana earns 600.00: PhilHealth 19.26, employer 25.92. Nothing else.
        var draft = await hr.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" });
        var juanLine = draft.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal([("Sss", 1_667L, 2_223L), ("PhilHealth", 2_568L, 3_456L), ("WithholdingTax", 2_577L, 0L)],
            juanLine.Deductions.Select(item => (item.Agency!, item.AmountMinor, item.EmployerShareMinor)));
        Assert.Equal((6_812L, 73_188L, 5_679L), (juanLine.DeductionsMinor, juanLine.NetPayMinor, juanLine.EmployerSharesMinor));
        var anaLine = draft.Lines.Single(line => line.WorkerId == ana.Id);
        Assert.Equal(("PhilHealth", 1_926L, 2_592L), (Statutory(anaLine, "PhilHealth").Agency!, Statutory(anaLine, "PhilHealth").AmountMinor,
            Statutory(anaLine, "PhilHealth").EmployerShareMinor));
        Assert.Single(anaLine.Deductions);
        Assert.Equal((2, 140_000L, 8_738L, 131_262L, 8_271L), (draft.RunsPerMonth, draft.TotalGrossMinor, draft.TotalDeductionsMinor, draft.TotalNetMinor,
            draft.TotalEmployerSharesMinor));

        // HR adjusts Juan's PhilHealth with a reason; the amount is kept with what the table gave.
        var adjusted = await hr.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/statutory/PhilHealth/adjust",
            new { amountMinor = 2_600L, reason = "Agency statement", version = draft.Version });
        var philHealth = Statutory(adjusted.Lines.Single(line => line.WorkerId == juan.Id), "PhilHealth");
        Assert.Equal((2_600L, 2_568L, true, "Agency statement"), (philHealth.AmountMinor, philHealth.CalculatedMinor!.Value, philHealth.IsAdjusted, philHealth.AdjustmentReason));

        // Manual deductions replace only the manual set.
        var withAdvance = await hr.PutAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/workers/{juan.Id}/deductions",
            new { version = adjusted.Version, deductions = new[] { new { description = "Cash advance", amountMinor = 10_000L } } });
        var advanced = withAdvance.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal(4, advanced.Deductions.Count);
        Assert.Equal(1_667 + 2_600 + 2_577 + 10_000, advanced.DeductionsMinor);

        // Recalculating resets the adjustment and keeps the advance.
        var recalculated = await hr.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = withAdvance.Version });
        var reset = recalculated.Lines.Single(line => line.WorkerId == juan.Id);
        Assert.Equal((2_568L, false), (Statutory(reset, "PhilHealth").AmountMinor, Statutory(reset, "PhilHealth").IsAdjusted));
        Assert.Equal(6_812 + 10_000, reset.DeductionsMinor);

        // The remittance: per agency, the workers' shares, the employer's, and the sum to remit.
        var remit = await hr.GetAsync<PayrollStatutoryDto>($"/api/payroll-runs/{draft.Id}/statutory");
        Assert.Equal([("Sss", 1, 1_667L, 2_223L, 3_890L), ("PhilHealth", 2, 4_494L, 6_048L, 10_542L), ("WithholdingTax", 1, 2_577L, 0L, 2_577L)],
            remit.Agencies.Select(item => (item.Agency, item.WorkerCount, item.EmployeeMinor, item.EmployerMinor, item.ToRemitMinor)));
        Assert.Equal((8_738L, 8_271L, 17_009L), (remit.TotalEmployeeMinor, remit.TotalEmployerMinor, remit.TotalToRemitMinor));
        Assert.Equal(["Artificial test brackets"], remit.Agencies[0].ScheduleNames);
        Assert.Equal(2, remit.Workers.Count);

        // HR can't approve; an accountant approves and the labor cost posted is still the gross pay (800.00 + 600.00).
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = recalculated.Version })).StatusCode);
        var approved = await accountant.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = recalculated.Version });
        var posted = await PostedCostsAsync(_factory, user.TenantId, draft.Id);
        Assert.Equal(140_000, posted.Sum(entry => entry.GrossMinor));
        Assert.Equal(approved.TotalGrossMinor, posted.Sum(entry => entry.GrossMinor));

        // The register carries the statutory columns.
        var csv = Encoding.UTF8.GetString((await (await accountant.GetAsync($"/api/payroll-runs/{draft.Id}/export")).Content.ReadAsByteArrayAsync())[3..])
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith("Withholding tax,Employer contributions", csv[0], StringComparison.Ordinal);
        Assert.EndsWith(",16.67,22.23,25.68,34.56,0.00,0.00,25.77,56.79", csv.Single(row => row.StartsWith("W-001", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith(",16.67,22.23,44.94,60.48,0.00,0.00,25.77,82.71", csv[^1], StringComparison.Ordinal);

        // A final payroll: no adjust, no recalculate, and the table it used can't be changed any more.
        var final = await hr.PostAsync($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = approved.Version });
        Assert.Equal(HttpStatusCode.Conflict, final.StatusCode);
        var fresh = await accountant.GetAsync<StatutoryScheduleDto>($"{Schedules}/{sss.Id}");
        Assert.True(fresh.UsedByApprovedPayroll);
        var edit = await accountant.PutAsync($"{Schedules}/{sss.Id}", SssTable(version: fresh.Version));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("rule_violation", (await ApiClient.ProblemAsync(edit)).GetProperty("code").GetString());

        // A later table and retiring the used one don't touch the approved payroll's amounts.
        await accountant.PostAsync<StatutoryScheduleDto>(Schedules, SssTable("2026-10-01"));
        await accountant.PostAsync<StatutoryScheduleDto>($"{Schedules}/{sss.Id}/retire", new { version = fresh.Version });
        var after = await accountant.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}");
        Assert.Equal(1_667, Statutory(after.Lines.Single(line => line.WorkerId == juan.Id), "Sss").AmountMinor);
        Assert.Equal(17_009, (await accountant.GetAsync<PayrollStatutoryDto>($"/api/payroll-runs/{draft.Id}/statutory")).TotalToRemitMinor);

        // No audit line carries a pay figure or a reason.
        var audit = await AuditSummariesAsync(_factory, user.TenantId);
        AssertNoPayFigures(audit);
        Assert.DoesNotContain(audit, line => line.Contains("Agency statement", StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task Needs_setup_is_judged_on_the_day_the_payroll_is_calculated_from_not_on_today()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        // The only SSS table starts on 1 Jan 2027: it is in force for a period ending after that, and not for one ending in 2026.
        await admin.PostAsync<StatutoryScheduleDto>(Schedules, SssTable(effectiveFrom: "2027-01-01"));

        var before = await admin.GetAsync<StatutoryStatusDto>("/api/statutory/status?asOf=2026-12-31");
        var after = await admin.GetAsync<StatutoryStatusDto>("/api/statutory/status?asOf=2027-01-01");

        Assert.Equal("NeedsSetup", before.Agencies.Single(item => item.Agency == "Sss").State);
        Assert.Equal("Available", after.Agencies.Single(item => item.Agency == "Sss").State);
        // The answer says which day it judged (today when none is asked about).
        Assert.Equal(new DateOnly(2026, 12, 31), before.Today);
    }

    [SqlServerFact]
    public async Task A_payroll_is_not_approved_after_a_table_was_added_or_changed_until_it_is_recalculated()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var juan = await AssignAsync(admin, await HireAsync(admin, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), project);
        await RecordAsync(admin, juan.Id, project.Id, "2026-10-05", 8m);
        var sss = await admin.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        var draft = await admin.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" });

        // A table is corrected after the draft was worked out: approving would leave deductions that no longer match the table they cite.
        var corrected = (object)new
        {
            agency = "Sss", name = "Artificial test brackets", effectiveFrom = "2026-01-01", method = "Brackets", periodBasis = "Monthly", version = sss.Version,
            rows = new object[]
            {
                new { fromMinor = 0L, toMinor = (long?)100_000, employeeMinor = 1_111L, employerMinor = 2_222L, baseTaxMinor = 0L, rateBps = 0 },
                new { fromMinor = 100_000L, toMinor = (long?)null, employeeMinor = 4_000L, employerMinor = 5_000L, baseTaxMinor = 0L, rateBps = 0 }
            }
        };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{Schedules}/{sss.Id}", corrected)).StatusCode);

        var refused = await admin.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Recalculate the contributions and tax, then approve", (await ApiClient.ProblemAsync(refused)).GetProperty("detail").GetString());

        // Recalculating takes the table as it is now, and then approval goes through.
        var current = await admin.GetAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}");
        var recalculated = await admin.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = current.Version });
        Assert.Equal("Approved", (await admin.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/approve", new { version = recalculated.Version })).Status);
    }

    [SqlServerFact]
    public async Task A_table_added_after_the_draft_also_stops_approval_until_recalculated()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await NewProjectAsync(admin);
        var juan = await AssignAsync(admin, await HireAsync(admin, "W-001", "Juan", "Dela Cruz", rateMinor: 80_000), project);
        await RecordAsync(admin, juan.Id, project.Id, "2026-10-05", 8m);
        var draft = await admin.PostAsync<PayrollRunDto>("/api/payroll-runs", new { periodStart = "2026-10-01", periodEnd = "2026-10-15" });
        await admin.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());

        var refused = await admin.PostAsync($"/api/payroll-runs/{draft.Id}/approve", new { version = draft.Version });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("table was added or changed", (await ApiClient.ProblemAsync(refused)).GetProperty("detail").GetString());
    }

    [SqlServerFact]
    public async Task Runs_per_month_and_worker_flags_change_what_a_recalculation_takes()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        await client.PostAsync<StatutoryScheduleDto>(Schedules, TaxTable());
        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001", rateMinor: 80_000), project);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 8m);
        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        // Four runs a month: base 800.00 x 4 = 3,200.00, still the second row: 33.33 / 4 = 8.3325 -> 8.33.
        // Tax: 3,200.00 - 33.33 = 3,166.67; 10% of 2,166.67 = 216.667 -> 216.67 a month -> 54.1675 -> 54.17 a run.
        var organization = await client.GetAsync<OrganizationDto>("/api/organization");
        var changed = await client.PutAsync<OrganizationDto>("/api/organization", new
        {
            name = organization.Name, currencyCode = organization.CurrencyCode, vatRateBps = organization.VatRateBps, timeZoneId = organization.TimeZoneId,
            hoursPerDay = organization.HoursPerDay, overtimeMultiplierBps = organization.OvertimeMultiplierBps, varianceBasis = organization.VarianceBasis,
            payrollRunsPerMonth = 4, version = organization.Version
        });
        Assert.Equal(4, changed.PayrollRunsPerMonth);
        var fourRuns = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = draft.Version });
        Assert.Equal(4, fourRuns.RunsPerMonth);
        Assert.Equal([833L, 5_417L], fourRuns.Lines[0].Deductions.Select(item => item.AmountMinor));

        // Out of SSS and exempt from tax: nothing is taken any more.
        var detail = await client.GetAsync<WorkerDetailDto>($"/api/workers/{worker.Id}");
        var flagged = await client.PutAsync<WorkerDetailDto>($"/api/workers/{worker.Id}/statutory",
            new { sssMember = false, philHealthMember = true, pagIbigMember = true, taxExempt = true, version = detail.Version });
        Assert.Equal(new WorkerStatutoryDto(false, true, true, true), flagged.Statutory);
        var none = await client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{draft.Id}/recalculate-statutory", new { version = fourRuns.Version });
        Assert.Empty(none.Lines[0].Deductions);

        var invalid = await client.PutAsync("/api/organization", new
        {
            name = organization.Name, currencyCode = organization.CurrencyCode, vatRateBps = organization.VatRateBps, timeZoneId = organization.TimeZoneId,
            hoursPerDay = organization.HoursPerDay, overtimeMultiplierBps = organization.OvertimeMultiplierBps, varianceBasis = organization.VarianceBasis,
            payrollRunsPerMonth = 5, version = changed.Version
        });
        Assert.Contains("payrollRunsPerMonth", await CostingTestKit.ErrorFieldsAsync(invalid));
    }

    [SqlServerFact]
    public async Task Deductions_above_the_pay_are_limited_and_flagged_instead_of_failing_the_run()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        // A per-run table that takes 70.00 from the employee whatever the pay, against 1 hour at 50.00 an hour.
        await client.PostAsync<StatutoryScheduleDto>(Schedules, new
        {
            agency = "Sss", name = "Artificial flat table", effectiveFrom = "2026-01-01", method = "Brackets", periodBasis = "PerRun", version = 0,
            rows = new[] { new { fromMinor = 0L, toMinor = (long?)null, employeeMinor = 7_000L, employerMinor = 1_000L, baseTaxMinor = 0L, rateBps = 0 } }
        });
        var project = await NewProjectAsync(client);
        var worker = await AssignAsync(client, await HireAsync(client, "W-001", rateMinor: 5_000, unit: "Hourly"), project);
        await RecordAsync(client, worker.Id, project.Id, "2026-10-05", 1m);

        var draft = await CreateRunAsync(client, "2026-10-01", "2026-10-15");

        var line = Assert.Single(draft.Lines);
        var sss = Statutory(line, "Sss");
        Assert.Equal((5_000L, 7_000L, true, 0L, true), (sss.AmountMinor, sss.CalculatedMinor!.Value, sss.IsLimited, line.NetPayMinor, line.StatutoryLimited));
        var tooMuch = await client.PostAsync($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/statutory/Sss/adjust",
            new { amountMinor = 5_001L, reason = "Try", version = draft.Version });
        Assert.Contains("amountMinor", await CostingTestKit.ErrorFieldsAsync(tooMuch));
        var manual = await client.PutAsync($"/api/payroll-runs/{draft.Id}/workers/{worker.Id}/deductions",
            new { version = draft.Version, deductions = new[] { new { description = "Cash advance", amountMinor = 1L } } });
        Assert.Contains("deductions", await CostingTestKit.ErrorFieldsAsync(manual));
    }

    [SqlServerFact]
    public async Task Tables_are_validated_kept_one_per_date_repeated_safely_and_versioned()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;

        var bad = await client.PostAsync(Schedules, new
        {
            agency = "Sss", name = "", effectiveFrom = "2026-01-01", method = "TaxBrackets", periodBasis = "Weekly", version = 0,
            rows = new[] { new { fromMinor = 5L, toMinor = (long?)null, employeeMinor = 0L, employerMinor = 0L, baseTaxMinor = 0L, rateBps = 0 } }
        });
        Assert.Equal(["method", "name", "periodBasis", "rows"], (await CostingTestKit.ErrorFieldsAsync(bad)).Order(StringComparer.Ordinal));
        var gap = await client.PostAsync(Schedules, new
        {
            agency = "Sss", name = "Gap", effectiveFrom = "2026-01-01", method = "Brackets", periodBasis = "Monthly", version = 0,
            rows = new object[]
            {
                new { fromMinor = 0L, toMinor = (long?)100, employeeMinor = 1L, employerMinor = 1L, baseTaxMinor = 0L, rateBps = 0 },
                new { fromMinor = 200L, toMinor = (long?)null, employeeMinor = 1L, employerMinor = 1L, baseTaxMinor = 0L, rateBps = 0 }
            }
        });
        var gapProblem = await ApiClient.ProblemAsync(gap);
        Assert.Equal("Row 2 must start at ₱1.00, where row 1 ends, so there are no gaps or overlaps.",
            gapProblem.GetProperty("errors").GetProperty("rows")[0].GetString());

        var requestId = Guid.NewGuid().ToString("N");
        var first = await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable(clientRequestId: requestId));
        var repeat = await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable(clientRequestId: requestId));
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(1, (await client.GetAsync<PagedResult<StatutoryScheduleListItemDto>>(Schedules)).Total);
        var reused = await client.PostAsync(Schedules, SssTable("2026-02-01", requestId));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        var sameDate = await client.PostAsync(Schedules, SssTable());
        Assert.Equal(["effectiveFrom"], await CostingTestKit.ErrorFieldsAsync(sameDate));

        var updated = await client.PutAsync<StatutoryScheduleDto>($"{Schedules}/{first.Id}", new
        {
            name = "Renamed", effectiveFrom = "2026-01-01", method = "Percentage", periodBasis = "PerRun", employeeRateBps = 100, employerRateBps = 200,
            baseFloorMinor = 0L, version = first.Version
        });
        Assert.Equal(("Renamed", "Percentage", 0, "Sss"), (updated.Name, updated.Method, updated.Rows.Count, updated.Agency));
        var stale = await client.PutAsync($"{Schedules}/{first.Id}", SssTable(version: first.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", (await ApiClient.ProblemAsync(stale)).GetProperty("code").GetString());

        var retired = await client.PostAsync<StatutoryScheduleDto>($"{Schedules}/{first.Id}/retire", new { version = updated.Version });
        Assert.Equal(("Retired", false), (retired.Status, retired.InForce));
        Assert.Equal(0, (await client.GetAsync<PagedResult<StatutoryScheduleListItemDto>>(Schedules)).Total);
        Assert.Equal(1, (await client.GetAsync<PagedResult<StatutoryScheduleListItemDto>>($"{Schedules}?includeRetired=true")).Total);
        // A retired table steps aside, so a corrected table can take its date.
        await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsync($"{Schedules}/{first.Id}", SssTable(version: retired.Version))).StatusCode);
    }

    [SqlServerFact]
    public async Task Hr_reads_tables_but_only_accountants_and_administrators_change_them_and_pay_stays_with_payroll()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var table = await admin.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        var project = await NewProjectAsync(admin);
        var worker = await AssignAsync(admin, await HireAsync(admin, "W-001"), project);
        await RecordAsync(admin, worker.Id, project.Id, "2026-10-05", 8m);
        var run = await CreateRunAsync(admin, "2026-10-01", "2026-10-15");

        var (hr, _) = await SignedInAsync(_factory, AccessRole.HrOfficer, user.TenantId);
        using var _h = hr;
        Assert.Equal(HttpStatusCode.OK, (await hr.GetAsync($"{Schedules}/{table.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync(Schedules, SssTable("2026-03-01"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PutAsync($"{Schedules}/{table.Id}", SssTable(version: table.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync($"{Schedules}/{table.Id}/retire", new { version = table.Version })).StatusCode);

        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Viewer, AccessRole.WarehouseKeeper })
        {
            var (other, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _o = other;
            foreach (var url in new[] { "/api/statutory/status", Schedules, $"{Schedules}/{table.Id}", $"/api/payroll-runs/{run.Id}/statutory" })
                Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/payroll-runs/{run.Id}/recalculate-statutory", new { version = run.Version })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/payroll-runs/{run.Id}/workers/{worker.Id}/statutory/Sss/adjust",
                new { amountMinor = 1L, reason = "x", version = run.Version })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsync($"/api/workers/{worker.Id}/statutory",
                new { sssMember = false, philHealthMember = false, pagIbigMember = false, taxExempt = true, version = 1 })).StatusCode);
        }

        // A project manager sees the worker but not the contribution flags.
        var (manager, _) = await SignedInAsync(_factory, AccessRole.ProjectManager, user.TenantId);
        using var _m = manager;
        var seen = await RawAsync(manager, $"/api/workers/{worker.Id}");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, seen.GetProperty("statutory").ValueKind);
        AssertNoPayFields(seen);
    }

    [SqlServerFact]
    public async Task A_second_company_can_not_see_or_change_another_companys_tables_or_payroll_contributions()
    {
        var (first, _) = await SignedInAsync(_factory);
        using var _f = first;
        var table = await first.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        var project = await NewProjectAsync(first);
        var worker = await AssignAsync(first, await HireAsync(first, "W-001"), project);
        await RecordAsync(first, worker.Id, project.Id, "2026-10-05", 8m);
        var run = await CreateRunAsync(first, "2026-10-01", "2026-10-15");

        var (second, _) = await SignedInAsync(_factory);
        using var _s = second;
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"{Schedules}/{table.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PutAsync($"{Schedules}/{table.Id}", SssTable(version: table.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsync($"{Schedules}/{table.Id}/retire", new { version = table.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/payroll-runs/{run.Id}/statutory")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsync($"/api/payroll-runs/{run.Id}/recalculate-statutory", new { version = run.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsync($"/api/payroll-runs/{run.Id}/workers/{worker.Id}/statutory/Sss/adjust",
            new { amountMinor = 1L, reason = "x", version = run.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PutAsync($"/api/workers/{worker.Id}/statutory",
            new { sssMember = false, philHealthMember = false, pagIbigMember = false, taxExempt = true, version = 1 })).StatusCode);
        Assert.Equal(0, (await second.GetAsync<PagedResult<StatutoryScheduleListItemDto>>(Schedules)).Total);
        Assert.All((await second.GetAsync<StatutoryStatusDto>("/api/statutory/status")).Agencies, item => Assert.Equal("NeedsSetup", item.State));
        // The other company's table never applies to this company's payroll.
        var project2 = await NewProjectAsync(second);
        var worker2 = await AssignAsync(second, await HireAsync(second, "W-001"), project2);
        await RecordAsync(second, worker2.Id, project2.Id, "2026-10-05", 8m);
        Assert.Empty(Assert.Single((await CreateRunAsync(second, "2026-10-01", "2026-10-15")).Lines).Deductions);
    }

    [SqlServerFact]
    public async Task Preparing_a_payroll_and_reading_its_contributions_cost_the_same_reads_for_five_workers_as_for_forty()
    {
        var counter = new SelectCounter();
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(counter)));
        var user = await TenantSeeder.CreateTenantAsync(_factory);
        using var client = new ApiClient(host);
        await client.SignInAsync(user.Username, user.Password);
        await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable());
        await client.PostAsync<StatutoryScheduleDto>(Schedules, PhilHealthTable());
        await client.PostAsync<StatutoryScheduleDto>(Schedules, TaxTable());
        var project = await NewProjectAsync(client);
        var workers = new List<WorkerDetailDto>();
        for (var index = 1; index <= 40; index++) workers.Add(await HireAsync(client, $"W-{index:000}", $"First{index}", $"Last{index}"));

        async Task RecordCrewAsync(string date, IEnumerable<WorkerDetailDto> crew) =>
            await client.PostAsync<List<AttendanceEntryDto>>("/api/attendance/batch", new
            {
                projectId = project.Id, workDate = date, entries = crew.Select(worker => new { workerId = worker.Id, regularHours = 8m, overtimeHours = 0m }).ToArray()
            });
        await RecordCrewAsync("2026-09-02", workers.Take(5));
        await RecordCrewAsync("2026-09-17", workers);

        var (small, smallCreate) = await counter.MeasureAsync(() => CreateRunAsync(client, "2026-09-01", "2026-09-15"));
        var (large, largeCreate) = await counter.MeasureAsync(() => CreateRunAsync(client, "2026-09-16", "2026-09-30"));
        Assert.Equal((5, 40), (small.Lines.Count, large.Lines.Count));
        Assert.Equal(3, large.Lines[0].Deductions.Count);
        // Reading the tables and the workers' flags adds no statement per worker: preparing 40 reads as much as preparing 5.
        Assert.Equal(smallCreate, largeCreate);

        var (_, smallRemit) = await counter.MeasureAsync(() => client.GetAsync<PayrollStatutoryDto>($"/api/payroll-runs/{small.Id}/statutory"));
        var (largeRemitDto, largeRemit) = await counter.MeasureAsync(() => client.GetAsync<PayrollStatutoryDto>($"/api/payroll-runs/{large.Id}/statutory"));
        Assert.Equal(40, largeRemitDto.Workers.Count);
        Assert.Equal(smallRemit, largeRemit);
        var (_, smallRecalc) = await counter.MeasureAsync(() => client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{small.Id}/recalculate-statutory", new { version = small.Version }));
        var (_, largeRecalc) = await counter.MeasureAsync(() => client.PostAsync<PayrollRunDto>($"/api/payroll-runs/{large.Id}/recalculate-statutory", new { version = large.Version }));
        Assert.Equal(smallRecalc, largeRecalc);
        var (_, status) = await counter.MeasureAsync(() => client.GetAsync<StatutoryStatusDto>("/api/statutory/status"));
        for (var month = 2; month <= 9; month++) await client.PostAsync<StatutoryScheduleDto>(Schedules, SssTable($"2026-0{month}-01"));
        var (list, listReads) = await counter.MeasureAsync(() => client.GetAsync<PagedResult<StatutoryScheduleListItemDto>>($"{Schedules}?pageSize=100"));
        var (_, statusAfter) = await counter.MeasureAsync(() => client.GetAsync<StatutoryStatusDto>("/api/statutory/status"));
        Assert.Equal(11, list.Total);
        Assert.Single(list.Items, item => item.InForce && item.Agency == "Sss");
        Assert.Equal(status, statusAfter);
        // Fixed numbers, so a regression shows up here: the contributions read is the run and one grouped read of its deductions; the
        // list a count and a page; preparing a payroll 11 reads (organization, attendance, workers and their histories, the tables in
        // force, then the run read back); the status one read of the tables in force plus the organization.
        Assert.Equal((2, 3, 11, 2), (largeRemit, listReads, largeCreate, status));
    }

    /// <summary>Counts the reads (SELECT statements) a request makes; batched writes grow with the rows saved and are left out.</summary>
    private sealed class SelectCounter : DbCommandInterceptor
    {
        private int _count;

        public async Task<(T Result, int Reads)> MeasureAsync<T>(Func<Task<T>> action)
        {
            var before = Volatile.Read(ref _count);
            var result = await action();
            return (result, Volatile.Read(ref _count) - before);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (IsRead(command.CommandText)) Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static bool IsRead(string text) =>
            !text.Contains("INSERT ", StringComparison.OrdinalIgnoreCase) && !text.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("DELETE ", StringComparison.OrdinalIgnoreCase) && !text.Contains("MERGE ", StringComparison.OrdinalIgnoreCase);
    }
}
