using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Rider aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IRiderRepository
{
    void Add(Rider rider);

    Task<Rider?> GetByIdAsync(Guid riderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks a rider up by the directory's natural key within the current tenant:
    /// service type + normalized name (see <see cref="Rider.NormalizeName"/>).
    /// </summary>
    Task<Rider?> GetByKeyAsync(
        TripServiceType serviceType,
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
