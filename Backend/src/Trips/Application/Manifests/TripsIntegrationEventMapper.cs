using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;

namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// Trips' explicit domain-event → integration-event translation. Only manifest
/// completion is public contract today — Fleet consumes it to materialize the pre- and
/// post-trip vehicle inspection records; everything else stays internal (null).
/// Extending Trips' public surface means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Trips/ — never auto-publishing.
/// </summary>
public sealed class TripsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            TripManifestCompletedDomainEvent completed => MapCompleted(completed, (TripManifest)aggregate),
            _ => null,
        };

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
