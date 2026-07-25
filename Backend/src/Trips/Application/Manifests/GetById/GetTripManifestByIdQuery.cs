using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Manifests.GetById;

/// <summary>Fetches one manifest by id (tenant-scoped by the read service).</summary>
public sealed record GetTripManifestByIdQuery(Guid TenantId, Guid ManifestId) : IQuery<TripManifestResponse>;
