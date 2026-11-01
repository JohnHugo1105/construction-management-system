using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The bonds register rules: the dates, every refusal, extend, the final states and the expiry boundaries (docs/development/notes/bonds-rules.md).</summary>
public sealed class BondTests
{
    private static readonly DateOnly Today = TestData.Today; // 2 Nov 2026
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly Guid User = TestData.UserId;

    private static BondDetails Details(BondKind kind = BondKind.Performance, string? issuer = "Metro Surety", string? number = "PB-2026-014",
        long amountMinor = 5_000_000_00, DateOnly? issuedOn = null, DateOnly? validUntil = null, string? otherParty = null, string? note = null) =>
        new(kind, issuer, number, amountMinor, issuedOn ?? new DateOnly(2026, 6, 1), validUntil ?? new DateOnly(2027, 6, 1), otherParty, note);

    private static (Bond Bond, Project Project) Active(BondDirection direction = BondDirection.Posted, BondDetails? details = null)
    {
        var project = TestData.NewProject();
        var bond = Bond.Create(Guid.NewGuid(), TestData.Tenant, project, direction, details ?? Details(), "PHP", User, null, Now);
        return (bond, project);
    }

    private static Project ArchivedProject(Project project)
    {
        project.Archive(ArchiveReason.Finished, null, Now);
        return project;
    }

    private static IReadOnlyDictionary<string, string[]> Errors(Action action) => Assert.Throws<ValidationFailedException>(action).Errors;

    // ---- Recording a bond -----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_bond_is_active_keeps_the_company_currency_and_starts_its_history()
    {
        var (bond, project) = Active(BondDirection.Received, Details(BondKind.AdvancePayment, "  Metro Surety ", " AP-77 ", 1_250_000_50,
            otherParty: " Dela Cruz Electrical ", note: " original on file "));

        Assert.Equal(BondStatus.Active, bond.Status);
        Assert.Equal(project.Id, bond.ProjectId);
        Assert.Equal(BondDirection.Received, bond.Direction);
        Assert.Equal(BondKind.AdvancePayment, bond.Kind);
        Assert.Equal("Metro Surety", bond.IssuerName);
        Assert.Equal("AP-77", bond.BondNumber);
        Assert.Equal(1_250_000_50, bond.AmountMinor);
        Assert.Equal("PHP", bond.CurrencyCode);
        Assert.Equal("Dela Cruz Electrical", bond.OtherPartyName);
        Assert.Equal("original on file", bond.Note);
        var recorded = Assert.Single(bond.Events);
        Assert.Equal(BondEventKind.Recorded, recorded.Kind);
        Assert.Equal(User, recorded.ByUserId);
        Assert.Equal(Now, recorded.AtUtc);
    }

    [Fact]
    public void Every_problem_with_a_new_bond_is_reported_at_once()
    {
        var errors = Errors(() => Bond.Create(Guid.NewGuid(), TestData.Tenant, TestData.NewProject(), (BondDirection)(-1),
            new BondDetails((BondKind)(-1), " ", "", 0, default, default, null, null), "PHP", User, null, Now));

        Assert.Equal(new HashSet<string> { "direction", "kind", "issuerName", "bondNumber", "amountMinor", "issuedOn", "validUntil" }, errors.Keys.ToHashSet());
        Assert.Equal("Enter the bond amount, more than zero.", Assert.Single(errors["amountMinor"]));
        Assert.Equal("Enter the bank or surety that issued it.", Assert.Single(errors["issuerName"]));
    }

    [Fact]
    public void The_amount_is_bounded_by_the_money_limit_and_must_be_more_than_zero()
    {
        var (atLimit, _) = Active(details: Details(amountMinor: MoneyLimits.MaximumAmountMinor));
        Assert.Equal(MoneyLimits.MaximumAmountMinor, atLimit.AmountMinor);

        var over = Errors(() => Active(details: Details(amountMinor: MoneyLimits.MaximumAmountMinor + 1)));
        Assert.Equal("The bond amount can be at most ₱100,000,000,000.00.", Assert.Single(over["amountMinor"]));
        Assert.Contains("amountMinor", Errors(() => Active(details: Details(amountMinor: -1))).Keys);
    }

    [Fact]
    public void Text_lengths_have_exact_boundaries()
    {
        Active(details: Details(issuer: new string('i', 120), number: new string('n', 60), note: new string('x', 500)));
        Active(BondDirection.Received, Details(otherParty: new string('o', 120)));

        var errors = Errors(() => Active(BondDirection.Received, Details(issuer: new string('i', 121), number: new string('n', 61), note: new string('x', 501),
            otherParty: new string('o', 121))));

        Assert.Equal(new HashSet<string> { "issuerName", "bondNumber", "note", "otherPartyName" }, errors.Keys.ToHashSet());
    }

