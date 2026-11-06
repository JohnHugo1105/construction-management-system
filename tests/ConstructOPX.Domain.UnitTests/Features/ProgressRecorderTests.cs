using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The rules of recording progress on a batch of budget lines, each refusal checked down to the field and its message.</summary>
public sealed class ProgressRecorderTests
{
    private static readonly DateOnly Today = TestData.Today;

    private static BudgetLine NewLine(Project project, string description = "Forms") =>
        BudgetLine.Create(Guid.NewGuid(), TestData.Tenant, project, null, 3, null, null, "3100", description, ItemType.Material, 1m, "l.s", 1_000_000,
            VatTreatment.Exempt, 1_200, new DateOnly(2026, 2, 1), null, TestData.UserId, null, TestData.Now);

    private sealed record Scene(Project Project, BudgetLine First, BudgetLine Second)
    {
        public Dictionary<Guid, BudgetLine> Lines => new() { [First.Id] = First, [Second.Id] = Second };
    }

    private static Scene NewScene()
    {
        var project = TestData.NewProject();
        return new Scene(project, NewLine(project, "Forms"), NewLine(project, "Rebar"));
    }

    private static ProgressBatchPlan Plan(Scene scene, IReadOnlyList<ProgressChange>? changes, DateOnly? asOf = null,
        IReadOnlyDictionary<Guid, LatestProgress>? latest = null, string? requestId = null, Project? project = null) =>
        ProgressRecorder.Plan(TestData.Tenant, project ?? scene.Project, asOf ?? Today, Today, changes, scene.Lines,
            latest ?? new Dictionary<Guid, LatestProgress>(), TestData.UserId, requestId, TestData.Now);

    private static Dictionary<string, string[]> Errors(Action action) =>
        Assert.Throws<ValidationFailedException>(action).Errors.ToDictionary(pair => pair.Key, pair => pair.Value);

    [Fact]
    public void A_batch_records_one_entry_per_changed_line_with_who_when_and_the_shared_request_id()
    {
        var scene = NewScene();

        var plan = Plan(scene, [new ProgressChange(scene.First.Id, 2_500, null, null), new ProgressChange(scene.Second.Id, 10_000, "  Poured  ", null)],
            asOf: Today.AddDays(-1), requestId: "req-1");

        Assert.Equal(0, plan.UnchangedLines);
        Assert.Equal(2, plan.Entries.Count);
        var first = plan.Entries.Single(entry => entry.BudgetLineId == scene.First.Id);
        Assert.Equal(2_500, first.PercentCompleteBps);
        Assert.Equal(Today.AddDays(-1), first.AsOfDate);
        Assert.Null(first.Note);
        Assert.Equal(TestData.UserId, first.RecordedByUserId);
        Assert.Equal(TestData.Tenant, first.TenantId);
        Assert.Equal(scene.Project.Id, first.ProjectId);
        Assert.Equal(TestData.Now, first.RecordedAtUtc);
        Assert.Equal("req-1", first.ClientRequestId);
        Assert.Equal("Poured", plan.Entries.Single(entry => entry.BudgetLineId == scene.Second.Id).Note);
    }

