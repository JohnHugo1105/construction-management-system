using System.Buffers;
using System.Globalization;
using System.Text;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>
/// Builds the CSV files people open in Excel. Rules that matter for money and safety:
/// UTF-8 with a byte-order mark (so Excel reads accents correctly), invariant culture (a decimal point and ISO dates whatever the server's
/// language), amounts as plain decimals with two places worked out from whole minor units (no floating point), and protection against
/// formula injection: a text cell that starts with <c>= + - @</c> (or a tab or carriage return) gets a leading apostrophe, so a supplier
/// named <c>=HYPERLINK(...)</c> is shown as text and never run.
/// </summary>
public static class CsvExport
{
    public const string ContentType = "text/csv; charset=utf-8";

    private const int MaximumFileNamePartLength = 60;

    // ---- Cell formatting ----------------------------------------------------------------------------------------------------

    /// <summary>A text cell, made safe against formula injection. Quoting for commas and line breaks happens when the row is written.</summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    /// <summary>Minor units as a plain decimal with two places: 125050 becomes 1250.50 and -5 becomes -0.05. Never protected: it is a number.</summary>
    public static string Money(long minor) => Fixed2(minor);

    /// <summary>Basis points as a percentage with two places: 1200 becomes 12.00. Empty when there is no figure.</summary>
    public static string Percent(int? basisPoints) => basisPoints is null ? "" : Fixed2(basisPoints.Value);

    public static string Quantity(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Fixed2(long value)
    {
        // Work on the magnitude as unsigned so long.MinValue can't overflow, and format integers only.
        var magnitude = value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
        var sign = value < 0 ? "-" : "";
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{magnitude / 100UL}.{magnitude % 100UL:00}");
    }

    // ---- File name ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>Tower-A-budget-2026-11-02.csv</c>. The project name keeps only plain letters and digits (accents are dropped, everything else
    /// becomes a dash), so the name is safe in a download header and on every operating system.
    /// </summary>
    public static string FileName(string? projectName, string kind, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{SafeNamePart(projectName)}-{kind}-{date:yyyy-MM-dd}.csv");

    public static string SafeNamePart(string? text)
    {
        var builder = new StringBuilder();
        foreach (var character in (text ?? "").Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(character)) builder.Append(character);
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                     (builder.Length == 0 || builder[^1] != '-')) builder.Append('-');
        }

        var safe = builder.ToString().Trim('-');
        if (safe.Length > MaximumFileNamePartLength) safe = safe[..MaximumFileNamePartLength].Trim('-');
        return safe.Length == 0 ? "project" : safe;
    }

    // ---- Document -----------------------------------------------------------------------------------------------------------

    /// <summary>Collects rows as UTF-8 bytes while they are produced, so a large export is never held as one big string.</summary>
    public sealed class Document
    {
        private readonly MemoryStream _stream = new();
        private readonly StreamWriter _writer;

        public Document(params string[] headers)
        {
            // true: write the byte-order mark.
            _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 16 * 1024, leaveOpen: true) { NewLine = "\r\n" };
            Row(headers);
        }

        /// <summary>Writes one row of cells already formatted with <see cref="Text"/>, <see cref="Money"/> and the other helpers.</summary>
        public void Row(params string[] cells) => _writer.WriteLine(string.Join(',', cells.Select(Quote)));

        public byte[] ToArray()
        {
            _writer.Flush();
            return _stream.ToArray();
        }

        private static readonly SearchValues<char> NeedsQuotes = SearchValues.Create(",\"\r\n");

        private static string Quote(string cell) =>
            cell.AsSpan().IndexOfAny(NeedsQuotes) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }
}
