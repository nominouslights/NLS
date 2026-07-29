using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Keeps <c>trips.vehicle_lookup</c> current from the Fleet module's change stream, so
/// vehicle assignment can validate existence + Active status without a library reference.
/// Handlers run outside any HTTP request, so the event's tenant is pushed as the ambient
/// tenant for the write (RLS session variable). Delivery is at-least-once: the upsert is
/// keyed on VehicleId, so replays converge on the same row. Mirrors
/// <c>DriverChangedIntegrationEventHandler</c>; supersedes the log-only
/// <c>VehicleStatusChangedIntegrationEventHandler</c>.
/// </summary>
public sealed class VehicleChangedIntegrationEventHandler(
    IVehicleLookupRepository repository,
    ILogger<VehicleChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
{
    public async Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await repository.UpsertAsync(
                new VehicleLookup
                {
                    VehicleId = integrationEvent.VehicleId,
                    TenantId = integrationEvent.TenantId,
                    UnitNumber = integrationEvent.UnitNumber,
                    Status = integrationEvent.Status,
                    RequiredLicenceClass = integrationEvent.RequiredLicenceClass,
                    SeatingCapacity = integrationEvent.SeatingCapacity,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Trips upserted vehicle_lookup for vehicle {VehicleId} of tenant {TenantId} ({EventId}): {Status}",
            integrationEvent.VehicleId, integrationEvent.TenantId, integrationEvent.EventId, integrationEvent.Status);
    }
}
