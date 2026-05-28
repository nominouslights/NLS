using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class GetLocationsQueryHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<GetLocationsQuery, IReadOnlyList<LocationListItemResult>>
{
    public async Task<IReadOnlyList<LocationListItemResult>> Handle(
        GetLocationsQuery request,
        CancellationToken cancellationToken)
    {
        var locations = await locationRepository.GetAllAsync(cancellationToken);
        return locations
            .Select(l => new LocationListItemResult(
                l.Id,
                l.Name,
                l.Address,
                l.Latitude,
                l.Longitude,
                l.CreatedAt))
            .ToList();
    }
}
