using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Progress;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Infrastructure.Features.Progress;

internal sealed class ProgressRepository(ConstructOpxDbContext db) : IProgressRepository
{
    public async Task<IReadOnlyDictionary<Guid, BudgetLine>> GetActiveLinesAsync(Guid projectId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        (await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.ProjectId == projectId && line.Status == BudgetLineStatus.Active && ids.Contains(line.Id))
            .ToListAsync(cancellationToken)).ToDictionary(line => line.Id);

    public async Task<IReadOnlyDictionary<Guid, LatestProgress>> GetLatestAsync(IReadOnlyCollection<Guid> budgetLineIds,
        CancellationToken cancellationToken)
    {
        // One statement for every line: the latest entry of each is a sub-select per line, not a query per line.
        var rows = await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => budgetLineIds.Contains(line.Id))
            .Select(line => new
            {
                line.Id,
                Latest = db.Set<ProgressEntry>().Where(entry => entry.BudgetLineId == line.Id)
                    .OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => new { entry.PercentCompleteBps, entry.AsOfDate }).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return rows.Where(row => row.Latest is not null)
            .ToDictionary(row => row.Id, row => new LatestProgress(row.Latest!.PercentCompleteBps, row.Latest.AsOfDate));
    }

    public async Task<IReadOnlyList<ProgressEntry>> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        await db.Set<ProgressEntry>().AsNoTracking().Where(entry => entry.ClientRequestId == clientRequestId).OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

    public void Add(IEnumerable<ProgressEntry> entries) => db.Set<ProgressEntry>().AddRange(entries);
}

internal sealed class ProgressReader(ConstructOpxDbContext db) : IProgressReader
{
    public async Task<IReadOnlyList<ProgressLineRow>> ListLinesAsync(Guid projectId, DateOnly asOf, CancellationToken cancellationToken)
    {
        // One statement: the current percent of each line (its latest entry on or before the date) is a sub-select per line inside it.
        var rows = await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.ProjectId == projectId && line.Status == BudgetLineStatus.Active)
            .OrderBy(line => line.Division == null).ThenBy(line => line.Division).ThenBy(line => line.WbsCode).ThenBy(line => line.Description)
            .ThenBy(line => line.Id)
            .Select(line => new
            {
                line.Id, line.Description, line.Division, line.WbsCode, line.NetMinor, line.GrossMinor,
                Current = db.Set<ProgressEntry>().Where(entry => entry.BudgetLineId == line.Id && entry.AsOfDate <= asOf)
                    .OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => new { entry.PercentCompleteBps, entry.AsOfDate }).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new ProgressLineRow(row.Id, row.Description, row.Division, row.WbsCode, row.NetMinor, row.GrossMinor,
            row.Current?.PercentCompleteBps, row.Current?.AsOfDate)).ToArray();
    }

    public async Task<MeasuredAmounts> SumActualAsync(Guid projectId, DateOnly asOf, CancellationToken cancellationToken)
    {
        // The same posted cost the dashboard adds up, limited to what was dated on or before the date.
        var sum = await db.Set<CostEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.Status == CostEntryStatus.Posted && entry.EntryDate <= asOf)
            .GroupBy(_ => 1)
            .Select(group => new { Net = group.Sum(entry => entry.NetMinor), Gross = group.Sum(entry => entry.GrossMinor) })
            .SingleOrDefaultAsync(cancellationToken);
        return sum is null ? default : new MeasuredAmounts(sum.Net, sum.Gross);
    }

    public async Task<PagedResult<ProgressHistoryItemDto>> HistoryAsync(Guid budgetLineId, PageRequest page, CancellationToken cancellationToken)
    {
        var entries = db.Set<ProgressEntry>().AsNoTracking().Where(entry => entry.BudgetLineId == budgetLineId);
        var total = await entries.CountAsync(cancellationToken);
        var items = await entries
            .OrderByDescending(entry => entry.AsOfDate).ThenByDescending(entry => entry.RecordedAtUtc).ThenByDescending(entry => entry.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(entry => new ProgressHistoryItemDto(entry.Id, entry.AsOfDate, entry.PercentCompleteBps, entry.Note,
                db.Set<User>().Where(user => user.Id == entry.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                entry.RecordedAtUtc))
            .ToListAsync(cancellationToken);
        return PagedResult<ProgressHistoryItemDto>.Of(items, total, page);
    }
}
