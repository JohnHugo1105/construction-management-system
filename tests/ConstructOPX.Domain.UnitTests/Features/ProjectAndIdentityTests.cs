using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.UnitTests.Features;

public sealed class ProjectTests
{
    [Fact]
    public void A_new_project_starts_in_planning_with_a_normalized_name_for_duplicate_checks()
    {
        var project = TestData.NewProject("  Tower A  ");

        Assert.Equal(ProjectStatus.Planning, project.Status);
        Assert.Equal("Tower A", project.Name);
        Assert.Equal("TOWER A", project.NormalizedName);
    }

    [Fact]
    public void Details_are_validated_together()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Project.Create(Guid.NewGuid(), TestData.Tenant, " ", null, null, -5,
            default, null, TestData.Now));

        Assert.Contains("name", ex.Errors.Keys);
        Assert.Contains("contractPriceMinor", ex.Errors.Keys);
        Assert.Contains("startDate", ex.Errors.Keys);
    }

    [Fact]
    public void The_target_end_date_can_not_precede_the_start()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Project.Create(Guid.NewGuid(), TestData.Tenant, "X", null, null, null,
            new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1), TestData.Now));
        Assert.Contains("targetEndDate", ex.Errors.Keys);
    }

    [Fact]
    public void Follows_the_lifecycle_and_rejects_impossible_moves()
    {
        var project = TestData.NewProject();

        Assert.Throws<DomainRuleException>(() => project.PutOnHold(TestData.Now));
        project.Start(TestData.Now);
        project.PutOnHold(TestData.Now);
        project.Resume(TestData.Now);
        project.Complete(TestData.Now);
        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Throws<DomainRuleException>(() => project.Start(TestData.Now));
        project.Reopen(TestData.Now);
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void Archiving_needs_a_note_when_the_reason_is_other_and_restoring_returns_the_previous_status()
    {
        var project = TestData.NewProject();
        project.Start(TestData.Now);

        Assert.Throws<ValidationFailedException>(() => project.Archive(ArchiveReason.Other, " ", TestData.Now));
        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.True(project.IsArchived);
        Assert.Equal(ProjectStatus.Active, project.StatusBeforeArchive);

        project.Restore(TestData.Now);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Null(project.ArchiveReason);
        Assert.Throws<DomainRuleException>(() => project.Restore(TestData.Now));
    }

    [Fact]
    public void An_archived_project_is_read_only()
    {
        var project = TestData.NewProject();
        project.Archive(ArchiveReason.Finished, null, TestData.Now);

        Assert.Throws<DomainRuleException>(() => project.UpdateDetails("New name", null, null, null, new DateOnly(2026, 1, 5), null, TestData.Now));
        Assert.Throws<DomainRuleException>(() => project.AddSubProject(Guid.NewGuid(), "Phase 2", new DateOnly(2026, 6, 1), TestData.Now));
        Assert.Throws<DomainRuleException>(() => project.EnsureCanReceiveRecordsFor(null));
    }

    [Fact]
    public void Sub_projects_are_unique_within_a_project_ignoring_case_and_follow_the_project_into_the_archive()
    {
        var project = TestData.NewProject();
        var sub = project.AddSubProject(Guid.NewGuid(), "Building 1", new DateOnly(2026, 2, 1), TestData.Now);

        Assert.Throws<DomainRuleException>(() => project.AddSubProject(Guid.NewGuid(), " building 1 ", new DateOnly(2026, 2, 1), TestData.Now));
        project.EnsureCanReceiveRecordsFor(sub.Id);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Equal(SubProjectStatus.Archived, sub.Status);
        project.Restore(TestData.Now);
        Assert.Equal(SubProjectStatus.Active, sub.Status);
    }

    [Fact]
    public void Only_active_sub_projects_of_this_project_receive_records()
    {
        var project = TestData.NewProject();
        var sub = project.AddSubProject(Guid.NewGuid(), "Building 1", new DateOnly(2026, 2, 1), TestData.Now);
        project.ArchiveSubProject(sub.Id, TestData.Now);

        Assert.Throws<DomainRuleException>(() => project.EnsureCanReceiveRecordsFor(sub.Id));
        Assert.Throws<ValidationFailedException>(() => project.EnsureCanReceiveRecordsFor(Guid.NewGuid()));
    }
}

public sealed class UserTests
{
    private static User NewUser(AccessRole roles = AccessRole.CostEngineer) =>
        User.Create(Guid.NewGuid(), TestData.Tenant, "  Maria.Santos ", "Maria Santos", null, "hash", roles, false, TestData.Now);

    [Fact]
    public void Usernames_are_lowercased_and_trimmed() => Assert.Equal("maria.santos", NewUser().Username);

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public void Odd_usernames_are_rejected(string username) =>
        Assert.Throws<ValidationFailedException>(() => User.Create(Guid.NewGuid(), TestData.Tenant, username, "X", null, "hash",
            AccessRole.Viewer, false, TestData.Now));

