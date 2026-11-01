using Cms.Application.Common;
using Cms.Application.Features.ChangeOrders;
using Cms.Domain.Features.ChangeOrders;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.ChangeOrders;

internal sealed class ChangeOrderRepository(CmsDbContext db) : IChangeOrderRepository
{
    public Task<ChangeOrder?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ChangeOrder>().Include(order => order.Lines).SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<ChangeOrder?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<ChangeOrder>().SingleOrDefaultAsync(order => order.ClientRequestId == clientRequestId, cancellationToken);

    public Task<string?> LatestApprovedAfterAsync(Guid projectId, Guid excludingId, DateTimeOffset approvedAtUtc, long sequence,
        CancellationToken cancellationToken) =>
        LaterApproved(db.Set<ChangeOrder>().AsNoTracking(), projectId, excludingId, approvedAtUtc, sequence)
            .Select(order => order.Number).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AnyApprovedAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<ChangeOrder>().AnyAsync(order => order.ProjectId == projectId && order.Status == ChangeOrderStatus.Approved, cancellationToken);

    public void Add(ChangeOrder order) => db.Set<ChangeOrder>().Add(order);

    /// <summary>Approved change orders of the project decided after the given one, newest first.</summary>
    internal static IQueryable<ChangeOrder> LaterApproved(IQueryable<ChangeOrder> orders, Guid projectId, Guid excludingId, DateTimeOffset approvedAtUtc,
        long sequence) =>
        orders.Where(order => order.ProjectId == projectId && order.Id != excludingId && order.Status == ChangeOrderStatus.Approved &&
                (order.DecidedAtUtc > approvedAtUtc || (order.DecidedAtUtc == approvedAtUtc && order.Sequence > sequence)))
            .OrderByDescending(order => order.DecidedAtUtc).ThenByDescending(order => order.Sequence);
}

internal sealed class ChangeOrderBudgetLineRepository(CmsDbContext db) : IChangeOrderBudgetLineRepository
{
    public async Task<IReadOnlyList<BudgetLine>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        var distinct = ids.Distinct().ToArray();
        return await db.Set<BudgetLine>().Where(line => distinct.Contains(line.Id)).ToListAsync(cancellationToken);
    }
}

