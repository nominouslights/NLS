using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class CreateLocationCommandHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<CreateLocationCommand, CreateLocationResult>
{
    public async Task<CreateLocationResult> Handle(
        CreateLocationCommand request,
        CancellationToken cancellationToken)
    {
        var location = SavedLocation.Create(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude);

        await locationRepository.AddAsync(location, cancellationToken);

        return new CreateLocationResult(location.Id);
    }
}
