using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Read side — queries <c>trips.rm_routes</c> and maps to the public contract.</summary>
internal sealed class RouteReadService(TripsDbContext context) : IRouteReadService
{
    public async Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await context.RouteReadModels
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return routes.Select(r => new RouteResponse(
            r.Id,
            r.Name,
            r.Stops.Select(stop => new RouteStopResponse(
                stop.StopId,
                stop.Name,
                stop.Order,
                stop.Latitude,
                stop.Longitude,
                stop.OutboundOffsetMinutes,
                stop.ReturnOffsetMinutes)).ToList(),
            r.Origin,
            r.Destination,
            r.DistanceKm,
            r.EstimatedDurationMinutes,
            r.RequiredLicenceClass,
            r.Active,
            r.CreatedAtUtc,
            r.UpdatedAtUtc)).ToList();
    }
}
