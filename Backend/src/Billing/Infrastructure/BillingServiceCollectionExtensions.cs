using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Billing.Infrastructure;

/// <summary>
/// DI entry point for the Billing domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "billing"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class BillingServiceCollectionExtensions
{
    public const string SchemaName = "billing";

    public static IServiceCollection AddBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "billing")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
