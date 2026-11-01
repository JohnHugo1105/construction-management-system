using Cms.Domain.Features.Billing;
using Cms.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Billing;

internal sealed class ProjectBillingTermsConfiguration : IEntityTypeConfiguration<ProjectBillingTerms>
{
    public void Configure(EntityTypeBuilder<ProjectBillingTerms> builder)
    {
        builder.ToTable("ProjectBillingTerms");
        builder.HasKey(item => item.Id);
        builder.Ignore(item => item.Snapshot);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // One set of terms per project. Two people saving a project's first terms at the same moment: the second is refused.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId }).IsUnique();
    }
}

internal sealed class ClientInvoiceConfiguration : IEntityTypeConfiguration<ClientInvoice>
{
    public void Configure(EntityTypeBuilder<ClientInvoice> builder)
    {
        builder.ToTable("ClientInvoices");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Number).HasMaxLength(20);
        builder.Property(item => item.Description).HasMaxLength(ClientInvoice.DescriptionMaxLength).IsRequired();
        builder.Property(item => item.VoidReason).HasMaxLength(ClientInvoice.ReasonMaxLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.PaidMinor);
        builder.Ignore(item => item.OutstandingMinor);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Payments).WithOne().HasForeignKey(payment => payment.ClientInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The chain of a project's issued bills: at most one issued bill in each place, so two first bills issued at the same moment
        // can't both stand. (Later places are also guarded by the change each issue makes to the bill before it.)
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ChainIndex }).IsUnique().HasFilter("[Status] = 'Issued'");
        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique().HasFilter("[Number] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
        // A project's bills (its tab, its ledger, its latest issued bill) and the company's bills by date and by due date.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.DueDate });
        builder.HasIndex(item => new { item.TenantId, item.BillingDate });
    }
}

internal sealed class ClientPaymentConfiguration : IEntityTypeConfiguration<ClientPayment>
{
    public void Configure(EntityTypeBuilder<ClientPayment> builder)
    {
        builder.ToTable("ClientPayments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Reference).HasMaxLength(80);
        builder.Property(item => item.Note).HasMaxLength(ClientInvoice.ReasonMaxLength);
        builder.Property(item => item.VoidReason).HasMaxLength(ClientInvoice.ReasonMaxLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasIndex(item => new { item.ClientInvoiceId, item.IsVoided });
        builder.HasIndex(item => item.TenantId);
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}
