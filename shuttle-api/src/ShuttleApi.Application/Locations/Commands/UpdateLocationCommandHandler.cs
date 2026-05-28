using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class UpdateLocationCommandHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<UpdateLocationCommand>
{
    public async Task Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await locationRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"SavedLocation {request.Id} not found.");

        location.Update(request.Name, request.Address, request.Latitude, request.Longitude);

        await locationRepository.UpdateAsync(location, cancellationToken);
    }
}
