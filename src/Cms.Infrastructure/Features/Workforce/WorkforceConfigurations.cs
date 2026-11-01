using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Features.Workforce;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("Workers");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EmployeeNumber).HasMaxLength(40).IsRequired();
        builder.Property(item => item.NormalizedEmployeeNumber).HasMaxLength(40).IsRequired();
        builder.Property(item => item.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(item => item.MiddleName).HasMaxLength(80);
        builder.Property(item => item.LastName).HasMaxLength(80).IsRequired();
        builder.Property(item => item.MaritalStatus).HasMaxLength(40);
        builder.Property(item => item.SpouseName).HasMaxLength(120);
        builder.Property(item => item.Religion).HasMaxLength(60);
        builder.Property(item => item.EducationalAttainment).HasMaxLength(80);
        builder.Property(item => item.PresentAddress).HasMaxLength(250);
        builder.Property(item => item.ProvincialAddress).HasMaxLength(250);
        builder.Property(item => item.ContactNumber).HasMaxLength(40);
        builder.Property(item => item.Reference).HasMaxLength(200);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.StatusReason).HasMaxLength(300);
        builder.Ignore(item => item.FullName);
        builder.Ignore(item => item.CurrentRate);
        builder.Ignore(item => item.CurrentAssignment);

        builder.HasMany(item => item.Rates).WithOne().HasForeignKey(rate => rate.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Assignments).WithOne().HasForeignKey(assignment => assignment.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Cases).WithOne().HasForeignKey(workerCase => workerCase.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Rates).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Cases).UsePropertyAccessMode(PropertyAccessMode.Field);

        // An employee number is unique per company. The service checks first to give a friendly message; the index is the backstop.
        builder.HasIndex(item => new { item.TenantId, item.NormalizedEmployeeNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.LastName, item.FirstName });
        builder.HasIndex(item => new { item.TenantId, item.Status });
        // Hiring twice from one form submission is refused by the database as well as by the service.
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class WorkerRateConfiguration : IEntityTypeConfiguration<WorkerRate>
{
    public void Configure(EntityTypeBuilder<WorkerRate> builder)
    {
        builder.ToTable("WorkerRates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Designation).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Unit).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Reason).HasMaxLength(300);

        // History is only added to, and one line starts on one date.
        builder.HasIndex(item => new { item.WorkerId, item.EffectiveFrom }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}

internal sealed class WorkerAssignmentConfiguration : IEntityTypeConfiguration<WorkerAssignment>
{
    public void Configure(EntityTypeBuilder<WorkerAssignment> builder)
    {
        builder.ToTable("WorkerAssignments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Ignore(item => item.IsCurrent);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubProject>().WithMany().HasForeignKey(item => item.SubProjectId).OnDelete(DeleteBehavior.Restrict);

        // "Who is on this project now" and "where is this worker now".
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.EndDate });
        builder.HasIndex(item => new { item.WorkerId, item.StartDate });
        // A worker has at most one current posting, whatever two simultaneous requests do.
        builder.HasIndex(item => item.WorkerId).IsUnique().HasFilter("[EndDate] IS NULL");
    }
}

internal sealed class WorkerCaseConfiguration : IEntityTypeConfiguration<WorkerCase>
{
    public void Configure(EntityTypeBuilder<WorkerCase> builder)
    {
        builder.ToTable("WorkerCases");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Remarks).HasMaxLength(1000);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.WorkerId, item.CaseDate });
        builder.HasIndex(item => item.TenantId);
    }
}
