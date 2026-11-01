using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Infrastructure;

public sealed record SeededUser(Guid TenantId, Guid UserId, string Username, string Password, AccessRole Roles);

/// <summary>
/// Creates an organization and users straight in the database, so each test has its own tenant and none depends on another.
/// (First-time setup, which creates the very first administrator through the API, has its own test on an empty database.)
/// </summary>
public static class TenantSeeder
{
    public const string Password = "Correct-horse-7-battery";

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    public static async Task<SeededUser> CreateTenantAsync(ConstructOpxApiFactory factory, AccessRole roles = AccessRole.Administrator, string company = "Test Builders")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTimeOffset.UtcNow;

        var organization = Organization.Create(Guid.NewGuid(), company, now);
        var user = User.Create(Guid.NewGuid(), organization.Id, Unique("admin"), "Test Admin", null, hasher.Hash(Password), roles, false, now);
        db.Add(organization);
        db.Add(user);
        await db.SaveChangesAsync();
        return new SeededUser(organization.Id, user.Id, user.Username, Password, roles);
    }

    public static async Task<SeededUser> AddUserAsync(ConstructOpxApiFactory factory, Guid tenantId, AccessRole roles, bool mustChangePassword = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.Create(Guid.NewGuid(), tenantId, Unique("user"), "Test User", null, hasher.Hash(Password), roles, mustChangePassword,
            DateTimeOffset.UtcNow);
        db.Add(user);
        await db.SaveChangesAsync();
        return new SeededUser(tenantId, user.Id, user.Username, Password, roles);
    }
}
