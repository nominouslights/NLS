using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class UpdatePassengerBoardingStatusCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<UpdatePassengerBoardingStatusCommand>
{
    public async Task Handle(UpdatePassengerBoardingStatusCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.UpdatePassengerBoardingStatus(request.PassengerId, request.BoardingStatus);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
