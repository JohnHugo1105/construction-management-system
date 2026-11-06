using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Bonds;

internal sealed class BondConfiguration : IEntityTypeConfiguration<Bond>
{
    public void Configure(EntityTypeBuilder<Bond> builder)
    {
        builder.ToTable("Bonds");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Direction).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.IssuerName).HasMaxLength(Bond.MaximumNameLength).IsRequired();
        builder.Property(item => item.BondNumber).HasMaxLength(Bond.MaximumNumberLength).IsRequired();
        builder.Property(item => item.OtherPartyName).HasMaxLength(Bond.MaximumNameLength);
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Note).HasMaxLength(Bond.MaximumNoteLength);
        builder.Property(item => item.ReleaseNote).HasMaxLength(Bond.MaximumNoteLength);
        builder.Property(item => item.CallNote).HasMaxLength(Bond.MaximumNoteLength);
        builder.Property(item => item.VoidReason).HasMaxLength(Bond.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Events).WithOne().HasForeignKey(item => item.BondId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The company register filters by state and reads Active bonds by valid-until; a project's list filters by project and state.
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ValidUntil });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class BondEventConfiguration : IEntityTypeConfiguration<BondEvent>
{
    public void Configure(EntityTypeBuilder<BondEvent> builder)
    {
        builder.ToTable("BondEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Note).HasMaxLength(Bond.MaximumNoteLength);

        builder.HasIndex(item => new { item.BondId, item.AtUtc });
        builder.HasIndex(item => item.TenantId);
    }
}
