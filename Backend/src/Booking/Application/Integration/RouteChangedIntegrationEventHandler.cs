using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Abstractions;

namespace NorthernLink.Booking.Application.Integration;

/// <summary>
/// Keeps <c>booking.corridor_lookup</c> current from the Trips module's change stream, so
/// bookings can validate their corridor and snapshot its name without a library reference.
/// Handlers run outside any HTTP request, so the event's tenant is pushed as the ambient
/// tenant for the write (RLS session variable). Delivery is at-least-once and the first
/// poll replays the routing key's whole history: the upsert is keyed on CorridorId, so
/// replays converge on the same row. Mirrors Trips' <c>VehicleChangedIntegrationEventHandler</c>.
/// </summary>
public sealed class RouteChangedIntegrationEventHandler(
    ICorridorLookupRepository repository,
    ILogger<RouteChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<RouteChangedIntegrationEvent>
{
    public async Task Handle(RouteChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await repository.UpsertAsync(
                new CorridorLookup
                {
                    CorridorId = integrationEvent.RouteId,
                    TenantId = integrationEvent.TenantId,
                    Name = integrationEvent.Name,
                    Origin = integrationEvent.Origin,
                    Destination = integrationEvent.Destination,
                    Active = integrationEvent.Active,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Booking upserted corridor_lookup for route {RouteId} of tenant {TenantId} ({EventId}): {Name}",
            integrationEvent.RouteId, integrationEvent.TenantId, integrationEvent.EventId, integrationEvent.Name);
    }
}