    [Fact]
    public void The_valid_until_date_can_be_the_issue_date_but_not_before_it()
    {
        var (sameDay, _) = Active(details: Details(issuedOn: new DateOnly(2026, 6, 1), validUntil: new DateOnly(2026, 6, 1)));
        Assert.Equal(new DateOnly(2026, 6, 1), sameDay.ValidUntil);

        var errors = Errors(() => Active(details: Details(issuedOn: new DateOnly(2026, 6, 2), validUntil: new DateOnly(2026, 6, 1))));
        Assert.Equal("The bond can't be valid until a date before it was issued.", Assert.Single(errors["validUntil"]));
    }

    [Fact]
    public void Dates_outside_a_sensible_range_are_refused()
    {
        var errors = Errors(() => Active(details: Details(issuedOn: new DateOnly(1999, 12, 31), validUntil: new DateOnly(2101, 1, 1))));

        Assert.Equal("Choose a valid date.", Assert.Single(errors["issuedOn"]));
        Assert.Equal("Choose a valid date.", Assert.Single(errors["validUntil"]));
    }

    [Fact]
    public void Only_a_received_bond_names_the_firm_that_gave_it()
    {
        Active(BondDirection.Posted, Details(otherParty: "  "));
        var errors = Errors(() => Active(BondDirection.Posted, Details(otherParty: "Dela Cruz Electrical")));

        Assert.Contains("Only a bond you received", Assert.Single(errors["otherPartyName"]));
    }

    [Fact]
    public void A_bond_that_has_already_lapsed_can_still_be_recorded_and_is_shown_as_expired()
    {
        var (bond, _) = Active(details: Details(issuedOn: new DateOnly(2025, 1, 1), validUntil: new DateOnly(2026, 1, 1)));

        Assert.Equal(BondExpiry.Expired, bond.ExpiryOn(Today));
    }

    // ---- Expiry --------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, BondExpiry.Expired)]
    [InlineData(0, BondExpiry.ExpiringSoon)]
    [InlineData(1, BondExpiry.ExpiringSoon)]
    [InlineData(29, BondExpiry.ExpiringSoon)]
    [InlineData(30, BondExpiry.ExpiringSoon)]
    [InlineData(31, BondExpiry.Valid)]
    [InlineData(365, BondExpiry.Valid)]
    public void Expiry_is_judged_on_the_days_left_with_the_thirty_day_edge_inclusive(int daysLeft, BondExpiry expected)
    {
        Assert.Equal(expected, Bond.ExpiryFor(Today.AddDays(daysLeft), Today));
        Assert.Equal(daysLeft, Bond.DaysLeft(Today.AddDays(daysLeft), Today));
    }

    [Fact]
    public void The_last_valid_day_is_still_in_force_and_the_next_day_is_expired()
    {
        var (bond, _) = Active(details: Details(validUntil: Today));

        Assert.Equal(BondExpiry.ExpiringSoon, bond.ExpiryOn(Today));
        Assert.Equal(BondExpiry.Expired, bond.ExpiryOn(Today.AddDays(1)));
    }

    [Fact]
    public void Expiry_is_judged_on_the_company_calendar_day_not_the_UTC_day()
    {
        // 17:00 UTC on 1 Nov 2026 is 01:00 on 2 Nov in Manila: a bond valid until 1 Nov has already lapsed for the company.
        var instant = new DateTimeOffset(2026, 11, 1, 17, 0, 0, TimeSpan.Zero);
        var organization = Organization.Create(Guid.NewGuid(), "Test Builders", Now);
        var manilaToday = organization.TodayLocal(instant);
        var validUntil = new DateOnly(2026, 11, 1);

        Assert.Equal(new DateOnly(2026, 11, 2), manilaToday);
        Assert.Equal(BondExpiry.Expired, Bond.ExpiryFor(validUntil, manilaToday));
        Assert.Equal(BondExpiry.ExpiringSoon, Bond.ExpiryFor(validUntil, DateOnly.FromDateTime(instant.UtcDateTime)));
    }

    [Fact]
    public void A_bond_that_is_no_longer_active_has_no_expiry_state()
    {
        var (bond, project) = Active(details: Details(validUntil: Today.AddDays(-10)));
        bond.Release(project, Today, null, User, Today, Now);

        Assert.Null(bond.ExpiryOn(Today));
    }

