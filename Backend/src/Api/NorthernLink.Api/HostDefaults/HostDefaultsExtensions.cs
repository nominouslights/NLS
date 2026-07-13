using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NorthernLink.Api.HostDefaults;

/// <summary>
/// Local-dev-orchestration hooks (service discovery registration + liveness/health endpoints)
/// for whatever hosts the API — plain <c>dotnet run</c> or the Aspire AppHost. Lives in the
/// gateway itself (not Shared) because it is purely a host concern. No OpenTelemetry here by
/// design — that's a later, separate effort tied to the self-hosted monitoring stack, not
/// something to half-wire now.
/// </summary>
public static class HostDefaultsExtensions
{
    public static IHostApplicationBuilder AddHostDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddServiceDiscovery();

        builder.Services.AddHealthChecks();

        return builder;
    }

    public static WebApplication MapDefaultHostEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live"),
            });
        }

        return app;
    }
}
