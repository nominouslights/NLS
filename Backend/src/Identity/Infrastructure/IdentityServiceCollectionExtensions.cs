using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Identity.Infrastructure;

/// <summary>
/// DI entry point for the Identity domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "identity"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public const string SchemaName = "identity";

    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "identity")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
