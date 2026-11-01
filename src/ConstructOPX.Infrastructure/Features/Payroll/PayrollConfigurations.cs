using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Statutory;
using ConstructOPX.Domain.Features.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Payroll;

internal sealed class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    public void Configure(EntityTypeBuilder<AttendanceEntry> builder)
    {
        builder.ToTable("AttendanceEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RegularHours).HasPrecision(18, 4);
        builder.Property(item => item.OvertimeHours).HasPrecision(18, 4);
        builder.Property(item => item.NightHours).HasPrecision(18, 4);
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
        builder.Property(item => item.StatutoryBasis).HasMaxLength(400);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(item => item.TotalGrossMinor);
        builder.Ignore(item => item.TotalDeductionsMinor);
        builder.Ignore(item => item.TotalNetMinor);
        builder.Ignore(item => item.IsThirteenthMonth);

        // A draft being recalculated can lose a worker who is no longer paid in it; EF removes that loaded line and the database removes its rows. Runs
        // themselves are never deleted: the database refuses a raw DELETE of a run, so an approved payroll can't be wiped by a stray script.
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.PayrollRunId).OnDelete(DeleteBehavior.ClientCascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.TenantId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.Status });
        // "Does an approved payroll cover this date?" for the holiday calendar, and a year's approved payrolls for the 13th-month pay.
        builder.HasIndex(item => new { item.TenantId, item.Kind, item.Status, item.PeriodEnd });
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
        builder.Property(item => item.NightHours).HasPrecision(18, 4);
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
        // A draft's project shares and holiday days are replaced when it is recalculated, so they go with the line like its deductions.
        builder.HasMany(item => item.Allocations).WithOne().HasForeignKey(allocation => allocation.PayrollLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.HolidayPays).WithOne().HasForeignKey(holiday => holiday.PayrollLineId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Deductions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.HolidayPays).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.PayrollRunId, item.WorkerId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WorkerId });
        // One standing 13th-month line per worker and year: the year is cleared when the payroll is voided, so it can be paid again.
        builder.HasIndex(item => new { item.TenantId, item.WorkerId, item.ThirteenthMonthYear }).IsUnique()
            .HasFilter("[ThirteenthMonthYear] IS NOT NULL");
    }
}

internal sealed class PayrollHolidayPayConfiguration : IEntityTypeConfiguration<PayrollHolidayPay>
{
    public void Configure(EntityTypeBuilder<PayrollHolidayPay> builder)
    {
        builder.ToTable("PayrollHolidayPays");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.HasOne<Worker>().WithMany().HasForeignKey(item => item.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PayrollLineId);
        // A worker is paid for a regular holiday not worked by one standing payroll only: the claim is cleared when that payroll is voided.
        builder.HasIndex(item => new { item.TenantId, item.WorkerId, item.ClaimDate }).IsUnique().HasFilter("[ClaimDate] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ClaimDate });
    }
}

internal sealed class PayRulesConfiguration : IEntityTypeConfiguration<PayRules>
{
    public void Configure(EntityTypeBuilder<PayRules> builder)
    {
        builder.ToTable("PayRules");
        builder.HasKey(item => item.Id);
        builder.Ignore(item => item.Factors);

        // One set of rules per company. Two people saving the first rules at the same moment: the second is refused.
        builder.HasIndex(item => item.TenantId).IsUnique();
    }
}

internal sealed class CompanyHolidayConfiguration : IEntityTypeConfiguration<CompanyHoliday>
{
    public void Configure(EntityTypeBuilder<CompanyHoliday> builder)
    {
        builder.ToTable("CompanyHolidays");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(CompanyHoliday.MaximumNameLength).IsRequired();
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);

        // One holiday per date.
        builder.HasIndex(item => new { item.TenantId, item.Date }).IsUnique();
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
