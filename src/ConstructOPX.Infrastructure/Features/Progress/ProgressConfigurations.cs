using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Progress;

internal sealed class ProgressEntryConfiguration : IEntityTypeConfiguration<ProgressEntry>
{
    public void Configure(EntityTypeBuilder<ProgressEntry> builder)
    {
        builder.ToTable("ProgressEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Note).HasMaxLength(ProgressEntry.MaximumNoteLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        // Progress never points at a line or project that is not there: the database refuses it, whatever the application does.
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BudgetLine>().WithMany().HasForeignKey(item => item.BudgetLineId).OnDelete(DeleteBehavior.Restrict);

        // "The latest entry of this line on or before a date" (the current percent) and a line's history, newest first.
        builder.HasIndex(item => new { item.TenantId, item.BudgetLineId, item.AsOfDate, item.RecordedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId });
        // A batch shares one request id, so the id is unique per line: pressing Save twice never records a line twice.
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId, item.BudgetLineId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}
