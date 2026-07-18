using NorthernLink.Trips.Application.Manifests;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for manifest queries — returns response DTOs directly, skipping the aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripManifestReadService
{
    /// <summary>Lists manifests, optionally narrowed to a trip number and/or vehicle unit.</summary>
    Task<IReadOnlyList<TripManifestResponse>> GetManifestsAsync(
        string? tripNumber = null,
        string? unit = null,
        CancellationToken cancellationToken = default);

    Task<TripManifestResponse?> GetManifestAsync(Guid manifestId, CancellationToken cancellationToken = default);
}
