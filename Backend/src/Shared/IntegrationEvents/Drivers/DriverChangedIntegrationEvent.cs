using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published whenever a driver record is created or its identity/status fields change —
/// routing key <c>drivers.driver-changed</c>. One event covers register, update, status
/// transitions, and linking/unlinking the driver's Identity user, so consumers can maintain
/// a replica by upserting on <see cref="DriverId"/> (idempotent under at-least-once
/// delivery). Trips consumes it to keep its <c>driver_lookup</c> table current for
/// assignment validation and for resolving a Driver Field App caller to the driver they
/// are, without a library reference. Status travels as a string ("Active", "Inactive",
/// "Deactivated"): integration events are the module's public contract and never reference
/// Drivers' internal enums. <see cref="TenantId"/> is part of the payload because handlers
/// run outside any HTTP request.
/// <para>
/// <see cref="UserId"/> is the Identity user (<c>sub</c>) linked to this driver, or null
/// when nobody can sign in as them. It was appended after the event first shipped, with a
/// default so payloads written before it existed still deserialize (the outbox stores JSON
/// text and replays history on a consumer's first poll) — never make it required.
/// </para>
/// </summary>
public sealed record DriverChangedIntegrationEvent(
    Guid DriverId,
    Guid TenantId,
    string Name,
    string LicenceClass,
    string Status,
    string Source,
    Guid? UserId = null) : IntegrationEvent;
