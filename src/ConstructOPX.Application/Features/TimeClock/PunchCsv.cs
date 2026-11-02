using System.Globalization;
using System.Text.RegularExpressions;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>A row of an import file, as written: the row number in the file, the badge code, the time and the direction (if any).</summary>
public sealed record PunchCsvRow(int Row, string Code, string Time, string? Direction);

/// <summary>
/// Reads a time clock export: one scan per row, <c>code,timestamp[,direction]</c>, with an optional header row starting with "code". The file is read
/// in memory and never stored. A time with an offset (2026-11-02T07:02:00+08:00, or Z) is taken as written; a time without one
/// (2026-11-02 07:02) is the company's local time.
/// </summary>
public static partial class PunchCsv
{
    /// <summary>The largest file read: about 1 MB of text, far more than 5,000 rows need.</summary>
    public const int MaximumCharacters = 1_000_000;

    public static IReadOnlyList<PunchCsvRow> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ValidationFailedException("file", "The file has no rows. Each row needs a badge code and a time.");
        if (text.Length > MaximumCharacters) throw new ValidationFailedException("file", "The file is too large. Import at most 5,000 rows at a time.");

        var rows = new List<PunchCsvRow>();
        var lines = text.Split('\n');
        var headerChecked = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = line.Split(',').Select(Clean).ToArray();
            if (!headerChecked)
            {
                headerChecked = true;
                if (string.Equals(fields[0], "code", StringComparison.OrdinalIgnoreCase)) continue;
            }

            if (rows.Count == TimeClockRules.MaximumImportRows)
                throw new ValidationFailedException("file", "A file can have at most 5,000 rows. Split it and import each part.");
            rows.Add(new PunchCsvRow(index + 1, fields[0], fields.Length > 1 ? fields[1] : "", fields.Length > 2 && fields[2].Length > 0 ? fields[2] : null));
        }

        if (rows.Count == 0) throw new ValidationFailedException("file", "The file has no rows. Each row needs a badge code and a time.");
        return rows;
    }

    /// <summary>The instant a row's time means, or null when it can't be read (no time of day, or not a date).</summary>
    public static DateTimeOffset? ParseTime(string text, TimeZoneInfo zone)
    {
        var value = text.Trim();
        if (value.Length is 0 or > 40 || !value.Contains(':')) return null;
        if (Offset().IsMatch(value))
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var stamped) ? stamped : null;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        try
        {
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            // A wall-clock time that doesn't exist in the zone (the hour skipped when clocks go forward).
            return null;
        }
    }

    private static string Clean(string field)
    {
        var value = field.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1].Trim() : value;
    }

    [GeneratedRegex(@"(?:[zZ]|[+-]\d{2}:?\d{2})$")]
    private static partial Regex Offset();
}
