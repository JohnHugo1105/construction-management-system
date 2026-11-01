using Cms.Application.Common;
using Cms.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cms.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without starting the web host. Generating a migration never connects to the database, so
/// the fallback connection string carries no credentials; set CMS_DATABASE_CONNECTION to apply migrations to a real server.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CMS_DATABASE_CONNECTION")
            ?? "Server=localhost;Database=Cms_DesignTime;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<CmsDbContext>().UseSqlServer(connection).Options;
        return new CmsDbContext(options, new DesignTimeUser());
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
