using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Clients.Infrastructure;

/// <summary>
/// DI entry point for the Clients domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "clients"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class ClientsServiceCollectionExtensions
{
    public const string SchemaName = "clients";

    public static IServiceCollection AddClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "clients")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
