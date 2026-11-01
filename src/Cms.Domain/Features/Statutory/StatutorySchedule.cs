using System.Globalization;
using Cms.Domain.Common;

namespace Cms.Domain.Features.Statutory;

/// <summary>The government agency a contribution or tax is paid to. The order is the order deductions are taken from pay.</summary>
public enum StatutoryAgency
{
    Sss = 0,
    PhilHealth = 1,
    PagIbig = 2,
    WithholdingTax = 3
}

/// <summary>How a table turns a base amount into the shares.</summary>
public enum StatutoryMethod
{
    /// <summary>Rows of base ranges, each with a fixed employee and employer amount.</summary>
    Brackets = 0,

    /// <summary>A rate of the base for each share, with the base held between a floor and a ceiling and optional limits on each share.</summary>
    Percentage = 1,

    /// <summary>Rows of taxable ranges, each with a base tax and a rate on the excess over the start of the range.</summary>
    TaxBrackets = 2
}

/// <summary>Whether a table's amounts are for a month or for one pay run.</summary>
public enum StatutoryPeriodBasis
{
    Monthly = 0,
    PerRun = 1
}

public enum StatutoryScheduleStatus
{
    Active = 0,
    Retired = 1
}

/// <summary>Names and order of the agencies, in one place.</summary>
public static class StatutoryAgencies
{
    /// <summary>The order deductions are taken (and limited) from pay: contributions first, then tax, which is worked out after them.</summary>
    public static readonly IReadOnlyList<StatutoryAgency> InOrder =
        [StatutoryAgency.Sss, StatutoryAgency.PhilHealth, StatutoryAgency.PagIbig, StatutoryAgency.WithholdingTax];

    public static string Label(StatutoryAgency agency) => agency switch
    {
        StatutoryAgency.Sss => "SSS",
        StatutoryAgency.PhilHealth => "PhilHealth",
        StatutoryAgency.PagIbig => "Pag-IBIG",
        StatutoryAgency.WithholdingTax => "Withholding tax",
        _ => agency.ToString()
    };

    /// <summary>The description of the deduction on a payslip and in the payroll register.</summary>
    public static string DeductionDescription(StatutoryAgency agency) =>
        agency == StatutoryAgency.WithholdingTax ? "Withholding tax" : $"{Label(agency)} contribution";

    public static bool IsContribution(StatutoryAgency agency) => agency != StatutoryAgency.WithholdingTax;
}

/// <summary>One row of a bracket table as a person entered it. Fields that the table's method doesn't use are ignored.</summary>
public sealed record StatutoryRowInput(long FromMinor, long? ToMinor, long EmployeeMinor, long EmployerMinor, long BaseTaxMinor, int RateBps);

/// <summary>The figures of a percentage table.</summary>
public sealed record StatutoryPercentageInput(int EmployeeRateBps, int EmployerRateBps, long BaseFloorMinor, long? BaseCeilingMinor,
    long? EmployeeMinimumMinor, long? EmployeeMaximumMinor, long? EmployerMinimumMinor, long? EmployerMaximumMinor);

/// <summary>
/// One row of a bracket table. A row covers amounts from <see cref="FromMinor"/> up to, but not including, <see cref="ToMinor"/>; the
/// next row starts exactly where this one ends, and the last row has no end, so every amount falls in exactly one row.
/// </summary>
public sealed class StatutoryScheduleRow
{
    private StatutoryScheduleRow() { }

    internal StatutoryScheduleRow(Guid id, Guid tenantId, Guid scheduleId, int rowNumber, long fromMinor, long? toMinor, long employeeMinor,
        long employerMinor, long baseTaxMinor, int rateBps)
    {
        Id = id;
        TenantId = tenantId;
        ScheduleId = scheduleId;
        RowNumber = rowNumber;
        FromMinor = fromMinor;
        ToMinor = toMinor;
        EmployeeMinor = employeeMinor;
        EmployerMinor = employerMinor;
        BaseTaxMinor = baseTaxMinor;
        RateBps = rateBps;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public int RowNumber { get; private set; }
    public long FromMinor { get; private set; }
    public long? ToMinor { get; private set; }
    public long EmployeeMinor { get; private set; }
    public long EmployerMinor { get; private set; }
    public long BaseTaxMinor { get; private set; }
    public int RateBps { get; private set; }

    public bool Contains(long amountMinor) => amountMinor >= FromMinor && (ToMinor is null || amountMinor < ToMinor);
}

/// <summary>
/// A contribution or tax table a person entered from the official schedule, in force from <see cref="EffectiveFrom"/> until a later
/// table of the same agency takes over. Rates and brackets change by law, so nothing here is built in: every figure is data.
/// A table used by an approved payroll is never changed (that payroll must stay explainable); a new table with a later date replaces it.
/// </summary>
public sealed class StatutorySchedule : AggregateRoot
{
    public const int MaximumRows = 100;
    public const int MaximumRateBps = 10_000;

