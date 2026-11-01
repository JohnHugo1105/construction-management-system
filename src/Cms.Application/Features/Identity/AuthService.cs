using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Domain.Common;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Organizations;

namespace Cms.Application.Features.Identity;

/// <summary>
/// Sign-in, the first-time setup, and "who am I". The retired desktop app compared plain-text passwords, never locked an account,
/// opened with no sign-in at all, and let anyone at the login screen create an administrator.
/// </summary>
public sealed class AuthService(IUserRepository users, IOrganizationRepository organizations, IPasswordHasher hasher, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit, ICurrentUser currentUser)
{
    // One message for an unknown name, a wrong password and a locked account, so the sign-in page can't be used to find out which
    // usernames exist or which accounts someone has been guessing at.
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    private const string GenericFailure =
        "The username or password is incorrect, or the account is locked for 15 minutes after too many wrong tries. Check them and try again, or ask an administrator.";

    public async Task<SignedInSession> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        var username = User.NormalizeUsername(request.Username);
        var password = request.Password ?? "";
        var now = clock.UtcNow;

        var user = username.Length == 0 ? null : await users.FindByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            hasher.Verify(null, password);
            throw new AuthenticationFailedException(GenericFailure);
        }

        if (user.IsLockedOut(now))
        {
            // Spend the same time as a real check, and don't count guesses made while the account is already locked.
            hasher.Verify(user.PasswordHash, password);
            throw new AuthenticationFailedException(GenericFailure);
        }

        if (!hasher.Verify(user.PasswordHash, password))
        {
            user.RecordSignInFailure(now);
            audit.RecordAs(user.TenantId, user.Id, user.Username, "User.SignInFailed", nameof(User), user.Id, "A wrong password was entered.");
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A parallel attempt saved first. The person still gets the same answer; the sign-in rate limiter bounds parallel guessing.
            }
            throw new AuthenticationFailedException(GenericFailure);
        }

        if (!user.IsActive) throw new AuthenticationFailedException("This account is turned off. Ask an administrator to turn it back on.");

        user.RecordSignInSuccess(now);
        audit.RecordAs(user.TenantId, user.Id, user.Username, "User.SignedIn", nameof(User), user.Id, "Signed in.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildSignedInAsync(user, cancellationToken);
    }

    /// <summary>Creates the organization and its first administrator. Allowed exactly once, while the system has no users.</summary>
    public async Task<SignedInSession> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        // One at a time per process, so two simultaneous setup requests can't both see an empty system and each create a company and an
        // administrator. (Across several servers the first-time-setup switch, which is on only for the first start, is the guard.)
        await BootstrapLock.WaitAsync(cancellationToken);
        try
        {
            return await BootstrapCoreAsync(request, cancellationToken);
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    private async Task<SignedInSession> BootstrapCoreAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        if (await users.AnyUserAsync(cancellationToken))
            throw new DomainRuleException("This system is already set up. Sign in with your account instead.");

        var username = User.NormalizeUsername(request.Username);
        var problems = PasswordPolicy.Check(request.Password, username);
        if (problems.Count > 0) throw new ValidationFailedException(new Dictionary<string, string[]> { ["password"] = problems.ToArray() });

        var now = clock.UtcNow;
        var organization = Organization.Create(Guid.NewGuid(), request.CompanyName ?? "", now);
        var admin = User.Create(Guid.NewGuid(), organization.Id, username, request.DisplayName ?? "", null, hasher.Hash(request.Password!),
            AccessRole.Administrator, mustChangePassword: false, now);

        organizations.Add(organization);
        users.Add(admin);
        audit.RecordAs(organization.Id, admin.Id, admin.Username, "System.SetUp", nameof(Organization), organization.Id,
            $"{organization.Name} was set up with {admin.Username} as the first administrator.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildSignedInAsync(admin, cancellationToken);
    }

    /// <summary>True while the system has no users, so the web app offers first-time setup instead of sign-in.</summary>
    public async Task<bool> NeedsSetupAsync(CancellationToken cancellationToken) => !await users.AnyUserAsync(cancellationToken);

    public async Task<SessionDto> GetSessionAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(currentUser.UserId, cancellationToken) ?? throw new NotFoundException("your account");
        return await BuildSessionAsync(user, cancellationToken);
    }

    /// <summary>Signing out ends the person's sessions on every device, so a copied cookie is worthless from then on.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(currentUser.UserId, cancellationToken);
        if (user is null) return;
        user.EndAllSessions(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Changes the person's own password, ends their other sessions, and returns the new session for the current device.</summary>
    public async Task<SignedInSession> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(currentUser.UserId, cancellationToken) ?? throw new NotFoundException("your account");
        var errors = new ValidationErrors();
        if (!hasher.Verify(user.PasswordHash, request.CurrentPassword ?? "")) errors.Add("currentPassword", "That isn't your current password.");
        var problems = PasswordPolicy.Check(request.NewPassword, user.Username);
        foreach (var problem in problems) errors.Add("newPassword", problem);
        if (problems.Count == 0 && request.NewPassword == request.CurrentPassword) errors.Add("newPassword", "Choose a password you haven't used just now.");
        errors.ThrowIfAny();

        user.ChangePassword(hasher.Hash(request.NewPassword!), mustChangeAtNextSignIn: false, clock.UtcNow);
        audit.Record("User.PasswordChanged", nameof(User), user.Id, "Changed their own password.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildSignedInAsync(user, cancellationToken);
    }

    private async Task<SignedInSession> BuildSignedInAsync(User user, CancellationToken cancellationToken) =>
        new(await BuildSessionAsync(user, cancellationToken), user.SecurityStamp);

    private async Task<SessionDto> BuildSessionAsync(User user, CancellationToken cancellationToken)
    {
        var organization = await organizations.FindAsync(user.TenantId, cancellationToken) ?? throw new NotFoundException("your organization");
        var permissions = Permissions.All.Where(permission => Permissions.IsAllowed(user.Roles, permission)).Order().ToArray();
        return new SessionDto(user.Id, user.TenantId, user.Username, user.DisplayName,
            AccessRoles.Split(user.Roles).Select(role => role.ToString()).ToArray(), permissions, user.MustChangePassword, organization.Name,
            organization.CurrencyCode, organization.TimeZoneId, organization.VatRateBps);
    }
}
