using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Incidents.Infrastructure;

/// <summary>
/// DI entry point for the Incidents domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "incidents"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class IncidentsServiceCollectionExtensions
{
    public const string SchemaName = "incidents";

    public static IServiceCollection AddIncidents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "incidents")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
