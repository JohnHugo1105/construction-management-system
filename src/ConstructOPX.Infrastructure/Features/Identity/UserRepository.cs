using ConstructOPX.Application.Features.Identity;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Identity;

internal sealed class UserRepository(ConstructOpxDbContext db) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        db.Set<User>().IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Username == normalizedUsername, cancellationToken);

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<User>().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken) =>
        await db.Set<User>().OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        db.Set<User>().IgnoreQueryFilters().AnyAsync(user => user.Username == normalizedUsername, cancellationToken);

    public Task<bool> AnyUserAsync(CancellationToken cancellationToken) =>
        db.Set<User>().IgnoreQueryFilters().AnyAsync(cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        db.Set<User>().CountAsync(user => user.IsActive && (user.Roles & AccessRole.Administrator) != 0, cancellationToken);

    public void Add(User user) => db.Set<User>().Add(user);
}
