using Cms.Domain.Features.Audit;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(item => item.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.HoursPerDay).HasPrecision(5, 2);
        builder.Property(item => item.VarianceBasis).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Username).HasMaxLength(64).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Email).HasMaxLength(254);
        builder.Property(item => item.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Roles).HasConversion<int>();
        builder.Ignore(item => item.IsActiveAdministrator);

        // Sign-in has no organization field, so a username is unique across the whole system.
        builder.HasIndex(item => item.Username).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Client).HasMaxLength(120);
        builder.Property(item => item.Location).HasMaxLength(200);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.StatusBeforeArchive).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ArchiveReason).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ArchiveNote).HasMaxLength(500);
        builder.Property(item => item.BudgetBaselineUnlockReason).HasMaxLength(300);
        builder.Ignore(item => item.IsArchived);
        builder.Ignore(item => item.IsBudgetLocked);

        builder.HasMany(item => item.SubProjects).WithOne().HasForeignKey(sub => sub.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.SubProjects).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status });
    }
}

internal sealed class SubProjectConfiguration : IEntityTypeConfiguration<SubProject>
{
    public void Configure(EntityTypeBuilder<SubProject> builder)
    {
        builder.ToTable("SubProjects");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(item => new { item.ProjectId, item.NormalizedName }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.UserName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Action).HasMaxLength(80).IsRequired();
        builder.Property(item => item.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Summary).HasMaxLength(500).IsRequired();
        builder.Property(item => item.CorrelationId).HasMaxLength(80);

        builder.HasIndex(item => new { item.TenantId, item.OccurredAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.EntityType, item.EntityId });
    }
}
