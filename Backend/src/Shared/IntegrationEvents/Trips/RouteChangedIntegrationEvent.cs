using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published whenever a route (service corridor) is created or edited — routing key
/// <c>trips.route-changed</c>. One event covers create and update so consumers maintain
/// a replica by upserting on <see cref="RouteId"/> (idempotent under at-least-once
/// delivery). Booking consumes it to keep its <c>corridor_lookup</c> table current —
/// a booking corridor IS a Trips route, referenced by id with name snapshots, never a
/// library reference. Origin/Destination are the outbound leg's first and last stop
/// names at publish time. <see cref="TenantId"/> is part of the payload because
/// handlers run outside any HTTP request.
/// <para>
/// Backfill note: routes that existed before this event was introduced never published
/// it, so a fresh consumer replica starts empty until each route is re-saved once.
/// </para>
/// </summary>
public sealed record RouteChangedIntegrationEvent(
    Guid RouteId,
    Guid TenantId,
    string Name,
    string Origin,
    string Destination,
    bool Active) : IntegrationEvent;
