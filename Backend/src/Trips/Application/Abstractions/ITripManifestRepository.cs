using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the TripManifest aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripManifestRepository
{
    void Add(TripManifest manifest);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
