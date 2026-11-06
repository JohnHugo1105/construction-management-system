using ConstructOPX.Application.Common;
using ConstructOPX.Infrastructure.Persistence;
using ConstructOPX.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ConstructOpx";

    /// <summary>
    /// Registers persistence and adapters. Classes in this assembly named <c>*Repository</c>, <c>*Reader</c>, <c>*CommitmentSource</c>, <c>*ForecastSource</c>, <c>*NeedsActionSource</c>, <c>*CapabilityCheck</c> or <c>*FileStore</c> are
    /// registered against the Application ports they implement, so a feature adds its adapters without editing this file.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"The connection string \"{ConnectionStringName}\" is not configured. Set ConnectionStrings__{ConnectionStringName} or CONSTRUCTOPX_DATABASE_CONNECTION.");

        // Interceptors registered in the container (tests add a statement counter) are attached to every context.
        services.AddDbContext<ConstructOpxDbContext>((provider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(provider.GetServices<IInterceptor>()));
        services.Configure<PasswordHashingOptions>(options =>
        {
            if (int.TryParse(configuration[$"{PasswordHashingOptions.SectionName}:Iterations"], out var iterations)) options.Iterations = iterations;
        });
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<INumberSequence, NumberSequenceStore>();
        services.AddScoped<Seeding.DevelopmentSeeder>();

        var applicationAssembly = typeof(IClock).Assembly;
        var adapters = typeof(ConstructOpxDbContext).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && (type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                type.Name.EndsWith("Reader", StringComparison.Ordinal) ||
                type.Name.EndsWith("CommitmentSource", StringComparison.Ordinal) ||
                type.Name.EndsWith("ForecastSource", StringComparison.Ordinal) ||
                type.Name.EndsWith("NeedsActionSource", StringComparison.Ordinal) ||
                type.Name.EndsWith("CapabilityCheck", StringComparison.Ordinal) ||
                type.Name.EndsWith("FileStore", StringComparison.Ordinal)));
        foreach (var adapter in adapters)
            foreach (var port in adapter.GetInterfaces().Where(item => item.Assembly == applicationAssembly))
                services.AddScoped(port, adapter);

        return services;
    }
}
