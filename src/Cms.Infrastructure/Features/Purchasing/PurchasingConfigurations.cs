using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Purchasing;
using Cms.Domain.Features.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Purchasing;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Number).HasMaxLength(20).IsRequired();
        builder.Property(item => item.SupplierReference).HasMaxLength(80);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.CloseReason).HasMaxLength(300);
        builder.Property(item => item.CancelReason).HasMaxLength(300);
        builder.Ignore(item => item.IsFinal);

        // An order never points at a firm or a project that isn't there, whatever the application does.
        builder.HasOne<Supplier>().WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        // A draft's lines are replaced as a set, so dropping one from the draft deletes its row: the only delete in this feature. An approved
        // order's lines are locked, and an order itself is never deleted.
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.SupplierId });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.OrderDate });
        // Double-clicking "Save purchase order" never raises two orders: a repeated request id is refused by the database as well as the service.
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.MainScope).HasMaxLength(120);
        builder.Property(item => item.SubScope).HasMaxLength(160);
        builder.Property(item => item.WbsCode).HasMaxLength(20);
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.Unit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(item => new { item.PurchaseOrderId, item.LineNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderId });
    }
}
