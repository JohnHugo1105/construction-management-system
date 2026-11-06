using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Bonds;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Bonds;

internal sealed class BondRepository(ConstructOpxDbContext db) : IBondRepository
{
    public Task<Bond?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Bond>().Include(item => item.Events).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<BondRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<Bond>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new BondRequestOwner(item.Id, item.ProjectId, item.Direction)).SingleOrDefaultAsync(cancellationToken);

    public void Add(Bond bond) => db.Set<Bond>().Add(bond);
}

/// <summary>
/// Reads bonds straight into DTOs. The company list is two statements (count and page), a project's list one, a bond two (the bond and its
/// history), and the totals one grouped statement, however many bonds exist. The expiry state is worked out here from the valid-until date and
/// the company's calendar day, never stored.
/// </summary>
internal sealed class BondReader(ConstructOpxDbContext db) : IBondReader
{
    /// <summary>The newest steps of a bond's history that one read returns.</summary>
    private const int HistoryLimit = 100;

    public async Task<PagedResult<BondListItemDto>> ListAsync(BondListFilter filter, DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = Rows();
        if (filter.ProjectId is { } projectId) rows = rows.Where(row => row.Bond.ProjectId == projectId);
        if (filter.Direction is { } direction) rows = rows.Where(row => row.Bond.Direction == direction);
        if (filter.Kind is { } kind) rows = rows.Where(row => row.Bond.Kind == kind);
        if (filter.Status is { } status) rows = rows.Where(row => row.Bond.Status == status);
        if (filter.Expiry is { } expiry)
        {
            // Only an Active bond has an expiry state.
            var soonLimit = today.AddDays(Bond.ExpiringSoonDays);
            rows = expiry switch
            {
                BondExpiry.Expired => rows.Where(row => row.Bond.Status == BondStatus.Active && row.Bond.ValidUntil < today),
                BondExpiry.ExpiringSoon => rows.Where(row => row.Bond.Status == BondStatus.Active && row.Bond.ValidUntil >= today && row.Bond.ValidUntil <= soonLimit),
                _ => rows.Where(row => row.Bond.Status == BondStatus.Active && row.Bond.ValidUntil > soonLimit)
            };
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await Ordered(rows).Skip(page.Skip).Take(page.SafePageSize).Select(ItemProjection()).ToListAsync(cancellationToken);
        return PagedResult<BondListItemDto>.Of(items.Select(row => ToItem(row, today)).ToArray(), total, page);
    }

    public async Task<IReadOnlyList<BondListItemDto>> ListForProjectAsync(Guid projectId, DateOnly today, int limit, CancellationToken cancellationToken)
    {
        var items = await Ordered(Rows().Where(row => row.Bond.ProjectId == projectId && row.Bond.Status != BondStatus.Void))
            .Take(limit).Select(ItemProjection()).ToListAsync(cancellationToken);
        return items.Select(row => ToItem(row, today)).ToArray();
    }

    public async Task<BondDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var head = await (
            from bond in db.Set<Bond>().AsNoTracking()
            where bond.Id == id
            join project in db.Set<Project>() on bond.ProjectId equals project.Id
            select new
            {
                Bond = bond,
                ProjectName = project.Name,
                ProjectStatus = project.Status,
                CreatedBy = users.Where(user => user.Id == bond.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (head is null) return null;

        var events = await db.Set<BondEvent>().AsNoTracking().Where(item => item.BondId == id)
            .OrderByDescending(item => item.AtUtc).ThenByDescending(item => item.Id).Take(HistoryLimit)
            .Select(item => new
            {
                Event = item,
                By = users.Where(user => user.Id == item.ByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var b = head.Bond;
        var expiry = b.ExpiryOn(today);
        return new BondDto(b.Id, b.ProjectId, head.ProjectName, head.ProjectStatus == ProjectStatus.Archived, b.Direction.ToString(), b.Kind.ToString(),
            b.IssuerName, b.BondNumber, b.OtherPartyName, b.AmountMinor, b.CurrencyCode, b.IssuedOn, b.ValidUntil, b.Note, b.Status.ToString(),
            expiry?.ToString(), expiry is null ? null : Bond.DaysLeft(b.ValidUntil, today), b.ReleasedOn, b.ReleaseNote, b.CalledOn, b.CallNote,
            b.VoidReason, head.CreatedBy, b.CreatedAtUtc,
            events.Select(row => new BondEventDto(row.Event.Id, row.Event.Kind.ToString(), row.By, row.Event.AtUtc, row.Event.PreviousValidUntil,
                row.Event.NewValidUntil, row.Event.EventDate, row.Event.Note)).ToArray(),
            b.Version);
    }

    public async Task<BondTotalsDto> TotalsAsync(DateOnly today, string currencyCode, CancellationToken cancellationToken)
    {
        var soonLimit = today.AddDays(Bond.ExpiringSoonDays);
        var groups = await db.Set<Bond>().AsNoTracking().Where(item => item.Status == BondStatus.Active)
            .GroupBy(item => item.Direction)
            .Select(group => new
            {
                Direction = group.Key,
                Count = group.Count(),
                Amount = group.Sum(item => item.AmountMinor),
                Expired = group.Count(item => item.ValidUntil < today),
                ExpiringSoon = group.Count(item => item.ValidUntil >= today && item.ValidUntil <= soonLimit)
            }).ToListAsync(cancellationToken);

        BondDirectionTotalDto Total(BondDirection direction) =>
            groups.FirstOrDefault(group => group.Direction == direction) is { } found ? new BondDirectionTotalDto(found.Count, found.Amount) : new BondDirectionTotalDto(0, 0);

        return new BondTotalsDto(currencyCode, Total(BondDirection.Posted), Total(BondDirection.Received), groups.Sum(group => group.Expired),
            groups.Sum(group => group.ExpiringSoon));
    }

    private IQueryable<BondRow> Rows() =>
        from bond in db.Set<Bond>().AsNoTracking()
        join project in db.Set<Project>() on bond.ProjectId equals project.Id
        select new BondRow { Bond = bond, ProjectName = project.Name };

    /// <summary>Active bonds first, the one that lapses soonest at the top; then the closed ones, the most recently valid first.</summary>
    private static IOrderedQueryable<BondRow> Ordered(IQueryable<BondRow> rows) =>
        rows.OrderBy(row => row.Bond.Status == BondStatus.Active ? 0 : 1)
            .ThenBy(row => row.Bond.Status == BondStatus.Active ? row.Bond.ValidUntil : (DateOnly?)null)
            .ThenByDescending(row => row.Bond.ValidUntil)
            .ThenBy(row => row.Bond.Id);

    private static System.Linq.Expressions.Expression<Func<BondRow, ItemRow>> ItemProjection() => row => new ItemRow(row.Bond.Id, row.Bond.ProjectId,
        row.ProjectName, row.Bond.Direction, row.Bond.Kind, row.Bond.IssuerName, row.Bond.BondNumber, row.Bond.OtherPartyName, row.Bond.AmountMinor,
        row.Bond.CurrencyCode, row.Bond.IssuedOn, row.Bond.ValidUntil, row.Bond.Status, row.Bond.Version);

    private static BondListItemDto ToItem(ItemRow row, DateOnly today)
    {
        var active = row.Status == BondStatus.Active;
        return new BondListItemDto(row.Id, row.ProjectId, row.ProjectName, row.Direction.ToString(), row.Kind.ToString(), row.IssuerName, row.BondNumber,
            row.OtherPartyName, row.AmountMinor, row.CurrencyCode, row.IssuedOn, row.ValidUntil, row.Status.ToString(),
            active ? Bond.ExpiryFor(row.ValidUntil, today).ToString() : null, active ? Bond.DaysLeft(row.ValidUntil, today) : null, row.Version);
    }

    /// <summary>A bond with its project's name. A class with initializers (not a record) so filters and ordering still compose in SQL.</summary>
    private sealed class BondRow
    {
        public required Bond Bond { get; init; }
        public required string ProjectName { get; init; }
    }

    private sealed record ItemRow(Guid Id, Guid ProjectId, string ProjectName, BondDirection Direction, BondKind Kind, string IssuerName, string BondNumber,
        string? OtherPartyName, long AmountMinor, string CurrencyCode, DateOnly IssuedOn, DateOnly ValidUntil, BondStatus Status, int Version);
}
