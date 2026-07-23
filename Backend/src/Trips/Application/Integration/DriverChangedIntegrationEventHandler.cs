using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Keeps <c>trips.driver_lookup</c> current from the Drivers module's change stream, so
/// driver assignment can validate existence + Active status without a library reference.
/// Handlers run outside any HTTP request, so the event's tenant is pushed as the ambient
/// tenant for the write (RLS session variable). Delivery is at-least-once: the upsert is
/// keyed on DriverId, so replays converge on the same row.
/// </summary>
public sealed class DriverChangedIntegrationEventHandler(
    IDriverLookupRepository repository,
    ILogger<DriverChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<DriverChangedIntegrationEvent>
{
    public async Task Handle(DriverChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await repository.UpsertAsync(
                new DriverLookup
                {
                    DriverId = integrationEvent.DriverId,
                    TenantId = integrationEvent.TenantId,
                    Name = integrationEvent.Name,
                    LicenceClass = integrationEvent.LicenceClass,
                    Status = integrationEvent.Status,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Trips upserted driver_lookup for driver {DriverId} of tenant {TenantId} ({EventId}): {Status}",
            integrationEvent.DriverId, integrationEvent.TenantId, integrationEvent.EventId, integrationEvent.Status);
    }
}
