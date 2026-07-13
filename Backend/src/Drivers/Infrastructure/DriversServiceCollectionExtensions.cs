using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Drivers.Infrastructure;

/// <summary>
/// DI entry point for the Drivers domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "drivers"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class DriversServiceCollectionExtensions
{
    public const string SchemaName = "drivers";

    public static IServiceCollection AddDrivers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "drivers")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
