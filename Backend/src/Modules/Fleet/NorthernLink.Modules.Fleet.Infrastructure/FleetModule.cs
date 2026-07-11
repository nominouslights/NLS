using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Fleet.Infrastructure;

/// <summary>
/// DI entry point for the Fleet module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "fleet"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class FleetModule
{
    public const string SchemaName = "fleet";

    public static IServiceCollection AddFleetModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "fleet")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
