namespace ConstructOPX.Domain.Features.Progress;

/// <summary>
/// One statement of how far along a budget line is, on a business date. Entries are only ever added: a correction is a newer entry (and a
/// lower percent says why in its note), so the history of "who said what, when" is never lost. A line's current percent is its latest
/// entry (latest as-of date, then latest recorded time).
/// </summary>
public sealed class ProgressEntry
{
    public const int FullBps = 10_000;
    public const int MaximumNoteLength = 300;

    private ProgressEntry() { }

    internal ProgressEntry(Guid id, Guid tenantId, Guid projectId, Guid budgetLineId, DateOnly asOfDate, int percentCompleteBps, string? note,
        Guid recordedByUserId, string? clientRequestId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        BudgetLineId = budgetLineId;
        AsOfDate = asOfDate;
        PercentCompleteBps = percentCompleteBps;
        Note = note;
        RecordedByUserId = recordedByUserId;
        ClientRequestId = clientRequestId;
        RecordedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid BudgetLineId { get; private set; }

    /// <summary>The business date the percent was true on (not in the future, in the company time zone).</summary>
    public DateOnly AsOfDate { get; private set; }

    /// <summary>How much of the line's work is done, in basis points: 0 to 10,000 (100%).</summary>
    public int PercentCompleteBps { get; private set; }

    public string? Note { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    /// <summary>The request id of the batch this entry came from. Every entry of one batch shares it.</summary>
    public string? ClientRequestId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}
