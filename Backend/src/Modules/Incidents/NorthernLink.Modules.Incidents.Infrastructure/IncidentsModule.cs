using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Incidents.Infrastructure;

/// <summary>
/// DI entry point for the Incidents module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "incidents"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class IncidentsModule
{
    public const string SchemaName = "incidents";

    public static IServiceCollection AddIncidentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "incidents")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
