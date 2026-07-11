using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Clients.Infrastructure;

/// <summary>
/// DI entry point for the Clients module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "clients"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class ClientsModule
{
    public const string SchemaName = "clients";

    public static IServiceCollection AddClientsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "clients")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
