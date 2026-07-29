using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;

namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// Trips' explicit domain-event → integration-event translation. The public contracts
/// today are trip completion (Billing records a billable trip) and post-hoc round-trip
/// pairing changes (Billing re-keys its uninvoiced replica rows). Manifest events stay
/// internal: since inspections detached from the manifest (Phase B moved them into Fleet's
/// own VehicleInspection records), the manifest no longer carries anything another module
/// consumes. Everything unmapped stays internal (null). Extending Trips' public surface
/// means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Trips/ — never auto-publishing.
/// </summary>
public sealed class TripsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            TripCompletedDomainEvent => MapTripCompleted((Trip)aggregate),
            TripRoundTripChangedDomainEvent => MapRoundTripChanged((Trip)aggregate),
            _ => null,
        };

    private static TripCompletedIntegrationEvent MapTripCompleted(Trip trip) => new(
        trip.Id,
        trip.TenantId,
        trip.TripNumber,
        trip.ClientId,
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
        trip.CompletedAtUtc ?? DateTimeOffset.UtcNow);

    private static TripRoundTripChangedIntegrationEvent MapRoundTripChanged(Trip trip) => new(
        trip.Id,
        trip.TenantId,
        trip.RoundTripKey,
        trip.Direction?.ToString());
}
