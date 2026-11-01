using Cms.Application.Features.Costing;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Purchasing;

/// <summary>
/// What approved purchase orders have not been invoiced for yet. For each line of an approved order the open remainder is the line amount
/// minus the posted supplier invoices matched to it, never below zero (an invoice larger than the line leaves nothing open, and does not
/// reduce what other lines still owe); net and gross are worked out separately. A voided invoice stops counting as matched, so its remainder
/// returns. Draft, closed and cancelled orders commit nothing. One grouped statement however many orders and projects there are.
/// </summary>
internal sealed class PurchaseOrderCommitmentSource(CmsDbContext db) : ICommitmentSource
{
    public async Task<IReadOnlyList<ProjectAmounts>> OpenCommitmentsAsync(Guid? projectId, bool includeArchived, CancellationToken cancellationToken)
    {
        var orders = db.Set<PurchaseOrder>().AsNoTracking().Where(order => order.Status == PurchaseOrderStatus.Approved);
        if (projectId is { } only) orders = orders.Where(order => order.ProjectId == only);
        if (!includeArchived)
            orders = orders.Where(order => db.Set<Project>().Any(project => project.Id == order.ProjectId && project.Status != ProjectStatus.Archived));

        // Each line with what is invoiced against it (a scalar sum over the covering index on the cost entry's order line).
        var perLine = orders.SelectMany(order => order.Lines, (order, line) => new
        {
            order.ProjectId,
            line.NetMinor,
            line.GrossMinor,
            MatchedNet = db.Set<CostEntry>().Where(entry => entry.PurchaseOrderLineId == line.Id && entry.Status == CostEntryStatus.Posted)
                .Sum(entry => (long?)entry.NetMinor) ?? 0L,
            MatchedGross = db.Set<CostEntry>().Where(entry => entry.PurchaseOrderLineId == line.Id && entry.Status == CostEntryStatus.Posted)
                .Sum(entry => (long?)entry.GrossMinor) ?? 0L
        });

        var rows = await perLine.GroupBy(row => row.ProjectId)
            .Select(group => new ProjectAmounts(group.Key,
                group.Sum(row => row.NetMinor > row.MatchedNet ? row.NetMinor - row.MatchedNet : 0L),
                group.Sum(row => row.GrossMinor > row.MatchedGross ? row.GrossMinor - row.MatchedGross : 0L)))
            .ToListAsync(cancellationToken);
        // A project whose lines are all fully invoiced is simply absent.
        return rows.Where(row => row.NetMinor > 0 || row.GrossMinor > 0).ToArray();
    }
}
