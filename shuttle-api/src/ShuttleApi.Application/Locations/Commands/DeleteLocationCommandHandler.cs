using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Application.Locations;

internal sealed class DeleteLocationCommandHandler(ISavedLocationRepository locationRepository)
    : IRequestHandler<DeleteLocationCommand>
{
    public async Task Handle(DeleteLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await locationRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"SavedLocation {request.Id} not found.");

        await locationRepository.DeleteAsync(location, cancellationToken);
    }
}
