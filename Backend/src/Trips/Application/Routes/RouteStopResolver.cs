using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// Turns an ordered list of catalog stop references into ordered <see cref="RouteStop"/>
/// snapshots (name + coordinates + province), loading them from the same tenant/domain and
/// carrying each stop's timetable offsets through untouched — the offsets themselves are
/// validated by the aggregate, which is the only place that can see the whole leg. Fails if any
/// id is missing or inactive. The province is the catalog address's, normalised to its code
/// (a row written before normalisation may still say "Manitoba"); whether it is inside the
/// operating area is the aggregate's check, so a Saskatchewan stop resolves fine here and is
/// refused by <c>Route.Create</c>/<c>Update</c>. Shared by the create and update route handlers.
/// </summary>
internal static class RouteStopResolver
{
    public static async Task<Result<List<RouteStop>>> ResolveAsync(
        IStopRepository stops,
        IReadOnlyList<RouteStopInput> stopInputs,
        CancellationToken cancellationToken)
    {
        var loaded = await stops.GetByIdsAsync([.. stopInputs.Select(input => input.StopId)], cancellationToken);
        var byId = loaded.ToDictionary(stop => stop.Id);

        var snapshots = new List<RouteStop>(stopInputs.Count);
        for (var index = 0; index < stopInputs.Count; index++)
        {
            var input = stopInputs[index];
            if (!byId.TryGetValue(input.StopId, out var stop))
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
                Province = CanadianProvince.Normalize(stop.Address.Province) ?? stop.Address.Province,
                OutboundOffsetMinutes = input.OutboundOffsetMinutes,
                ReturnOffsetMinutes = input.ReturnOffsetMinutes,
            });
        }

        return Result.Success(snapshots);
    }
}
