using Microsoft.Data.SqlClient;
using Xunit;

namespace Cms.TestSupport;

/// <summary>
/// Marks a test that needs a real SQL Server. Set <c>CMS_TEST_SQLSERVER</c> to a connection string for a server (no database name;
/// each test class makes and drops its own). Without it the test is reported as skipped, never as passed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTesting.IsConfigured)
            Skip = "Needs SQL Server: set CMS_TEST_SQLSERVER (see docs/development/testing.md).";
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (!SqlServerTesting.IsConfigured)
            Skip = "Needs SQL Server: set CMS_TEST_SQLSERVER (see docs/development/testing.md).";
    }
}

public static class SqlServerTesting
{
    public const string EnvironmentVariable = "CMS_TEST_SQLSERVER";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static string ServerConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) ?? throw new InvalidOperationException($"{EnvironmentVariable} is not set.");
}

/// <summary>A uniquely named database that exists for the life of a test class and is dropped afterwards.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string name, string connectionString)
    {
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var name = $"CmsTest_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(SqlServerTesting.ServerConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(SqlServerTesting.ServerConnectionString) { InitialCatalog = name };
        return new TestDatabase(name, builder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(SqlServerTesting.ServerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]";
        await command.ExecuteNonQueryAsync();
    }
}
