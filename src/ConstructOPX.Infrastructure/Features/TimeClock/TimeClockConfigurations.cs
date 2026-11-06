using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.TimeClock;

internal sealed class TimeClockDeviceConfiguration : IEntityTypeConfiguration<TimeClockDevice>
{
    public void Configure(EntityTypeBuilder<TimeClockDevice> builder)
    {
        builder.ToTable("TimeClockDevices");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(TimeClockDevice.MaximumNameLength).IsRequired();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        // SHA-256 of the device key; the key itself is never stored.
        builder.Property(item => item.KeyHash).HasMaxLength(TimeClockDevice.KeyHashLength).IsFixedLength().IsRequired();
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.IsActive);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // A machine's request is matched to its device by the key's hash before any company is known, so the hash is unique across all companies.
        builder.HasIndex(item => item.KeyHash).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.Name });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class TimePunchConfiguration : IEntityTypeConfiguration<TimePunch>
{
    public void Configure(EntityTypeBuilder<TimePunch> builder)
    {
        builder.ToTable("TimePunches");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(item => item.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientPunchId).HasMaxLength(TimeClockRules.MaximumClientPunchIdLength);
        builder.Property(item => item.Reason).HasMaxLength(TimePunch.MaximumReasonLength);
        builder.Property(item => item.VoidReason).HasMaxLength(TimePunch.MaximumReasonLength);
        builder.Ignore(item => item.IsVoided);

        builder.HasOne<Worker>().WithMany().HasForeignKey(item => item.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TimeClockDevice>().WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Restrict);

        // The machine's scan id is its idempotency key: unique per machine (a supervisor's request id is unique among scans with no machine).
        builder.HasIndex(item => new { item.TenantId, item.DeviceId, item.ClientPunchId }).IsUnique().HasFilter("[ClientPunchId] IS NOT NULL");
        // A project's day on the review page, and what still waits on a project.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.WorkDate, item.Status });
        // A worker's scans around a day: duplicates and the automatic direction.
        builder.HasIndex(item => new { item.TenantId, item.WorkerId, item.WorkDate });
    }
}