internal sealed class ChangeOrderReader(CmsDbContext db) : IChangeOrderReader
{
    public async Task<PagedResult<ChangeOrderListItemDto>> ListAsync(Guid projectId, ChangeOrderStatus? status, PageRequest page,
        CancellationToken cancellationToken)
    {
        var orders = db.Set<ChangeOrder>().AsNoTracking().Where(order => order.ProjectId == projectId);
        if (status is { } wanted) orders = orders.Where(order => order.Status == wanted);

        var total = await orders.CountAsync(cancellationToken);
        // Newest first: a numbered order by its number, drafts (no number yet) above them by when they were started.
        var rows = await orders
            .OrderBy(order => order.Sequence != null).ThenByDescending(order => order.Sequence).ThenByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(order => new
            {
                order.Id,
                order.ProjectId,
                order.Number,
                order.Title,
                order.Reason,
                order.Status,
                order.VoidedFromStatus,
                LineCount = order.Lines.Count(),
                order.BudgetEffectNetMinor,
                order.BudgetEffectGrossMinor,
                order.ContractPriceDeltaMinor,
                order.CreatedAtUtc,
                order.SubmittedAtUtc,
                order.DecidedAtUtc,
                order.VoidedAtUtc,
                order.Version
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new ChangeOrderListItemDto(row.Id, row.ProjectId, row.Number, row.Title, row.Reason.ToString(), row.Status.ToString(),
            row.VoidedFromStatus?.ToString(), row.LineCount, row.BudgetEffectNetMinor, row.BudgetEffectGrossMinor, row.ContractPriceDeltaMinor,
            row.CreatedAtUtc, row.SubmittedAtUtc, row.DecidedAtUtc, row.VoidedAtUtc, row.Version)).ToArray();
        return PagedResult<ChangeOrderListItemDto>.Of(items, total, page);
    }

    public async Task<ChangeOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var all = db.Set<ChangeOrder>().AsNoTracking();
        var users = db.Set<User>();
        var header = await all.Where(order => order.Id == id).Select(order => new
        {
            Order = order,
            CreatedBy = users.Where(user => user.Id == order.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            SubmittedBy = users.Where(user => user.Id == order.SubmittedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            DecidedBy = users.Where(user => user.Id == order.DecidedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            VoidedBy = users.Where(user => user.Id == order.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            // Only an approved order can be blocked: the newest order approved after it must be voided first.
            VoidBlockedBy = order.Status == ChangeOrderStatus.Approved
                ? all.Where(other => other.ProjectId == order.ProjectId && other.Id != order.Id && other.Status == ChangeOrderStatus.Approved &&
                        (other.DecidedAtUtc > order.DecidedAtUtc || (other.DecidedAtUtc == order.DecidedAtUtc && other.Sequence > order.Sequence)))
                    .OrderByDescending(other => other.DecidedAtUtc).ThenByDescending(other => other.Sequence)
                    .Select(other => other.Number).FirstOrDefault()
                : null
        }).SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var lines = await (
            from line in db.Set<ChangeOrderLine>().AsNoTracking()
            where line.ChangeOrderId == id
            join sub in db.Set<SubProject>() on line.SubProjectId equals sub.Id into subs
            from sub in subs.DefaultIfEmpty()
            orderby line.LineNumber
            select new { Line = line, SubProjectName = sub.Name }).ToListAsync(cancellationToken);

        var order = header.Order;
        return new ChangeOrderDto(order.Id, order.ProjectId, order.Number, order.Title, order.Description, order.Reason.ToString(), order.Status.ToString(),
            order.VoidedFromStatus?.ToString(), order.ContractPriceDeltaMinor, order.BudgetEffectNetMinor, order.BudgetEffectVatMinor,
            order.BudgetEffectGrossMinor, header.CreatedBy, order.CreatedAtUtc, header.SubmittedBy, order.SubmittedAtUtc, header.DecidedBy,
            order.DecidedAtUtc, order.DecisionReason, header.VoidedBy, order.VoidedAtUtc, order.VoidReason, header.VoidBlockedBy,
            lines.Select(row => ToDto(row.Line, row.SubProjectName)).ToArray(), order.Version);
    }

    public async Task<BaselineFigures?> BaselineFiguresAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Set<Project>().AsNoTracking().Where(item => item.Id == projectId).Select(item => new
        {
            item.Id,
            item.Name,
            item.Version,
            item.ContractPriceMinor,
            item.BudgetBaselineLockedAtUtc,
            LockedBy = db.Set<User>().Where(user => user.Id == item.BudgetBaselineLockedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            item.BaselineBudgetNetMinor,
            item.BaselineBudgetGrossMinor,
            item.BaselineContractPriceMinor,
            item.BudgetBaselineUnlockReason,
            item.BudgetBaselineUnlockedAtUtc
        }).SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        // One grouped statement for the active budget lines, one for the change orders that count (approved) or wait (submitted).
        var active = await db.Set<BudgetLine>().AsNoTracking()
            .Where(line => line.ProjectId == projectId && line.Status == BudgetLineStatus.Active)
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Net = group.Sum(line => line.NetMinor), Gross = group.Sum(line => line.GrossMinor) })
            .SingleOrDefaultAsync(cancellationToken);

        var orders = await db.Set<ChangeOrder>().AsNoTracking()
            .Where(order => order.ProjectId == projectId && (order.Status == ChangeOrderStatus.Approved || order.Status == ChangeOrderStatus.Submitted))
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Net = group.Sum(order => order.BudgetEffectNetMinor),
                Gross = group.Sum(order => order.BudgetEffectGrossMinor),
                Price = group.Sum(order => order.ContractPriceDeltaMinor)
            })
            .ToListAsync(cancellationToken);
        var approved = orders.SingleOrDefault(row => row.Status == ChangeOrderStatus.Approved);
        var pending = orders.SingleOrDefault(row => row.Status == ChangeOrderStatus.Submitted);

        return new BaselineFigures(project.Id, project.Name, project.Version, project.ContractPriceMinor, project.BudgetBaselineLockedAtUtc, project.LockedBy,
            project.BaselineBudgetNetMinor, project.BaselineBudgetGrossMinor, project.BaselineContractPriceMinor, project.BudgetBaselineUnlockReason,
            project.BudgetBaselineUnlockedAtUtc, active?.Count ?? 0, active?.Net ?? 0, active?.Gross ?? 0, approved?.Count ?? 0, approved?.Net ?? 0,
            approved?.Gross ?? 0, approved?.Price ?? 0, pending?.Count ?? 0, pending?.Net ?? 0, pending?.Gross ?? 0, pending?.Price ?? 0);
    }

    public Task<string?> NumberOfAsync(Guid changeOrderId, CancellationToken cancellationToken) =>
        db.Set<ChangeOrder>().AsNoTracking().Where(order => order.Id == changeOrderId).Select(order => order.Number).SingleOrDefaultAsync(cancellationToken);

    private static ChangeOrderLineDto ToDto(ChangeOrderLine line, string? subProjectName) => new(line.Id, line.LineNumber, line.Kind.ToString(),
        line.BudgetLineId, line.SubProjectId, subProjectName, line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description,
        line.ItemType.ToString(), line.Quantity, line.Unit, line.UnitCostMinor, line.VatTreatment.ToString(), line.VatRateBps, line.EntryDate, line.Remarks,
        line.NetMinor, line.VatMinor, line.GrossMinor, line.PreviousQuantity, line.PreviousUnitCostMinor, line.PreviousNetMinor, line.PreviousGrossMinor,
        line.EffectNetMinor, line.EffectVatMinor, line.EffectGrossMinor);
}
