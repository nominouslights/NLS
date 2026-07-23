using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Route aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IRouteRepository
{
    void Add(Route route);

    Task<Route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
