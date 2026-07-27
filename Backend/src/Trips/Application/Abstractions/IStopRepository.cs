using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Stop aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IStopRepository
{
    void Add(Stop stop);

    Task<Stop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the stops for the given ids within the current tenant (same domain — a direct
    /// call, never an integration event). Order and completeness are the caller's concern:
    /// missing ids simply won't appear in the result.
    /// </summary>
    Task<IReadOnlyList<Stop>> GetByIdsAsync(IReadOnlyCollection<Guid> stopIds, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
