using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Suppliers;

namespace ConstructOPX.Application.Features.Suppliers;

/// <summary>
/// The firms the company buys from and hires. A name is unique in the company (case and spacing ignored) so a firm is never entered twice
/// under slightly different spellings, which is how the retired app's supplier names drifted apart.
/// </summary>
public sealed class SupplierService(ISupplierRepository suppliers, ISupplierReader reader, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
    IAuditTrail audit)
{
    public Task<PagedResult<SupplierListItemDto>> ListAsync(SupplierListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(query, page, cancellationToken);

    public Task<IReadOnlyList<SupplierLookupDto>> LookupAsync(string? kind, CancellationToken cancellationToken) =>
        reader.LookupAsync(kind, cancellationToken);

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken) => SupplierMapper.ToDto(await LoadAsync(id, cancellationToken));

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = Supplier.Create(Guid.NewGuid(), currentUser.TenantId, request.Name, SupplierMapper.KindOf(request.IsSupplier, request.IsSubcontractor),
            request.Tin, request.ContactPerson, request.Phone, request.Email, request.Address, request.Notes, clock.UtcNow);
        await EnsureNameIsFreeAsync(supplier, null, cancellationToken);

        suppliers.Add(supplier);
        audit.Record("Supplier.Created", nameof(Supplier), supplier.Id, $"Added {Describe(supplier)} {supplier.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SupplierMapper.ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(supplier.Version, request.Version);
        var previousName = supplier.Name;
        supplier.Update(request.Name, SupplierMapper.KindOf(request.IsSupplier, request.IsSubcontractor), request.Tin, request.ContactPerson, request.Phone,
            request.Email, request.Address, request.Notes, clock.UtcNow);
        await EnsureNameIsFreeAsync(supplier, supplier.Id, cancellationToken);

        audit.Record("Supplier.Updated", nameof(Supplier), supplier.Id,
            previousName == supplier.Name ? $"Updated {supplier.Name}." : $"Updated {previousName}, now named {supplier.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SupplierMapper.ToDto(supplier);
    }

    public async Task<SupplierDto> ArchiveAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var supplier = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(supplier.Version, request.Version);
        supplier.Archive(clock.UtcNow);
        audit.Record("Supplier.Archived", nameof(Supplier), supplier.Id, $"Archived {supplier.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SupplierMapper.ToDto(supplier);
    }

    public async Task<SupplierDto> RestoreAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var supplier = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(supplier.Version, request.Version);
        supplier.Restore(clock.UtcNow);
        audit.Record("Supplier.Restored", nameof(Supplier), supplier.Id, $"Restored {supplier.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SupplierMapper.ToDto(supplier);
    }

    private async Task<Supplier> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await suppliers.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that firm");

    private async Task EnsureNameIsFreeAsync(Supplier supplier, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await suppliers.NameExistsAsync(supplier.NormalizedName, excludeId, cancellationToken))
            throw new ValidationFailedException("name", $"A firm called {supplier.Name} already exists. Open it instead of adding it again.");
    }

    private static string Describe(Supplier supplier) =>
        supplier is { IsSupplier: true, IsSubcontractor: true } ? "supplier and subcontractor" : supplier.IsSubcontractor ? "subcontractor" : "supplier";
}
