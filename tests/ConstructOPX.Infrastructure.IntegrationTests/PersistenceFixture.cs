using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.IntegrationTests;

/// <summary>A real SQL Server database for one test class, created from the migrations (or the model until one exists).</summary>
public sealed class PersistenceFixture : IAsyncLifetime
{
    private TestDatabase? _database;

    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public async Task InitializeAsync()
    {
        if (!SqlServerTesting.IsConfigured) return;
        _database = await TestDatabase.CreateAsync();
        await using var db = Create(Guid.Empty);
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }

    /// <summary>A context as a signed-in member of <paramref name="tenantId"/> (or nobody, with <see cref="Guid.Empty"/>).</summary>
    public ConstructOpxDbContext Create(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ConstructOpxDbContext>().UseSqlServer(_database!.ConnectionString).Options;
        return new ConstructOpxDbContext(options, new FakeUser(tenantId));
    }

    private sealed class FakeUser(Guid tenantId) : ICurrentUser
    {
        public bool IsAuthenticated => tenantId != Guid.Empty;
        public Guid UserId => tenantId == Guid.Empty ? Guid.Empty : Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string UserName => "test";
        public string DisplayName => "Test";
        public AccessRole Roles => AccessRole.Administrator;
        public string? CorrelationId => null;
    }
}