    // ---- Editing ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_active_bond_is_edited_and_the_change_is_recorded_once()
    {
        var (bond, project) = Active();

        var changed = bond.Edit(project, Details(BondKind.Warranty, "Capital Bank", "WB-9", 750_000_00, new DateOnly(2026, 6, 1), new DateOnly(2027, 6, 1),
            note: "Two-year warranty"), User, Now.AddHours(1));

        Assert.True(changed);
        Assert.Equal(BondKind.Warranty, bond.Kind);
        Assert.Equal("Capital Bank", bond.IssuerName);
        Assert.Equal(750_000_00, bond.AmountMinor);
        Assert.Equal(new[] { BondEventKind.Recorded, BondEventKind.Edited }, bond.Events.Select(item => item.Kind).ToArray());
        Assert.Null(bond.Events[1].NewValidUntil);
    }

    [Fact]
    public void Saving_the_same_details_changes_nothing_and_records_nothing()
    {
        var (bond, project) = Active();
        var updatedBefore = bond.UpdatedAtUtc;

        Assert.False(bond.Edit(project, Details(), User, Now.AddHours(1)));

        Assert.Single(bond.Events);
        Assert.Equal(updatedBefore, bond.UpdatedAtUtc);
    }

    [Fact]
    public void An_active_bond_cannot_be_edited_into_an_earlier_valid_until_date()
    {
        var (bond, project) = Active(details: Details(validUntil: new DateOnly(2027, 6, 1)));

        var errors = Errors(() => bond.Edit(project, Details(validUntil: new DateOnly(2027, 5, 31)), User, Now));

        Assert.Equal("An active bond can't be changed to an earlier date than 1 Jun 2027. Extend it to move the date later.", Assert.Single(errors["validUntil"]));
        Assert.Equal(new DateOnly(2027, 6, 1), bond.ValidUntil);
    }

    [Fact]
    public void An_edit_that_moves_the_date_later_records_the_old_and_new_date()
    {
        var (bond, project) = Active(details: Details(validUntil: new DateOnly(2027, 6, 1)));

        Assert.True(bond.Edit(project, Details(validUntil: new DateOnly(2027, 9, 1)), User, Now));

        var edited = bond.Events[^1];
        Assert.Equal(BondEventKind.Edited, edited.Kind);
        Assert.Equal(new DateOnly(2027, 6, 1), edited.PreviousValidUntil);
        Assert.Equal(new DateOnly(2027, 9, 1), edited.NewValidUntil);
    }

    [Fact]
    public void A_refused_edit_leaves_the_bond_exactly_as_it_was()
    {
        var (bond, project) = Active();

        Errors(() => bond.Edit(project, Details(BondKind.Warranty, issuer: " ", amountMinor: 0), User, Now));

        Assert.Equal(BondKind.Performance, bond.Kind);
        Assert.Equal("Metro Surety", bond.IssuerName);
        Assert.Equal(5_000_000_00, bond.AmountMinor);
        Assert.Single(bond.Events);
    }

    // ---- Extending ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Extending_moves_the_date_later_and_records_the_old_and_new_date_with_the_note()
    {
        var (bond, project) = Active(details: Details(validUntil: new DateOnly(2027, 6, 1)));

        bond.Extend(project, new DateOnly(2027, 12, 1), "  Bank renewed it for six months ", User, Now.AddDays(1));

        Assert.Equal(new DateOnly(2027, 12, 1), bond.ValidUntil);
        Assert.Equal(BondStatus.Active, bond.Status);
        var extended = bond.Events[^1];
        Assert.Equal(BondEventKind.Extended, extended.Kind);
        Assert.Equal(new DateOnly(2027, 6, 1), extended.PreviousValidUntil);
        Assert.Equal(new DateOnly(2027, 12, 1), extended.NewValidUntil);
        Assert.Equal("Bank renewed it for six months", extended.Note);
        Assert.Equal(Now.AddDays(1), bond.UpdatedAtUtc);
    }

