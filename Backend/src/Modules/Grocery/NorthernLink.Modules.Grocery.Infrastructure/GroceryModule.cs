using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Grocery.Infrastructure;

/// <summary>
/// DI entry point for the Grocery module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "grocery"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class GroceryModule
{
    public const string SchemaName = "grocery";

    public static IServiceCollection AddGroceryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "grocery")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
