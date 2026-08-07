using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;

namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// Trips' explicit domain-event → integration-event translation. The public contracts today are
/// a trip becoming billable (Billing records a billable trip) and post-hoc round-trip pairing
/// changes (Billing re-keys its uninvoiced replica rows).
/// <para>
/// The billable feed hangs off <see cref="TripReadyForBillingDomainEvent"/>, not completion:
/// under the billing-driven lifecycle a trip reaches Completed only once payment is confirmed,
/// by which point there is nothing left to invoice. <see cref="TripCompletedDomainEvent"/>,
/// <see cref="TripInvoicedDomainEvent"/>, and <see cref="TripWrittenOffDomainEvent"/> are all
/// internal — the first two describe facts that originated in Billing, so echoing them back
/// would be a loop.
/// </para>
/// <para>
/// Manifest events also stay internal: since inspections detached from the manifest (Phase B
/// moved them into Fleet's own VehicleInspection records), the manifest no longer carries
/// anything another module consumes. Everything unmapped stays internal (null). Extending Trips'
/// public surface means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Trips/ — never auto-publishing.
/// </para>
/// </summary>
public sealed class TripsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            TripReadyForBillingDomainEvent => MapReadyForBilling((Trip)aggregate),
            TripClosedWithoutBillingDomainEvent => MapClosedWithoutBilling((Trip)aggregate),
            TripRoundTripChangedDomainEvent => MapRoundTripChanged((Trip)aggregate),
            _ => null,
        };

    /// <summary>
    /// The undo of the billable feed: tells Billing to drop the trip from its uninvoiced pool.
    /// Only the dispatcher's close-without-billing raises this — an invoice-driven write-off
    /// stays internal, since that fact came from Billing to begin with.
    /// </summary>
    private static TripClosedWithoutBillingIntegrationEvent MapClosedWithoutBilling(Trip trip) => new(
        trip.Id,
        trip.TenantId,
        trip.TripNumber,
        trip.WrittenOffReason ?? string.Empty);

    /// <summary>
    /// Defence in depth: the aggregate only raises the ready-for-billing event on the client
    /// branch, but the integration event's ClientId is non-nullable, so a clientless trip
    /// arriving here would throw rather than publish. Returning null keeps that impossible.
    /// </summary>
    private static TripReadyForBillingIntegrationEvent? MapReadyForBilling(Trip trip) =>
        trip.ClientId is not { } clientId
            ? null
            : new TripReadyForBillingIntegrationEvent(
                trip.Id,
                trip.TenantId,
                trip.TripNumber,
                clientId,
                trip.ClientName,
                trip.ServiceType.ToString(),
                trip.RouteName,
                trip.Origin,
                trip.Destination,
                trip.DistanceKm,
                trip.ServiceDate,
                trip.RoundTripKey,
                trip.Direction?.ToString(),
                trip.IsEmptyLeg,
                trip.PoNumber,
                trip.OperationsFinishedAtUtc ?? DateTimeOffset.UtcNow);

    private static TripRoundTripChangedIntegrationEvent MapRoundTripChanged(Trip trip) => new(
        trip.Id,
        trip.TenantId,
        trip.RoundTripKey,
        trip.Direction?.ToString());
}
