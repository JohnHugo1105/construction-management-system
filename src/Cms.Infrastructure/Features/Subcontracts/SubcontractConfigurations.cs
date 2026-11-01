using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Subcontracts;
using Cms.Domain.Features.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Subcontracts;

internal sealed class SubcontractConfiguration : IEntityTypeConfiguration<Subcontract>
{
    public void Configure(EntityTypeBuilder<Subcontract> builder)
    {
        builder.ToTable("Subcontracts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Number).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.CancelReason).HasMaxLength(300);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.OutstandingMinor);
        builder.Ignore(item => item.RetentionBalanceMinor);

        builder.HasOne<Supplier>().WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Claims).WithOne().HasForeignKey(claim => claim.SubcontractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Payments).WithOne().HasForeignKey(payment => payment.SubcontractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Variations).WithOne().HasForeignKey(variation => variation.SubcontractId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Claims).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Variations).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The number is unique in the company; the list filters by project, firm and status, newest first; the commitment report reads
        // active subcontracts grouped by project.
        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.SupplierId });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class SubcontractClaimConfiguration : IEntityTypeConfiguration<SubcontractClaim>
{
    public void Configure(EntityTypeBuilder<SubcontractClaim> builder)
    {
        builder.ToTable("SubcontractClaims");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Notes).HasMaxLength(300);
        builder.Property(item => item.VoidReason).HasMaxLength(300);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.IsDraft);
        builder.Ignore(item => item.IsCertified);
        builder.Ignore(item => item.Breakdown);

        builder.HasOne<CostEntry>().WithMany().HasForeignKey(item => item.CostEntryId).OnDelete(DeleteBehavior.Restrict);

        // Claim numbers are unique within a subcontract, and only one draft is open at a time (the domain checks; these are the backstops).
        builder.HasIndex(item => new { item.SubcontractId, item.ClaimNumber }).IsUnique().HasFilter("[ClaimNumber] IS NOT NULL");
        builder.HasIndex(item => item.SubcontractId).IsUnique().HasFilter("[Status] = 'Draft'").HasDatabaseName("IX_SubcontractClaims_OneDraft");
        builder.HasIndex(item => item.TenantId);
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class SubcontractPaymentConfiguration : IEntityTypeConfiguration<SubcontractPayment>
{
    public void Configure(EntityTypeBuilder<SubcontractPayment> builder)
    {
        builder.ToTable("SubcontractPayments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Reference).HasMaxLength(80);
        builder.Property(item => item.Note).HasMaxLength(300);
        builder.Property(item => item.VoidReason).HasMaxLength(300);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.IsRecorded);

        builder.HasOne<SubcontractClaim>().WithMany().HasForeignKey(item => item.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.SubcontractId);
        builder.HasIndex(item => item.ClaimId);
        builder.HasIndex(item => item.TenantId);
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class SubcontractVariationConfiguration : IEntityTypeConfiguration<SubcontractVariation>
{
    public void Configure(EntityTypeBuilder<SubcontractVariation> builder)
    {
        builder.ToTable("SubcontractVariations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();

        builder.HasIndex(item => new { item.SubcontractId, item.Number }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}
