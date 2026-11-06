using ConstructOPX.Application.Features.SiteDiary;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.SiteDiary;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The site diary rules: the date, every refusal, the author window and its company-zone boundary, corrections, void and archived projects (docs/business-rules.md section 18).</summary>
public sealed class SiteDiaryTests
{
    private static readonly DateOnly Today = TestData.Today; // Mon 2 Nov 2026
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly Guid Author = TestData.UserId;
    private static readonly Guid Colleague = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Organization Company = Organization.Create(Guid.NewGuid(), "Acme Builders", DateTimeOffset.UnixEpoch); // Asia/Manila

    private static SiteDiaryDetails Details(DiaryWeather weather = DiaryWeather.Fair, string? weatherNote = null, string? workDone = "Poured the slab.",
        string? issues = null, string? visitors = null, string? safety = null, int subcontractors = 4) =>
        new(weather, weatherNote, workDone, issues, visitors, safety, subcontractors);

    private static (SiteDiaryEntry Entry, Project Project) Posted(DateOnly? date = null, SiteDiaryDetails? details = null)
    {
        var project = TestData.NewProject();
        var entry = SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, date ?? Today, details ?? Details(), Author, null, Today, Now);
        return (entry, project);
    }

    private static IReadOnlyDictionary<string, string[]> Errors(Action action) => Assert.Throws<ValidationFailedException>(action).Errors;

    // ---- Writing a report -----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_report_is_posted_by_its_author_for_its_day_and_trims_what_was_typed()
    {
        var (entry, project) = Posted(Today.AddDays(-1), Details(DiaryWeather.Rain, " Showers after lunch ", " Poured the slab. ", " Late delivery ", " Inspector ",
            " Harness check ", 12));

        Assert.Equal((DiaryStatus.Posted, project.Id, Today.AddDays(-1), Author), (entry.Status, entry.ProjectId, entry.ReportDate, entry.AuthorUserId));
        Assert.Equal((DiaryWeather.Rain, "Showers after lunch", "Poured the slab."), (entry.Weather, entry.WeatherNote, entry.WorkDone));
        Assert.Equal(("Late delivery", "Inspector", "Harness check", 12), (entry.IssuesAndDelays, entry.VisitorsAndInspections, entry.SafetyNotes, entry.SubcontractorWorkers));
        Assert.Equal(TestData.Tenant, entry.TenantId);
        Assert.Null(entry.ClientRequestId);
    }

    [Fact]
    public void Optional_text_left_empty_is_not_stored_and_a_request_id_is_kept()
    {
        var project = TestData.NewProject();
        var entry = SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today, Details(weatherNote: "  ", issues: "", visitors: null, safety: " "), Author,
            " req-1 ", Today, Now);

        Assert.Equal((null, null, null, null, "req-1"), (entry.WeatherNote, entry.IssuesAndDelays, entry.VisitorsAndInspections, entry.SafetyNotes, entry.ClientRequestId));
    }

    [Fact]
    public void The_date_can_be_today_or_earlier_but_never_in_the_future()
    {
        Assert.Equal(Today, Posted(Today).Entry.ReportDate);
        Assert.Equal(new DateOnly(2000, 1, 1), Posted(new DateOnly(2000, 1, 1)).Entry.ReportDate);

        var project = TestData.NewProject();
        var tomorrow = Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today.AddDays(1), Details(), Author, null, Today, Now));
        Assert.Contains("hasn't come yet", Assert.Single(tomorrow["reportDate"]));
        Assert.Contains("Choose the date", Assert.Single(Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, default, Details(), Author, null,
            Today, Now))["reportDate"]));
        Assert.Equal("Choose a valid date.", Assert.Single(Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, new DateOnly(1999, 12, 31),
            Details(), Author, null, Today, Now))["reportDate"]));
    }

    [Fact]
    public void Not_in_the_future_is_judged_on_the_company_calendar_day_never_the_utc_day()
    {
        // 16:30 UTC on 2 Nov is 00:30 on 3 Nov in Manila: 3 Nov is already today there, though it is still 2 Nov in UTC.
        var instant = new DateTimeOffset(2026, 11, 2, 16, 30, 0, TimeSpan.Zero);
        var manilaToday = Company.TodayLocal(instant);
        Assert.Equal(new DateOnly(2026, 11, 3), manilaToday);

        var project = TestData.NewProject();
        var entry = SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, new DateOnly(2026, 11, 3), Details(), Author, null, manilaToday, instant);
        Assert.Equal(new DateOnly(2026, 11, 3), entry.ReportDate);
        // Just before midnight in Manila the same date is still tomorrow.
        var before = Company.TodayLocal(new DateTimeOffset(2026, 11, 2, 15, 59, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 11, 2), before);
        Assert.Contains("hasn't come yet", Assert.Single(Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, new DateOnly(2026, 11, 3), Details(),
            Author, null, before, instant))["reportDate"]));
    }

    [Fact]
    public void Every_problem_with_a_new_report_is_reported_at_once()
    {
        var project = TestData.NewProject();
        var errors = Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today.AddDays(1),
            Details((DiaryWeather)(-1), new string('a', 201), " ", new string('b', 1001), new string('c', 501), new string('d', 1001), 2001), Author,
            new string('r', 81), Today, Now));

        Assert.Equal(new[] { "clientRequestId", "issuesAndDelays", "reportDate", "safetyNotes", "subcontractorWorkers", "visitorsAndInspections", "weather", "weatherNote", "workDone" },
            errors.Keys.Order().ToArray());
        Assert.Contains("Choose the weather", Assert.Single(errors["weather"]));
        Assert.Contains("what work was done", Assert.Single(errors["workDone"]));
        Assert.Contains("0 to 2,000", Assert.Single(errors["subcontractorWorkers"]));
    }

    [Fact]
    public void The_text_limits_are_exact_and_the_worker_count_runs_from_zero_to_two_thousand()
    {
        var project = TestData.NewProject();
        var longest = SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today,
            Details(DiaryWeather.Storm, new string('a', 200), new string('w', 2000), new string('i', 1000), new string('v', 500), new string('s', 1000), 2000), Author, null, Today, Now);
        Assert.Equal((2000, 2000), (longest.WorkDone.Length, longest.SubcontractorWorkers));
        Assert.Equal(0, Posted(details: Details(subcontractors: 0)).Entry.SubcontractorWorkers);

        Assert.Contains("at most 2000", Assert.Single(Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today,
            Details(workDone: new string('w', 2001)), Author, null, Today, Now))["workDone"]));
        Assert.Contains("0 to 2,000", Assert.Single(Errors(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today,
            Details(subcontractors: -1), Author, null, Today, Now))["subcontractorWorkers"]));
    }

    [Fact]
    public void An_archived_project_takes_no_report()
    {
        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, Now);

        var refusal = Assert.Throws<DomainRuleException>(() => SiteDiaryEntry.Create(Guid.NewGuid(), TestData.Tenant, project, Today, Details(), Author, null, Today, Now));
        Assert.Equal("This project is archived. Restore it before making changes.", refusal.Message);
    }

    // ---- The author window ----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_author_window_runs_to_the_end_of_the_day_after_the_report_date()
    {
        var reportDate = new DateOnly(2026, 11, 2);
        Assert.Equal(new DateOnly(2026, 11, 3), SiteDiaryEntry.AuthorWindowEnds(reportDate));
        Assert.True(SiteDiaryEntry.IsInAuthorWindow(reportDate, reportDate));
        Assert.True(SiteDiaryEntry.IsInAuthorWindow(reportDate, new DateOnly(2026, 11, 3)));
        Assert.False(SiteDiaryEntry.IsInAuthorWindow(reportDate, new DateOnly(2026, 11, 4)));
        Assert.Equal(new DateOnly(2027, 1, 1), SiteDiaryEntry.AuthorWindowEnds(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void The_window_closes_at_midnight_in_the_company_zone_not_in_utc()
    {
        var (entry, _) = Posted(new DateOnly(2026, 11, 2));
        // 15:59 UTC on 3 Nov is 23:59 in Manila: still the day after the report date. One minute later it is 4 Nov in Manila.
        var open = Company.TodayLocal(new DateTimeOffset(2026, 11, 3, 15, 59, 0, TimeSpan.Zero));
        var closed = Company.TodayLocal(new DateTimeOffset(2026, 11, 3, 16, 0, 0, TimeSpan.Zero));

        Assert.Equal((new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4)), (open, closed));
        Assert.True(entry.CanBeChangedBy(Author, false, open));
        Assert.False(entry.CanBeChangedBy(Author, false, closed));
    }

    [Fact]
    public void Only_the_author_inside_the_window_or_a_person_who_manages_projects_may_change_a_report()
    {
        var (entry, _) = Posted(new DateOnly(2026, 11, 2));
        var lastDay = new DateOnly(2026, 11, 3);
        var later = new DateOnly(2026, 11, 20);

        Assert.True(entry.CanBeChangedBy(Author, false, lastDay));
        Assert.False(entry.CanBeChangedBy(Author, false, later));
        Assert.False(entry.CanBeChangedBy(Colleague, false, lastDay));
        Assert.True(entry.CanBeChangedBy(Colleague, true, later));
        Assert.True(entry.CanBeChangedBy(Author, true, later));
    }

    // ---- Correcting a report --------------------------------------------------------------------------------------------------

    [Fact]
    public void A_correction_names_the_fields_that_changed_and_nothing_else()
    {
        var (entry, project) = Posted();

        var changed = entry.Correct(project, Details(DiaryWeather.Cloudy, "Windy", "Poured the slab and tied rebar.", "Delay", "Inspector", "Toolbox talk", 9), Author, false,
            Today, Now.AddHours(2));

        Assert.Equal(new[] { "weather", "weather note", "work done", "issues and delays", "visitors and inspections", "safety notes", "subcontractor workers" }, changed);
        Assert.Equal((DiaryWeather.Cloudy, "Poured the slab and tied rebar.", 9), (entry.Weather, entry.WorkDone, entry.SubcontractorWorkers));
        Assert.Equal(Now.AddHours(2), entry.UpdatedAtUtc);
        // The report's day and author never change.
        Assert.Equal((Today, Author), (entry.ReportDate, entry.AuthorUserId));
    }

    [Fact]
    public void A_correction_that_changes_nothing_reports_no_fields_and_does_not_touch_the_report()
    {
        var (entry, project) = Posted();

        var changed = entry.Correct(project, Details(workDone: "  Poured the slab.  "), Author, false, Today, Now.AddHours(2));

        Assert.Empty(changed);
        Assert.Equal(Now, entry.UpdatedAtUtc);
    }

    [Fact]
    public void A_correction_reports_one_changed_field_by_name()
    {
        var (entry, project) = Posted();

        Assert.Equal(new[] { "safety notes" }, entry.Correct(project, Details(safety: "Helmets checked"), Author, false, Today, Now));
        Assert.Equal(new[] { "weather" }, entry.Correct(project, Details(DiaryWeather.Storm, safety: "Helmets checked"), Author, false, Today, Now));
    }

    [Fact]
    public void A_correction_is_refused_when_a_field_is_invalid_and_changes_nothing()
    {
        var (entry, project) = Posted();

        var errors = Errors(() => entry.Correct(project, Details(DiaryWeather.Rain, workDone: " ", subcontractors: 5000), Author, false, Today, Now));

        Assert.Equal(new[] { "subcontractorWorkers", "workDone" }, errors.Keys.Order().ToArray());
        Assert.Equal((DiaryWeather.Fair, "Poured the slab.", 4), (entry.Weather, entry.WorkDone, entry.SubcontractorWorkers));
    }

    [Fact]
    public void A_colleague_cannot_correct_a_report_but_a_manager_can_at_any_time()
    {
        var (entry, project) = Posted(new DateOnly(2026, 11, 2));

        var notAuthor = Assert.Throws<DomainRuleException>(() => entry.Correct(project, Details(workDone: "Other"), Colleague, false, new DateOnly(2026, 11, 3), Now));
        Assert.Contains("Only the person who wrote this report", notAuthor.Message);

        var changed = entry.Correct(project, Details(workDone: "Corrected by the PM"), Colleague, true, new DateOnly(2027, 3, 1), Now);
        Assert.Equal(new[] { "work done" }, changed);
    }

    [Fact]
    public void The_author_is_refused_after_the_window_with_the_date_it_ended()
    {
        var (entry, project) = Posted(new DateOnly(2026, 11, 2));

        var refusal = Assert.Throws<DomainRuleException>(() => entry.Correct(project, Details(workDone: "Late"), Author, false, new DateOnly(2026, 11, 4), Now));

        Assert.Contains("until the end of 3 Nov 2026", refusal.Message);
        Assert.Contains("Ask a Project Manager or an Administrator", refusal.Message);
        Assert.Equal("Poured the slab.", entry.WorkDone);
    }

    [Fact]
    public void A_correction_on_an_archived_project_is_refused()
    {
        var (entry, project) = Posted();
        project.Archive(ArchiveReason.Finished, null, Now);

        Assert.Equal("This project is archived. Restore it before making changes.",
            Assert.Throws<DomainRuleException>(() => entry.Correct(project, Details(workDone: "Late"), Author, true, Today, Now)).Message);
    }

    // ---- Voiding a report -----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_report_is_voided_with_a_reason_who_and_when_and_can_no_longer_be_changed()
    {
        var (entry, project) = Posted();

        entry.Void(project, "  Wrote it on the wrong project  ", Author, false, Today, Now.AddHours(1));

        Assert.Equal((DiaryStatus.Void, "Wrote it on the wrong project", Author, Now.AddHours(1)), (entry.Status, entry.VoidReason, entry.VoidedByUserId, entry.VoidedAtUtc));
        Assert.False(entry.CanBeChangedBy(Author, true, Today));
        var again = Assert.Throws<DomainRuleException>(() => entry.Void(project, "Again", Author, true, Today, Now));
        Assert.Equal("This report was already voided.", again.Message);
        var correct = Assert.Throws<DomainRuleException>(() => entry.Correct(project, Details(workDone: "x"), Author, true, Today, Now));
        Assert.Contains("This report was voided", correct.Message);
        Assert.Contains("Write a new report for 2 Nov 2026", correct.Message);
    }

    [Fact]
    public void A_void_needs_a_reason_of_at_most_three_hundred_characters()
    {
        var (entry, project) = Posted();

        Assert.Contains("Enter the reason", Assert.Single(Errors(() => entry.Void(project, "  ", Author, false, Today, Now))["reason"]));
        Assert.Contains("at most 300", Assert.Single(Errors(() => entry.Void(project, new string('r', 301), Author, false, Today, Now))["reason"]));
        Assert.Equal(DiaryStatus.Posted, entry.Status);
        entry.Void(project, new string('r', 300), Author, false, Today, Now);
        Assert.Equal(DiaryStatus.Void, entry.Status);
    }

    [Fact]
    public void A_void_follows_the_same_who_and_when_as_a_correction_and_refuses_an_archived_project()
    {
        var (entry, project) = Posted(new DateOnly(2026, 11, 2));

        Assert.Contains("Only the person who wrote this report", Assert.Throws<DomainRuleException>(() =>
            entry.Void(project, "Mistake", Colleague, false, new DateOnly(2026, 11, 3), Now)).Message);
        Assert.Contains("that time has passed", Assert.Throws<DomainRuleException>(() =>
            entry.Void(project, "Mistake", Author, false, new DateOnly(2026, 11, 5), Now)).Message);

        project.Archive(ArchiveReason.Finished, null, Now);
        Assert.Equal("This project is archived. Restore it before making changes.",
            Assert.Throws<DomainRuleException>(() => entry.Void(project, "Mistake", Author, true, Today, Now)).Message);
        Assert.Equal(DiaryStatus.Posted, entry.Status);
    }

    [Fact]
    public void A_report_cannot_be_used_with_another_project()
    {
        var (entry, _) = Posted();

        Assert.Throws<ArgumentException>(() => entry.Void(TestData.NewProject("Other"), "Mistake", Author, true, Today, Now));
    }

    // ---- The list preview -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_list_shows_only_the_first_line_of_work_done_cut_to_a_row()
    {
        Assert.Equal("Poured the slab.", DiaryPreview.FirstLine("Poured the slab.\nThen cleaned up."));
        Assert.Equal("", DiaryPreview.FirstLine(null));
        Assert.Equal(new string('a', 140), DiaryPreview.FirstLine(new string('a', 140)));
        var cut = DiaryPreview.FirstLine(new string('a', 141));
        Assert.Equal(140, cut.Length);
        Assert.EndsWith("…", cut);
    }
}
