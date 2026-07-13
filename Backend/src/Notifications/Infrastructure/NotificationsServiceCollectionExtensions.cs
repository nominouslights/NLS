using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Notifications.Infrastructure;

/// <summary>
/// DI entry point for the Notifications domain library — the only thing the API gateway sees.
/// Handlers, the library's DbContext (Postgres schema "notifications"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class NotificationsServiceCollectionExtensions
{
    public const string SchemaName = "notifications";

    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "notifications")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
