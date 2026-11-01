using System.Reflection;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Endpoints;

/// <summary>
/// Liveness (the process is up), readiness (the database answers and no migration is pending) and version (which build is running).
/// Readiness is what a load balancer or deploy script waits for; a deploy is healthy only when it reports the commit that was deployed.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health").AllowAnonymous().WithTags("Health");

        group.MapGet("/live", () => Results.Ok(new { status = "live" }));

        group.MapGet("/ready", async (CmsDbContext db, CancellationToken ct) =>
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct)) return NotReady("unreachable", "unknown");
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
                return pending == 0
                    ? Results.Ok(new { status = "ready", database = "ok", migrations = "current" })
                    : NotReady("ok", $"{pending} pending");
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return NotReady("unreachable", "unknown");
            }
        });

        group.MapGet("/version", (IHostEnvironment environment) =>
        {
            var assembly = typeof(HealthEndpoints).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var commit = Environment.GetEnvironmentVariable("CMS_RELEASE_SHA") ?? "local";
            return Results.Ok(new
            {
                service = "cms-api",
                version = informational.Split('+')[0],
                commit,
                environment = environment.EnvironmentName,
                runtime = Environment.Version.ToString()
            });
        });
    }

    private static IResult NotReady(string database, string migrations) =>
        Results.Json(new { status = "not-ready", database, migrations }, statusCode: StatusCodes.Status503ServiceUnavailable);
}
