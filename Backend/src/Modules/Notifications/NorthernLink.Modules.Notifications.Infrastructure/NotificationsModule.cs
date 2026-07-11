using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.Notifications.Infrastructure;

/// <summary>
/// DI entry point for the Notifications module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "notifications"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class NotificationsModule
{
    public const string SchemaName = "notifications";

    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "notifications")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
