using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;

namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// Trips' explicit domain-event → integration-event translation. Public contract today:
/// manifest completion (Fleet materializes the pre-/post-trip vehicle inspection
/// records) and trip completion (Billing records a billable trip). Everything else
/// stays internal (null). Extending Trips' public surface means adding a case here plus
/// an event record in NorthernLink.Shared/IntegrationEvents/Trips/ — never auto-publishing.
/// </summary>
public sealed class TripsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            TripManifestCompletedDomainEvent completed => MapCompleted(completed, (TripManifest)aggregate),
            TripCompletedDomainEvent => MapTripCompleted((Trip)aggregate),
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
        trip.PoNumber,
        trip.CompletedAtUtc ?? DateTimeOffset.UtcNow);

    private static TripManifestCompletedIntegrationEvent MapCompleted(
        TripManifestCompletedDomainEvent completed,
        TripManifest manifest) => new(
        completed.ManifestId,
        manifest.TenantId,
        manifest.TripNumber,
        manifest.Unit,
        manifest.DriverName,
        manifest.Source.ToString(),
        manifest.EnteredBy,
        manifest.TripDate,
        manifest.CertifiedAt,
        manifest.OdometerStartKm,
        manifest.OdometerEndKm,
        manifest.PreTripItems.Select(item => new TripManifestPreTripItem(
            item.Group,
            item.Item,
            item.Status.ToString(),
            item.Severity?.ToString(),
            item.Note)).ToList(),
        manifest.PostTripItems.Select(item => new TripManifestPostTripItem(item.Item, item.Ok)).ToList());
}
