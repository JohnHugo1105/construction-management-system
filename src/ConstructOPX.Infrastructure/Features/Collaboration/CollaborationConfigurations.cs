using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Collaboration;

internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("WorkTasks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(WorkTask.MaximumTitleLength).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(WorkTask.MaximumDescriptionLength);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.LinkKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.CancelReason).HasMaxLength(WorkTask.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // "Assigned to me" and "Created by me" read by person and state; the queue counts a person's Open tasks by due date.
        builder.HasIndex(item => new { item.TenantId, item.AssigneeUserId, item.Status, item.DueDate });
        builder.HasIndex(item => new { item.TenantId, item.CreatedByUserId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TargetKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Text).HasMaxLength(Comment.MaximumTextLength).IsRequired();
        builder.Property(item => item.VoidReason).HasMaxLength(Comment.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Mentions).WithOne().HasForeignKey(item => item.CommentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Mentions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A record's discussion is read newest first.
        builder.HasIndex(item => new { item.TenantId, item.TargetKind, item.TargetId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class CommentMentionConfiguration : IEntityTypeConfiguration<CommentMention>
{
    public void Configure(EntityTypeBuilder<CommentMention> builder)
    {
        builder.ToTable("CommentMentions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.CommentId, item.UserId }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.TargetKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Message).HasMaxLength(Notification.MaximumMessageLength).IsRequired();
        builder.Ignore(item => item.IsRead);

        // A person's list (newest first) and the bell's unread count.
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.ReadAtUtc, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.CreatedAtUtc });
    }
}
