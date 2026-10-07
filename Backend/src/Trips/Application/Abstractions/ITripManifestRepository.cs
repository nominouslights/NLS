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

    /// <summary>
    /// A trip's manifest: the linked one when <paramref name="manifestId"/> is set, otherwise the
    /// earliest manifest recorded under <paramref name="tripNumber"/> — linking is an async
    /// reaction, so a just-recorded manifest can exist before the trip points at it. The same
    /// resolution the Bookeo import uses.
    /// </summary>
    Task<TripManifest?> GetForTripAsync(
        Guid? manifestId,
        string tripNumber,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
