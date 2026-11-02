using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Schedule;

internal sealed class ScheduleActivityConfiguration : IEntityTypeConfiguration<ScheduleActivity>
{
    public void Configure(EntityTypeBuilder<ScheduleActivity> builder)
    {
        builder.ToTable("ScheduleActivities");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(ScheduleActivity.MaximumNameLength).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.RemovedReason).HasMaxLength(ScheduleActivity.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        // The links are the activity's own list: changing the lines replaces rows, so they go with the activity (which is never deleted itself:
        // a removed activity keeps its row and reason and simply links nothing).
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(item => item.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A project's schedule is read by project and state; "behind" looks for active activities by planned finish.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.PlannedFinish });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class ScheduleActivityLineConfiguration : IEntityTypeConfiguration<ScheduleActivityLine>
{
    public void Configure(EntityTypeBuilder<ScheduleActivityLine> builder)
    {
        builder.ToTable("ScheduleActivityLines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<BudgetLine>().WithMany().HasForeignKey(item => item.BudgetLineId).OnDelete(DeleteBehavior.Restrict);

        // A budget line belongs to at most one activity: the database holds it even when two people link the same line at the same moment.
        builder.HasIndex(item => new { item.TenantId, item.BudgetLineId }).IsUnique();
        builder.HasIndex(item => item.ActivityId);
    }
}
