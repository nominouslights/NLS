using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Grocery.Infrastructure;

/// <summary>
/// DI entry point for the Grocery domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "grocery"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class GroceryServiceCollectionExtensions
{
    public const string SchemaName = "grocery";

    public static IServiceCollection AddGrocery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "grocery")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
