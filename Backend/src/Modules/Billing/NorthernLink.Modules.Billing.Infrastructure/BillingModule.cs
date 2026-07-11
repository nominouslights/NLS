using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Billing.Infrastructure;

/// <summary>
/// DI entry point for the Billing module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "billing"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class BillingModule
{
    public const string SchemaName = "billing";

    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "billing")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
