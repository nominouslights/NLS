using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a trip reaches Completed — routing key <c>trips.trip-completed</c>.
/// Carries everything Billing needs to record a billable trip without a library
/// reference: the client linkage, route/corridor identification, distance, and the
/// <see cref="RoundTripKey"/> that pairs a template-generated outbound leg with its
/// return leg (null for ad-hoc trips). Billing inserts into <c>billable_trips</c>
/// if-absent on <see cref="TripId"/> (idempotent under at-least-once delivery);
/// draft invoice generation later groups uninvoiced legs by RoundTripKey to price
/// complete pairs at the contract's rate per round trip — a group counts as a
/// complete round trip only when it pairs an "Outbound" leg with an "Inbound" one, so
/// <see cref="Direction"/> ("Inbound"/"Outbound"/null) travels with each leg.
/// <see cref="IsEmptyLeg"/> marks a deadhead (empty repositioning) leg — a pair
/// containing one still prices at the full round-trip rate but its worksheet line is
/// flagged for an optional manual discount. Older outbox payloads lack the field and
/// deserialize to false, which is correct for every pre-deadhead trip.
/// ServiceType and Direction travel as strings: integration events never reference
/// Trips' internal enums.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any
/// HTTP request.
/// </summary>
public sealed record TripCompletedIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string TripNumber,
    Guid? ClientId,
    string? ClientName,
    string ServiceType,
    string RouteName,
    string Origin,
    string Destination,
    int DistanceKm,
    DateOnly ServiceDate,
    string? RoundTripKey,
    string? Direction,
    bool IsEmptyLeg,
    string? PoNumber,
    DateTimeOffset CompletedAtUtc) : IntegrationEvent;
