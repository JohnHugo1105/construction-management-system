using Cms.Domain.Features.Payroll;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Statutory;
using Cms.Domain.Features.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Payroll;

internal sealed class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    public void Configure(EntityTypeBuilder<AttendanceEntry> builder)
    {
        builder.ToTable("AttendanceEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RegularHours).HasPrecision(18, 4);
        builder.Property(item => item.OvertimeHours).HasPrecision(18, 4);
        builder.Property(item => item.Remarks).HasMaxLength(300);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.VoidReason).HasMaxLength(300);
        builder.Ignore(item => item.IsVoided);
        builder.Ignore(item => item.IsLocked);

        builder.HasOne<Worker>().WithMany().HasForeignKey(item => item.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollRun>().WithMany().HasForeignKey(item => item.PayrollRunId).OnDelete(DeleteBehavior.Restrict);

        // One entry per posting and day. A removed entry steps aside, so the hours can be entered again. SQL Server treats two NULL
        // sub-projects as equal here, so "no sub-project" is one posting too.
        builder.HasIndex(item => new { item.TenantId, item.WorkerId, item.ProjectId, item.SubProjectId, item.WorkDate })
            .IsUnique().HasFilter("[Status] <> 'Voided'");
        // A worker's hours across all projects for a day (the 24-hour rule), a project's day sheet, payroll's unpaid attendance.
        builder.HasIndex(item => new { item.TenantId, item.WorkerId, item.WorkDate });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.WorkDate });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.WorkDate });
        builder.HasIndex(item => new { item.TenantId, item.PayrollRunId });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("PayrollRuns");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.HoursPerDay).HasPrecision(5, 2);
        builder.Property(item => item.VoidReason).HasMaxLength(300);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Ignore(item => item.TotalGrossMinor);
        builder.Ignore(item => item.TotalDeductionsMinor);
        builder.Ignore(item => item.TotalNetMinor);

        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.TenantId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> builder)
    {
        builder.ToTable("PayrollLines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        // A full name is a first, a middle and a last name of up to 80 characters each, plus two spaces.
        builder.Property(item => item.WorkerName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.EmployeeNumber).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Designation).HasMaxLength(80).IsRequired();
        builder.Property(item => item.RegularHours).HasPrecision(18, 4);
        builder.Property(item => item.OvertimeHours).HasPrecision(18, 4);
        builder.Ignore(item => item.GrossPayMinor);
        builder.Ignore(item => item.DeductionsMinor);
        builder.Ignore(item => item.NetPayMinor);
        builder.Ignore(item => item.ManualDeductionsMinor);
        builder.Ignore(item => item.StatutoryDeductionsMinor);
        builder.Ignore(item => item.EmployerSharesMinor);
        builder.Ignore(item => item.StatutoryLimited);

        builder.HasOne<Worker>().WithMany().HasForeignKey(item => item.WorkerId).OnDelete(DeleteBehavior.Restrict);
        // A draft's deductions are replaced as a set, so removing one from the line deletes its row (the only delete in this feature).
        builder.HasMany(item => item.Deductions).WithOne().HasForeignKey(deduction => deduction.PayrollLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Allocations).WithOne().HasForeignKey(allocation => allocation.PayrollLineId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Deductions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.PayrollRunId, item.WorkerId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WorkerId });
    }
}

internal sealed class PayrollAllocationConfiguration : IEntityTypeConfiguration<PayrollAllocation>
{
    public void Configure(EntityTypeBuilder<PayrollAllocation> builder)
    {
        builder.ToTable("PayrollAllocations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.RegularHours).HasPrecision(18, 4);
        builder.Property(item => item.OvertimeHours).HasPrecision(18, 4);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PayrollLineId);
        builder.HasIndex(item => new { item.TenantId, item.ProjectId });
    }
}

internal sealed class PayrollDeductionConfiguration : IEntityTypeConfiguration<PayrollDeduction>
{
    public void Configure(EntityTypeBuilder<PayrollDeduction> builder)
    {
        builder.ToTable("PayrollDeductions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Description).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Agency).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.AdjustmentReason).HasMaxLength(300);
        builder.Ignore(item => item.IsStatutory);
        builder.Ignore(item => item.IsLimited);

        // A calculated deduction remembers the table it came from, so a table an approved payroll used is never changed.
        builder.HasOne<StatutorySchedule>().WithMany().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PayrollLineId);
        builder.HasIndex(item => item.TenantId);
        builder.HasIndex(item => new { item.TenantId, item.ScheduleId }).HasFilter("[ScheduleId] IS NOT NULL");
    }
}
