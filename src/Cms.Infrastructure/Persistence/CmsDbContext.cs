using System.Linq.Expressions;
using System.Reflection;
using Cms.Application.Common;
using Cms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cms.Infrastructure.Persistence;

/// <summary>
/// The one database context. Three rules are enforced here so no feature can forget them:
/// <list type="number">
/// <item>Every entity with a <c>TenantId</c> is filtered to the signed-in organization on every query (nobody is signed in: nothing is returned).</item>
/// <item>Every aggregate's <c>Version</c> is bumped on update; the old value is the concurrency token.</item>
/// <item>A new record stamped with another organization's id is refused.</item>
/// </list>
/// Mappings live in <c>IEntityTypeConfiguration</c> classes anywhere in this assembly and are picked up automatically, so a feature
/// adds its own without editing this file.
/// </summary>
public sealed class CmsDbContext(DbContextOptions<CmsDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    private static readonly MethodInfo EfPropertyGuid = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(Guid));

    /// <summary>Read by the query filters on every query. <see cref="Guid.Empty"/> when nobody is signed in, which matches no rows.</summary>
    public Guid CurrentTenantId => currentUser.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => !type.IsOwned()).ToArray())
        {
            if (typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AggregateRoot.Version)).IsConcurrencyToken();
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AggregateRoot.Id)).ValueGeneratedNever();
            }

            if (entityType.FindProperty("TenantId") is { ClrType: var type } && type == typeof(Guid) && entityType.BaseType is null)
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(TenantFilter(entityType));
        }
    }

    private LambdaExpression TenantFilter(IMutableEntityType entityType)
    {
        var entity = Expression.Parameter(entityType.ClrType, "entity");
        var tenantOfEntity = Expression.Call(EfPropertyGuid, entity, Expression.Constant("TenantId"));
        var tenantOfContext = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(tenantOfEntity, tenantOfContext), entity);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void PrepareChanges()
    {
        var tenant = CurrentTenantId;
        foreach (var entry in ChangeTracker.Entries().ToArray())
        {
            if (entry.State == EntityState.Modified && entry.Entity is AggregateRoot)
            {
                var version = entry.Property(nameof(AggregateRoot.Version));
                version.CurrentValue = (int)version.OriginalValue! + 1;
            }

            // Defence in depth: the domain stamps the tenant, and this refuses a mismatch. First-time setup and sign-in run before anyone
            // is signed in (tenant is empty) and create or touch only their own records.
            if (entry.State == EntityState.Added && tenant != Guid.Empty && entry.Metadata.FindProperty("TenantId") is not null &&
                entry.Property("TenantId").CurrentValue is Guid owner && owner != tenant)
                throw new InvalidOperationException("A record for another organization can't be saved in this session.");
        }
    }
}
