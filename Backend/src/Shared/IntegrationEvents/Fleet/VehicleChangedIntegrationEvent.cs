using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Fleet;

/// <summary>
/// Published whenever a Fleet vehicle is registered or its identity/status fields change —
/// routing key <c>fleet.vehicle-changed</c>. One event covers register, update, and status
/// transitions so consumers can maintain a replica by upserting on <see cref="VehicleId"/>
/// (idempotent under at-least-once delivery). Trips consumes it to keep its
/// <c>vehicle_lookup</c> table current for assignment validation without a library
/// reference. Status travels as a string ("Active", "InMaintenance", "OutOfService",
/// "Retired", "Sold", "Recycled"): integration events are the module's public contract and
/// never reference Fleet's internal enums. <see cref="TenantId"/> is part of the payload
/// because handlers run outside any HTTP request. Mirrors
/// <c>Drivers/DriverChangedIntegrationEvent</c>.
/// </summary>
public sealed record VehicleChangedIntegrationEvent(
    Guid VehicleId,
    Guid TenantId,
    string UnitNumber,
    string Status,
    string RequiredLicenceClass,
    int SeatingCapacity,
    string Source) : IntegrationEvent;
