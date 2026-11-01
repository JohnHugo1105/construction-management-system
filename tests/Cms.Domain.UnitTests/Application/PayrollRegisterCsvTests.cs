using System.Globalization;
using System.Text;
using Cms.Application.Features.Payroll;

namespace Cms.Domain.UnitTests.Application;

public sealed class PayrollRegisterCsvTests
{
    private static PayrollRunDto Run(params PayrollLineDto[] lines) => new(Guid.NewGuid(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), "Draft",
        lines.Length, lines.Sum(line => line.GrossPayMinor), lines.Sum(line => line.DeductionsMinor), lines.Sum(line => line.NetPayMinor), 8m, 12_500, null,
        DateTimeOffset.UnixEpoch, null, null, null, null, null, lines, 1);

    private static PayrollLineDto Line(string name, string number = "W-001", string designation = "Mason", long regular = 80_000, long overtime = 25_000,
        params (string, long)[] deductions)
    {
        var items = deductions.Select(item => new PayrollDeductionDto(Guid.NewGuid(), item.Item1, item.Item2)).ToArray();
        var deducted = items.Sum(item => item.AmountMinor);
        return new PayrollLineDto(Guid.NewGuid(), name, number, designation, 8m, 2m, regular, overtime, regular + overtime, deducted, regular + overtime - deducted,
            items, []);
    }

    private static string[] Lines(PayrollExportFile file) => new UTF8Encoding(false).GetString(file.Content[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void The_file_starts_with_a_UTF8_byte_order_mark_and_is_named_for_the_period()
    {
        var file = PayrollRegisterCsv.Build(Run(Line("Juan Dela Cruz")));

        Assert.Equal([0xEF, 0xBB, 0xBF], file.Content[..3]);
        Assert.Equal("payroll-2026-10-01-to-2026-10-15.csv", file.FileName);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void Amounts_are_plain_two_decimal_numbers_whatever_the_computers_language(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var lines = Lines(PayrollRegisterCsv.Build(Run(Line("Juan Dela Cruz", deductions: [("Cash advance", 20_050)]))));

            // 800.00 + 250.00 = 1,050.00 less 200.50. No thousands separator and always a dot.
            Assert.Equal("W-001,Juan Dela Cruz,Mason,8,2,800.00,250.00,1050.00,200.50,849.50,Cash advance 200.50,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00", lines[1]);
            Assert.Equal("Total,,,8,2,800.00,250.00,1050.00,200.50,849.50,,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00", lines[2]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Each_agency_has_an_employee_and_an_employer_column_and_the_employer_total_is_last()
    {
        static PayrollDeductionDto Statutory(string agency, long employee, long employer) =>
            new(Guid.NewGuid(), agency, employee, "Statutory", agency, employer, employee);
        var deductions = new[]
        {
            Statutory("Sss", 11_111, 22_222), Statutory("PhilHealth", 3_333, 3_333), Statutory("PagIbig", 1_000, 1_000), Statutory("WithholdingTax", 4_444, 0),
            new PayrollDeductionDto(Guid.NewGuid(), "Cash advance", 5_000)
        };
        var line = new PayrollLineDto(Guid.NewGuid(), "Juan Dela Cruz", "W-001", "Mason", 8m, 2m, 80_000, 25_000, 105_000, 24_888, 80_112, deductions, []);
        var lines = Lines(PayrollRegisterCsv.Build(Run(line, line)));

        Assert.EndsWith(",SSS employee,SSS employer,PhilHealth employee,PhilHealth employer,Pag-IBIG employee,Pag-IBIG employer,Withholding tax,Employer contributions",
            lines[0], StringComparison.Ordinal);
        // Employer: 222.22 + 33.33 + 10.00 = 265.55 a worker; two workers in the total.
        Assert.EndsWith(",111.11,222.22,33.33,33.33,10.00,10.00,44.44,265.55", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",222.22,444.44,66.66,66.66,20.00,20.00,88.88,531.10", lines[3], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://example.com\")", "\"'=HYPERLINK(\"\"http://example.com\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    public void A_text_cell_a_spreadsheet_could_run_as_a_formula_is_made_plain_text(string worker, string expectedCell)
    {
        var lines = Lines(PayrollRegisterCsv.Build(Run(Line(worker))));

        Assert.StartsWith($"W-001,{expectedCell},Mason,", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Commas_quotes_and_line_breaks_stay_inside_their_cell_and_ordinary_text_is_left_alone()
    {
        var file = PayrollRegisterCsv.Build(Run(Line("Dela Cruz, Juan \"JC\"", designation: "Mason\r\nLead")));
        var text = new UTF8Encoding(false).GetString(file.Content[3..]);

        Assert.Contains("\"Dela Cruz, Juan \"\"JC\"\"\"", text);
        Assert.Contains("\"Mason\r\nLead\"", text);
        Assert.Contains("W-001,", text);
    }
}
