using System.Text.Json.Serialization;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Application.Features.NeedsAction;

// "Needs your action": a per-person queue of the work waiting on the signed-in person, read from statuses that already exist. Nothing is
// stored, sent or scheduled. Which groups a person receives is decided on the server from their own permissions: see docs/business-rules.md,
// "Needs your action", and docs/api/needs-action.md.

// ---- Groups -----------------------------------------------------------------------------------------------------------------

/// <summary>The kinds of waiting work, in the order the page lists them when none is urgent.</summary>
public enum NeedsActionGroup
{
    PurchaseOrdersToApprove,
    ChangeOrdersToApprove,
    BillsToIssue,
    ClaimsToCertify,
    PayrollToApprove,
    BillsToChase,
    DeliveriesExpected,
    BondsToAttend,
    ProgressNotUpdated,
    AttendanceNotRecorded,
    StatutoryNeedsSetup
}

// ---- Ports ------------------------------------------------------------------------------------------------------------------

/// <summary>
/// Reads the waiting work of the signed-in organization, one group at a time, straight from the records' statuses. Every group is a count
/// and, separately, its most urgent rows: one SQL statement each, however many records exist. Deliveries and the statutory tables are not
/// here: they reuse the receiving read model and the statutory status.
/// </summary>
public interface INeedsActionReader
{
    Task<int> CountAsync(NeedsActionGroup group, NeedsActionDates dates, CancellationToken cancellationToken);

    Task<IReadOnlyList<NeedsActionRecord>> TopAsync(NeedsActionGroup group, NeedsActionDates dates, int take, CancellationToken cancellationToken);
}

/// <summary>
/// What a reader selects for one waiting record, before it is put into words. Each group fills the fields it has (see
/// <see cref="NeedsActionRows"/> for what each one means per group); nothing else is ever read.
/// </summary>
/// <param name="Id">The record the row opens (for a progress or attendance row, the project).</param>
/// <param name="ParentId">The record whose page holds this one: the subcontract of a claim.</param>
/// <param name="Number">The document number (PO-0001, CO-001, BILL-0001, a bond number, a subcontract's number).</param>
/// <param name="Party">The firm, client, issuer or title the record concerns.</param>
/// <param name="Kind">The record's own kind, where it has one (a bill's kind, a bond's kind, a payroll's kind).</param>
/// <param name="SinceUtc">When it started waiting (drafted or submitted).</param>
/// <param name="Date">The date that decides urgency or the label: a due date, a valid-until date, the latest progress, a period start.</param>
/// <param name="OtherDate">A second date: a project's start, a period end.</param>
/// <param name="AmountMinor">The one amount the group shows, or null for a group that shows none.</param>
/// <param name="Quantity">A count: the workers of a payroll, the workers without attendance.</param>
public sealed record NeedsActionRecord(Guid Id, Guid? ParentId, string? Number, string? Party, string? Kind, Guid? ProjectId, string? ProjectName,
    DateTimeOffset? SinceUtc, DateOnly? Date, DateOnly? OtherDate, long? AmountMinor, int? Quantity);

/// <summary>
/// A group of waiting work contributed by another feature (tasks, the site diary, documents, the schedule), so that feature owns its rule and
/// this page owns only the shape. Implementations are classes named <c>*NeedsActionSource</c> in Infrastructure and are registered
/// automatically. A source says who may act on its group, counts it, and reads its most urgent rows. It must be set-based (at most two SQL
/// statements: the count, and the rows only when the count is above zero), must stay inside the company filter, and must never put an amount in
/// a row for someone who may not see it (leave <see cref="NeedsActionRowDto.AmountMinor"/> null). Its key must also be added to the web page
/// (<c>needs-action.models.ts</c> and <c>needs-action-presentation.ts</c>).
/// </summary>
public interface INeedsActionSource
{
    /// <summary>The group's key, for example "tasks-assigned".</summary>
    string Key { get; }

    /// <summary>True when the roles hold the permission that acts on the group. A group a person cannot act on is not read and not returned.</summary>
    bool CanActOn(AccessRole roles);

    Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken);

    /// <summary>The group with its true count and at most <paramref name="take"/> of its most urgent rows.</summary>
    Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken);
}

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

/// <summary>The caller's groups, urgent groups first. Only groups the caller may act on are present, even when they hold nothing.</summary>
public sealed record NeedsActionDto(IReadOnlyList<NeedsActionGroupDto> Groups);

/// <param name="Key">"purchase-orders-to-approve" and so on (see <see cref="NeedsActionGroups.Key"/>).</param>
/// <param name="Count">How many records wait in all; <paramref name="Rows"/> holds at most the eight most urgent.</param>
public sealed record NeedsActionGroupDto(string Key, int Count, IReadOnlyList<NeedsActionRowDto> Rows);

/// <summary>
/// One waiting record in plain words. The server returns ids, never web routes: the page maps the group and the ids to the record's page.
/// <paramref name="AmountMinor"/> is left out of the JSON entirely for a group that carries no amount (payroll, deliveries, progress,
/// attendance, the statutory tables), so a person who may not see prices never receives an amount property.
/// </summary>
/// <param name="Id">The record to open; empty for the statutory tables row (the page is the company's tables).</param>
/// <param name="ParentId">The subcontract of a claim (the claim is certified on its subcontract's page).</param>
/// <param name="Detail">"Waiting 3 days", "5 days overdue", "Expires in 12 days": the reason it is waiting, already worded.</param>
/// <param name="IsUrgent">Overdue, late or expired.</param>
public sealed record NeedsActionRowDto(Guid Id, Guid? ParentId, string Label, Guid? ProjectId, string? ProjectName, string Detail, DateTimeOffset? SinceUtc,
    DateOnly? DueDate, bool IsUrgent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? AmountMinor);

/// <summary>The nav badge: how many records wait on the caller in all (the sum of the groups' counts).</summary>
public sealed record NeedsActionCountDto(int Total);
