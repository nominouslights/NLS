using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// Turns an ordered list of catalog stop ids into ordered <see cref="RouteStop"/> snapshots
/// (name + coordinates), loading them from the same tenant/domain. Fails if any id is
/// missing or inactive. Shared by the create and update route handlers.
/// </summary>
internal static class RouteStopResolver
{
    public static async Task<Result<List<RouteStop>>> ResolveAsync(
        IStopRepository stops,
        IReadOnlyList<Guid> stopIds,
        CancellationToken cancellationToken)
    {
        var loaded = await stops.GetByIdsAsync(stopIds, cancellationToken);
        var byId = loaded.ToDictionary(stop => stop.Id);

        var snapshots = new List<RouteStop>(stopIds.Count);
        for (var index = 0; index < stopIds.Count; index++)
        {
            if (!byId.TryGetValue(stopIds[index], out var stop))
            {
                return Result.Failure<List<RouteStop>>(RouteErrors.UnknownStop);
            }

            if (!stop.Active)
            {
                return Result.Failure<List<RouteStop>>(RouteErrors.InactiveStop);
            }

            snapshots.Add(new RouteStop
            {
                StopId = stop.Id,
                Name = stop.Name,
                Order = index,
                Latitude = stop.Coordinate.Latitude,
                Longitude = stop.Coordinate.Longitude,
            });
        }

        return Result.Success(snapshots);
    }
}
