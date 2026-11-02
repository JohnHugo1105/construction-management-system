using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Organizations;

namespace ConstructOPX.Domain.UnitTests.Application;

/// <summary>The "Needs your action" thresholds and their boundaries, who receives which group, and how a waiting record is put into words.</summary>
public sealed class NeedsActionRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);
    private static readonly Organization Company = Organization.Create(Guid.NewGuid(), "Acme Builders", DateTimeOffset.UnixEpoch);

    private static NeedsActionRecord Record(Guid? id = null, string? number = null, string? party = null, string? kind = null, DateOnly? date = null,
        DateOnly? otherDate = null, DateTimeOffset? since = null, long? amount = null, int? quantity = null, Guid? parent = null) =>
        new(id ?? Guid.NewGuid(), parent, number, party, kind, Guid.NewGuid(), "Riverside Residences", since, date, otherDate, amount, quantity);

    private static NeedsActionRowDto Row(NeedsActionGroup group, NeedsActionRecord record) => NeedsActionRows.From(group, record, Today, Company.TodayLocal);

    private static ReceivingOrderListItemDto Order(DateOnly? expected, string state = "NotReceived") =>
        new(Guid.NewGuid(), "PO-0007", "Visayas Steel", "Riverside Residences", null, expected, state, 1);

    // ---- Thresholds ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_bond_needs_attention_from_thirty_days_before_it_lapses_and_is_urgent_the_day_after()
    {
        Assert.False(NeedsActionRules.BondNeedsAttention(Today.AddDays(31), Today));
        Assert.True(NeedsActionRules.BondNeedsAttention(Today.AddDays(30), Today));
        Assert.True(NeedsActionRules.BondNeedsAttention(Today, Today));
        Assert.True(NeedsActionRules.BondNeedsAttention(Today.AddDays(-1), Today));
        // The reader's cut-off is the same rule: valid until on or before it.
        Assert.Equal(Today.AddDays(30), new NeedsActionDates(Today).BondsNeedAttentionUntil);

        var thirty = Row(NeedsActionGroup.BondsToAttend, Record(number: "PB-1", party: "Metro Surety", kind: "Performance", date: Today.AddDays(30),
            amount: 5_000_000_00));
        Assert.Equal(("Performance bond PB-1, Metro Surety", "Expires in 30 days, on 2 Dec 2026", false, 5_000_000_00L),
            (thirty.Label, thirty.Detail, thirty.IsUrgent, thirty.AmountMinor!.Value));
        Assert.Equal(("Expires today", false), Pair(Row(NeedsActionGroup.BondsToAttend, Record(kind: "Bid", date: Today))));
        Assert.Equal(("Expires tomorrow", false), Pair(Row(NeedsActionGroup.BondsToAttend, Record(kind: "Warranty", date: Today.AddDays(1)))));
        Assert.Equal(("Expired 1 day ago, on 1 Nov 2026", true),
            Pair(Row(NeedsActionGroup.BondsToAttend, Record(kind: "AdvancePayment", date: Today.AddDays(-1)))));
    }

    [Fact]
    public void Progress_is_stale_once_it_is_more_than_fourteen_days_old()
    {
        var start = new DateOnly(2026, 1, 5);
        Assert.False(NeedsActionRules.IsProgressStale(Today.AddDays(-14), start, Today));
        Assert.True(NeedsActionRules.IsProgressStale(Today.AddDays(-15), start, Today));
        Assert.Equal(Today.AddDays(-14), new NeedsActionDates(Today).ProgressStaleBefore);

        var row = Row(NeedsActionGroup.ProgressNotUpdated, Record(date: Today.AddDays(-15), otherDate: start));
        Assert.Equal(("Riverside Residences", "Progress not updated for 15 days", false, (long?)null), (row.Label, row.Detail, row.IsUrgent, row.AmountMinor));
    }

    [Fact]
    public void A_project_with_no_progress_is_listed_once_it_started_more_than_fourteen_days_ago()
    {
        Assert.False(NeedsActionRules.IsProgressStale(null, Today.AddDays(-14), Today));
        Assert.True(NeedsActionRules.IsProgressStale(null, Today.AddDays(-15), Today));
        Assert.Equal("No progress recorded yet", Row(NeedsActionGroup.ProgressNotUpdated, Record(otherDate: Today.AddDays(-15))).Detail);
    }

    [Fact]
    public void A_bill_is_overdue_from_the_day_after_its_due_date()
    {
        Assert.False(NeedsActionRules.IsBillOverdue(Today, Today));
        Assert.True(NeedsActionRules.IsBillOverdue(Today.AddDays(-1), Today));

        var row = Row(NeedsActionGroup.BillsToChase, Record(number: "BILL-0003", party: "ACME Realty", date: Today.AddDays(-1), amount: 250_000_00));
        Assert.Equal(("BILL-0003, ACME Realty", "1 day overdue, due 1 Nov 2026", true, Today.AddDays(-1), 250_000_00L),
            (row.Label, row.Detail, row.IsUrgent, row.DueDate!.Value, row.AmountMinor!.Value));
    }

    [Fact]
    public void A_delivery_expected_today_is_not_late_and_one_expected_yesterday_is()
    {
        Assert.False(NeedsActionRules.IsDeliveryLate(Today, Today));
        Assert.True(NeedsActionRules.IsDeliveryLate(Today.AddDays(-1), Today));
        Assert.False(NeedsActionRules.IsDeliveryLate(null, Today));

        var today = NeedsActionRows.Delivery(Order(Today), Today);
        var late = NeedsActionRows.Delivery(Order(Today.AddDays(-1), "PartlyReceived"), Today);
        var undated = NeedsActionRows.Delivery(Order(null), Today);

        Assert.Equal(("PO-0007, Visayas Steel", "Expected today", false), (today.Label, today.Detail, today.IsUrgent));
        Assert.Equal(("1 day late, expected 1 Nov 2026, partly received", true), (late.Detail, late.IsUrgent));
        Assert.Equal(("No expected date", false), (undated.Detail, undated.IsUrgent));
        Assert.Equal("Expected 5 Nov 2026", NeedsActionRows.Delivery(Order(Today.AddDays(3)), Today).Detail);
        // A delivery row never carries money.
        Assert.All(new[] { today, late, undated }, row => Assert.Null(row.AmountMinor));
    }

    [Fact]
    public void Attendance_is_checked_for_yesterday_against_the_six_days_before()
    {
        var dates = new NeedsActionDates(Today);
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)),
            (dates.AttendanceDay, dates.AttendanceLookbackFrom, dates.AttendanceLookbackTo));

        Assert.Equal("4 workers have no attendance recorded for 1 Nov",
            Row(NeedsActionGroup.AttendanceNotRecorded, Record(date: dates.AttendanceDay, quantity: 4)).Detail);
        Assert.Equal("1 worker has no attendance recorded for 1 Nov",
            Row(NeedsActionGroup.AttendanceNotRecorded, Record(date: dates.AttendanceDay, quantity: 1)).Detail);
    }

    // ---- Wording ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Waiting_is_counted_in_days_on_the_company_calendar()
    {
        // 30 Oct 20:00 UTC is already 31 Oct in Manila: two days before 2 Nov there.
        var row = Row(NeedsActionGroup.PurchaseOrdersToApprove, Record(number: "PO-0001", party: "Visayas Steel",
            since: new DateTimeOffset(2026, 10, 30, 20, 0, 0, TimeSpan.Zero), amount: 1_120_00));
        Assert.Equal(("PO-0001, Visayas Steel", "Waiting 2 days", 1_120_00L), (row.Label, row.Detail, row.AmountMinor!.Value));

        Assert.Equal("Waiting 1 day", Row(NeedsActionGroup.ChangeOrdersToApprove,
            Record(number: "CO-002", party: "Extra footing", since: new DateTimeOffset(2026, 10, 31, 16, 30, 0, TimeSpan.Zero))).Detail);
        Assert.Equal("Waiting since today", Row(NeedsActionGroup.BillsToIssue,
            Record(kind: "Progress", since: new DateTimeOffset(2026, 11, 1, 16, 0, 0, TimeSpan.Zero))).Detail);
    }

    [Fact]
    public void Each_draft_names_its_document_and_who_it_concerns()
    {
        var since = new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);
        Assert.Equal("CO-002 Extra footing", Row(NeedsActionGroup.ChangeOrdersToApprove, Record(number: "CO-002", party: "Extra footing", since: since)).Label);
        Assert.Equal("Down payment bill for ACME Realty", Row(NeedsActionGroup.BillsToIssue, Record(kind: "Advance", party: "ACME Realty", since: since)).Label);
        Assert.Equal("Retention release bill", Row(NeedsActionGroup.BillsToIssue, Record(kind: "RetentionRelease", since: since)).Label);

        var subcontract = Guid.NewGuid();
        var claim = Row(NeedsActionGroup.ClaimsToCertify, Record(number: "SC-0004", party: "Masonry Crew", parent: subcontract, since: since, amount: 112_000_00));
        Assert.Equal(("Claim on SC-0004, Masonry Crew", subcontract, 112_000_00L), (claim.Label, claim.ParentId!.Value, claim.AmountMinor!.Value));
    }

    [Fact]
    public void A_payroll_says_its_period_or_that_it_is_thirteenth_month_pay_and_never_an_amount()
    {
        var since = new DateTimeOffset(2026, 10, 30, 2, 0, 0, TimeSpan.Zero);
        var regular = Row(NeedsActionGroup.PayrollToApprove, Record(kind: "Regular", date: new DateOnly(2026, 10, 16), otherDate: new DateOnly(2026, 10, 31),
            quantity: 12, since: since, amount: 999));
        var thirteenth = Row(NeedsActionGroup.PayrollToApprove, Record(kind: "ThirteenthMonth", date: new DateOnly(2026, 1, 1),
            otherDate: new DateOnly(2026, 12, 31), quantity: 1, since: since));

        Assert.Equal(("Payroll for 16 Oct - 31 Oct 2026", "12 workers, waiting 3 days", (long?)null), (regular.Label, regular.Detail, regular.AmountMinor));
        Assert.Equal(("13th-month pay for 2026", "13th-month pay, 1 worker, waiting 3 days"), (thirteenth.Label, thirteenth.Detail));
    }

    [Fact]
    public void The_statutory_row_names_the_missing_tables()
    {
        Assert.Equal("No table in force for withholding tax, so payroll can't work them out.", NeedsActionRows.Statutory(["withholding tax"]).Detail);
        Assert.Equal("No table in force for SSS, PhilHealth and withholding tax, so payroll can't work them out.",
            NeedsActionRows.Statutory(["SSS", "PhilHealth", "withholding tax"]).Detail);
    }

    // ---- Who receives what --------------------------------------------------------------------------------------------------

    [Fact]
    public void Each_role_receives_only_the_groups_it_can_act_on()
    {
        string[] Keys(AccessRole role) => NeedsActionGroups.For(role).Select(NeedsActionGroups.Key).ToArray();

        Assert.Equal(NeedsActionGroups.InOrder.Count, Keys(AccessRole.Administrator).Length);
        Assert.Equal(["purchase-orders-to-approve", "change-orders-to-approve", "bills-to-chase", "deliveries-expected", "bonds-to-attend",
            "progress-not-updated", "attendance-not-recorded"], Keys(AccessRole.ProjectManager));
        Assert.Equal(["purchase-orders-to-approve", "bills-to-issue", "claims-to-certify", "payroll-to-approve", "bills-to-chase", "deliveries-expected",
            "bonds-to-attend", "statutory-needs-setup"], Keys(AccessRole.Accountant));
        Assert.Equal(["deliveries-expected", "progress-not-updated"], Keys(AccessRole.CostEngineer));
        Assert.Equal(["attendance-not-recorded"], Keys(AccessRole.HrOfficer));
        Assert.Equal(["deliveries-expected"], Keys(AccessRole.WarehouseKeeper));
        Assert.Empty(Keys(AccessRole.Viewer));
        Assert.Empty(Keys(AccessRole.None));
        // Roles combine.
        Assert.Equal(["deliveries-expected", "progress-not-updated", "attendance-not-recorded"], Keys(AccessRole.CostEngineer | AccessRole.HrOfficer));
    }

    [Fact]
    public void Groups_with_an_urgent_row_come_first_and_otherwise_keep_the_page_order()
    {
        NeedsActionGroupDto Group(string key, bool urgent) =>
            new(key, 1, [new NeedsActionRowDto(Guid.NewGuid(), null, key, null, null, "", null, null, urgent, null)]);

        var ordered = NeedsActionGroups.Ordered([Group("a", false), Group("b", true), Group("c", false), Group("d", true)]);

        Assert.Equal(["b", "d", "a", "c"], ordered.Select(group => group.Key));
    }

    private static (string Detail, bool Urgent) Pair(NeedsActionRowDto row) => (row.Detail, row.IsUrgent);
}