    [Fact]
    public void Extending_one_day_later_is_allowed_and_the_same_or_an_earlier_day_is_refused()
    {
        var (bond, project) = Active(details: Details(validUntil: new DateOnly(2027, 6, 1)));
        bond.Extend(project, new DateOnly(2027, 6, 2), null, User, Now);

        var same = Errors(() => bond.Extend(project, new DateOnly(2027, 6, 2), null, User, Now));
        var earlier = Errors(() => bond.Extend(project, new DateOnly(2027, 6, 1), null, User, Now));

        Assert.Equal("The new date must be later than the current valid-until date, 2 Jun 2027.", Assert.Single(same["validUntil"]));
        Assert.Contains("must be later", Assert.Single(earlier["validUntil"]));
        Assert.Equal(new DateOnly(2027, 6, 2), bond.ValidUntil);
        Assert.Equal(2, bond.Events.Count);
    }

    [Fact]
    public void An_expired_bond_can_be_extended_and_becomes_valid_again()
    {
        var (bond, project) = Active(details: Details(issuedOn: new DateOnly(2025, 1, 1), validUntil: Today.AddDays(-5)));
        Assert.Equal(BondExpiry.Expired, bond.ExpiryOn(Today));

        bond.Extend(project, Today.AddDays(90), null, User, Now);

        Assert.Equal(BondExpiry.Valid, bond.ExpiryOn(Today));
    }

    [Fact]
    public void Extending_needs_a_date_and_a_sensible_one()
    {
        var (bond, project) = Active();

        Assert.Equal("Choose the new valid-until date.", Assert.Single(Errors(() => bond.Extend(project, default, null, User, Now))["validUntil"]));
        Assert.Equal("Choose a valid date.", Assert.Single(Errors(() => bond.Extend(project, new DateOnly(2101, 1, 1), null, User, Now))["validUntil"]));
        Assert.Contains("note", Errors(() => bond.Extend(project, new DateOnly(2028, 1, 1), new string('x', 501), User, Now)).Keys);
    }

    // ---- Releasing and calling -----------------------------------------------------------------------------------------------

    [Fact]
    public void Releasing_closes_the_bond_with_the_date_and_note()
    {
        var (bond, project) = Active();

        bond.Release(project, Today, " Returned by the client ", User, Today, Now);

        Assert.Equal(BondStatus.Released, bond.Status);
        Assert.Equal(Today, bond.ReleasedOn);
        Assert.Equal("Returned by the client", bond.ReleaseNote);
        var released = bond.Events[^1];
        Assert.Equal(BondEventKind.Released, released.Kind);
        Assert.Equal(Today, released.EventDate);
    }

    [Fact]
    public void A_release_date_is_not_in_the_future_and_not_before_the_bond_was_issued()
    {
        var (bond, project) = Active(details: Details(issuedOn: new DateOnly(2026, 6, 1)));

        Assert.Equal("The date can't be in the future.", Assert.Single(Errors(() => bond.Release(project, Today.AddDays(1), null, User, Today, Now))["releasedOn"]));
        Assert.Equal("The date can't be before the bond was issued on 1 Jun 2026.",
            Assert.Single(Errors(() => bond.Release(project, new DateOnly(2026, 5, 31), null, User, Today, Now))["releasedOn"]));
        Assert.Equal("Choose the date it was released.", Assert.Single(Errors(() => bond.Release(project, default, null, User, Today, Now))["releasedOn"]));

        bond.Release(project, new DateOnly(2026, 6, 1), null, User, Today, Now); // the issue day itself is allowed
        Assert.Equal(BondStatus.Released, bond.Status);
    }

    [Fact]
    public void Marking_a_bond_as_called_needs_a_date_and_a_note_and_closes_it()
    {
        var (bond, project) = Active();

        var noNote = Errors(() => bond.MarkCalled(project, Today, " ", User, Today, Now));
        Assert.Equal("Enter a note on what was claimed.", Assert.Single(noNote["note"]));
        Assert.Equal(BondStatus.Active, bond.Status);

        bond.MarkCalled(project, Today.AddDays(-3), "Client claimed after the roof defect", User, Today, Now);

        Assert.Equal(BondStatus.Called, bond.Status);
        Assert.Equal(Today.AddDays(-3), bond.CalledOn);
        Assert.Equal("Client claimed after the roof defect", bond.CallNote);
        Assert.Equal(BondEventKind.Called, bond.Events[^1].Kind);
    }

    [Fact]
    public void A_call_date_follows_the_same_date_rules_as_a_release()
    {
        var (bond, project) = Active();

        Assert.Contains("future", Assert.Single(Errors(() => bond.MarkCalled(project, Today.AddDays(1), "x", User, Today, Now))["calledOn"]));
        Assert.Contains("before the bond was issued", Assert.Single(Errors(() => bond.MarkCalled(project, new DateOnly(2026, 5, 1), "x", User, Today, Now))["calledOn"]));
    }

