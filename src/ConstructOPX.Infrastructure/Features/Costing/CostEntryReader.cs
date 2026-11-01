using System.Runtime.CompilerServices;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Costing;

internal sealed class CostEntryReader(ConstructOpxDbContext db) : ICostEntryReader
{
    public async Task<TotaledPage<CostEntryDto>> ListAsync(Guid projectId, CostEntryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var entries = Filtered(projectId, filter);

        // One statement counts the rows of the whole filtered set and adds up their money. Voided entries (when they are listed) are counted
        // as rows but add nothing to the totals.
        var summary = await entries
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Net = group.Sum(entry => entry.Status == CostEntryStatus.Posted ? entry.NetMinor : 0L),
                Vat = group.Sum(entry => entry.Status == CostEntryStatus.Posted ? entry.VatMinor : 0L),
                Gross = group.Sum(entry => entry.Status == CostEntryStatus.Posted ? entry.GrossMinor : 0L)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (summary is null) return TotaledPage<CostEntryDto>.Of([], 0, page, MoneyTotalsDto.Zero);

        var items = await ToDtos(Ordered(entries).Skip(page.Skip).Take(page.SafePageSize)).ToListAsync(cancellationToken);
        return TotaledPage<CostEntryDto>.Of(items, summary.Count, page, new MoneyTotalsDto(summary.Net, summary.Vat, summary.Gross));
    }

    public async IAsyncEnumerable<CostEntryDto> StreamAsync(Guid projectId, CostEntryFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = ToDtos(Ordered(Filtered(projectId, filter))).AsAsyncEnumerable().WithCancellation(cancellationToken);
        await foreach (var row in rows) yield return row;
    }

    private IQueryable<CostEntry> Filtered(Guid projectId, CostEntryFilter filter)
    {
        var entries = db.Set<CostEntry>().AsNoTracking().Where(entry => entry.ProjectId == projectId);
        if (!filter.IncludeVoided) entries = entries.Where(entry => entry.Status == CostEntryStatus.Posted);
        if (filter.SubProjectId is { } subProjectId) entries = entries.Where(entry => entry.SubProjectId == subProjectId);
        if (filter.Division is { } division) entries = entries.Where(entry => entry.Division == division);
        if (filter.ItemType is { } itemType) entries = entries.Where(entry => entry.ItemType == itemType);
        if (filter.Origin is { } origin) entries = entries.Where(entry => entry.Origin == origin);
        if (filter.From is { } from) entries = entries.Where(entry => entry.EntryDate >= from);
        if (filter.To is { } to) entries = entries.Where(entry => entry.EntryDate <= to);
        if (filter.Search is { } search)
        {
            var pattern = Sql.Contains(search);
            entries = entries.Where(entry => EF.Functions.Like(entry.Description, pattern, Sql.LikeEscape) ||
                EF.Functions.Like(entry.SourceDealer, pattern, Sql.LikeEscape) || EF.Functions.Like(entry.InvoiceNumber, pattern, Sql.LikeEscape));
        }

        return entries;
    }

    // Newest first, the way a journal is read. The id keeps pages stable when several entries share a date.
    private static IOrderedQueryable<CostEntry> Ordered(IQueryable<CostEntry> entries) =>
        entries.OrderByDescending(entry => entry.EntryDate).ThenByDescending(entry => entry.CreatedAtUtc).ThenBy(entry => entry.Id);

    private IQueryable<CostEntryDto> ToDtos(IQueryable<CostEntry> entries) => entries.Select(entry => new CostEntryDto(
        entry.Id, entry.ProjectId, entry.SubProjectId,
        db.Set<SubProject>().Where(sub => sub.Id == entry.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
        entry.Division, entry.MainScope, entry.SubScope, entry.WbsCode, entry.Description, entry.ItemType.ToString(), entry.Quantity, entry.Unit,
        entry.UnitCostMinor, entry.VatTreatment.ToString(), entry.VatRateBps, entry.NetMinor, entry.VatMinor, entry.GrossMinor, entry.EntryDate,
        entry.Remarks, entry.Origin.ToString(), entry.OriginReferenceId, entry.SourceDealer, entry.InvoiceNumber, entry.Status.ToString(),
        entry.CreatedAtUtc, entry.VoidedAtUtc, entry.VoidReason,
        entry.Origin == CostOrigin.Manual && entry.Status == CostEntryStatus.Posted, entry.Version, entry.PurchaseOrderLineId,
        db.Set<PurchaseOrder>().Where(order => order.Lines.Any(line => line.Id == entry.PurchaseOrderLineId)).Select(order => order.Number).FirstOrDefault()));
}
