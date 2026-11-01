using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Domain.Features.Organizations;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Organizations;

internal sealed class OrganizationRepository(CmsDbContext db, ICurrentUser currentUser) : IOrganizationRepository
{
    public Task<Organization> GetCurrentAsync(CancellationToken cancellationToken) =>
        db.Set<Organization>().SingleAsync(organization => organization.Id == currentUser.TenantId, cancellationToken);

    public Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Organization>().IgnoreQueryFilters().SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public void Add(Organization organization) => db.Set<Organization>().Add(organization);
}
