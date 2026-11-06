using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructOPX.Infrastructure.Features.Documents;

internal sealed class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.ToTable("ProjectDocuments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(ProjectDocument.MaximumTitleLength).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(ProjectDocument.MaximumDescriptionLength);
        builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.VoidReason).HasMaxLength(ProjectDocument.MaximumReasonLength);
        builder.Property(item => item.ClientRequestId).HasMaxLength(80);

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Versions).WithOne().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A project's list filters by project and state; the needs-action group reads Active documents by valid-until date.
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.Category });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ValidUntil });
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(item => item.FileName).HasMaxLength(DocumentFiles.MaximumFileNameLength);
        builder.Property(item => item.ContentType).HasMaxLength(100);
        builder.Property(item => item.Sha256).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.Property(item => item.StoreKey).HasMaxLength(ProjectDocument.MaximumStoreKeyLength).IsUnicode(false);
        builder.Property(item => item.Url).HasMaxLength(DocumentLinks.MaximumLength);
        builder.Property(item => item.Note).HasMaxLength(ProjectDocument.MaximumNoteLength);

        // One row per number per document: two people adding a version at once can't both make version 3.
        builder.HasIndex(item => new { item.DocumentId, item.Number }).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}
