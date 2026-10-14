using Microsoft.Extensions.Logging;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Application.Integration;

/// <summary>
/// Keeps <c>compliance.vehicle_lookup</c> current from Fleet's change stream — the vehicles whose
/// regulated status, insurance and registration this module tracks (HTA s. 1(1) "bus"; NL-SP-02
/// s. 12.1). Subscribed as a <b>fan-out</b> consumer: Trips already consumes the same routing key,
/// and the status-tracked path would split the rows between the two modules. Delivery is
/// at-least-once and replays history, so the upsert is keyed on VehicleId and converges.
/// </summary>
public sealed class VehicleChangedIntegrationEventHandler(
    IVehicleLookupRepository vehicles,
    ILogger<VehicleChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
{
    public async Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await vehicles.UpsertAsync(
                new VehicleLookup
                {
                    VehicleId = integrationEvent.VehicleId,
                    TenantId = integrationEvent.TenantId,
                    UnitNumber = integrationEvent.UnitNumber,
                    Status = integrationEvent.Status,
                    SeatingCapacity = integrationEvent.SeatingCapacity,
                    RequiredLicenceClass = integrationEvent.RequiredLicenceClass,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        // TODO(Epic 2 task 5): refresh carrier readiness for the tenant once CarrierReadiness exists.
        logger.LogInformation(
            "Compliance upserted vehicle_lookup for vehicle {VehicleId} of tenant {TenantId} ({EventId})",
            integrationEvent.VehicleId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}

/// <summary>
/// A reaction to a driver's replica row changing — the seam Epic 2 task 9 fills (an Active
/// driver with a signed plan gets an acknowledgement task; a Deactivated one has theirs
/// cancelled). <paramref name="before"/> is null for a driver seen for the first time. No
/// implementation is registered yet, so the handler runs none.
/// </summary>
public interface IDriverReplicaObserver
{
    Task OnDriverChangedAsync(DriverLookup? before, DriverLookup after, CancellationToken cancellationToken);
}

/// <summary>
/// Keeps <c>compliance.driver_lookup</c> current from Drivers' change stream (fan-out — Trips also
/// consumes it), then lets every <see cref="IDriverReplicaObserver"/> react to the change.
/// </summary>
public sealed class DriverChangedIntegrationEventHandler(
    IDriverLookupRepository drivers,
    IEnumerable<IDriverReplicaObserver> observers,
    ILogger<DriverChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<DriverChangedIntegrationEvent>
{
    public async Task Handle(DriverChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var after = new DriverLookup
            {
                DriverId = integrationEvent.DriverId,
                TenantId = integrationEvent.TenantId,
                Name = integrationEvent.Name,
                Status = integrationEvent.Status,
                UserId = integrationEvent.UserId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };

            var before = await drivers.UpsertAsync(after, cancellationToken);
            foreach (var observer in observers)
            {
                await observer.OnDriverChangedAsync(before, after, cancellationToken);
            }
        }

        logger.LogInformation(
            "Compliance upserted driver_lookup for driver {DriverId} of tenant {TenantId} ({EventId})",
            integrationEvent.DriverId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}

/// <summary>
/// Keeps <c>compliance.user_lookup</c> current from Identity's change stream (fan-out — Budgeting
/// also consumes it), so signatures and designations resolve a user id to a name.
/// </summary>
public sealed class UserChangedIntegrationEventHandler(
    IUserLookupRepository users,
    ILogger<UserChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<UserChangedIntegrationEvent>
{
    public async Task Handle(UserChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await users.UpsertAsync(
                new UserLookup
                {
                    UserId = integrationEvent.UserId,
                    TenantId = integrationEvent.TenantId,
                    Email = integrationEvent.Email,
                    FullName = integrationEvent.FullName,
                    Role = integrationEvent.Role,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Compliance upserted user_lookup for user {UserId} of tenant {TenantId} ({EventId})",
            integrationEvent.UserId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
