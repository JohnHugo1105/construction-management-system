using Cms.Application.Common;
using Cms.Domain.Features.Suppliers;

namespace Cms.Application.Features.Suppliers;

public interface ISupplierRepository
{
    /// <summary>Loads a firm tracked for change. Null when it isn't in the organization.</summary>
    Task<Supplier?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeSupplierId, CancellationToken cancellationToken);

    void Add(Supplier supplier);
}

public interface ISupplierReader
{
    Task<PagedResult<SupplierListItemDto>> ListAsync(SupplierListQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every active firm of a kind, by name, for pickers.</summary>
    Task<IReadOnlyList<SupplierLookupDto>> LookupAsync(string? kind, CancellationToken cancellationToken);
}

/// <param name="Kind">Supplier or Subcontractor to narrow the list; empty for every firm.</param>
public sealed record SupplierListQuery(string? Search, string? Kind, bool IncludeArchived);

public sealed record SupplierListItemDto(Guid Id, string Name, bool IsSupplier, bool IsSubcontractor, string? ContactPerson, string? Phone, string? Email,
    bool IsArchived, int Version);

public sealed record SupplierLookupDto(Guid Id, string Name, bool IsSupplier, bool IsSubcontractor);

public sealed record SupplierDto(Guid Id, string Name, bool IsSupplier, bool IsSubcontractor, string? Tin, string? ContactPerson, string? Phone,
    string? Email, string? Address, string? Notes, bool IsArchived, DateTimeOffset? ArchivedAtUtc, int Version);

/// <param name="IsSupplier">Buys materials and equipment from this firm.</param>
/// <param name="IsSubcontractor">Hires this firm to do work.</param>
public sealed record SaveSupplierRequest(string? Name, bool IsSupplier, bool IsSubcontractor, string? Tin, string? ContactPerson, string? Phone,
    string? Email, string? Address, string? Notes, int Version);

public static class SupplierMapper
{
    public static SupplierDto ToDto(Supplier supplier) => new(supplier.Id, supplier.Name, supplier.IsSupplier, supplier.IsSubcontractor, supplier.Tin,
        supplier.ContactPerson, supplier.Phone, supplier.Email, supplier.Address, supplier.Notes, supplier.IsArchived, supplier.ArchivedAtUtc,
        supplier.Version);

    public static SupplierKind KindOf(bool isSupplier, bool isSubcontractor) =>
        (isSupplier ? SupplierKind.Supplier : SupplierKind.None) | (isSubcontractor ? SupplierKind.Subcontractor : SupplierKind.None);
}
