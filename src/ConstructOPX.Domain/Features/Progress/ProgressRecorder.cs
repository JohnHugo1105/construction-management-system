using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Progress;

/// <summary>
/// What a person asked for on one line. <paramref name="ExpectedPercentBps"/> is the percent they were looking at when they changed it
/// (null when the caller does not say): if someone saved another figure meanwhile, the whole update is refused as stale.
/// </summary>
public sealed record ProgressChange(Guid BudgetLineId, int PercentCompleteBps, string? Note, int? ExpectedPercentBps);

/// <summary>A line's latest statement: how far along it is and the date that was said. A line never updated has none.</summary>
public readonly record struct LatestProgress(int PercentCompleteBps, DateOnly AsOfDate);

/// <summary>The outcome of checking a batch: the entries to save, and how many lines were left alone because nothing changed.</summary>
public sealed record ProgressBatchPlan(IReadOnlyList<ProgressEntry> Entries, int UnchangedLines);

/// <summary>
/// The rules of recording progress, for a batch of lines at once. The whole batch is accepted or refused together and every problem is
/// reported at once, keyed <c>lines[0].percentBps</c> (the position in the request), so a person fixes the form in one go.
/// </summary>
public static class ProgressRecorder
{
    public const int MaximumLinesPerBatch = 200;

    public static ProgressBatchPlan Plan(Guid tenantId, Project project, DateOnly asOfDate, DateOnly today, IReadOnlyList<ProgressChange>? changes,
        IReadOnlyDictionary<Guid, BudgetLine> activeLines, IReadOnlyDictionary<Guid, LatestProgress> latest, Guid recordedByUserId,
        string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureNotArchived();

        var errors = new ValidationErrors();
        if (asOfDate == default) errors.Add("asOfDate", "Choose the date this progress is true on.");
        else if (asOfDate > today) errors.Add("asOfDate", "Progress can't be dated in the future.");

        changes ??= [];
        if (changes.Count == 0) errors.Add("lines", "Change the progress of at least one line before saving.");
        if (changes.Count > MaximumLinesPerBatch) errors.Add("lines", $"Update at most {MaximumLinesPerBatch} lines at once.");
        errors.ThrowIfAny();

        // Someone else saved this line's progress after the person opened the form: say so, and change nothing.
        foreach (var change in changes)
        {
            if (!activeLines.ContainsKey(change.BudgetLineId)) continue;
            var seenPercent = latest.TryGetValue(change.BudgetLineId, out var found) ? found.PercentCompleteBps : 0;
            if (change.ExpectedPercentBps is { } expected && expected != seenPercent) throw new ConcurrencyConflictException();
        }

        var seen = new HashSet<Guid>();
        var planned = new List<ProgressEntry>(changes.Count);
        var unchanged = 0;
        DateOnly? latestUpdate = null;

        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            var prefix = $"lines[{index.ToString(CultureInfo.InvariantCulture)}].";

            if (!activeLines.ContainsKey(change.BudgetLineId))
            {
                errors.Add(prefix + "budgetLineId", "That budget line isn't part of this project any more. Reload the page and try again.");
                continue;
            }
            if (!seen.Add(change.BudgetLineId))
            {
                errors.Add(prefix + "budgetLineId", "This line is in the update twice. Keep one.");
                continue;
            }
            if (change.PercentCompleteBps is < 0 or > ProgressEntry.FullBps)
            {
                errors.Add(prefix + "percentBps", "Enter a percent between 0 and 100.");
                continue;
            }

            var hasCurrent = latest.TryGetValue(change.BudgetLineId, out var current);
            var currentBps = hasCurrent ? current.PercentCompleteBps : 0;
            if (change.PercentCompleteBps == currentBps)
            {
                unchanged++;
                continue;
            }

            string? note = null;
            try { note = Guard.OptionalText(change.Note, prefix + "note", "the note", ProgressEntry.MaximumNoteLength); }
            catch (ValidationFailedException ex) { errors.Add(prefix + "note", ex.Errors[prefix + "note"][0]); }
            if (change.PercentCompleteBps < currentBps && note is null)
                errors.Add(prefix + "note", "Say why the progress went down, for example rework.");
            if (hasCurrent && (latestUpdate is null || current.AsOfDate > latestUpdate)) latestUpdate = current.AsOfDate;

            planned.Add(new ProgressEntry(Guid.NewGuid(), tenantId, project.Id, change.BudgetLineId, asOfDate, change.PercentCompleteBps, note,
                recordedByUserId, clientRequestId, now));
        }

        // The current percent is the latest entry, so an update dated before a line's last one would be hidden behind it.
        if (latestUpdate is { } last && asOfDate != default && asOfDate < last)
            errors.Add("asOfDate", $"A line you changed was already updated on {last.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}. Use that date or a later one.");
        errors.ThrowIfAny();

        if (planned.Count == 0) throw new ValidationFailedException("lines", "Nothing changed. Change the progress of at least one line before saving.");
        return new ProgressBatchPlan(planned, unchanged);
    }
}
