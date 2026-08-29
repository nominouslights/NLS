using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Riders.UpsertFromTrip;

/// <summary>
/// Same-module secondary command dispatched by the projection worker when a
/// <c>TripManifestLinkedDomainEvent</c> appears in the journal (the create path: a new
/// manifest was just linked to its trip, so the trip's service type is now known). Loads
/// the trip's manifest and folds its passengers into the rider directory. Every missing
/// link (no trip, no manifest id, no manifest) is a success no-op — delivery is
/// at-least-once and the upsert is idempotent. The worker runs it under the journal row's
/// tenant.
/// </summary>
public sealed record UpsertRidersFromTripCommand(Guid TripId) : ICommand;
