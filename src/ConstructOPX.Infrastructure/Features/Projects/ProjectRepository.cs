using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Projects;

internal sealed class ProjectRepository(ConstructOpxDbContext db) : IProjectRepository
{
    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Project>().Include(project => project.SubProjects).SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeProjectId, CancellationToken cancellationToken) =>
        db.Set<Project>().AnyAsync(project => project.NormalizedName == normalizedName && project.Id != excludeProjectId, cancellationToken);

    public void Add(Project project) => db.Set<Project>().Add(project);
}

internal sealed class ProjectReader(ConstructOpxDbContext db) : IProjectReader
{
    public async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var projects = db.Set<Project>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ProjectStatus>(query.Status, true, out var status))
            projects = projects.Where(project => project.Status == status);
        else if (!query.IncludeArchived)
            projects = projects.Where(project => project.Status != ProjectStatus.Archived);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Sql.Contains(query.Search);
            projects = projects.Where(project => EF.Functions.Like(project.Name, pattern, Sql.LikeEscape) ||
                (project.Client != null && EF.Functions.Like(project.Client, pattern, Sql.LikeEscape)));
        }

        var total = await projects.CountAsync(cancellationToken);
        var items = await projects
            .OrderBy(project => project.Name)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(project => new ProjectListItemDto(project.Id, project.Name, project.Client, project.Location, project.Status.ToString(),
                project.StartDate, project.TargetEndDate, project.ContractPriceMinor, project.SubProjects.Count(), project.Version))
            .ToListAsync(cancellationToken);
        return PagedResult<ProjectListItemDto>.Of(items, total, page);
    }

    public async Task<IReadOnlyList<ProjectLookupDto>> LookupAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var projects = await db.Set<Project>().AsNoTracking()
            .Where(project => includeArchived || project.Status != ProjectStatus.Archived)
            .OrderBy(project => project.Name)
            .Select(project => new
            {
                project.Id,
                project.Name,
                Status = project.Status.ToString(),
                Subs = project.SubProjects.OrderBy(sub => sub.Name)
                    .Select(sub => new SubProjectDto(sub.Id, sub.Name, sub.StartDate, sub.Status.ToString())).ToList()
            })
            .ToListAsync(cancellationToken);
        return projects.Select(project => new ProjectLookupDto(project.Id, project.Name, project.Status, project.Subs)).ToArray();
    }
}
