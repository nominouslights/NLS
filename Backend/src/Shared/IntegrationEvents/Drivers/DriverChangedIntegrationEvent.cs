using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published whenever a driver record is created or its identity/status fields change —
/// routing key <c>drivers.driver-changed</c>. One event covers register, update, and
/// status transitions so consumers can maintain a replica by upserting on
/// <see cref="DriverId"/> (idempotent under at-least-once delivery). Trips consumes it
/// to keep its <c>driver_lookup</c> table current for assignment validation without a
/// library reference. Status travels as a string ("Active", "Inactive", "Deactivated"):
/// integration events are the module's public contract and never reference Drivers'
/// internal enums. <see cref="TenantId"/> is part of the payload because handlers run
/// outside any HTTP request.
/// </summary>
public sealed record DriverChangedIntegrationEvent(
    Guid DriverId,
    Guid TenantId,
    string Name,
    string LicenceClass,
    string Status,
    string Source) : IntegrationEvent;
