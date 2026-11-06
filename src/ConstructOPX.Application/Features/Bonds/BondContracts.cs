using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Bonds;

namespace ConstructOPX.Application.Features.Bonds;

public interface IBondRepository
{
    /// <summary>Loads a bond with its history, tracked for change. Null when it isn't in the organization.</summary>
    Task<Bond?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The bond (and its project) an earlier create with this request id made.</summary>
    Task<BondRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(Bond bond);
}

/// <summary>Who a request id already belongs to.</summary>
public sealed record BondRequestOwner(Guid BondId, Guid ProjectId, BondDirection Direction);

/// <summary>
/// Reads bonds straight into DTOs. The expiry state is judged on <c>today</c>, the company's calendar day, which the caller works out once.
/// Every read is a fixed number of statements however many bonds exist.
/// </summary>
public interface IBondReader
{
    Task<PagedResult<BondListItemDto>> ListAsync(BondListFilter filter, DateOnly today, PageRequest page, CancellationToken cancellationToken);

    /// <summary>A project's bonds that are not voided, Active first by valid-until, at most <paramref name="limit"/> of them.</summary>
    Task<IReadOnlyList<BondListItemDto>> ListForProjectAsync(Guid projectId, DateOnly today, int limit, CancellationToken cancellationToken);

    Task<BondDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken);

    Task<BondTotalsDto> TotalsAsync(DateOnly today, string currencyCode, CancellationToken cancellationToken);
}

public sealed record BondListQuery(Guid? ProjectId, string? Direction, string? Kind, string? Status, string? Expiry);

public sealed record BondListFilter(Guid? ProjectId, BondDirection? Direction, BondKind? Kind, BondStatus? Status, BondExpiry? Expiry);

/// <summary>
/// One row of a list. <c>Expiry</c> (Valid, ExpiringSoon or Expired) and <c>DaysLeft</c> (negative once past) are present only for an Active bond:
/// the server judges them on the company's calendar day so every screen agrees.
/// </summary>
public sealed record BondListItemDto(Guid Id, Guid ProjectId, string ProjectName, string Direction, string Kind, string IssuerName, string BondNumber,
    string? OtherPartyName, long AmountMinor, string CurrencyCode, DateOnly IssuedOn, DateOnly ValidUntil, string Status, string? Expiry, int? DaysLeft,
    int Version);

/// <param name="ProjectArchived">The project is archived: the bond can be read but not changed.</param>
/// <param name="History">Newest first, at most the latest 100 steps.</param>
public sealed record BondDto(Guid Id, Guid ProjectId, string ProjectName, bool ProjectArchived, string Direction, string Kind, string IssuerName,
    string BondNumber, string? OtherPartyName, long AmountMinor, string CurrencyCode, DateOnly IssuedOn, DateOnly ValidUntil, string? Note, string Status,
    string? Expiry, int? DaysLeft, DateOnly? ReleasedOn, string? ReleaseNote, DateOnly? CalledOn, string? CallNote, string? VoidReason,
    string? CreatedByName, DateTimeOffset CreatedAtUtc, IReadOnlyList<BondEventDto> History, int Version);

public sealed record BondEventDto(Guid Id, string Kind, string? ByName, DateTimeOffset AtUtc, DateOnly? PreviousValidUntil, DateOnly? NewValidUntil,
    DateOnly? EventDate, string? Note);

/// <summary>What the company holds and has given: the Active amounts by direction, and how many Active bonds need attention.</summary>
/// <param name="ExpiredCount">Active bonds whose valid-until date has passed.</param>
/// <param name="ExpiringSoonCount">Active bonds with 30 days or fewer left.</param>
public sealed record BondTotalsDto(string CurrencyCode, BondDirectionTotalDto Posted, BondDirectionTotalDto Received, int ExpiredCount,
    int ExpiringSoonCount);

public sealed record BondDirectionTotalDto(int ActiveCount, long ActiveAmountMinor);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <param name="Direction">Posted (you gave it to a client) or Received (a subcontractor or supplier gave it to you).</param>
/// <param name="Kind">Bid, Performance, AdvancePayment, Warranty or Other.</param>
/// <param name="AmountMinor">The guaranteed amount in centavos.</param>
/// <param name="OtherPartyName">For a received bond, the firm that gave it.</param>
public sealed record CreateBondRequest(Guid ProjectId, string? Direction, string? Kind, string? IssuerName, string? BondNumber, long AmountMinor,
    DateOnly? IssuedOn, DateOnly? ValidUntil, string? OtherPartyName, string? Note, string? ClientRequestId);

public sealed record UpdateBondRequest(string? Kind, string? IssuerName, string? BondNumber, long AmountMinor, DateOnly? IssuedOn, DateOnly? ValidUntil,
    string? OtherPartyName, string? Note, int Version);

public sealed record ExtendBondRequest(DateOnly? ValidUntil, string? Note, int Version);

public sealed record ReleaseBondRequest(DateOnly? ReleasedOn, string? Note, int Version);

public sealed record CallBondRequest(DateOnly? CalledOn, string? Note, int Version);

public sealed record BondReasonRequest(string? Reason, int Version);
