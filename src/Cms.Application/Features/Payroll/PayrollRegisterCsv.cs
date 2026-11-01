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
        "Employee number", "Worker", "Designation", "Regular hours", "Overtime hours", "Night hours", "Regular pay", "Premium pay", "Overtime pay",
        "Night differential", "Holiday pay", "13th-month pay", "Gross pay", "Deductions", "Net pay", "Deduction details", "SSS employee",
        "SSS employer", "PhilHealth employee", "PhilHealth employer", "Pag-IBIG employee", "Pag-IBIG employer", "Withholding tax",
        "Employer contributions"
    ];

    /// <summary>The statutory columns, in order: each agency's employee share, then its employer share (tax has no employer share).</summary>
    private static readonly (string Agency, bool Employer)[] StatutoryColumns =
    [
        ("Sss", false), ("Sss", true), ("PhilHealth", false), ("PhilHealth", true), ("PagIbig", false), ("PagIbig", true), ("WithholdingTax", false)
    ];

    public static PayrollExportFile Build(PayrollRunDto run)
    {
        var text = new StringBuilder();
        AppendRow(text, Headers.Select(Quote));

        foreach (var line in run.Lines)
            AppendRow(text,
            [
                Text(line.EmployeeNumber), Text(line.WorkerName), Text(line.Designation), Hours(line.RegularHours), Hours(line.OvertimeHours),
                Hours(line.NightHours), Money(line.RegularPayMinor), Money(line.PremiumPayMinor), Money(line.OvertimePayMinor), Money(line.NightPayMinor),
                Money(line.HolidayPayMinor), Money(line.ThirteenthMonthPayMinor), Money(line.GrossPayMinor), Money(line.DeductionsMinor), Money(line.NetPayMinor),
                Text(string.Join("; ", line.Deductions.Select(deduction => $"{deduction.Description} {Money(deduction.AmountMinor)}"))),
                .. StatutoryColumns.Select(column => Money(Statutory([line], column.Agency, column.Employer))),
                Money(line.Deductions.Sum(deduction => deduction.EmployerShareMinor))
            ]);

        AppendRow(text,
        [
            Text("Total"), "", "", Hours(run.Lines.Sum(line => line.RegularHours)), Hours(run.Lines.Sum(line => line.OvertimeHours)),
            Hours(run.Lines.Sum(line => line.NightHours)), Money(run.Lines.Sum(line => line.RegularPayMinor)), Money(run.Lines.Sum(line => line.PremiumPayMinor)),
            Money(run.Lines.Sum(line => line.OvertimePayMinor)), Money(run.Lines.Sum(line => line.NightPayMinor)), Money(run.Lines.Sum(line => line.HolidayPayMinor)),
            Money(run.Lines.Sum(line => line.ThirteenthMonthPayMinor)), Money(run.TotalGrossMinor), Money(run.TotalDeductionsMinor), Money(run.TotalNetMinor), "",
            .. StatutoryColumns.Select(column => Money(Statutory(run.Lines, column.Agency, column.Employer))),
            Money(run.Lines.SelectMany(line => line.Deductions).Sum(deduction => deduction.EmployerShareMinor))
        ]);

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text.ToString());
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);

        var fileName = string.Create(CultureInfo.InvariantCulture, $"payroll-{run.PeriodStart:yyyy-MM-dd}-to-{run.PeriodEnd:yyyy-MM-dd}.csv");
        return new PayrollExportFile(fileName, "text/csv; charset=utf-8", content);
    }

    private static long Statutory(IEnumerable<PayrollLineDto> lines, string agency, bool employer) =>
        lines.SelectMany(line => line.Deductions)
            .Where(deduction => deduction.Source == "Statutory" && deduction.Agency == agency)
            .Sum(deduction => employer ? deduction.EmployerShareMinor : deduction.AmountMinor);

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
