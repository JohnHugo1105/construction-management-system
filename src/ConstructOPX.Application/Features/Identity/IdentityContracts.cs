using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Application.Features.Identity;

public interface IUserRepository
{
    /// <summary>Finds a user by normalized username across all organizations (needed before anyone is signed in).</summary>
    Task<User?> FindByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);

    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Usernames are unique across the whole system, because sign-in has no organization field.</summary>
    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken);

    Task<bool> AnyUserAsync(CancellationToken cancellationToken);
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken);
    void Add(User user);
}

public sealed record SignInRequest(string? Username, string? Password);

public sealed record BootstrapRequest(string? CompanyName, string? Username, string? DisplayName, string? Password);

public sealed record CreateUserRequest(string? Username, string? DisplayName, string? Email, string? Password, IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(string? DisplayName, string? Email, IReadOnlyList<string>? Roles, int Version);

public sealed record ResetPasswordRequest(string? NewPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserDto(Guid Id, string Username, string DisplayName, string? Email, IReadOnlyList<string> Roles, bool IsActive,
    bool MustChangePassword, DateTimeOffset? LastSignInUtc, DateTimeOffset? LockedUntilUtc, int Version);

public sealed record SessionDto(Guid UserId, Guid TenantId, string Username, string DisplayName, IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions, bool MustChangePassword, string OrganizationName, string CurrencyCode, string TimeZoneId,
    int VatRateBps);

/// <summary>
/// What a successful sign-in hands to the delivery layer: the session the web app sees, and the stamp the cookie must carry so that
/// the session can be ended from the server. The stamp is never sent to the browser as data.
/// </summary>
public sealed record SignedInSession(SessionDto Session, Guid SecurityStamp);

public static class IdentityMapper
{
    public static UserDto ToDto(User user) => new(user.Id, user.Username, user.DisplayName, user.Email,
        AccessRoles.Split(user.Roles).Select(role => role.ToString()).ToArray(), user.IsActive, user.MustChangePassword, user.LastSignInUtc,
        user.LockedUntilUtc, user.Version);
}

public static class AccessRoleParser
{
    public static AccessRole Parse(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0) throw new ConstructOPX.Domain.Common.ValidationFailedException("roles", "Choose at least one role.");
        var roles = AccessRole.None;
        foreach (var name in names)
        {
            if (!Enum.TryParse<AccessRole>(name, ignoreCase: true, out var role) || role == AccessRole.None || !Enum.IsDefined(role))
                throw new ConstructOPX.Domain.Common.ValidationFailedException("roles", $"\"{name}\" isn't a role.");
            roles |= role;
        }
        return roles;
    }
}
