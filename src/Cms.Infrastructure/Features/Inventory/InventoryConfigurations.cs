using Cms.Domain.Features.Inventory;
using Cms.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Inventory;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Brand).HasMaxLength(80);
        builder.Property(item => item.SerialNumber).HasMaxLength(80);
        builder.Property(item => item.NormalizedSerialNumber).HasMaxLength(80);
        builder.Property(item => item.Unit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.SourceDealer).HasMaxLength(120);
        builder.Property(item => item.InvoiceNumber).HasMaxLength(60);
        builder.Property(item => item.Remarks).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.RetiredReason).HasMaxLength(300);
        builder.Ignore(item => item.IsUnitTracked);
        builder.Ignore(item => item.TotalQuantity);

        // A balance row that reaches zero is removed from the item, so removal must delete the row. The ledger is never removed.
        builder.HasMany(item => item.Balances).WithOne().HasForeignKey(balance => balance.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Balances).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(item => item.Movements).WithOne().HasForeignKey(movement => movement.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One serial number identifies one piece in a company (formatting aside: PC200-8 and pc200 8 are the same number).
        builder.HasIndex(item => new { item.TenantId, item.NormalizedSerialNumber }).IsUnique().HasFilter("[NormalizedSerialNumber] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.Description });
        builder.HasIndex(item => new { item.TenantId, item.Status });
    }
}

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("StockBalances");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.LocationKey).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Ignore(item => item.Location);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // An item has one row per place it is in.
        builder.HasIndex(item => new { item.ItemId, item.LocationKey }).IsUnique();
        // "What is on this project now".
        builder.HasIndex(item => new { item.TenantId, item.ProjectId }).HasFilter("[ProjectId] IS NOT NULL");
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.FromKey).HasMaxLength(40);
        builder.Property(item => item.ToKey).HasMaxLength(40);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.Note).HasMaxLength(300);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.From);
        builder.Ignore(item => item.To);
        builder.Ignore(item => item.IsReversed);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.FromProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ToProjectId).OnDelete(DeleteBehavior.Restrict);

        // The ledger of one item, newest first.
        builder.HasIndex(item => new { item.ItemId, item.RecordedAtUtc });
        // Double-clicking a stock action never records it twice: a repeated request id is refused by the database as well as the service.
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}
