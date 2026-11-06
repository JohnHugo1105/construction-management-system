using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.ChangeOrders;

internal sealed class ChangeOrderConfiguration : IEntityTypeConfiguration<ChangeOrder>
{
    public void Configure(EntityTypeBuilder<ChangeOrder> builder)
    {
        builder.ToTable("ChangeOrders");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Number).HasMaxLength(20);
        builder.Property(item => item.Title).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1_000);
        builder.Property(item => item.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.VoidedFromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.DecisionReason).HasMaxLength(500);
        builder.Property(item => item.VoidReason).HasMaxLength(500);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        // A draft's lines can be removed one at a time, so a line taken out of the order deletes its row; the order itself is never deleted.
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.ChangeOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        // A budget line added by a change order points back at it.
        builder.HasMany<BudgetLine>().WithOne().HasForeignKey(line => line.ChangeOrderId).OnDelete(DeleteBehavior.Restrict);

        // The project's list, newest first, and the "approved after this one" check before a void.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.DecidedAtUtc });
        // Numbers are unique per project; the sequence hands them out and this is the backstop.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Number }).IsUnique().HasFilter("[Number] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class ChangeOrderLineConfiguration : IEntityTypeConfiguration<ChangeOrderLine>
{
    public void Configure(EntityTypeBuilder<ChangeOrderLine> builder)
    {
        builder.ToTable("ChangeOrderLines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.MainScope).HasMaxLength(120);
        builder.Property(item => item.SubScope).HasMaxLength(160);
        builder.Property(item => item.WbsCode).HasMaxLength(20);
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.PreviousQuantity).HasPrecision(18, 4);
        builder.Property(item => item.Unit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Remarks).HasMaxLength(500);
        builder.Ignore(item => item.Effect);

        builder.HasOne<BudgetLine>().WithMany().HasForeignKey(item => item.BudgetLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.ChangeOrderId, item.LineNumber });
        builder.HasIndex(item => item.TenantId);
    }
}
