using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Riders.UpsertFromManifest;

/// <summary>
/// Same-module secondary command dispatched by the projection worker when a
/// <c>TripManifestUpdatedDomainEvent</c> appears in the journal (the edit path). Resolves
/// the manifest's trip by trip number — the manifest itself carries no service type — and
/// folds the revised passenger list into the rider directory. A manifest with no linked
/// trip is a success no-op (no trip, no service type). Delivery is at-least-once; the
/// upsert is idempotent. The worker runs it under the journal row's tenant.
/// </summary>
public sealed record UpsertRidersFromManifestCommand(Guid ManifestId) : ICommand;
