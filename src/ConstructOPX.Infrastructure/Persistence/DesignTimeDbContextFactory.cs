using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConstructOPX.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without starting the web host. Generating a migration never connects to the database, so
/// the fallback connection string carries no credentials; set CONSTRUCTOPX_DATABASE_CONNECTION to apply migrations to a real server.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConstructOpxDbContext>
{
    public ConstructOpxDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CONSTRUCTOPX_DATABASE_CONNECTION")
            ?? "Server=localhost;Database=ConstructOpx_DesignTime;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<ConstructOpxDbContext>().UseSqlServer(connection).Options;
        return new ConstructOpxDbContext(options, new DesignTimeUser());
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public string UserName => "design-time";
        public string DisplayName => "design-time";
        public AccessRole Roles => AccessRole.None;
        public string? CorrelationId => null;
    }
}
