using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Organizations;

internal sealed class OrganizationRepository(ConstructOpxDbContext db, ICurrentUser currentUser) : IOrganizationRepository
{
    public Task<Organization> GetCurrentAsync(CancellationToken cancellationToken) =>
        db.Set<Organization>().SingleAsync(organization => organization.Id == currentUser.TenantId, cancellationToken);

    public Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Organization>().IgnoreQueryFilters().SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public void Add(Organization organization) => db.Set<Organization>().Add(organization);
}
