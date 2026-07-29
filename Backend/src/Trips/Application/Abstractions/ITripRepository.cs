using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Trip aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripRepository
{
    void Add(Trip trip);

    Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Looks a trip up by its human trip number — unique per tenant.</summary>
    Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default);

    /// <summary>All legs sharing a round-trip key (normally two) — unpair clears every one.</summary>
    Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(string roundTripKey, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
