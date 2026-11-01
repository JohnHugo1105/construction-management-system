using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Costing;

internal sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("BudgetLines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MainScope).HasMaxLength(120);
        builder.Property(item => item.SubScope).HasMaxLength(160);
        builder.Property(item => item.WbsCode).HasMaxLength(20);
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.Unit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Remarks).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.RemovedReason).HasMaxLength(300);
        builder.Ignore(item => item.IsRemoved);

        // Money never points at a project that isn't there: the database refuses it, whatever the application does.
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.ProjectId, item.Division, item.WbsCode });
        // Double-clicking Save never creates two lines: a repeated request id is refused by the database as well as by the service.
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class CostEntryConfiguration : IEntityTypeConfiguration<CostEntry>
{
    public void Configure(EntityTypeBuilder<CostEntry> builder)
    {
        builder.ToTable("CostEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MainScope).HasMaxLength(120);
        builder.Property(item => item.SubScope).HasMaxLength(160);
        builder.Property(item => item.WbsCode).HasMaxLength(20);
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.Unit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Remarks).HasMaxLength(500);
        builder.Property(item => item.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.SourceDealer).HasMaxLength(120).IsRequired();
        builder.Property(item => item.NormalizedSourceDealer).HasMaxLength(120).IsRequired();
        builder.Property(item => item.InvoiceNumber).HasMaxLength(60);
        builder.Property(item => item.NormalizedInvoiceNumber).HasMaxLength(60);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.VoidReason).HasMaxLength(300);
        builder.Ignore(item => item.IsVoided);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.ProjectId, item.Division, item.WbsCode });
        builder.HasIndex(item => new { item.TenantId, item.Origin, item.OriginReferenceId });
        // The cost list reads one project newest first and filters by date range.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.EntryDate });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
        // A supplier invoice is recorded once per project. The retired desktop app let the same invoice in again and again.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.NormalizedSourceDealer, item.NormalizedInvoiceNumber })
            .IsUnique().HasFilter("[Status] = 'Posted' AND [Origin] = 'Manual'");
    }
}
