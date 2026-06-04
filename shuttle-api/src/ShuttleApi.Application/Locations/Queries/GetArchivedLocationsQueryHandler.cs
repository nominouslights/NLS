using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class GetArchivedLocationsQueryHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<GetArchivedLocationsQuery, IReadOnlyList<ArchivedLocationResult>>
{
    public async Task<IReadOnlyList<ArchivedLocationResult>> Handle(
        GetArchivedLocationsQuery request,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddYears(-1);
        await locationRepository.PurgeExpiredAsync(cutoff, cancellationToken);

        var archived = await locationRepository.GetAllArchivedAsync(cancellationToken);

        return archived.Select(l => new ArchivedLocationResult(
            l.Id,
            l.Name,
            l.Address,
            l.Latitude,
            l.Longitude,
            l.CreatedAt,
            l.DeletedAt
        )).ToList();
    }
}