    // ---- Final states and voiding ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_released_bond_is_final_nothing_but_a_void_changes_it()
    {
        var (bond, project) = Active();
        bond.Release(project, Today, null, User, Today, Now);

        var refusals = new Action[]
        {
            () => bond.Edit(project, Details(note: "changed"), User, Now),
            () => bond.Extend(project, new DateOnly(2028, 1, 1), null, User, Now),
            () => bond.Release(project, Today, null, User, Today, Now),
            () => bond.MarkCalled(project, Today, "claimed", User, Today, Now)
        };
        foreach (var refusal in refusals)
            Assert.Contains("This bond was released", Assert.Throws<DomainRuleException>(refusal).Message);

        bond.Void(project, "Released the wrong bond", User, Now);
        Assert.Equal(BondStatus.Void, bond.Status);
    }

    [Fact]
    public void A_called_bond_is_final_nothing_but_a_void_changes_it()
    {
        var (bond, project) = Active();
        bond.MarkCalled(project, Today, "Claimed", User, Today, Now);

        foreach (var refusal in new Action[]
                 {
                     () => bond.Edit(project, Details(note: "changed"), User, Now),
                     () => bond.Extend(project, new DateOnly(2028, 1, 1), null, User, Now),
                     () => bond.Release(project, Today, null, User, Today, Now),
                     () => bond.MarkCalled(project, Today, "again", User, Today, Now)
                 })
            Assert.Contains("This bond was called", Assert.Throws<DomainRuleException>(refusal).Message);

        bond.Void(project, "Entered against the wrong project", User, Now);
        Assert.Equal(BondStatus.Void, bond.Status);
    }

    [Fact]
    public void Voiding_needs_a_reason_and_keeps_the_bond_on_record()
    {
        var (bond, project) = Active();

        Assert.Equal("Enter the reason for voiding it.", Assert.Single(Errors(() => bond.Void(project, " ", User, Now))["reason"]));
        Assert.Contains("reason", Errors(() => bond.Void(project, new string('x', 301), User, Now)).Keys);
        Assert.Equal(BondStatus.Active, bond.Status);

        bond.Void(project, "  Entered twice ", User, Now);

        Assert.Equal(BondStatus.Void, bond.Status);
        Assert.Equal("Entered twice", bond.VoidReason);
        Assert.Equal(BondEventKind.Voided, bond.Events[^1].Kind);
        Assert.Null(bond.ExpiryOn(Today));
    }

    [Fact]
    public void A_voided_bond_refuses_every_further_step()
    {
        var (bond, project) = Active();
        bond.Void(project, "Entered twice", User, Now);

        Assert.Equal("This bond was already voided.", Assert.Throws<DomainRuleException>(() => bond.Void(project, "again", User, Now)).Message);
        foreach (var refusal in new Action[]
                 {
                     () => bond.Edit(project, Details(note: "changed"), User, Now),
                     () => bond.Extend(project, new DateOnly(2028, 1, 1), null, User, Now),
                     () => bond.Release(project, Today, null, User, Today, Now),
                     () => bond.MarkCalled(project, Today, "claimed", User, Today, Now)
                 })
            Assert.Contains("This bond was voided", Assert.Throws<DomainRuleException>(refusal).Message);
    }

    // ---- Archived project and wrong project -------------------------------------------------------------------------------------

    [Fact]
    public void An_archived_project_refuses_every_change()
    {
        var (bond, project) = Active();
        ArchivedProject(project);

        foreach (var refusal in new Action[]
                 {
                     () => Bond.Create(Guid.NewGuid(), TestData.Tenant, project, BondDirection.Posted, Details(), "PHP", User, null, Now),
                     () => bond.Edit(project, Details(note: "changed"), User, Now),
                     () => bond.Extend(project, new DateOnly(2028, 1, 1), null, User, Now),
                     () => bond.Release(project, Today, null, User, Today, Now),
                     () => bond.MarkCalled(project, Today, "claimed", User, Today, Now),
                     () => bond.Void(project, "mistake", User, Now)
                 })
            Assert.Equal("This project is archived. Restore it before making changes.", Assert.Throws<DomainRuleException>(refusal).Message);

        Assert.Equal(BondStatus.Active, bond.Status);
        Assert.Single(bond.Events);
    }

    [Fact]
    public void A_step_with_another_project_is_a_programming_error()
    {
        var (bond, _) = Active();

        Assert.Throws<ArgumentException>(() => bond.Void(TestData.NewProject("Another"), "mistake", User, Now));
    }
}
