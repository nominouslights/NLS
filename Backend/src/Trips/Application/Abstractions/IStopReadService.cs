using NorthernLink.Trips.Application.Stops;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for stop queries — returns response DTOs from <c>rm_stops</c>.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IStopReadService
{
    Task<IReadOnlyList<StopResponse>> GetStopsAsync(CancellationToken cancellationToken = default);
}
