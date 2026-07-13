using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Infrastructure;

/// <summary>
/// DI entry point for the Trips domain library — the only thing the API gateway sees.
/// Handlers and the library's DbContext (Postgres schema "trips") register here as
/// they are built.
/// </summary>
public static class TripsServiceCollectionExtensions
{
    public const string SchemaName = "trips";

    public static IServiceCollection AddTrips(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold otherwise. Registration order inside a library:
        //   1. DbContext (ModuleDbContext base, schema "trips")
        //   2. Command/query handlers
        //   3. Integration event consumers (below)
        services.AddIntegrationEventConsumer(SchemaName, subscriptions => subscriptions
            .On<VehicleStatusChangedIntegrationEvent, VehicleStatusChangedIntegrationEventHandler>());

        return services;
    }
}
