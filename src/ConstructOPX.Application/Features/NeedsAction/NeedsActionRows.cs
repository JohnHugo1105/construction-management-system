using System.Globalization;
using ConstructOPX.Application.Features.Payroll;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Purchasing;

namespace ConstructOPX.Application.Features.NeedsAction;

/// <summary>
/// Puts each waiting record into plain words: a label (the document number and who it concerns), a detail saying why it waits, and whether
/// it is urgent. Every day count is judged on the company's calendar day. What each group reads from a <see cref="NeedsActionRecord"/>:
/// <list type="bullet">
/// <item>Purchase order: Number, Party (supplier), SinceUtc (drafted), AmountMinor (total with VAT).</item>
/// <item>Change order: Number, Party (title), SinceUtc (submitted), AmountMinor (the budget change with VAT).</item>
/// <item>Bill to issue: Kind, Party (client), SinceUtc (drafted), AmountMinor (net receivable).</item>
/// <item>Claim: ParentId (subcontract), Number (subcontract's), Party (subcontractor), SinceUtc (drafted), AmountMinor (claim's gross).</item>
/// <item>Payroll: Kind, Date and OtherDate (the period), Quantity (workers), SinceUtc (drafted). No amount.</item>
/// <item>Bill to chase: Number, Party (client), Date (due), AmountMinor (outstanding).</item>
/// <item>Bond: Number, Party (issuer), Kind, Date (valid until), AmountMinor (bond amount).</item>
/// <item>Progress: Id and project (the project), Date (latest progress, or null), OtherDate (start).</item>
/// <item>Attendance: Id and project (the project), Date (the day), Quantity (workers without an entry).</item>
/// </list>
/// </summary>
public static class NeedsActionRows
{
    public static NeedsActionRowDto From(NeedsActionGroup group, NeedsActionRecord record, DateOnly today, Func<DateTimeOffset, DateOnly> localDate)
    {
        var waiting = Waiting(record.SinceUtc, today, localDate);
        return group switch
        {
            NeedsActionGroup.PurchaseOrdersToApprove => Row(record, Join(record.Number, record.Party), Capital(waiting), record.AmountMinor),
            NeedsActionGroup.ChangeOrdersToApprove => Row(record, string.Join(' ', new[] { record.Number, record.Party }.Where(Present)), Capital(waiting),
                record.AmountMinor),
            NeedsActionGroup.BillsToIssue => Row(record, BillKind(record.Kind) + (Present(record.Party) ? $" for {record.Party}" : ""), Capital(waiting),
                record.AmountMinor),
            NeedsActionGroup.ClaimsToCertify => Row(record, Join($"Claim on {record.Number}", record.Party), Capital(waiting), record.AmountMinor),
            NeedsActionGroup.PayrollToApprove => Payroll(record, waiting),
            NeedsActionGroup.BillsToChase => Chase(record, today),
            NeedsActionGroup.BondsToAttend => BondRow(record, today),
            NeedsActionGroup.ProgressNotUpdated => Row(record, record.ProjectName ?? "", record.Date is { } latest
                ? $"Progress not updated for {Days(NeedsActionRules.DaysBetween(latest, today))}"
                : "No progress recorded yet", null),
            NeedsActionGroup.AttendanceNotRecorded => Row(record, record.ProjectName ?? "",
                $"{Workers(record.Quantity ?? 0)} {(record.Quantity == 1 ? "has" : "have")} no attendance recorded for " +
                (record.Date ?? NeedsActionRules.AttendanceDay(today)).ToString("d MMM", CultureInfo.InvariantCulture), null),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "This group is not read from records.")
        };
    }

    /// <summary>An order still to be delivered, from the quantity-only receiving read model: never an amount.</summary>
    public static NeedsActionRowDto Delivery(ReceivingOrderListItemDto order, DateOnly today)
    {
        var late = NeedsActionRules.IsDeliveryLate(order.ExpectedDate, today);
        var when = order.ExpectedDate switch
        {
            null => "No expected date",
            { } expected when late => $"{Days(NeedsActionRules.DaysBetween(expected, today))} late, expected {Day(expected)}",
            { } expected when expected == today => "Expected today",
            { } expected => $"Expected {Day(expected)}"
        };
        if (order.DeliveryState == nameof(DeliveryState.PartlyReceived)) when += ", partly received";
        return new NeedsActionRowDto(order.Id, null, Join(order.Number, order.SupplierName), null,
            order.SubProjectName is null ? order.ProjectName : $"{order.ProjectName}, {order.SubProjectName}", when, null, order.ExpectedDate, late, null);
    }

    /// <summary>The one row of the statutory group: which tables are missing, in the payroll order.</summary>
    public static NeedsActionRowDto Statutory(IReadOnlyList<string> agenciesNeedingSetup) =>
        new(Guid.Empty, null, "Contribution and tax tables", null, null,
            $"No table in force for {List(agenciesNeedingSetup)}, so payroll can't work them out.", null, null, false, null);

    private static NeedsActionRowDto Row(NeedsActionRecord record, string label, string detail, long? amountMinor, DateOnly? dueDate = null,
        bool urgent = false) =>
        new(record.Id, record.ParentId, label, record.ProjectId, record.ProjectName, detail, record.SinceUtc, dueDate, urgent, amountMinor);

    private static NeedsActionRowDto Payroll(NeedsActionRecord record, string waiting)
    {
        var start = record.Date ?? default;
        var end = record.OtherDate ?? start;
        var thirteenth = record.Kind == "ThirteenthMonth";
        var label = thirteenth ? $"13th-month pay for {start.Year}" : $"Payroll for {PayrollService.Period(start, end)}";
        var detail = string.Join(", ", new[] { thirteenth ? "13th-month pay" : null, Workers(record.Quantity ?? 0), waiting }.Where(Present));
        return Row(record, label, detail, null);
    }

    private static NeedsActionRowDto Chase(NeedsActionRecord record, DateOnly today)
    {
        var due = record.Date ?? today;
        var overdue = NeedsActionRules.IsBillOverdue(due, today);
        var detail = overdue ? $"{Days(NeedsActionRules.DaysBetween(due, today))} overdue, due {Day(due)}" : $"Due {Day(due)}";
        return Row(record, Join(record.Number, record.Party), detail, record.AmountMinor, due, overdue);
    }

    private static NeedsActionRowDto BondRow(NeedsActionRecord record, DateOnly today)
    {
        var validUntil = record.Date ?? today;
        var daysLeft = Bond.DaysLeft(validUntil, today);
        var detail = daysLeft switch
        {
            < 0 => $"Expired {Days(-daysLeft)} ago, on {Day(validUntil)}",
            0 => "Expires today",
            1 => "Expires tomorrow",
            _ => $"Expires in {Days(daysLeft)}, on {Day(validUntil)}"
        };
        var kind = record.Kind switch
        {
            nameof(BondKind.Bid) => "Bid bond",
            nameof(BondKind.Performance) => "Performance bond",
            nameof(BondKind.AdvancePayment) => "Advance payment bond",
            nameof(BondKind.Warranty) => "Warranty bond",
            _ => "Bond"
        };
        return Row(record, Join($"{kind} {record.Number}".Trim(), record.Party), detail, record.AmountMinor, validUntil,
            Bond.ExpiryFor(validUntil, today) == BondExpiry.Expired);
    }

    /// <summary>"waiting since today", "waiting 1 day", "waiting 3 days" (lower case, to sit inside a sentence).</summary>
    private static string Waiting(DateTimeOffset? sinceUtc, DateOnly today, Func<DateTimeOffset, DateOnly> localDate)
    {
        if (sinceUtc is not { } since) return "waiting";
        var days = NeedsActionRules.DaysBetween(localDate(since), today);
        return days <= 0 ? "waiting since today" : $"waiting {Days(days)}";
    }

    private static string BillKind(string? kind) => kind switch
    {
        "RetentionRelease" => "Retention release bill",
        "Advance" => "Down payment bill",
        _ => "Progress bill"
    };

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    private static string Workers(int count) => count == 1 ? "1 worker" : $"{count} workers";

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Join(string? first, string? second) => string.Join(", ", new[] { first, second }.Where(Present));

    private static bool Present(string? text) => !string.IsNullOrWhiteSpace(text);

    private static string List(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}"
    };
}
