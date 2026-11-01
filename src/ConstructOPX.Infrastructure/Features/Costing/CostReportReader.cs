using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Costing;

/// <summary>
/// The sums behind the dashboard and the project cost summary. Every method is a fixed number of grouped statements: the rows that come
/// back are one per project, or one per work-breakdown code, sub-project or item type, never one per budget line or cost entry.
/// </summary>
internal sealed class CostReportReader(ConstructOpxDbContext db) : ICostReportReader
{
    public async Task<IReadOnlyList<ProjectHeader>> ListProjectsAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var rows = await VisibleProjects(includeArchived).OrderBy(project => project.Name)
            .Select(project => new { project.Id, project.Name, project.Status, project.ContractPriceMinor })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ProjectHeader(row.Id, row.Name, row.Status.ToString(), row.ContractPriceMinor)).ToArray();
    }

    public async Task<IReadOnlyList<ProjectAmounts>> SumByProjectAsync(CostMeasure measure, bool includeArchived, CancellationToken cancellationToken)
    {
        // The list of projects is a sub-select, so archived projects' lines are not even summed unless they were asked for.
        var visible = VisibleProjects(includeArchived).Select(project => project.Id);

        return measure == CostMeasure.Budget
            ? await db.Set<BudgetLine>().AsNoTracking()
                .Where(line => line.Status == BudgetLineStatus.Active && visible.Contains(line.ProjectId))
                .GroupBy(line => line.ProjectId)
                .Select(group => new ProjectAmounts(group.Key, group.Sum(line => line.NetMinor), group.Sum(line => line.GrossMinor)))
                .ToListAsync(cancellationToken)
            : await db.Set<CostEntry>().AsNoTracking()
                .Where(entry => entry.Status == CostEntryStatus.Posted && visible.Contains(entry.ProjectId))
                .GroupBy(entry => entry.ProjectId)
                .Select(group => new ProjectAmounts(group.Key, group.Sum(entry => entry.NetMinor), group.Sum(entry => entry.GrossMinor)))
                .ToListAsync(cancellationToken);
    }

    public async Task<GroupedSums> SumProjectAsync(Guid projectId, CostMeasure measure, CancellationToken cancellationToken)
    {
        var rows = Rows(projectId, measure);

        var byWbs = await rows
            .GroupBy(row => new { row.Division, row.WbsCode })
            .Select(group => new GroupedAmounts(group.Key.Division, group.Key.WbsCode, group.Max(row => row.SubScope), null, null,
                group.Sum(row => row.NetMinor), group.Sum(row => row.GrossMinor)))
            .ToListAsync(cancellationToken);

        var bySubProject = await rows
            .GroupBy(row => row.SubProjectId)
            .Select(group => new GroupedAmounts(null, null, null, group.Key, null, group.Sum(row => row.NetMinor), group.Sum(row => row.GrossMinor)))
            .ToListAsync(cancellationToken);

        var byItemType = await rows
            .GroupBy(row => row.ItemType)
            .Select(group => new GroupedAmounts(null, null, null, null, group.Key, group.Sum(row => row.NetMinor), group.Sum(row => row.GrossMinor)))
            .ToListAsync(cancellationToken);

        return new GroupedSums(byWbs, bySubProject, byItemType);
    }

    private IQueryable<Project> VisibleProjects(bool includeArchived)
    {
        var projects = db.Set<Project>().AsNoTracking();
        return includeArchived ? projects : projects.Where(project => project.Status != ProjectStatus.Archived);
    }

    /// <summary>The lines of a project that count (active budget lines, or posted cost entries) in one shape, so the grouping is written once.</summary>
    private IQueryable<AmountRow> Rows(Guid projectId, CostMeasure measure) => measure == CostMeasure.Budget
        ? db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.ProjectId == projectId && line.Status == BudgetLineStatus.Active)
            .Select(line => new AmountRow
            {
                SubProjectId = line.SubProjectId, Division = line.Division, WbsCode = line.WbsCode, SubScope = line.SubScope,
                ItemType = line.ItemType, NetMinor = line.NetMinor, GrossMinor = line.GrossMinor
            })
        : db.Set<CostEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.Status == CostEntryStatus.Posted)
            .Select(entry => new AmountRow
            {
                SubProjectId = entry.SubProjectId, Division = entry.Division, WbsCode = entry.WbsCode, SubScope = entry.SubScope,
                ItemType = entry.ItemType, NetMinor = entry.NetMinor, GrossMinor = entry.GrossMinor
            });

    private sealed class AmountRow
    {
        public Guid? SubProjectId { get; init; }
        public int? Division { get; init; }
        public string? WbsCode { get; init; }
        public string? SubScope { get; init; }
        public ItemType ItemType { get; init; }
        public long NetMinor { get; init; }
        public long GrossMinor { get; init; }
    }
}
