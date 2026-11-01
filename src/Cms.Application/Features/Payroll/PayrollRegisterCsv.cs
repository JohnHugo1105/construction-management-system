using System.Globalization;
using System.Text;

namespace Cms.Application.Features.Payroll;

/// <summary>
/// The payroll register as a CSV that opens correctly in Excel: UTF-8 with a byte-order mark, amounts as plain two-decimal numbers
/// written the same way on every computer, and no cell that a spreadsheet could run as a formula.
/// </summary>
public static class PayrollRegisterCsv
{
    private static readonly string[] Headers =
    [
        "Employee number", "Worker", "Designation", "Regular hours", "Overtime hours", "Regular pay", "Overtime pay", "Gross pay", "Deductions",
        "Net pay", "Deduction details"
    ];

    public static PayrollExportFile Build(PayrollRunDto run)
    {
        var text = new StringBuilder();
        AppendRow(text, Headers.Select(Quote));

        foreach (var line in run.Lines)
            AppendRow(text,
            [
                Text(line.EmployeeNumber), Text(line.WorkerName), Text(line.Designation), Hours(line.RegularHours), Hours(line.OvertimeHours),
                Money(line.RegularPayMinor), Money(line.OvertimePayMinor), Money(line.GrossPayMinor), Money(line.DeductionsMinor), Money(line.NetPayMinor),
                Text(string.Join("; ", line.Deductions.Select(deduction => $"{deduction.Description} {Money(deduction.AmountMinor)}")))
            ]);

        AppendRow(text,
        [
            Text("Total"), "", "", Hours(run.Lines.Sum(line => line.RegularHours)), Hours(run.Lines.Sum(line => line.OvertimeHours)),
            Money(run.Lines.Sum(line => line.RegularPayMinor)), Money(run.Lines.Sum(line => line.OvertimePayMinor)), Money(run.TotalGrossMinor),
            Money(run.TotalDeductionsMinor), Money(run.TotalNetMinor), ""
        ]);

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text.ToString());
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);

        var fileName = string.Create(CultureInfo.InvariantCulture, $"payroll-{run.PeriodStart:yyyy-MM-dd}-to-{run.PeriodEnd:yyyy-MM-dd}.csv");
        return new PayrollExportFile(fileName, "text/csv; charset=utf-8", content);
    }

    /// <summary>1,050.00 pesos is written 1050.00: no thousands separator, a dot for decimals, whatever the computer's language.</summary>
    internal static string Money(long minor) => (minor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Hours(decimal hours) => hours.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// A text cell. One that begins with = + - @ or a control character would be run as a formula by a spreadsheet (a worker named
    /// "=HYPERLINK(...)" would become a link), so a leading apostrophe makes it plain text.
    /// </summary>
    internal static string Text(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n') text = "'" + text;
        return Quote(text);
    }

    private static string Quote(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;

    private static void AppendRow(StringBuilder builder, IEnumerable<string> cells) => builder.Append(string.Join(',', cells)).Append("\r\n");
}
