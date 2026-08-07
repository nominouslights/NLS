using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a client trip's run ends and it becomes billable — routing key
/// <c>trips.trip-ready-for-billing</c>. Replaces <c>TripCompletedIntegrationEvent</c> as
/// Billing's feed: under the billing-driven trip lifecycle, a trip reaching Completed means
/// payment has been confirmed, which is far too late to start drafting a worksheet.
/// <para>
/// <see cref="ClientId"/> is deliberately non-nullable. A run with no client (community,
/// walk-up charter) never enters the billing arc — its fare was taken at booking or by the
/// dispatcher — so the exclusion is a fact of the contract rather than a filter a consumer has
/// to remember to apply.
/// </para>
/// <para>
/// Carries everything Billing needs to record a billable trip without a library reference: the
/// client linkage, route/corridor identification, distance, and the <see cref="RoundTripKey"/>
/// that pairs an outbound leg with its return (null for ad-hoc trips). Billing inserts into
/// <c>billable_trips</c> if-absent on <see cref="TripId"/> (idempotent under at-least-once
/// delivery); draft generation later groups uninvoiced legs by RoundTripKey to price complete
/// pairs at the contract's rate per round trip — a group counts as complete only when it pairs
/// an "Outbound" leg with an "Inbound" one, so <see cref="Direction"/> travels with each leg.
/// <see cref="IsEmptyLeg"/> marks a deadhead (empty repositioning) leg: a pair containing one
/// still prices at the full round-trip rate but its worksheet line is flagged for an optional
/// manual discount.
/// </para>
/// <para>
/// ServiceType and Direction travel as strings and <see cref="TenantId"/> is in the payload
/// because handlers run outside any HTTP request — integration events never reference another
/// module's internal enums.
/// </para>
/// </summary>
public sealed record TripReadyForBillingIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string TripNumber,
    Guid ClientId,
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
    DateTimeOffset OperationsFinishedAtUtc) : IntegrationEvent;