    [Fact]
    public void An_account_with_no_role_is_refused_so_a_mistake_never_grants_full_access() =>
        Assert.Throws<ValidationFailedException>(() => NewUser(AccessRole.None));

    [Fact]
    public void Roles_combine_and_unknown_bits_are_refused()
    {
        var user = NewUser(AccessRole.CostEngineer | AccessRole.Accountant);
        Assert.True(user.Roles.HasAny(AccessRole.Accountant));
        Assert.False(user.Roles.HasAny(AccessRole.HrOfficer));
        Assert.Equal(2, AccessRoles.Split(user.Roles).Count);
        Assert.Throws<ValidationFailedException>(() => user.SetRoles((AccessRole)1024, TestData.Now));
    }

    [Fact]
    public void Five_failed_sign_ins_lock_the_account_for_fifteen_minutes_and_success_clears_the_count()
    {
        var user = NewUser();
        for (var i = 0; i < 4; i++) user.RecordSignInFailure(TestData.Now);
        Assert.False(user.IsLockedOut(TestData.Now));

        user.RecordSignInFailure(TestData.Now);
        Assert.True(user.IsLockedOut(TestData.Now));
        Assert.False(user.CanSignIn(TestData.Now.AddMinutes(14)));
        Assert.True(user.CanSignIn(TestData.Now.AddMinutes(16)));

        user.RecordSignInFailure(TestData.Now.AddMinutes(16));
        user.RecordSignInSuccess(TestData.Now.AddMinutes(17));
        Assert.Equal(0, user.FailedSignInCount);
    }

    [Fact]
    public void A_deactivated_account_can_not_sign_in_and_is_not_counted_as_an_administrator()
    {
        var admin = NewUser(AccessRole.Administrator);
        Assert.True(admin.IsActiveAdministrator);
        admin.Deactivate(TestData.Now);
        Assert.False(admin.CanSignIn(TestData.Now));
        Assert.False(admin.IsActiveAdministrator);
        admin.Reactivate(TestData.Now);
        Assert.True(admin.CanSignIn(TestData.Now));
    }

    [Fact]
    public void The_password_policy_asks_for_length_a_letter_a_number_and_not_the_username()
    {
        Assert.Empty(PasswordPolicy.Check("correct horse 7 battery", "maria"));
        Assert.Contains(PasswordPolicy.Check("short1", "maria"), message => message.Contains("at least 10"));
        Assert.Contains(PasswordPolicy.Check("1234567890123", "maria"), message => message.Contains("letter"));
        Assert.Contains(PasswordPolicy.Check("abcdefghijklmnop", "maria"), message => message.Contains("number"));
        Assert.Contains(PasswordPolicy.Check("maria-secret-1", "maria"), message => message.Contains("username"));
        Assert.Empty(PasswordPolicy.Check(new string('a', 20) + "1" + new string('b', 100), "maria"));
        Assert.NotEmpty(PasswordPolicy.Check(new string('a', 130) + "1", "maria"));
    }
}

public sealed class OrganizationTests
{
    [Fact]
    public void Defaults_replace_the_values_the_desktop_app_hard_coded()
    {
        var organization = Organization.Create(TestData.Tenant, "GSG Builders", TestData.Now);

        Assert.Equal(TestData.Tenant, organization.TenantId);
        Assert.Equal(1_200, organization.VatRateBps);
        Assert.Equal("PHP", organization.CurrencyCode);
        Assert.Equal(8m, organization.HoursPerDay);
        Assert.Equal(12_500, organization.OvertimeMultiplierBps);
    }

    [Fact]
    public void Settings_are_validated_together()
    {
        var organization = Organization.Create(TestData.Tenant, "GSG Builders", TestData.Now);

        var ex = Assert.Throws<ValidationFailedException>(() => organization.UpdateSettings("", "peso", 20_000, "Mars/Olympus", 30m, 5_000,
            VarianceBasis.IncludingVat, TestData.Now));

        Assert.Equal(6, ex.Errors.Count);
    }

    [Fact]
    public void Today_is_the_business_day_not_the_utc_day()
    {
        var organization = Organization.Create(TestData.Tenant, "GSG Builders", TestData.Now);

        // 17:00 UTC on 1 Nov is 01:00 on 2 Nov in Manila (UTC+8).
        Assert.Equal(new DateOnly(2026, 11, 2), organization.TodayLocal(new DateTimeOffset(2026, 11, 1, 17, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 1), organization.TodayLocal(new DateTimeOffset(2026, 11, 1, 15, 59, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void A_change_to_the_vat_rate_is_accepted()
    {
        var organization = Organization.Create(TestData.Tenant, "GSG Builders", TestData.Now);
        organization.UpdateSettings("GSG Builders", "php", 1_000, "Asia/Manila", 8m, 12_500, VarianceBasis.ExcludingVat, TestData.Now);

        Assert.Equal(1_000, organization.VatRateBps);
        Assert.Equal("PHP", organization.CurrencyCode);
        Assert.Equal(VarianceBasis.ExcludingVat, organization.VarianceBasis);
    }
}
