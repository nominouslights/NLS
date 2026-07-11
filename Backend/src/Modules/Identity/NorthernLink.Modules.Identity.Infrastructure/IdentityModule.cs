using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Identity.Infrastructure;

/// <summary>
/// DI entry point for the Identity module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "identity"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class IdentityModule
{
    public const string SchemaName = "identity";

    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "identity")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
