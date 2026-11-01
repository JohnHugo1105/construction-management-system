using Cms.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Persistence;

/// <summary>One row per company and sequence: the last number handed out.</summary>
internal sealed class NumberSequenceRow
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    public long LastValue { get; set; }
}

internal sealed class NumberSequenceRowConfiguration : IEntityTypeConfiguration<NumberSequenceRow>
{
    public void Configure(EntityTypeBuilder<NumberSequenceRow> builder)
    {
        builder.ToTable("NumberSequences");
        builder.HasKey(item => new { item.TenantId, item.Key });
        builder.Property(item => item.Key).HasMaxLength(80).IsRequired();
    }
}

/// <summary>
/// Takes the next number with one atomic statement, so concurrent callers each get a different number without a lock held across a
/// save. The first call for a sequence creates its row; if two first calls race, the loser retries the update.
/// </summary>
public sealed class NumberSequenceStore(CmsDbContext db, ICurrentUser currentUser) : INumberSequence
{
    private const int MaximumKeyLength = 80;

    public async Task<long> NextAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaximumKeyLength) throw new ArgumentException("A number sequence needs a short name.", nameof(key));
        var tenant = currentUser.TenantId;
        if (tenant == Guid.Empty) throw new InvalidOperationException("Numbers are handed out only to a signed-in company.");

        var rows = await db.Database.SqlQuery<long>($"""
            SET NOCOUNT ON;
            DECLARE @next bigint;
            UPDATE NumberSequences SET @next = LastValue = LastValue + 1 WHERE TenantId = {tenant} AND [Key] = {key};
            IF @@ROWCOUNT = 0
            BEGIN
                BEGIN TRY
                    INSERT INTO NumberSequences (TenantId, [Key], LastValue) VALUES ({tenant}, {key}, 1);
                    SET @next = 1;
                END TRY
                BEGIN CATCH
                    UPDATE NumberSequences SET @next = LastValue = LastValue + 1 WHERE TenantId = {tenant} AND [Key] = {key};
                END CATCH
            END
            SELECT @next AS Value;
            """).ToListAsync(cancellationToken);
        return rows.Single();
    }
}
