using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.AttachManifest;

/// <summary>
/// Same-module secondary command dispatched by the projection worker when a
/// <c>TripManifestCreatedDomainEvent</c> appears in the journal: matches the manifest to a
/// trip by (tenant, trip number) and links it (sets <c>Trip.ManifestId</c>) — it no longer
/// changes trip status. There is deliberately no hard FK — a manifest for an ad-hoc trip
/// with no Trip row is a success no-op. Delivery is at-least-once; the handler is
/// idempotent. The worker runs it under the journal row's tenant.
/// </summary>
public sealed record AttachManifestToTripCommand(Guid ManifestId) : ICommand;
