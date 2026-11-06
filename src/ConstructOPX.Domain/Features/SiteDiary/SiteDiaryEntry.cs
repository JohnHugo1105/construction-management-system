using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.SiteDiary;

/// <summary>What a person types in a daily report. The workers on site and the deliveries are not typed: they are read from what is recorded.</summary>
public sealed record SiteDiaryDetails(DiaryWeather Weather, string? WeatherNote, string? WorkDone, string? IssuesAndDelays,
    string? VisitorsAndInspections, string? SafetyNotes, int SubcontractorWorkers);

/// <summary>
/// The site diary: one structured report per project per day. It records the weather, the work done, issues and delays, visitors and
/// inspections, safety notes and how many subcontractor workers were on site (a typed number: those crews are not on the company's payroll).
/// It carries no money. The workers with attendance that day and the deliveries received that day are read alongside it, never stored here.
/// </summary>
/// <remarks>
/// There is one live (not voided) report per project per report date. The person who wrote a report may correct or void it until the end of
/// the next day after the report date, judged on the company's calendar; a person who manages projects (a Project Manager or an Administrator)
/// may do so at any time. A voided report stays on record and its date can be reported again. A project that is archived is read-only.
/// </remarks>
public sealed class SiteDiaryEntry : AggregateRoot
{
    public const int MaximumWeatherNoteLength = 200;
    public const int MaximumWorkDoneLength = 2000;
    public const int MaximumIssuesLength = 1000;
    public const int MaximumVisitorsLength = 500;
    public const int MaximumSafetyLength = 1000;
    public const int MaximumReasonLength = 300;
    public const int MaximumSubcontractorWorkers = 2000;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);

    private SiteDiaryEntry() { }

    public Guid ProjectId { get; private set; }

    /// <summary>The day the report is about, on the company's calendar. Never in the future, and fixed once written.</summary>
    public DateOnly ReportDate { get; private set; }

    public DiaryWeather Weather { get; private set; }
    public string? WeatherNote { get; private set; }
    public string WorkDone { get; private set; } = "";
    public string? IssuesAndDelays { get; private set; }
    public string? VisitorsAndInspections { get; private set; }
    public string? SafetyNotes { get; private set; }

    /// <summary>Subcontractor workers on site, as typed: those crews are not on the company's payroll.</summary>
    public int SubcontractorWorkers { get; private set; }

    public DiaryStatus Status { get; private set; } = DiaryStatus.Posted;
    public Guid AuthorUserId { get; private set; }
    public string? ClientRequestId { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }

    private SiteDiaryDetails Current => new(Weather, WeatherNote, WorkDone, IssuesAndDelays, VisitorsAndInspections, SafetyNotes, SubcontractorWorkers);

    // ---- The author window ------------------------------------------------------------------------------------------------------

    /// <summary>The last company day on which the author may still change a report: the day after its report date.</summary>
    public static DateOnly AuthorWindowEnds(DateOnly reportDate) => reportDate.AddDays(1);

    /// <summary>True while <paramref name="today"/> (the company's calendar day) is no later than the end of the day after the report date.</summary>
    public static bool IsInAuthorWindow(DateOnly reportDate, DateOnly today) => today <= AuthorWindowEnds(reportDate);

    /// <summary>
    /// True when this person may correct or void the report now: a person who manages projects at any time, the author until the end of
    /// the next day. The report must also be Posted.
    /// </summary>
    public bool CanBeChangedBy(Guid userId, bool canManage, DateOnly today) =>
        Status == DiaryStatus.Posted && (canManage || (userId == AuthorUserId && IsInAuthorWindow(ReportDate, today)));

    // ---- Lifecycle --------------------------------------------------------------------------------------------------------------

    public static SiteDiaryEntry Create(Guid id, Guid tenantId, Project project, DateOnly reportDate, SiteDiaryDetails details, Guid authorUserId,
        string? clientRequestId, DateOnly today, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        var entry = new SiteDiaryEntry { ProjectId = project.Id, AuthorUserId = authorUserId };
        var errors = new ValidationErrors();

        if (reportDate == default) errors.Add("reportDate", "Choose the date of the report.");
        else if (reportDate < EarliestDate) errors.Add("reportDate", "Choose a valid date.");
        else if (reportDate > today) errors.Add("reportDate", "A report can't be for a day that hasn't come yet. Choose today or an earlier day.");
        entry.ReportDate = reportDate;

        entry.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));
        var clean = Clean(details, errors);
        errors.ThrowIfAny();

        entry.Assign(clean!);
        entry.Initialize(id, tenantId, now);
        return entry;
    }

    /// <summary>
    /// Corrects a Posted report. Returns the names of the fields that changed ("weather", "work done"...), in a fixed order, and records
    /// nothing when none did. The report date never changes: a report written for the wrong day is voided and written again.
    /// </summary>
    public IReadOnlyList<string> Correct(Project project, SiteDiaryDetails details, Guid userId, bool canManage, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsurePosted("changed");
        EnsureMayChange(userId, canManage, today, "changed");

        var errors = new ValidationErrors();
        var clean = Clean(details, errors);
        errors.ThrowIfAny();

        var changed = ChangedFields(Current, clean!);
        if (changed.Count == 0) return changed;
        Assign(clean!);
        Touch(now);
        return changed;
    }

    /// <summary>Marks a report that was entered by mistake. It stays on record with the reason, and its date can be reported again. Final.</summary>
    public void Void(Project project, string? reason, Guid userId, bool canManage, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        if (Status == DiaryStatus.Void) throw new DomainRuleException("This report was already voided.");
        EnsureMayChange(userId, canManage, today, "voided");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for voiding it", MaximumReasonLength);

        Status = DiaryStatus.Void;
        VoidReason = cleanReason;
        VoidedByUserId = userId;
        VoidedAtUtc = now;
        Touch(now);
    }

    // ---- Checks -----------------------------------------------------------------------------------------------------------------

    private static SiteDiaryDetails? Clean(SiteDiaryDetails details, ValidationErrors errors)
    {
        if (!Enum.IsDefined(details.Weather)) errors.Add("weather", "Choose the weather: fair, cloudy, rain, heavy rain or storm.");
        var weatherNote = Field(errors, () => Guard.OptionalText(details.WeatherNote, "weatherNote", "the weather note", MaximumWeatherNoteLength));
        var workDone = Field(errors, () => Guard.RequiredText(details.WorkDone, "workDone", "what work was done (on a day with no work, say why)",
            MaximumWorkDoneLength));
        var issues = Field(errors, () => Guard.OptionalText(details.IssuesAndDelays, "issuesAndDelays", "the issues and delays", MaximumIssuesLength));
        var visitors = Field(errors, () => Guard.OptionalText(details.VisitorsAndInspections, "visitorsAndInspections",
            "the visitors and inspections", MaximumVisitorsLength));
        var safety = Field(errors, () => Guard.OptionalText(details.SafetyNotes, "safetyNotes", "the safety notes", MaximumSafetyLength));
        if (details.SubcontractorWorkers is < 0 or > MaximumSubcontractorWorkers)
            errors.Add("subcontractorWorkers", $"Enter how many subcontractor workers were on site, from 0 to {MaximumSubcontractorWorkers:N0}.");

        return errors.Any ? null : new SiteDiaryDetails(details.Weather, weatherNote, workDone, issues, visitors, safety, details.SubcontractorWorkers);
    }

    private void Assign(SiteDiaryDetails clean)
    {
        Weather = clean.Weather;
        WeatherNote = clean.WeatherNote;
        WorkDone = clean.WorkDone!;
        IssuesAndDelays = clean.IssuesAndDelays;
        VisitorsAndInspections = clean.VisitorsAndInspections;
        SafetyNotes = clean.SafetyNotes;
        SubcontractorWorkers = clean.SubcontractorWorkers;
    }

    /// <summary>The names of the fields that differ, for the audit line. Never the text itself.</summary>
    private static List<string> ChangedFields(SiteDiaryDetails before, SiteDiaryDetails after)
    {
        var changed = new List<string>();
        if (before.Weather != after.Weather) changed.Add("weather");
        if (before.WeatherNote != after.WeatherNote) changed.Add("weather note");
        if (before.WorkDone != after.WorkDone) changed.Add("work done");
        if (before.IssuesAndDelays != after.IssuesAndDelays) changed.Add("issues and delays");
        if (before.VisitorsAndInspections != after.VisitorsAndInspections) changed.Add("visitors and inspections");
        if (before.SafetyNotes != after.SafetyNotes) changed.Add("safety notes");
        if (before.SubcontractorWorkers != after.SubcontractorWorkers) changed.Add("subcontractor workers");
        return changed;
    }

    private void EnsurePosted(string doing)
    {
        if (Status == DiaryStatus.Void)
            throw new DomainRuleException($"This report was voided, so it can't be {doing}. Write a new report for {Format(ReportDate)} instead.");
    }

    private void EnsureMayChange(Guid userId, bool canManage, DateOnly today, string doing)
    {
        if (canManage) return;
        if (userId != AuthorUserId)
            throw new DomainRuleException($"Only the person who wrote this report can have it {doing}, and only until the end of the next day. " +
                "Ask a Project Manager or an Administrator.");
        if (!IsInAuthorWindow(ReportDate, today))
            throw new DomainRuleException($"You could change this report until the end of {Format(AuthorWindowEnds(ReportDate))}, and that time has passed. " +
                "Ask a Project Manager or an Administrator to have it " + doing + ".");
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this report.", nameof(project));
    }

    /// <summary>"3 Dec 2026", for messages.</summary>
    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}
