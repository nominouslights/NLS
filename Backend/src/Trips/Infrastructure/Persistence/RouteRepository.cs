using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class RouteRepository(TripsDbContext context) : IRouteRepository
{
    public void Add(Route route) => context.Routes.Add(route);

    public Task<Route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        context.Routes.FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
