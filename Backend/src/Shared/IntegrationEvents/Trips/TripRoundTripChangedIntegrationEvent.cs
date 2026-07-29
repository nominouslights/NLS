using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a trip's round-trip pairing changes after creation (dispatcher merge,
/// deadhead-return creation, or unpair) — routing key <c>trips.trip-round-trip-changed</c>.
/// Billing applies it to the <c>billable_trips</c> replica: the row for
/// <see cref="TripId"/> gets the new <see cref="RoundTripKey"/>/<see cref="Direction"/>
/// (both null on unpair) IF it exists and is not yet invoiced; invoiced rows are left
/// alone (the worksheet already claimed them). Direction travels as a string
/// ("Inbound"/"Outbound"/null): integration events never reference Trips' internal enums.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any HTTP
/// request.
/// </summary>
public sealed record TripRoundTripChangedIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string? RoundTripKey,
    string? Direction) : IntegrationEvent;
