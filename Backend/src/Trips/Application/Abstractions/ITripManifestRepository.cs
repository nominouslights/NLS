using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the TripManifest aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripManifestRepository
{
    void Add(TripManifest manifest);

    Task<TripManifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
