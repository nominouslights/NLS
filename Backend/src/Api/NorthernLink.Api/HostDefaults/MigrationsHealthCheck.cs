using Microsoft.Extensions.Diagnostics.HealthChecks;
using NorthernLink.Shared.Persistence.Migrations;

namespace NorthernLink.Api.HostDefaults;

/// <summary>
/// Readiness gate for startup migrations. Kestrel starts listening before any
/// <c>IHostedService</c> runs, so hosted-service ordering alone cannot keep requests away from a
/// half-migrated schema — this check is what does it. Unhealthy (not Degraded) while migrations
/// are outstanding or have failed, so an orchestrator holds traffic back rather than merely
/// noting the problem.
/// </summary>
public sealed class MigrationsHealthCheck(MigrationState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (state.Completed)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Schema is current."));
        }

        return Task.FromResult(state.Failure is { } failure
            ? HealthCheckResult.Unhealthy($"Startup migrations failed: {failure}")
            : HealthCheckResult.Unhealthy("Startup migrations have not finished yet."));
    }
}
