using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Workforce;

/// <summary>
/// A person who works on the company's projects: personal details, pay history, postings and cases. Pay changes, postings and cases
/// are append-only histories kept on the worker, so the same question ("what was the rate on 3 Nov?", "where was this worker?")
/// always has one answer.
/// </summary>
public sealed class Worker : AggregateRoot
{
    private readonly List<WorkerRate> _rates = [];
    private readonly List<WorkerAssignment> _assignments = [];
    private readonly List<WorkerCase> _cases = [];

    private Worker() { }

    public string EmployeeNumber { get; private set; } = "";
    public string NormalizedEmployeeNumber { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = "";
    public DateOnly? DateOfBirth { get; private set; }
    public DateOnly DateHired { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? SpouseName { get; private set; }
    public string? Religion { get; private set; }
    public string? EducationalAttainment { get; private set; }
    public string? PresentAddress { get; private set; }
    public string? ProvincialAddress { get; private set; }
    public string? ContactNumber { get; private set; }
    public string? Reference { get; private set; }

    /// <summary>The id of the form submission that hired this worker, so a repeated submission returns the same worker.</summary>
    public string? ClientRequestId { get; private set; }

    public WorkerStatus Status { get; private set; } = WorkerStatus.Active;
    public DateOnly? StatusChangedOn { get; private set; }
    public string? StatusReason { get; private set; }

    public IReadOnlyList<WorkerRate> Rates => _rates;
    public IReadOnlyList<WorkerAssignment> Assignments => _assignments;
    public IReadOnlyList<WorkerCase> Cases => _cases;

    public string FullName => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public WorkerRate CurrentRate => _rates.OrderBy(rate => rate.EffectiveFrom).Last();

    public WorkerAssignment? CurrentAssignment => _assignments.SingleOrDefault(assignment => assignment.IsCurrent);

    public static string NormalizeEmployeeNumber(string? value) => (value ?? "").Trim().ToUpperInvariant();

    public static Worker Hire(Guid id, Guid tenantId, string? employeeNumber, string? firstName, string? middleName, string? lastName,
        DateOnly dateHired, string? designation, long rateMinor, RateUnit rateUnit, Guid recordedByUserId, DateOnly today,
        DateTimeOffset now, string? clientRequestId = null)
    {
        var errors = new ValidationErrors();
        string number = "", first = "", last = "", title = "";
        Capture(errors, () => number = Guard.RequiredText(employeeNumber, "employeeNumber", "the employee number", 40));
        Capture(errors, () => first = Guard.RequiredText(firstName, "firstName", "the first name", 80));
        Capture(errors, () => last = Guard.RequiredText(lastName, "lastName", "the last name", 80));
        Capture(errors, () => title = Guard.RequiredText(designation, "designation", "the designation (for example Mason)", 80));
        string? middle = null;
        Capture(errors, () => middle = Guard.OptionalText(middleName, "middleName", "the middle name", 80));
        string? requestId = null;
        Capture(errors, () => requestId = Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));
        if (dateHired == default) errors.Add("dateHired", "Choose the date the worker was hired.");
        else if (dateHired > today) errors.Add("dateHired", "The hire date can't be in the future.");
        ValidateRate(errors, rateMinor, rateUnit);
        errors.ThrowIfAny();

        var worker = new Worker
        {
            EmployeeNumber = number,
            NormalizedEmployeeNumber = NormalizeEmployeeNumber(number),
            FirstName = first,
            MiddleName = middle,
            LastName = last,
            DateHired = dateHired,
            ClientRequestId = requestId
        };
        worker.Initialize(id, tenantId, now);
        worker._rates.Add(new WorkerRate(Guid.NewGuid(), tenantId, id, title, rateMinor, rateUnit, RateChangeCategory.Initial, dateHired,
            "Hired", recordedByUserId, now));
        return worker;
    }

    public void UpdateProfile(string? firstName, string? middleName, string? lastName, DateOnly? dateOfBirth, string? maritalStatus,
        string? spouseName, string? religion, string? educationalAttainment, string? presentAddress, string? provincialAddress,
        string? contactNumber, string? reference, DateOnly today, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        string first = "", last = "";
        Capture(errors, () => first = Guard.RequiredText(firstName, "firstName", "the first name", 80));
        Capture(errors, () => last = Guard.RequiredText(lastName, "lastName", "the last name", 80));
        if (dateOfBirth is { } birth && (birth > today || birth < new DateOnly(1900, 1, 1)))
            errors.Add("dateOfBirth", "Enter a valid date of birth.");
        errors.ThrowIfAny();

        FirstName = first;
        LastName = last;
        MiddleName = Guard.OptionalText(middleName, "middleName", "the middle name", 80);
        DateOfBirth = dateOfBirth;
        MaritalStatus = Guard.OptionalText(maritalStatus, "maritalStatus", "the marital status", 40);
        SpouseName = Guard.OptionalText(spouseName, "spouseName", "the spouse's name", 120);
        Religion = Guard.OptionalText(religion, "religion", "the religion", 60);
        EducationalAttainment = Guard.OptionalText(educationalAttainment, "educationalAttainment", "the educational attainment", 80);
        PresentAddress = Guard.OptionalText(presentAddress, "presentAddress", "the present address", 250);
        ProvincialAddress = Guard.OptionalText(provincialAddress, "provincialAddress", "the provincial address", 250);
        ContactNumber = Guard.OptionalText(contactNumber, "contactNumber", "the contact number", 40);
        Reference = Guard.OptionalText(reference, "reference", "the reference", 200);
        Touch(now);
    }

    /// <summary>The pay rate in force on a date, or null when the date is before the worker was hired.</summary>
    public WorkerRate? RateOn(DateOnly date) =>
        _rates.Where(rate => rate.EffectiveFrom <= date).OrderBy(rate => rate.EffectiveFrom).LastOrDefault();

    /// <summary>
    /// Adds a pay-history line. The date must be after the latest line (history is not rewritten), and the category must match what
    /// changed: a promotion or demotion changes the designation, a rate adjustment changes only the pay.
    /// </summary>
    public void ChangeRate(RateChangeCategory category, string? designation, long rateMinor, RateUnit rateUnit, DateOnly effectiveFrom,
        string? reason, Guid recordedByUserId, DateTimeOffset now)
    {
        if (WorkerStatusRules.IsTerminal(Status)) throw new DomainRuleException("This worker has left. Reinstate them before changing their rate.");
        var errors = new ValidationErrors();
        string title = "";
        string? cleanReason = null;
        Capture(errors, () => title = Guard.RequiredText(designation, "designation", "the designation", 80));
        Capture(errors, () => cleanReason = Guard.OptionalText(reason, "reason", "the reason", 300));
        if (category is RateChangeCategory.Initial || !Enum.IsDefined(category)) errors.Add("category", "Choose promotion, demotion or rate adjustment.");
        ValidateRate(errors, rateMinor, rateUnit);
        var latest = CurrentRate;
        if (effectiveFrom <= latest.EffectiveFrom)
            errors.Add("effectiveFrom", $"Choose a date after {latest.EffectiveFrom:d MMM yyyy}, when the current rate started. Earlier pay isn't rewritten.");
        if (effectiveFrom < DateHired) errors.Add("effectiveFrom", "The change can't start before the worker was hired.");
        errors.ThrowIfAny();

        var sameDesignation = string.Equals(title, latest.Designation, StringComparison.OrdinalIgnoreCase);
        switch (category)
        {
            case RateChangeCategory.Promotion or RateChangeCategory.Demotion when sameDesignation:
                throw new ValidationFailedException("designation", "A promotion or demotion changes the designation. Enter the new designation, or choose rate adjustment.");
            case RateChangeCategory.RateAdjustment when !sameDesignation:
                throw new ValidationFailedException("designation", "A rate adjustment keeps the designation. Choose promotion or demotion to change it.");
            case RateChangeCategory.RateAdjustment when rateMinor == latest.RateMinor && rateUnit == latest.Unit:
                throw new ValidationFailedException("rateMinor", "That is the current rate. Enter a different amount.");
        }

        _rates.Add(new WorkerRate(Guid.NewGuid(), TenantId, Id, title, rateMinor, rateUnit, category, effectiveFrom, cleanReason,
            recordedByUserId, now));
        Touch(now);
    }

    /// <summary>
    /// Posts the worker to a project (and optionally a sub-project) from a date. The earlier posting ends on that date, so the
    /// postings never overlap. Only an active worker can be posted.
    /// </summary>
    public void AssignTo(Project project, Guid? subProjectId, DateOnly startDate, Guid assignedByUserId, DateTimeOffset now)
    {
        project.EnsureCanReceiveRecordsFor(subProjectId);
        if (Status != WorkerStatus.Active)
            throw new DomainRuleException($"{FullName} is {WorkerStatusRules.Label(Status).ToLowerInvariant()}. Only an active worker can be assigned to a project.");
        if (startDate < DateHired) throw new ValidationFailedException("startDate", "The posting can't start before the worker was hired.");

        var current = CurrentAssignment;
        if (current is not null)
        {
            if (current.ProjectId == project.Id && current.SubProjectId == subProjectId)
                throw new DomainRuleException($"{FullName} is already assigned there.");
            if (startDate <= current.StartDate)
                throw new ValidationFailedException("startDate", $"Choose a date after {current.StartDate:d MMM yyyy}, when the current posting started.");
            current.End(startDate);
        }

        _assignments.Add(new WorkerAssignment(Guid.NewGuid(), TenantId, Id, project.Id, subProjectId, startDate, assignedByUserId, now));
        Touch(now);
    }

    /// <summary>Ends the current posting without a new one (the worker is waiting for a project).</summary>
    public void Release(DateOnly endDate, DateTimeOffset now)
    {
        var current = CurrentAssignment ?? throw new DomainRuleException($"{FullName} isn't assigned to a project.");
        if (endDate <= current.StartDate)
            throw new ValidationFailedException("endDate", $"Choose a date after {current.StartDate:d MMM yyyy}, when the posting started.");
        current.End(endDate);
        Touch(now);
    }

    public void ChangeStatus(WorkerStatus target, DateOnly effectiveDate, string? reason, DateOnly today, DateTimeOffset now)
    {
        if (!Enum.IsDefined(target)) throw new ValidationFailedException("status", "Choose a status.");
        if (target == Status) throw new DomainRuleException($"{FullName} is already {WorkerStatusRules.Label(Status).ToLowerInvariant()}.");
        if (WorkerStatusRules.IsTerminal(Status))
            throw new DomainRuleException($"{FullName} has {Status.ToString().ToLowerInvariant()}. Use reinstate to bring them back.");
        if (effectiveDate < DateHired || effectiveDate > today)
            throw new ValidationFailedException("effectiveDate", "Choose a date between the hire date and today.");
        var cleanReason = Guard.OptionalText(reason, "reason", "the reason", 300);
        if (target is WorkerStatus.Suspended or WorkerStatus.Terminated && cleanReason is null)
            throw new ValidationFailedException("reason", target == WorkerStatus.Suspended ? "Say why the worker is suspended." : "Say why the worker is terminated.");

        Status = target;
        StatusChangedOn = effectiveDate;
        StatusReason = cleanReason;
        if (WorkerStatusRules.IsTerminal(target) && CurrentAssignment is { } current)
            current.End(effectiveDate > current.StartDate ? effectiveDate : current.StartDate);
        Touch(now);
    }

    /// <summary>Brings a worker who left back to active. A reason is required and the change is audited by the caller.</summary>
    public void Reinstate(DateOnly effectiveDate, string? reason, DateOnly today, DateTimeOffset now)
    {
        if (!WorkerStatusRules.IsTerminal(Status)) throw new DomainRuleException($"{FullName} hasn't left, so there is nothing to reinstate.");
        if (effectiveDate < (StatusChangedOn ?? DateHired) || effectiveDate > today)
            throw new ValidationFailedException("effectiveDate", "Choose a date after the worker left and not in the future.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for reinstating", 300);
        Status = WorkerStatus.Active;
        StatusChangedOn = effectiveDate;
        StatusReason = cleanReason;
        Touch(now);
    }

    public WorkerCase RecordCase(string? title, string? remarks, DateOnly caseDate, Guid? projectId, Guid recordedByUserId,
        DateOnly today, DateTimeOffset now)
    {
        var errors = new ValidationErrors();
        string cleanTitle = "";
        string? cleanRemarks = null;
        Capture(errors, () => cleanTitle = Guard.RequiredText(title, "title", "what happened", 200));
        Capture(errors, () => cleanRemarks = Guard.OptionalText(remarks, "remarks", "the remarks", 1000));
        if (caseDate < DateHired || caseDate > today) errors.Add("caseDate", "Choose a date between the hire date and today.");
        errors.ThrowIfAny();

        var workerCase = new WorkerCase(Guid.NewGuid(), TenantId, Id, projectId, caseDate, cleanTitle, cleanRemarks, recordedByUserId, now);
        _cases.Add(workerCase);
        Touch(now);
        return workerCase;
    }

    /// <summary>
    /// Whether attendance may be recorded for this worker on a date. A worker who has left can still be paid up to the day they left;
    /// one on leave or suspended can't be recorded from the day that began.
    /// </summary>
    public bool CanBeRecordedOn(DateOnly date)
    {
        if (date < DateHired) return false;
        return Status switch
        {
            WorkerStatus.Active => true,
            WorkerStatus.OnLeave or WorkerStatus.Suspended => StatusChangedOn is { } since && date < since,
            _ => StatusChangedOn is { } left && date <= left
        };
    }

    private static void ValidateRate(ValidationErrors errors, long rateMinor, RateUnit unit)
    {
        if (rateMinor <= 0) errors.Add("rateMinor", "Enter a pay rate greater than zero.");
        if (!Enum.IsDefined(unit)) errors.Add("rateUnit", "Choose whether the rate is per day or per hour.");
    }

    private static void Capture(ValidationErrors errors, Action check)
    {
        try { check(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
        }
    }
}
