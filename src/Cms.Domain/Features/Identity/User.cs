using System.Text.RegularExpressions;
using Cms.Domain.Common;

namespace Cms.Domain.Features.Identity;

public sealed partial class User : AggregateRoot
{
    public const int MaximumFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { }

    public string Username { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string? Email { get; private set; }

    /// <summary>A salted, slow hash with its algorithm and cost; never the password itself.</summary>
    public string PasswordHash { get; private set; } = "";

    public AccessRole Roles { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool MustChangePassword { get; private set; }
    public int FailedSignInCount { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }
    public DateTimeOffset? LastSignInUtc { get; private set; }

    public static string NormalizeUsername(string? username) => (username ?? "").Trim().ToLowerInvariant();

    public static User Create(Guid id, Guid tenantId, string username, string displayName, string? email, string passwordHash,
        AccessRole roles, bool mustChangePassword, DateTimeOffset now)
    {
        var normalized = NormalizeUsername(username);
        if (!UsernamePattern().IsMatch(normalized))
            throw new ValidationFailedException("username", "Use 3 to 64 letters, numbers, dots, dashes, underscores or @ for the username.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        if (!AccessRoles.IsValid(roles)) throw new ValidationFailedException("roles", "Choose at least one role.");

        var user = new User
        {
            Username = normalized,
            DisplayName = Guard.RequiredText(displayName, "displayName", "the person's name", 120),
            Email = Guard.OptionalText(email, "email", "the email address", 254),
            PasswordHash = passwordHash,
            Roles = roles,
            MustChangePassword = mustChangePassword
        };
        user.Initialize(id, tenantId, now);
        return user;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

    /// <summary>True when this account may sign in right now (active and not locked).</summary>
    public bool CanSignIn(DateTimeOffset now) => IsActive && !IsLockedOut(now);

    public void RecordSignInSuccess(DateTimeOffset now)
    {
        FailedSignInCount = 0;
        LockedUntilUtc = null;
        LastSignInUtc = now;
        Touch(now);
    }

    public void RecordSignInFailure(DateTimeOffset now)
    {
        FailedSignInCount++;
        if (FailedSignInCount >= MaximumFailedSignIns)
        {
            LockedUntilUtc = now + LockoutDuration;
            FailedSignInCount = 0;
        }
        Touch(now);
    }

    public void ChangePassword(string newPasswordHash, bool mustChangeAtNextSignIn, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash)) throw new ArgumentException("A password hash is required.", nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        MustChangePassword = mustChangeAtNextSignIn;
        FailedSignInCount = 0;
        LockedUntilUtc = null;
        Touch(now);
    }

    public void UpdateProfile(string displayName, string? email, DateTimeOffset now)
    {
        DisplayName = Guard.RequiredText(displayName, "displayName", "the person's name", 120);
        Email = Guard.OptionalText(email, "email", "the email address", 254);
        Touch(now);
    }

    public void SetRoles(AccessRole roles, DateTimeOffset now)
    {
        if (!AccessRoles.IsValid(roles)) throw new ValidationFailedException("roles", "Choose at least one role.");
        Roles = roles;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }

    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        FailedSignInCount = 0;
        LockedUntilUtc = null;
        Touch(now);
    }

    public bool IsActiveAdministrator => IsActive && Roles.HasAny(AccessRole.Administrator);

    [GeneratedRegex("^[a-z0-9._@-]{3,64}$")]
    private static partial Regex UsernamePattern();
}
