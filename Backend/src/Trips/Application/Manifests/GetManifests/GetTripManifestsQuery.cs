using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Manifests.GetManifests;

/// <summary>
/// Lists manifests visible to the current tenant, newest trip first, optionally
/// narrowed to a trip number and/or vehicle unit.
/// </summary>
public sealed record GetTripManifestsQuery(
    Guid TenantId,
    string? TripNumber = null,
    string? Unit = null) : IQuery<IReadOnlyList<TripManifestResponse>>;
