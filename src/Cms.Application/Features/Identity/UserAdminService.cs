using Cms.Application.Common;
using Cms.Domain.Common;
using Cms.Domain.Features.Identity;

namespace Cms.Application.Features.Identity;

public sealed class UserAdminService(IUserRepository users, IPasswordHasher hasher, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
    IAuditTrail audit)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken) =>
        (await users.ListAsync(cancellationToken)).Select(IdentityMapper.ToDto).ToArray();

    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        IdentityMapper.ToDto(await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that user"));

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var username = User.NormalizeUsername(request.Username);
        var roles = AccessRole.None;
        try { roles = AccessRoleParser.Parse(request.Roles); }
        catch (ValidationFailedException ex) { errors.Add("roles", ex.Errors["roles"][0]); }
        foreach (var problem in PasswordPolicy.Check(request.Password, username)) errors.Add("password", problem);
        if (username.Length > 0 && await users.UsernameExistsAsync(username, cancellationToken))
            errors.Add("username", "That username is taken. Choose another.");
        errors.ThrowIfAny();

        var user = User.Create(Guid.NewGuid(), currentUser.TenantId, username, request.DisplayName ?? "", request.Email, hasher.Hash(request.Password!),
            roles, mustChangePassword: true, clock.UtcNow);
        users.Add(user);
        audit.Record("User.Created", nameof(User), user.Id, $"Created {user.Username} with roles {Describe(roles)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityMapper.ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that user");
        ConcurrencyGuard.Ensure(user.Version, request.Version);
        var roles = AccessRoleParser.Parse(request.Roles);

        var losesAdmin = user.Roles.HasAny(AccessRole.Administrator) && !roles.HasAny(AccessRole.Administrator);
        if (losesAdmin)
        {
            if (user.Id == currentUser.UserId) throw new DomainRuleException("You can't remove your own administrator role. Ask another administrator.");
            await EnsureAnotherAdministratorAsync(user, cancellationToken);
        }

        var before = user.Roles;
        user.UpdateProfile(request.DisplayName ?? "", request.Email, clock.UtcNow);
        user.SetRoles(roles, clock.UtcNow);
        audit.Record("User.Updated", nameof(User), user.Id,
            before == roles ? $"Updated the details of {user.Username}." : $"Changed roles of {user.Username} from {Describe(before)} to {Describe(roles)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityMapper.ToDto(user);
    }

    public async Task<UserDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that user");
        if (user.Id == currentUser.UserId) throw new DomainRuleException("You can't turn off your own account.");
        if (!user.IsActive) throw new DomainRuleException("This account is already turned off.");
        if (user.IsActiveAdministrator) await EnsureAnotherAdministratorAsync(user, cancellationToken);

        user.Deactivate(clock.UtcNow);
        audit.Record("User.Deactivated", nameof(User), user.Id, $"Turned off {user.Username}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityMapper.ToDto(user);
    }

    public async Task<UserDto> ReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that user");
        if (user.IsActive) throw new DomainRuleException("This account is already on.");
        user.Reactivate(clock.UtcNow);
        audit.Record("User.Reactivated", nameof(User), user.Id, $"Turned on {user.Username}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityMapper.ToDto(user);
    }

    /// <summary>An administrator sets a temporary password; the person must choose their own at next sign-in.</summary>
    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that user");
        var problems = PasswordPolicy.Check(request.NewPassword, user.Username);
        if (problems.Count > 0) throw new ValidationFailedException(new Dictionary<string, string[]> { ["newPassword"] = problems.ToArray() });

        user.ChangePassword(hasher.Hash(request.NewPassword!), mustChangeAtNextSignIn: true, clock.UtcNow);
        audit.Record("User.PasswordReset", nameof(User), user.Id, $"Set a temporary password for {user.Username}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAnotherAdministratorAsync(User user, CancellationToken cancellationToken)
    {
        if (await users.CountActiveAdministratorsAsync(cancellationToken) <= 1 && user.IsActiveAdministrator)
            throw new DomainRuleException("There must always be at least one active administrator. Make someone else an administrator first.");
    }

    private static string Describe(AccessRole roles) => string.Join(", ", AccessRoles.Split(roles).Select(AccessRoles.Label));
}
