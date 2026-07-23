using NorthernLink.Trips.Application.Routes;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for route queries — returns response DTOs from <c>rm_routes</c>.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IRouteReadService
{
    Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(CancellationToken cancellationToken = default);
}
