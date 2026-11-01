using ConstructOPX.Domain.Features.Statutory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Statutory;

internal sealed class StatutoryScheduleConfiguration : IEntityTypeConfiguration<StatutorySchedule>
{
    public void Configure(EntityTypeBuilder<StatutorySchedule> builder)
    {
        builder.ToTable("StatutorySchedules");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Agency).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.PeriodBasis).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.IsActive);

        builder.HasMany(item => item.Rows).WithOne().HasForeignKey(row => row.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Rows).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One active table per agency and date (the service checks first to give a friendly message; this is the backstop). A retired
        // table steps aside, so a corrected table can take its date.
        builder.HasIndex(item => new { item.TenantId, item.Agency, item.EffectiveFrom }).IsUnique().HasFilter("[Status] = 'Active'");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.Agency, item.EffectiveFrom });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class StatutoryScheduleRowConfiguration : IEntityTypeConfiguration<StatutoryScheduleRow>
{
    public void Configure(EntityTypeBuilder<StatutoryScheduleRow> builder)
    {
        builder.ToTable("StatutoryScheduleRows");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.ScheduleId, item.RowNumber }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}