    [Fact]
    public void A_line_that_did_not_change_is_skipped_and_counted()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(4_000, Today.AddDays(-5)) };

        // The first line is already at 40%, so repeating 40% is no change; the second moves from 0% to 5%.
        var plan = Plan(scene, [new ProgressChange(scene.First.Id, 4_000, null, null), new ProgressChange(scene.Second.Id, 500, null, null)], latest: latest);

        Assert.Equal(1, plan.UnchangedLines);
        Assert.Equal(scene.Second.Id, Assert.Single(plan.Entries).BudgetLineId);
    }

    [Fact]
    public void Submitting_nothing_that_changed_is_an_error()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(4_000, Today.AddDays(-5)) };

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 4_000, null, null), new ProgressChange(scene.Second.Id, 0, null, null)],
            latest: latest));

        Assert.Equal("Nothing changed. Change the progress of at least one line before saving.", errors["lines"][0]);
    }

    [Fact]
    public void An_empty_batch_is_refused()
    {
        var scene = NewScene();

        Assert.Equal("Change the progress of at least one line before saving.", Errors(() => Plan(scene, []))["lines"][0]);
        Assert.Contains("lines", Errors(() => Plan(scene, null)).Keys);
    }

    [Fact]
    public void More_than_200_lines_are_refused_before_any_are_looked_at()
    {
        var scene = NewScene();
        var changes = Enumerable.Range(0, 201).Select(_ => new ProgressChange(Guid.NewGuid(), 1_000, null, null)).ToArray();

        var errors = Errors(() => Plan(scene, changes));

        Assert.Equal("Update at most 200 lines at once.", errors["lines"][0]);
        Assert.Single(errors);
    }

    [Fact]
    public void The_date_is_required_and_cannot_be_in_the_future()
    {
        var scene = NewScene();
        var change = new[] { new ProgressChange(scene.First.Id, 1_000, null, null) };

        Assert.Equal("Choose the date this progress is true on.", Errors(() => Plan(scene, change, asOf: default(DateOnly)))["asOfDate"][0]);
        Assert.Equal("Progress can't be dated in the future.", Errors(() => Plan(scene, change, asOf: Today.AddDays(1)))["asOfDate"][0]);
        Assert.Single(Plan(scene, change, asOf: Today).Entries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void A_percent_outside_0_to_100_is_refused_on_that_line(int percentBps)
    {
        var scene = NewScene();

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 500, null, null), new ProgressChange(scene.Second.Id, percentBps, null, null)]));

        Assert.Equal("Enter a percent between 0 and 100.", errors["lines[1].percentBps"][0]);
        Assert.Single(errors);
    }

    [Fact]
    public void The_whole_range_0_to_100_percent_is_accepted()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(5_000, Today.AddDays(-2)) };

        var plan = Plan(scene, [new ProgressChange(scene.First.Id, 0, "Scrapped", null), new ProgressChange(scene.Second.Id, 10_000, null, null)], latest: latest);

        Assert.Equal([0, 10_000], plan.Entries.Select(entry => entry.PercentCompleteBps).Order());
    }

    [Fact]
    public void A_lower_percent_than_the_current_one_needs_a_note()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(6_000, Today.AddDays(-2)) };

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 5_500, "   ", null)], latest: latest));

        Assert.Equal("Say why the progress went down, for example rework.", errors["lines[0].note"][0]);

        var accepted = Plan(scene, [new ProgressChange(scene.First.Id, 5_500, "Part of the slab was redone", null)], latest: latest);
        Assert.Equal("Part of the slab was redone", Assert.Single(accepted.Entries).Note);
    }

    [Fact]
    public void A_note_is_limited_to_300_characters()
    {
        var scene = NewScene();

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 1_000, new string('x', 301), null)]));

        Assert.Equal("The note can be at most 300 characters.", errors["lines[0].note"][0]);
        Assert.Single(Plan(scene, [new ProgressChange(scene.First.Id, 1_000, new string('x', 300), null)]).Entries);
    }

    [Fact]
    public void A_line_that_is_not_an_active_line_of_the_project_is_refused_on_that_line()
    {
        var scene = NewScene();

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 1_000, null, null), new ProgressChange(Guid.NewGuid(), 1_000, null, null)]));

        Assert.Equal("That budget line isn't part of this project any more. Reload the page and try again.", errors["lines[1].budgetLineId"][0]);
    }

    [Fact]
    public void The_same_line_twice_in_one_batch_is_refused()
    {
        var scene = NewScene();

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 1_000, null, null), new ProgressChange(scene.First.Id, 2_000, null, null)]));

        Assert.Equal("This line is in the update twice. Keep one.", errors["lines[1].budgetLineId"][0]);
    }

    [Fact]
    public void Every_problem_in_the_batch_is_reported_together()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.Second.Id] = new(6_000, Today.AddDays(-2)) };

        var errors = Errors(() => Plan(scene, [new ProgressChange(scene.First.Id, 20_000, null, null), new ProgressChange(scene.Second.Id, 1_000, null, null),
            new ProgressChange(Guid.NewGuid(), 1_000, null, null)], latest: latest));

        Assert.Equal(["lines[0].percentBps", "lines[1].note", "lines[2].budgetLineId"], errors.Keys.Order());
    }

    [Fact]
    public void An_update_dated_before_a_lines_latest_entry_is_refused_but_the_same_day_is_accepted()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(4_000, Today.AddDays(-3)) };
        var change = new[] { new ProgressChange(scene.First.Id, 5_000, null, null) };

        var errors = Errors(() => Plan(scene, change, asOf: Today.AddDays(-4), latest: latest));

        Assert.Equal("A line you changed was already updated on 30 Oct 2026. Use that date or a later one.", errors["asOfDate"][0]);
        Assert.Single(Plan(scene, change, asOf: Today.AddDays(-3), latest: latest).Entries);
    }

    [Fact]
    public void A_line_left_alone_does_not_hold_the_date_back()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(4_000, Today) };

        // The first line is unchanged (so its date does not matter); only the second is recorded, on an earlier date.
        var plan = Plan(scene, [new ProgressChange(scene.First.Id, 4_000, null, null), new ProgressChange(scene.Second.Id, 1_000, null, null)],
            asOf: Today.AddDays(-2), latest: latest);

        Assert.Equal(scene.Second.Id, Assert.Single(plan.Entries).BudgetLineId);
    }

    [Fact]
    public void An_update_made_on_a_figure_that_someone_else_has_since_changed_is_refused_as_stale()
    {
        var scene = NewScene();
        var latest = new Dictionary<Guid, LatestProgress> { [scene.First.Id] = new(4_000, Today.AddDays(-1)) };

        // The person saw 30%; someone saved 40% since.
        Assert.Throws<ConcurrencyConflictException>(() => Plan(scene, [new ProgressChange(scene.First.Id, 5_000, null, 3_000)], latest: latest));
        // A line that was never updated is at 0%.
        Assert.Throws<ConcurrencyConflictException>(() => Plan(scene, [new ProgressChange(scene.Second.Id, 5_000, null, 1_000)], latest: latest));
        // Saying nothing about it, or saying what is true, is fine.
        Assert.Equal(2, Plan(scene, [new ProgressChange(scene.First.Id, 5_000, null, 4_000), new ProgressChange(scene.Second.Id, 500, null, 0)], latest: latest).Entries.Count);
        Assert.Single(Plan(scene, [new ProgressChange(scene.First.Id, 5_000, null, null)], latest: latest).Entries);
    }

    [Fact]
    public void An_archived_project_takes_no_progress()
    {
        var scene = NewScene();
        scene.Project.Archive(ArchiveReason.Finished, null, TestData.Now);

        var ex = Assert.Throws<DomainRuleException>(() => Plan(scene, [new ProgressChange(scene.First.Id, 1_000, null, null)]));

        Assert.Equal("This project is archived. Restore it before making changes.", ex.Message);
    }

    [Fact]
    public void A_project_on_hold_or_completed_can_still_have_progress_recorded()
    {
        var scene = NewScene();
        scene.Project.Start(TestData.Now);
        scene.Project.Complete(TestData.Now);

        Assert.Single(Plan(scene, [new ProgressChange(scene.First.Id, 10_000, null, null)]).Entries);
    }
}
