using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.SiteDiary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.SiteDiary;

internal sealed class SiteDiaryEntryConfiguration : IEntityTypeConfiguration<SiteDiaryEntry>
{
    public void Configure(EntityTypeBuilder<SiteDiaryEntry> builder)
    {
        builder.ToTable("SiteDiaryEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Weather).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.WeatherNote).HasMaxLength(SiteDiaryEntry.MaximumWeatherNoteLength);
        builder.Property(item => item.WorkDone).HasMaxLength(SiteDiaryEntry.MaximumWorkDoneLength).IsRequired();
        builder.Property(item => item.IssuesAndDelays).HasMaxLength(SiteDiaryEntry.MaximumIssuesLength);
        builder.Property(item => item.VisitorsAndInspections).HasMaxLength(SiteDiaryEntry.MaximumVisitorsLength);
        builder.Property(item => item.SafetyNotes).HasMaxLength(SiteDiaryEntry.MaximumSafetyLength);
        builder.Property(item => item.VoidReason).HasMaxLength(SiteDiaryEntry.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // One live report per project per day: a voided report frees its date. This one index also serves the project's list, newest first.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ReportDate }).IsUnique().HasFilter("[Status] <> 'Void'")
            .HasDatabaseName("IX_SiteDiaryEntries_OneLivePerProjectPerDay");
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}
