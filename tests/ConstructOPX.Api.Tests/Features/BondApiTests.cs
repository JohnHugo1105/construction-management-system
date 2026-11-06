using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Bonds;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.BondTestKit;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>The bonds register over the real API and SQL Server: the round trip, every refusal and the filters (docs/business-rules.md section 15).</summary>
public sealed class BondApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public BondApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task A_bond_is_recorded_read_edited_extended_and_released()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var created = await client.PostAsync("/api/bonds", NewBond(project.Id, number: "PB-2026-014", validUntil: DaysFromToday(100), note: "Original in the safe"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.StartsWith("/api/bonds/", created.Headers.Location!.ToString());
        var bond = await client.ReadAsync<BondDto>(created);

        Assert.Equal(("Posted", "Performance", "Metro Surety", "PB-2026-014", 5_000_000_00L, "PHP"),
            (bond.Direction, bond.Kind, bond.IssuerName, bond.BondNumber, bond.AmountMinor, bond.CurrencyCode));
        Assert.Equal(("Active", "Valid", 100), (bond.Status, bond.Expiry, bond.DaysLeft));
        Assert.Equal(project.Name, bond.ProjectName);
        Assert.False(bond.ProjectArchived);
        Assert.Equal("Test Admin", bond.CreatedByName);
        var recorded = Assert.Single(bond.History);
        Assert.Equal(("Recorded", "Test Admin"), (recorded.Kind, recorded.ByName));
        var again = await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}");
        Assert.Equal((bond.Id, bond.Version, bond.Status, bond.ValidUntil), (again.Id, again.Version, again.Status, again.ValidUntil));
        Assert.Equal(bond.History.Select(item => item.Id), again.History.Select(item => item.Id));

        // Editing changes the details and the version.
        var edited = await client.ReadAsync<BondDto>(await EditAsync(client, bond, Edit(bond, kind: "Warranty", issuer: "Capital Bank", amountMinor: 750_000_00, note: "Two-year warranty")));
        Assert.Equal(("Warranty", "Capital Bank", 750_000_00L, "Two-year warranty"), (edited.Kind, edited.IssuerName, edited.AmountMinor, edited.Note));
        Assert.Equal(bond.Version + 1, edited.Version);
        Assert.Equal(new[] { "Edited", "Recorded" }, edited.History.Select(item => item.Kind).ToArray());

        // Extending moves the date later and keeps who, when and the old and new date.
        var extended = await client.ReadAsync<BondDto>(await ExtendAsync(client, edited, DaysFromToday(190), "Bank renewed it for 90 days"));
        Assert.Equal(DaysFromToday(190), Date(extended.ValidUntil));
        Assert.Equal(("Active", "Valid", 190), (extended.Status, extended.Expiry, extended.DaysLeft));
        var step = extended.History[0];
        Assert.Equal("Extended", step.Kind);
        Assert.Equal((DaysFromToday(100), DaysFromToday(190), "Bank renewed it for 90 days"),
            (Date(step.PreviousValidUntil!.Value), Date(step.NewValidUntil!.Value), step.Note));

        // Releasing closes it: no expiry state any more, and nothing else changes it.
        var released = await client.ReadAsync<BondDto>(await ReleaseAsync(client, extended, DaysFromToday(0), "Returned by the client"));
        Assert.Equal("Released", released.Status);
        Assert.Null(released.Expiry);
        Assert.Null(released.DaysLeft);
        Assert.Equal((DaysFromToday(0), "Returned by the client"), (Date(released.ReleasedOn!.Value), released.ReleaseNote));
        Assert.Equal(new[] { "Released", "Extended", "Edited", "Recorded" }, released.History.Select(item => item.Kind).ToArray());
    }

    [SqlServerFact]
    public async Task A_received_bond_names_the_firm_and_a_called_bond_keeps_the_note()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var bond = await CreateAsync(client, project.Id, "Received", "AdvancePayment", otherParty: "Dela Cruz Electrical");
        Assert.Equal(("Received", "AdvancePayment", "Dela Cruz Electrical"), (bond.Direction, bond.Kind, bond.OtherPartyName));

        var called = await client.ReadAsync<BondDto>(await CallAsync(client, bond, DaysFromToday(0), "The firm abandoned the site; claimed in full"));

        Assert.Equal("Called", called.Status);
        Assert.Equal((DaysFromToday(0), "The firm abandoned the site; claimed in full"), (Date(called.CalledOn!.Value), called.CallNote));
        Assert.Equal("Called", called.History[0].Kind);
    }

    [SqlServerFact]
    public async Task A_bond_entered_by_mistake_is_voided_with_a_reason_and_stays_on_record()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id);

        var voided = await client.ReadAsync<BondDto>(await VoidAsync(client, bond, "Entered against the wrong project"));

        Assert.Equal(("Void", "Entered against the wrong project"), (voided.Status, voided.VoidReason));
        Assert.Equal("Voided", voided.History[0].Kind);
        var again = await VoidAsync(client, voided);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already voided", await DetailAsync(again));

        // It stays readable and is findable by its state, but is left out of the project's own list.
        Assert.Equal("Void", (await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}")).Status);
        Assert.Equal(bond.Id, Assert.Single((await client.GetAsync<PagedResult<BondListItemDto>>("/api/bonds?status=Void")).Items).Id);
        Assert.Empty(await client.GetAsync<BondListItemDto[]>($"/api/projects/{project.Id}/bonds"));
    }

    [SqlServerFact]
    public async Task Released_and_called_bonds_are_final_and_a_wrong_release_is_voided_and_entered_again()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id);
        var released = await client.ReadAsync<BondDto>(await ReleaseAsync(client, bond));

        foreach (var refused in new[]
                 {
                     await EditAsync(client, released, Edit(released, note: "later")),
                     await ExtendAsync(client, released, DaysFromToday(300)),
                     await ReleaseAsync(client, released),
                     await CallAsync(client, released)
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("rule_violation", await RuleCodeAsync(refused));
            Assert.Contains("This bond was released", await DetailAsync(refused));
        }

        var voided = await client.ReadAsync<BondDto>(await VoidAsync(client, released, "Released the wrong bond"));
        var again = await CreateAsync(client, project.Id);
        Assert.Equal("Void", voided.Status);
        Assert.Equal("Active", again.Status);
    }

    [SqlServerFact]
    public async Task Every_problem_with_a_new_bond_is_reported_at_once()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var bad = await client.PostAsync("/api/bonds", NewBond(project.Id, direction: "Sideways", kind: "Mystery", issuer: " ", number: "", amountMinor: 0));
        Assert.Equal(new HashSet<string> { "direction", "kind", "issuerName", "bondNumber", "amountMinor" }, await ErrorFieldsAsync(bad));

        var dates = await client.PostAsync("/api/bonds", NewBond(project.Id, issuedOn: "2026-06-02", validUntil: "2026-06-01"));
        Assert.Equal("The bond can't be valid until a date before it was issued.", await ErrorMessageAsync(dates, "validUntil"));

        var tooBig = await client.PostAsync("/api/bonds", NewBond(project.Id, amountMinor: 10_000_000_000_001L));
        Assert.Equal("The bond amount can be at most ₱100,000,000,000.00.", await ErrorMessageAsync(tooBig, "amountMinor"));

        var posted = await client.PostAsync("/api/bonds", NewBond(project.Id, direction: "Posted", otherParty: "Dela Cruz Electrical"));
        Assert.Contains("otherPartyName", await ErrorFieldsAsync(posted));

        var numeric = await client.PostAsync("/api/bonds", NewBond(project.Id, kind: "2"));
        Assert.Contains("kind", await ErrorFieldsAsync(numeric));
    }

    [SqlServerFact]
    public async Task Extending_needs_a_later_date_and_editing_cannot_move_the_date_earlier()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id, validUntil: DaysFromToday(60));

        var same = await ExtendAsync(client, bond, DaysFromToday(60));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, same.StatusCode);
        Assert.Contains("must be later than the current valid-until date", await ErrorMessageAsync(same, "validUntil"));
        var earlier = await EditAsync(client, bond, Edit(bond, validUntil: DaysFromToday(59)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlier.StatusCode);
        Assert.Contains("can't be changed to an earlier date", await ErrorMessageAsync(earlier, "validUntil"));

        // Nothing changed, and an edit that moves the date later is recorded with the old and new date.
        var current = await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}");
        Assert.Equal(bond.Version, current.Version);
        var moved = await client.ReadAsync<BondDto>(await EditAsync(client, current, Edit(current, validUntil: DaysFromToday(70))));
        Assert.Equal((DaysFromToday(60), DaysFromToday(70)), (Date(moved.History[0].PreviousValidUntil!.Value), Date(moved.History[0].NewValidUntil!.Value)));

        // Saving the same details again is not a change.
        var unchanged = await client.ReadAsync<BondDto>(await EditAsync(client, moved, Edit(moved)));
        Assert.Equal(moved.Version, unchanged.Version);
        Assert.Equal(moved.History.Count, unchanged.History.Count);
    }

    [SqlServerFact]
    public async Task Release_and_call_dates_are_not_in_the_future_or_before_the_bond_was_issued_and_a_call_needs_a_note()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id);

        var future = await ReleaseAsync(client, bond, DaysFromToday(1));
        Assert.Equal("The date can't be in the future.", await ErrorMessageAsync(future, "releasedOn"));
        var early = await CallAsync(client, bond, "2024-12-31");
        Assert.Contains("before the bond was issued", await ErrorMessageAsync(early, "calledOn"));
        var noNote = await CallAsync(client, bond, note: " ");
        Assert.Contains("note", await ErrorFieldsAsync(noNote));
        var noReason = await VoidAsync(client, bond, reason: " ");
        Assert.Contains("reason", await ErrorFieldsAsync(noReason));
        Assert.Equal("Active", (await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}")).Status);
    }

    [SqlServerFact]
    public async Task A_stale_action_is_a_conflict_and_changes_nothing()
    {
        var (client, user) = await SignedInAsync(_factory);
        using var _c = client;
        using var other = await AnotherSessionAsync(_factory, user);
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id);

        // A colleague extends it; the first person then acts on the version they opened.
        await ExtendAsync(other, bond, DaysFromToday(150));
        foreach (var stale in new[]
                 {
                     await EditAsync(client, bond, Edit(bond, note: "mine")),
                     await ExtendAsync(client, bond, DaysFromToday(200)),
                     await ReleaseAsync(client, bond),
                     await CallAsync(client, bond),
                     await VoidAsync(client, bond)
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("concurrency_conflict", await RuleCodeAsync(stale));
        }

        var current = await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}");
        Assert.Equal(("Active", DaysFromToday(150)), (current.Status, Date(current.ValidUntil)));
        Assert.Equal(2, current.History.Count);
    }

    [SqlServerFact]
    public async Task An_archived_project_is_read_only_for_its_bonds()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var bond = await CreateAsync(client, project.Id);
        var closed = await CreateAsync(client, project.Id);
        await ReleaseAsync(client, closed);
        await ArchiveAsync(client, await client.GetAsync<ProjectDto>($"/api/projects/{project.Id}"));

        var read = await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}");
        Assert.True(read.ProjectArchived);
        foreach (var refused in new[]
                 {
                     await client.PostAsync("/api/bonds", NewBond(project.Id)),
                     await EditAsync(client, read, Edit(read, note: "changed")),
                     await ExtendAsync(client, read, DaysFromToday(200)),
                     await ReleaseAsync(client, read),
                     await CallAsync(client, read),
                     await VoidAsync(client, read)
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("rule_violation", await RuleCodeAsync(refused));
            Assert.Contains("archived", await DetailAsync(refused));
        }

        // Reading still works: the register, the bond and the project's list.
        Assert.Equal(2, (await client.GetAsync<PagedResult<BondListItemDto>>($"/api/bonds?projectId={project.Id}")).Total);
        Assert.Equal(2, (await client.GetAsync<BondListItemDto[]>($"/api/projects/{project.Id}/bonds")).Length);
    }

    [SqlServerFact]
    public async Task A_repeated_create_returns_the_original_and_a_request_id_reused_for_another_project_is_refused()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var other = await CreateProjectAsync(client);
        var request = NewBond(project.Id, clientRequestId: "form-1");

        var first = await client.PostAsync<BondDto>("/api/bonds", request);
        var repeat = await client.PostAsync<BondDto>("/api/bonds", request);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(1, (await client.GetAsync<PagedResult<BondListItemDto>>($"/api/bonds?projectId={project.Id}")).Total);

        var reused = await client.PostAsync("/api/bonds", NewBond(other.Id, clientRequestId: "form-1"));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Contains("already used", await DetailAsync(reused));
    }

    [SqlServerFact]
    public async Task The_register_filters_and_sorts_by_state_and_expiry_with_the_thirty_day_edge()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        var other = await CreateProjectAsync(client);

        var expired = await CreateAsync(client, project.Id, validUntil: DaysFromToday(-1));
        var lastDay = await CreateAsync(client, project.Id, "Received", "Warranty", DaysFromToday(0));
        var edge30 = await CreateAsync(client, project.Id, "Posted", "Bid", DaysFromToday(30));
        var edge31 = await CreateAsync(client, project.Id, "Received", "Performance", DaysFromToday(31));
        var valid = await CreateAsync(client, other.Id, "Posted", "AdvancePayment", DaysFromToday(200));
        var released = await CreateAsync(client, project.Id, validUntil: DaysFromToday(-40));
        await ReleaseAsync(client, released);

        Assert.Equal(("Expired", -1), (expired.Expiry, expired.DaysLeft));
        Assert.Equal(("ExpiringSoon", 0), (lastDay.Expiry, lastDay.DaysLeft));
        Assert.Equal(("ExpiringSoon", 30), (edge30.Expiry, edge30.DaysLeft));
        Assert.Equal(("Valid", 31), (edge31.Expiry, edge31.DaysLeft));

        // Active bonds first, the one that lapses soonest on top; the closed one last.
        var all = await client.GetAsync<PagedResult<BondListItemDto>>("/api/bonds?pageSize=100");
        Assert.Equal(new[] { expired.Id, lastDay.Id, edge30.Id, edge31.Id, valid.Id, released.Id }, all.Items.Select(item => item.Id).ToArray());
        Assert.Null(all.Items[^1].Expiry);

        Assert.Equal(new[] { expired.Id }, await IdsAsync(client, "expiry=Expired"));
        Assert.Equal(new[] { lastDay.Id, edge30.Id }, await IdsAsync(client, "expiry=ExpiringSoon"));
        Assert.Equal(new[] { edge31.Id, valid.Id }, await IdsAsync(client, "expiry=Valid"));
        Assert.Equal(new[] { lastDay.Id, edge31.Id }, await IdsAsync(client, "direction=Received"));
        Assert.Equal(new[] { edge30.Id }, await IdsAsync(client, "kind=Bid"));
        Assert.Equal(new[] { released.Id }, await IdsAsync(client, "status=Released"));
        Assert.Equal(new[] { valid.Id }, await IdsAsync(client, $"projectId={other.Id}"));
        Assert.Equal(new[] { lastDay.Id }, await IdsAsync(client, $"projectId={project.Id}&direction=Received&expiry=ExpiringSoon"));

        // Paging.
        var page = await client.GetAsync<PagedResult<BondListItemDto>>("/api/bonds?pageSize=4&page=2");
        Assert.Equal((6, 4, 2), (page.Total, page.PageSize, page.Page));
        Assert.Equal(new[] { valid.Id, released.Id }, page.Items.Select(item => item.Id).ToArray());

        // A word that is not a state is a field error, not an empty list.
        var bad = await client.GetAsync("/api/bonds?expiry=Soonish");
        Assert.Contains("expiry", await ErrorFieldsAsync(bad));
        Assert.Contains("status", await ErrorFieldsAsync(await client.GetAsync("/api/bonds?status=3")));

        // The project's own list leaves out nothing but voided bonds and has the Active ones first.
        var mine = await client.GetAsync<BondListItemDto[]>($"/api/projects/{project.Id}/bonds");
        Assert.Equal(new[] { expired.Id, lastDay.Id, edge30.Id, edge31.Id, released.Id }, mine.Select(item => item.Id).ToArray());
    }

    [SqlServerFact]
    public async Task The_totals_add_up_the_active_amounts_by_direction_and_count_the_bonds_that_need_attention()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);

        var empty = await client.GetAsync<BondTotalsDto>("/api/bonds/totals");
        Assert.Equal(("PHP", 0, 0L, 0, 0L, 0, 0), (empty.CurrencyCode, empty.Posted.ActiveCount, empty.Posted.ActiveAmountMinor, empty.Received.ActiveCount,
            empty.Received.ActiveAmountMinor, empty.ExpiredCount, empty.ExpiringSoonCount));

        await CreateAsync(client, project.Id, "Posted", "Performance", DaysFromToday(100), 5_000_000_00);
        await CreateAsync(client, project.Id, "Posted", "Bid", DaysFromToday(10), 1_250_000_50);
        await CreateAsync(client, project.Id, "Received", "Warranty", DaysFromToday(-3), 300_000_00);
        var released = await CreateAsync(client, project.Id, "Posted", "Warranty", DaysFromToday(50), 999_00);
        await ReleaseAsync(client, released);
        var voided = await CreateAsync(client, project.Id, "Received", "Bid", DaysFromToday(5), 888_00);
        await VoidAsync(client, voided);
        var called = await CreateAsync(client, project.Id, "Received", "Performance", DaysFromToday(70), 777_00);
        await CallAsync(client, called);

        var totals = await client.GetAsync<BondTotalsDto>("/api/bonds/totals");

        Assert.Equal((2, 6_250_000_50L), (totals.Posted.ActiveCount, totals.Posted.ActiveAmountMinor));
        Assert.Equal((1, 300_000_00L), (totals.Received.ActiveCount, totals.Received.ActiveAmountMinor));
        Assert.Equal((1, 1), (totals.ExpiredCount, totals.ExpiringSoonCount));
    }

    [SqlServerFact]
    public async Task A_bond_changes_no_money_figure_anywhere()
    {
        var (client, _) = await SignedInAsync(_factory);
        using var _c = client;
        var project = await CreateProjectAsync(client);
        await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line());
        await client.PostAsync<CostEntryDto>($"/api/projects/{project.Id}/cost-entries", Cost());
        string[] urls = [$"/api/projects/{project.Id}/cost-summary", $"/api/projects/{project.Id}", "/api/reports/dashboard"];
        var before = await TextsAsync(client, urls);

        var bond = await CreateAsync(client, project.Id, amountMinor: 90_000_000_00);
        await ExtendAsync(client, bond, DaysFromToday(300));
        await ReleaseAsync(client, await client.GetAsync<BondDto>($"/api/bonds/{bond.Id}"));

        Assert.Equal(before, await TextsAsync(client, urls));
    }

    private static async Task<string[]> TextsAsync(ApiClient client, string[] urls)
    {
        var texts = new List<string>();
        foreach (var url in urls)
        {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, $"{url} answered {(int)response.StatusCode}");
            texts.Add(await response.Content.ReadAsStringAsync());
        }
        return texts.ToArray();
    }

    private static async Task<Guid[]> IdsAsync(ApiClient client, string query) =>
        (await client.GetAsync<PagedResult<BondListItemDto>>($"/api/bonds?pageSize=100&{query}")).Items.Select(item => item.Id).ToArray();
}