    private readonly List<StatutoryScheduleRow> _rows = [];

    private StatutorySchedule() { }

    public StatutoryAgency Agency { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly EffectiveFrom { get; private set; }
    public StatutoryScheduleStatus Status { get; private set; } = StatutoryScheduleStatus.Active;
    public StatutoryMethod Method { get; private set; }
    public StatutoryPeriodBasis PeriodBasis { get; private set; }

    public int? EmployeeRateBps { get; private set; }
    public int? EmployerRateBps { get; private set; }
    public long? BaseFloorMinor { get; private set; }
    public long? BaseCeilingMinor { get; private set; }
    public long? EmployeeMinimumMinor { get; private set; }
    public long? EmployeeMaximumMinor { get; private set; }
    public long? EmployerMinimumMinor { get; private set; }
    public long? EmployerMaximumMinor { get; private set; }

    public string? ClientRequestId { get; private set; }
    public Guid? RetiredByUserId { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public IReadOnlyList<StatutoryScheduleRow> Rows => _rows;

    public bool IsActive => Status == StatutoryScheduleStatus.Active;

    public static StatutorySchedule Create(Guid id, Guid tenantId, StatutoryAgency agency, string? name, DateOnly effectiveFrom, StatutoryMethod method,
        StatutoryPeriodBasis periodBasis, StatutoryPercentageInput? percentage, IReadOnlyList<StatutoryRowInput>? rows, string? clientRequestId,
        DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        if (!Enum.IsDefined(agency)) errors.Add("agency", "Choose the agency: SSS, PhilHealth, Pag-IBIG or withholding tax.");
        string? requestId = null;
        try { requestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80); }
        catch (ValidationFailedException ex) { Merge(errors, ex); }

        var schedule = new StatutorySchedule { Agency = agency, ClientRequestId = requestId };
        schedule.Apply(errors, name, effectiveFrom, method, periodBasis, percentage, rows, tenantId, id);
        errors.ThrowIfAny();
        schedule.Initialize(id, tenantId, now);
        return schedule;
    }

    /// <summary>Changes the table. The caller first makes sure no approved payroll used it.</summary>
    public void Update(string? name, DateOnly effectiveFrom, StatutoryMethod method, StatutoryPeriodBasis periodBasis, StatutoryPercentageInput? percentage,
        IReadOnlyList<StatutoryRowInput>? rows, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainRuleException("A retired table can't be changed. Add a new table instead.");
        var errors = new ValidationErrors();
        Apply(errors, name, effectiveFrom, method, periodBasis, percentage, rows, TenantId, Id);
        errors.ThrowIfAny();
        Touch(now);
    }

    /// <summary>Stops the table applying to new calculations. Payrolls already calculated with it keep their amounts.</summary>
    public void Retire(Guid retiredByUserId, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainRuleException("This table is already retired.");
        Status = StatutoryScheduleStatus.Retired;
        RetiredByUserId = retiredByUserId;
        RetiredAtUtc = now;
        Touch(now);
    }

    /// <summary>The employee's and the employer's share for a base amount (a contribution table).</summary>
    public (long EmployeeMinor, long EmployerMinor) Contribution(long baseMinor)
    {
        if (Method == StatutoryMethod.Percentage)
        {
            var clamped = Math.Max(baseMinor, BaseFloorMinor ?? 0);
            if (BaseCeilingMinor is { } ceiling) clamped = Math.Min(clamped, ceiling);
            var employee = Clamp(Share(clamped, EmployeeRateBps ?? 0), EmployeeMinimumMinor, EmployeeMaximumMinor);
            var employer = Clamp(Share(clamped, EmployerRateBps ?? 0), EmployerMinimumMinor, EmployerMaximumMinor);
            return (employee, employer);
        }

        if (Method != StatutoryMethod.Brackets) throw new InvalidOperationException("A tax table has no contribution.");
        var row = RowFor(baseMinor);
        return (row.EmployeeMinor, row.EmployerMinor);
    }

    /// <summary>The tax on a taxable amount: the row's base tax plus its rate on the excess over the row's start, never negative.</summary>
    public long Tax(long taxableMinor)
    {
        if (Method != StatutoryMethod.TaxBrackets) throw new InvalidOperationException("Only a tax table works out tax.");
        var taxable = Math.Max(0, taxableMinor);
        var row = RowFor(taxable);
        return Math.Max(0, checked(row.BaseTaxMinor + Share(taxable - row.FromMinor, row.RateBps)));
    }

    private StatutoryScheduleRow RowFor(long amountMinor) =>
        _rows.OrderBy(row => row.RowNumber).FirstOrDefault(row => row.Contains(amountMinor))
        ?? throw new InvalidOperationException("The table has no row for that amount.");

    /// <summary>A share of an amount at a rate in basis points, rounded once, half away from zero.</summary>
    private static long Share(long amountMinor, int rateBps) =>
        checked((long)decimal.Round(amountMinor * (decimal)rateBps / 10_000m, 0, MidpointRounding.AwayFromZero));

    private static long Clamp(long value, long? minimum, long? maximum)
    {
        if (minimum is { } low && value < low) value = low;
        if (maximum is { } high && value > high) value = high;
        return value;
    }

    private void Apply(ValidationErrors errors, string? name, DateOnly effectiveFrom, StatutoryMethod method, StatutoryPeriodBasis periodBasis,
        StatutoryPercentageInput? percentage, IReadOnlyList<StatutoryRowInput>? rows, Guid tenantId, Guid id)
    {
        string cleanName = "";
        try { cleanName = Guard.RequiredText(name, "name", "a name for the table (for example the circular it comes from)", 120); }
        catch (ValidationFailedException ex) { Merge(errors, ex); }
        if (effectiveFrom == default) errors.Add("effectiveFrom", "Choose the date the table takes effect.");
        if (!Enum.IsDefined(periodBasis)) errors.Add("periodBasis", "Choose whether the table's amounts are per month or per pay run.");

        if (!Enum.IsDefined(method)) errors.Add("method", "Choose how the table works: brackets, a percentage, or tax brackets.");
        else if (Enum.IsDefined(Agency))
        {
            if (Agency == StatutoryAgency.WithholdingTax && method != StatutoryMethod.TaxBrackets)
                errors.Add("method", "Withholding tax uses tax brackets.");
            else if (Agency != StatutoryAgency.WithholdingTax && method == StatutoryMethod.TaxBrackets)
                errors.Add("method", "Tax brackets are only for withholding tax. Choose brackets or a percentage.");
        }

        var cleanRows = new List<StatutoryRowInput>();
        if (method == StatutoryMethod.Percentage) ValidatePercentage(errors, percentage);
        else if (method is StatutoryMethod.Brackets or StatutoryMethod.TaxBrackets) cleanRows = ValidateRows(errors, method, rows);
        if (errors.Any) return;

        Name = cleanName;
        EffectiveFrom = effectiveFrom;
        Method = method;
        PeriodBasis = periodBasis;
        var usesPercentage = method == StatutoryMethod.Percentage;
        EmployeeRateBps = usesPercentage ? percentage!.EmployeeRateBps : null;
        EmployerRateBps = usesPercentage ? percentage!.EmployerRateBps : null;
        BaseFloorMinor = usesPercentage ? percentage!.BaseFloorMinor : null;
        BaseCeilingMinor = usesPercentage ? percentage!.BaseCeilingMinor : null;
        EmployeeMinimumMinor = usesPercentage ? percentage!.EmployeeMinimumMinor : null;
        EmployeeMaximumMinor = usesPercentage ? percentage!.EmployeeMaximumMinor : null;
        EmployerMinimumMinor = usesPercentage ? percentage!.EmployerMinimumMinor : null;
        EmployerMaximumMinor = usesPercentage ? percentage!.EmployerMaximumMinor : null;

        _rows.Clear();
        var number = 0;
        foreach (var row in cleanRows)
        {
            number++;
            _rows.Add(method == StatutoryMethod.Brackets
                ? new StatutoryScheduleRow(Guid.NewGuid(), tenantId, id, number, row.FromMinor, row.ToMinor, row.EmployeeMinor, row.EmployerMinor, 0, 0)
                : new StatutoryScheduleRow(Guid.NewGuid(), tenantId, id, number, row.FromMinor, row.ToMinor, 0, 0, row.BaseTaxMinor, row.RateBps));
        }
    }

    private static void ValidatePercentage(ValidationErrors errors, StatutoryPercentageInput? input)
    {
        if (input is null)
        {
            errors.Add("employeeRateBps", "Enter the employee's rate.");
            return;
        }

        if (input.EmployeeRateBps is < 0 or > MaximumRateBps) errors.Add("employeeRateBps", "Enter the employee's rate between 0% and 100%.");
        if (input.EmployerRateBps is < 0 or > MaximumRateBps) errors.Add("employerRateBps", "Enter the employer's rate between 0% and 100%.");
        CheckAmount(errors, input.BaseFloorMinor, "baseFloorMinor", "the lowest base");
        if (input.BaseCeilingMinor is { } ceiling)
        {
            CheckAmount(errors, ceiling, "baseCeilingMinor", "the highest base");
            if (ceiling < input.BaseFloorMinor) errors.Add("baseCeilingMinor", "The highest base can't be below the lowest base.");
        }

        CheckRange(errors, input.EmployeeMinimumMinor, input.EmployeeMaximumMinor, "employee");
        CheckRange(errors, input.EmployerMinimumMinor, input.EmployerMaximumMinor, "employer");
    }

    private static void CheckRange(ValidationErrors errors, long? minimum, long? maximum, string who)
    {
        var field = who == "employee" ? "employee" : "employer";
        if (minimum is { } low) CheckAmount(errors, low, $"{field}MinimumMinor", $"the {who}'s smallest share");
        if (maximum is { } high) CheckAmount(errors, high, $"{field}MaximumMinor", $"the {who}'s largest share");
        if (minimum is { } min && maximum is { } max && max < min)
            errors.Add($"{field}MaximumMinor", $"The {who}'s largest share can't be below the smallest share.");
    }

    private static void CheckAmount(ValidationErrors errors, long value, string field, string label)
    {
        if (value < 0) errors.Add(field, $"{Capitalize(label)} can't be negative.");
        else MoneyLimits.CheckAmount(errors, value, field, label);
    }

    /// <summary>
    /// Rows start at 0, each ends where the next starts (no gaps, no overlaps), only the last is open-ended, and every amount and rate is
    /// in range. Every problem is reported, each naming its row.
    /// </summary>
    private static List<StatutoryRowInput> ValidateRows(ValidationErrors errors, StatutoryMethod method, IReadOnlyList<StatutoryRowInput>? rows)
    {
        var list = rows?.ToList() ?? [];
        if (list.Count == 0)
        {
            errors.Add("rows", "Add the table's rows, starting at ₱0.00.");
            return list;
        }

        if (list.Count > MaximumRows)
        {
            errors.Add("rows", $"A table can have at most {MaximumRows} rows.");
            return list;
        }

        if (list[0].FromMinor != 0) errors.Add("rows", "Row 1 must start at ₱0.00, so every amount is covered.");
        for (var index = 0; index < list.Count; index++)
        {
            var row = list[index];
            var number = index + 1;
            var last = index == list.Count - 1;
            if (row.FromMinor < 0 || row.FromMinor > MoneyLimits.MaximumAmountMinor) errors.Add("rows", $"Row {number}: enter a starting amount between ₱0.00 and ₱100,000,000,000.00.");
            if (last && row.ToMinor is not null) errors.Add("rows", $"Row {number} is the last row, so leave its upper limit empty: it covers every larger amount.");
            if (!last && row.ToMinor is null) errors.Add("rows", $"Row {number} needs an upper limit. Only the last row is open-ended.");
            if (row.ToMinor is { } to)
            {
                if (to <= row.FromMinor) errors.Add("rows", $"Row {number}: the upper limit must be above {Peso(row.FromMinor)}.");
                else if (to > MoneyLimits.MaximumAmountMinor) errors.Add("rows", $"Row {number}: the upper limit can be at most ₱100,000,000,000.00.");
                if (!last && list[index + 1].FromMinor != to)
                    errors.Add("rows", $"Row {number + 1} must start at {Peso(to)}, where row {number} ends, so there are no gaps or overlaps.");
            }

            if (method == StatutoryMethod.Brackets)
            {
                CheckRowAmount(errors, row.EmployeeMinor, number, "employee's share");
                CheckRowAmount(errors, row.EmployerMinor, number, "employer's share");
            }
            else
            {
                CheckRowAmount(errors, row.BaseTaxMinor, number, "base tax");
                if (row.RateBps is < 0 or > MaximumRateBps) errors.Add("rows", $"Row {number}: enter a rate between 0% and 100%.");
            }
        }

        return list;
    }

    private static void CheckRowAmount(ValidationErrors errors, long value, int number, string label)
    {
        if (value < 0) errors.Add("rows", $"Row {number}: the {label} can't be negative.");
        else if (value > MoneyLimits.MaximumAmountMinor) errors.Add("rows", $"Row {number}: the {label} can be at most ₱100,000,000,000.00.");
    }

    internal static string Peso(long minor) => "₱" + (minor / 100m).ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static void Merge(ValidationErrors errors, ValidationFailedException ex)
    {
        foreach (var (field, messages) in ex.Errors)
            foreach (var message in messages) errors.Add(field, message);
    }
}
