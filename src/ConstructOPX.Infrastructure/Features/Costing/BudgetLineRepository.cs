using System.Runtime.CompilerServices;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Costing;

internal sealed class BudgetLineRepository(ConstructOpxDbContext db) : IBudgetLineRepository
{
    public Task<BudgetLine?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<BudgetLine>().SingleOrDefaultAsync(line => line.Id == id, cancellationToken);

    public Task<BudgetLine?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<BudgetLine>().SingleOrDefaultAsync(line => line.ClientRequestId == clientRequestId, cancellationToken);

    public void Add(BudgetLine line) => db.Set<BudgetLine>().Add(line);
}

internal sealed class BudgetLineReader(ConstructOpxDbContext db) : IBudgetLineReader
{
    public async Task<TotaledPage<BudgetLineDto>> ListAsync(Guid projectId, BudgetLineFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var lines = Filtered(projectId, filter);

        // One statement counts the rows of the whole filtered set and adds up their money. Removed lines (when they are listed) are counted
        // as rows but add nothing to the totals.
        var summary = await lines
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Net = group.Sum(line => line.Status == BudgetLineStatus.Active ? line.NetMinor : 0L),
                Vat = group.Sum(line => line.Status == BudgetLineStatus.Active ? line.VatMinor : 0L),
                Gross = group.Sum(line => line.Status == BudgetLineStatus.Active ? line.GrossMinor : 0L)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (summary is null) return TotaledPage<BudgetLineDto>.Of([], 0, page, MoneyTotalsDto.Zero);

        var items = await ToDtos(Ordered(lines).Skip(page.Skip).Take(page.SafePageSize)).ToListAsync(cancellationToken);
        return TotaledPage<BudgetLineDto>.Of(items, summary.Count, page, new MoneyTotalsDto(summary.Net, summary.Vat, summary.Gross));
    }

    public async IAsyncEnumerable<BudgetLineDto> StreamAsync(Guid projectId, BudgetLineFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = ToDtos(Ordered(Filtered(projectId, filter))).AsAsyncEnumerable().WithCancellation(cancellationToken);
        await foreach (var row in rows) yield return row;
    }

    private IQueryable<BudgetLine> Filtered(Guid projectId, BudgetLineFilter filter)
    {
        var lines = db.Set<BudgetLine>().AsNoTracking().Where(line => line.ProjectId == projectId);
        if (!filter.IncludeRemoved) lines = lines.Where(line => line.Status == BudgetLineStatus.Active);
        if (filter.SubProjectId is { } subProjectId) lines = lines.Where(line => line.SubProjectId == subProjectId);
        if (filter.Division is { } division) lines = lines.Where(line => line.Division == division);
        if (filter.ItemType is { } itemType) lines = lines.Where(line => line.ItemType == itemType);
        if (filter.Search is { } search)
        {
            var pattern = Sql.Contains(search);
            lines = lines.Where(line => EF.Functions.Like(line.Description, pattern, Sql.LikeEscape) ||
                EF.Functions.Like(line.MainScope, pattern, Sql.LikeEscape) || EF.Functions.Like(line.SubScope, pattern, Sql.LikeEscape) ||
                EF.Functions.Like(line.WbsCode, pattern, Sql.LikeEscape));
        }

        return lines;
    }

    // Work-breakdown order, so a bill of quantities reads the way it is built; unclassified lines last. The id keeps pages stable.
    private static IOrderedQueryable<BudgetLine> Ordered(IQueryable<BudgetLine> lines) =>
        lines.OrderBy(line => line.Division == null).ThenBy(line => line.Division).ThenBy(line => line.WbsCode)
            .ThenBy(line => line.Description).ThenBy(line => line.Id);

    private IQueryable<BudgetLineDto> ToDtos(IQueryable<BudgetLine> lines) => lines.Select(line => new BudgetLineDto(
        line.Id, line.ProjectId, line.SubProjectId,
        db.Set<SubProject>().Where(sub => sub.Id == line.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
        line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description, line.ItemType.ToString(), line.Quantity, line.Unit,
        line.UnitCostMinor, line.VatTreatment.ToString(), line.VatRateBps, line.NetMinor, line.VatMinor, line.GrossMinor, line.EntryDate,
        line.Remarks, line.Status.ToString(), line.CreatedAtUtc, line.RemovedAtUtc, line.RemovedReason, line.Version, line.ChangeOrderId,
        db.Set<ChangeOrder>().Where(order => order.Id == line.ChangeOrderId).Select(order => order.Number).FirstOrDefault()));
}
