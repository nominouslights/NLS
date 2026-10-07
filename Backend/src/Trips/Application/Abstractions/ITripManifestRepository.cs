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

    /// <summary>
    /// Every manifest recorded under <paramref name="tripNumber"/>, oldest first. The
    /// (tenant, trip_number) index is not unique and linking is lazy, so a trip can have more
    /// than one — whatever must hold for "all of a trip's manifests" (converting it to a
    /// deadhead) reads this list plus the one on <c>Trip.ManifestId</c>.
    /// </summary>
    Task<IReadOnlyList<TripManifest>> GetByTripNumberAsync(
        string tripNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes a manifest on the next save. The delete carries the manifest's version token,
    /// so an edit that landed after it was loaded makes the save lose (a concurrency conflict)
    /// rather than silently discarding that edit; the audit pipeline still journals a final
    /// snapshot plus the synthetic aggregate-deleted row that drops its read-model row.
    /// </summary>
    void Remove(TripManifest manifest);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
