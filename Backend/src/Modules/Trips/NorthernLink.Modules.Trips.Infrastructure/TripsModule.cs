using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Trips.Infrastructure;

/// <summary>
/// DI entry point for the Trips module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "trips"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class TripsModule
{
    public const string SchemaName = "trips";

    public static IServiceCollection AddTripsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "trips")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
