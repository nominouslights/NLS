using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class RestoreLocationCommandHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<RestoreLocationCommand>
{
    public async Task Handle(RestoreLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await locationRepository.GetDeletedByIdAsync(request.LocationId, cancellationToken)
            ?? throw new NotFoundException($"Archived location {request.LocationId} not found.");

        location.Restore();

        await locationRepository.UpdateAsync(location, cancellationToken);
    }
}
