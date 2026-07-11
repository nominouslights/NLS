using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Drivers.Infrastructure;

/// <summary>
/// DI entry point for the Drivers module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "drivers"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class DriversModule
{
    public const string SchemaName = "drivers";

    public static IServiceCollection AddDriversModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "drivers")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
