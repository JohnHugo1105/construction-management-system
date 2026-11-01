using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Bonds;

/// <summary>
/// Bonds register use cases: record a bond a bank or surety gave (posted to a client, or received from a subcontractor or supplier), keep its
/// details, extend it, and close it by releasing it, marking it called or voiding it. Every change loads the bond, checks the version the
/// person opened and that the project is not archived, and saves once with its audit line. A bond is a record to watch, not money: nothing
/// here posts a cost, a bill or a payment.
/// </summary>
public sealed class BondService(IBondRepository bonds, IBondReader reader, IProjectRepository projects, IOrganizationRepository organizations,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    /// <summary>The most bonds a project's own list returns (the company register pages through all of them).</summary>
    public const int ProjectListLimit = 50;

    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    public async Task<PagedResult<BondListItemDto>> ListAsync(BondListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        // A project of another company answers 404, not an empty list.
        if (query.ProjectId is { } projectId) await LoadProjectAsync(projectId, cancellationToken);
        var filter = new BondListFilter(query.ProjectId, ParseFilter<BondDirection>(query.Direction, "direction", "Choose Posted or Received."),
            ParseFilter<BondKind>(query.Kind, "kind", "Choose Bid, Performance, AdvancePayment, Warranty or Other."),
            ParseFilter<BondStatus>(query.Status, "status", "Choose Active, Released, Called or Void."),
            ParseFilter<BondExpiry>(query.Expiry, "expiry", "Choose Valid, ExpiringSoon or Expired."));
        return await reader.ListAsync(filter, await TodayAsync(cancellationToken), page, cancellationToken);
    }

    public async Task<IReadOnlyList<BondListItemDto>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, cancellationToken);
        return await reader.ListForProjectAsync(projectId, await TodayAsync(cancellationToken), ProjectListLimit, cancellationToken);
    }

    public async Task<BondDto> GetAsync(Guid id, CancellationToken cancellationToken) => await ReadAsync(id, cancellationToken);

    public async Task<BondTotalsDto> TotalsAsync(CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        return await reader.TotalsAsync(organization.TodayLocal(clock.UtcNow), organization.CurrencyCode, cancellationToken);
    }

    // ---- Changes ----------------------------------------------------------------------------------------------------------------

    public async Task<BondDto> CreateAsync(CreateBondRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await bonds.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, request, cancellationToken);

        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var direction = Parse(request.Direction, (BondDirection)(-1));
        var bond = Bond.Create(Guid.NewGuid(), currentUser.TenantId, project, direction, Details(request.Kind, request.IssuerName, request.BondNumber,
            request.AmountMinor, request.IssuedOn, request.ValidUntil, request.OtherPartyName, request.Note), organization.CurrencyCode, currentUser.UserId,
            requestId, clock.UtcNow);

        bonds.Add(bond);
        audit.Record("Bond.Recorded", nameof(Bond), bond.Id,
            $"Recorded {Describe(bond)} of {CostingInput.MoneyText(bond.AmountMinor, bond.CurrencyCode)} on {project.Name}, valid until {Format(bond.ValidUntil)}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made.
            if (await bonds.FindByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SameCreateAsync(first, request, cancellationToken);
            throw;
        }
        return await ReadAsync(bond.Id, cancellationToken);
    }

    public async Task<BondDto> UpdateAsync(Guid id, UpdateBondRequest request, CancellationToken cancellationToken)
    {
        var (bond, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var changed = bond.Edit(project, Details(request.Kind, request.IssuerName, request.BondNumber, request.AmountMinor, request.IssuedOn,
            request.ValidUntil, request.OtherPartyName, request.Note), currentUser.UserId, clock.UtcNow);
        if (!changed) return await ReadAsync(bond.Id, cancellationToken);

        var moved = bond.Events[^1];
        audit.Record("Bond.Edited", nameof(Bond), bond.Id,
            moved.NewValidUntil is { } to
                ? $"Changed the details of {Describe(bond)} on {project.Name} and moved its valid-until date from {Format(moved.PreviousValidUntil!.Value)} to {Format(to)}."
                : $"Changed the details of {Describe(bond)} on {project.Name}.");
        return await SaveAndReadAsync(bond, cancellationToken);
    }

    public async Task<BondDto> ExtendAsync(Guid id, ExtendBondRequest request, CancellationToken cancellationToken)
    {
        var (bond, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var previous = bond.ValidUntil;
        bond.Extend(project, request.ValidUntil ?? default, request.Note, currentUser.UserId, clock.UtcNow);
        // The note is free text and stays on the bond's history, not in the audit line.
        audit.Record("Bond.Extended", nameof(Bond), bond.Id,
            $"Extended {Describe(bond)} on {project.Name} from {Format(previous)} to {Format(bond.ValidUntil)}.");
        return await SaveAndReadAsync(bond, cancellationToken);
    }

    public async Task<BondDto> ReleaseAsync(Guid id, ReleaseBondRequest request, CancellationToken cancellationToken)
    {
        var (bond, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        bond.Release(project, request.ReleasedOn ?? default, request.Note, currentUser.UserId, await TodayAsync(cancellationToken), clock.UtcNow);
        audit.Record("Bond.Released", nameof(Bond), bond.Id, $"Released {Describe(bond)} on {project.Name}.");
        return await SaveAndReadAsync(bond, cancellationToken);
    }

    public async Task<BondDto> MarkCalledAsync(Guid id, CallBondRequest request, CancellationToken cancellationToken)
    {
        var (bond, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        bond.MarkCalled(project, request.CalledOn ?? default, request.Note, currentUser.UserId, await TodayAsync(cancellationToken), clock.UtcNow);
        audit.Record("Bond.Called", nameof(Bond), bond.Id, $"Marked {Describe(bond)} on {project.Name} as called.");
        return await SaveAndReadAsync(bond, cancellationToken);
    }

    public async Task<BondDto> VoidAsync(Guid id, BondReasonRequest request, CancellationToken cancellationToken)
    {
        var (bond, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        bond.Void(project, request.Reason, currentUser.UserId, clock.UtcNow);
        // The reason is free text and stays on the record, not in the audit line.
        audit.Record("Bond.Voided", nameof(Bond), bond.Id, $"Voided {Describe(bond)} on {project.Name}.");
        return await SaveAndReadAsync(bond, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private async Task<BondDto> SaveAndReadAsync(Bond bond, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(bond.Id, cancellationToken);
    }

    /// <summary>A repeated create answers with the original only when it was for the same project and direction.</summary>
    private async Task<BondDto> SameCreateAsync(BondRequestOwner owner, CreateBondRequest request, CancellationToken cancellationToken) =>
        owner.ProjectId == request.ProjectId && owner.Direction == Parse(request.Direction, (BondDirection)(-1))
            ? await ReadAsync(owner.BondId, cancellationToken)
            : throw new DomainRuleException("That request was already used for a different bond. Reload the page and try again.");

    /// <summary>Loads the bond and its project for a change: the version must be the one the person opened (the project's own check is in the domain).</summary>
    private async Task<(Bond Bond, Project Project)> LoadForChangeAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var bond = await bonds.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that bond");
        ConcurrencyGuard.Ensure(bond.Version, version);
        return (bond, await LoadProjectAsync(bond.ProjectId, cancellationToken));
    }

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task<BondDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, await TodayAsync(cancellationToken), cancellationToken) ?? throw new NotFoundException("that bond");

    /// <summary>The company's calendar day: expiry is judged on it, never on the UTC day.</summary>
    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private static BondDetails Details(string? kind, string? issuerName, string? bondNumber, long amountMinor, DateOnly? issuedOn, DateOnly? validUntil,
        string? otherPartyName, string? note) =>
        new(Parse(kind, (BondKind)(-1)), issuerName, bondNumber, amountMinor, issuedOn ?? default, validUntil ?? default, otherPartyName, note);

    /// <summary>A word from the request as the enum, or <paramref name="invalid"/> (which the domain reports as "choose one"). Numbers are not words.</summary>
    private static T Parse<T>(string? text, T invalid) where T : struct, Enum =>
        !string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) && Enum.TryParse<T>(text.Trim(), ignoreCase: true, out var value) &&
        Enum.IsDefined(value)
            ? value
            : invalid;

    private static T? ParseFilter<T>(string? text, string field, string message) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parsed = Parse(text, (T)Enum.ToObject(typeof(T), -1));
        return Enum.IsDefined(parsed) ? parsed : throw new ValidationFailedException(field, message);
    }

    /// <summary>"Posted performance bond PB-2026-014", for audit lines (the number and issuer are identifiers, never free-form notes).</summary>
    private static string Describe(Bond bond) =>
        $"{bond.Direction.ToString().ToLowerInvariant()} {KindWords(bond.Kind)} bond {bond.BondNumber} from {bond.IssuerName}";

    private static string KindWords(BondKind kind) => kind switch
    {
        BondKind.AdvancePayment => "advance payment",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
