using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NorthernLink.Identity.Infrastructure.Persistence;

namespace NorthernLink.Api.HostDefaults;

/// <summary>
/// Host hooks for whatever runs the API — plain <c>dotnet run</c>, the Aspire AppHost, or a
/// container under an orchestrator: service discovery registration plus the liveness/readiness
/// endpoints. Lives in the gateway itself (not Shared) because it is purely a host concern. No
/// OpenTelemetry here by design — that's a later, separate effort tied to the self-hosted
/// monitoring stack, not something to half-wire now.
/// </summary>
public static class HostDefaultsExtensions
{
    public static IHostApplicationBuilder AddHostDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddServiceDiscovery();

        // "ready" gates traffic; nothing is tagged "live", so /alive stays a pure process check.
        // A liveness probe that fails on a database outage would restart every pod in a loop
        // instead of just draining them out of the load balancer.
        builder.Services
            .AddHealthChecks()
            .AddCheck<MigrationsHealthCheck>("migrations", tags: ["ready"])
            .AddCheck<DatabaseHealthCheck<IdentityDbContext>>("database", tags: ["ready"]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/health</c> (readiness) and <c>/alive</c> (liveness) in EVERY environment. These
    /// were once development-only, which would have made both a 404 in a deployed container —
    /// exactly where an orchestrator's probes need them. Neither is exposed through the ingress;
    /// probes reach the pod directly, and the API is never routed to from outside the cluster.
    /// </summary>
    public static WebApplication MapDefaultHostEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });

        return app;
    }
}
