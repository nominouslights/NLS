using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NorthernLink.Api.HostDefaults;

/// <summary>
/// Readiness check that the database is actually reachable, run through a real module DbContext
/// so it exercises the same provider, connection string and pool the request path uses. One
/// context is enough — every module shares a single database.
///
/// A deliberate hand-rolled check rather than an AspNetCore.HealthChecks.NpgSql package
/// reference: it is a dozen lines, and the platform's package set stays as small as it is.
/// </summary>
public sealed class DatabaseHealthCheck<TDbContext>(TDbContext context) : IHealthCheck
    where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database did not accept a connection.");
        }
        catch (Exception ex)
        {
            // Most often the managed-Postgres firewall (DigitalOcean Trusted Sources) has not been
            // opened to this host's egress address — worth surfacing the message, not just a flag.
            return HealthCheckResult.Unhealthy($"Database unreachable: {ex.Message}", ex);
        }
    }
}
