using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Infrastructure;

/// <summary>
/// Starts the real API against its own throwaway SQL Server database (created from the migrations, or from the model while none exist),
/// with cheap password hashing and no sign-in rate limit so tests run quickly.
/// </summary>
public sealed class ConstructOpxApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private TestDatabase? _database;

    /// <summary>Counts every SQL command the API runs, so a test can assert a read path's statement count doesn't grow with its rows.</summary>
    public SqlStatementCounter Sql { get; } = new();

    public async Task InitializeAsync()
    {
        if (!SqlServerTesting.IsConfigured) return;
        _database = await TestDatabase.CreateAsync();

        // Build the schema before the host serves requests.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting, not ConfigureAppConfiguration: Program.cs reads configuration while registering services, before the latter applies.
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:ConstructOpx"] = _database?.ConnectionString ?? "Server=unused;Database=unused;Integrated Security=true",
            ["PasswordHashing:Iterations"] = "1000",
            ["RateLimiting:SignInPerMinute"] = "100000",
            ["Bootstrap:Enabled"] = "true",
            ["Security:RequireSecureCookies"] = "false",
            ["Database:MigrateOnStartup"] = "false"
        };
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(Sql));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }
}
