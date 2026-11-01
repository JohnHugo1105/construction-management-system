using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Suppliers;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Infrastructure.Common;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Suppliers;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.Tin).HasMaxLength(30);
        builder.Property(item => item.ContactPerson).HasMaxLength(120);
        builder.Property(item => item.Phone).HasMaxLength(40);
        builder.Property(item => item.Email).HasMaxLength(254);
        builder.Property(item => item.Address).HasMaxLength(250);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Ignore(item => item.IsSupplier);
        builder.Ignore(item => item.IsSubcontractor);

        // A firm is entered once per company; the service checks first to give a friendly message and this index is the backstop.
        builder.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IsArchived, item.Kind });
    }
}

internal sealed class SupplierRepository(ConstructOpxDbContext db) : ISupplierRepository
{
    public Task<Supplier?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Supplier>().SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeSupplierId, CancellationToken cancellationToken) =>
        db.Set<Supplier>().AnyAsync(supplier => supplier.NormalizedName == normalizedName && supplier.Id != excludeSupplierId, cancellationToken);

    public void Add(Supplier supplier) => db.Set<Supplier>().Add(supplier);
}

internal sealed class SupplierReader(ConstructOpxDbContext db) : ISupplierReader
{
    public async Task<PagedResult<SupplierListItemDto>> ListAsync(SupplierListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        var suppliers = db.Set<Supplier>().AsNoTracking();
        if (!query.IncludeArchived) suppliers = suppliers.Where(supplier => !supplier.IsArchived);
        suppliers = FilterKind(suppliers, query.Kind);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Sql.Contains(query.Search);
            suppliers = suppliers.Where(supplier => EF.Functions.Like(supplier.Name, pattern, Sql.LikeEscape) ||
                (supplier.ContactPerson != null && EF.Functions.Like(supplier.ContactPerson, pattern, Sql.LikeEscape)));
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var rows = await suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(supplier => new { supplier.Id, supplier.Name, supplier.Kind, supplier.ContactPerson, supplier.Phone, supplier.Email, supplier.IsArchived, supplier.Version })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new SupplierListItemDto(row.Id, row.Name, row.Kind.HasFlag(SupplierKind.Supplier),
            row.Kind.HasFlag(SupplierKind.Subcontractor), row.ContactPerson, row.Phone, row.Email, row.IsArchived, row.Version)).ToList();
        return PagedResult<SupplierListItemDto>.Of(items, total, page);
    }

    public async Task<IReadOnlyList<SupplierLookupDto>> LookupAsync(string? kind, CancellationToken cancellationToken)
    {
        var rows = await FilterKind(db.Set<Supplier>().AsNoTracking().Where(supplier => !supplier.IsArchived), kind)
            .OrderBy(supplier => supplier.Name).Take(500)
            .Select(supplier => new { supplier.Id, supplier.Name, supplier.Kind })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new SupplierLookupDto(row.Id, row.Name, row.Kind.HasFlag(SupplierKind.Supplier), row.Kind.HasFlag(SupplierKind.Subcontractor))).ToArray();
    }

    private static IQueryable<Supplier> FilterKind(IQueryable<Supplier> suppliers, string? kind) =>
        Enum.TryParse<SupplierKind>(kind, ignoreCase: true, out var wanted) && wanted is SupplierKind.Supplier or SupplierKind.Subcontractor
            ? suppliers.Where(supplier => (supplier.Kind & wanted) == wanted)
            : suppliers;
}
