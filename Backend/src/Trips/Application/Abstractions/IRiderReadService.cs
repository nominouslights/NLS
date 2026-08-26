using NorthernLink.Trips.Application.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for rider-directory queries — returns response DTOs from <c>rm_riders</c>.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IRiderReadService
{
    /// <summary>
    /// Lists riders, optionally narrowed to a service type and/or a case-insensitive name
    /// search, ordered ContractCrew → Community → Nihb → Charter, then name ascending.
    /// </summary>
    Task<IReadOnlyList<RiderResponse>> GetRidersAsync(
        TripServiceType? serviceType = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
