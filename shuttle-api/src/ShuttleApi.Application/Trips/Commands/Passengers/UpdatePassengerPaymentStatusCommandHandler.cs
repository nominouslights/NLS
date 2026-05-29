using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class UpdatePassengerPaymentStatusCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<UpdatePassengerPaymentStatusCommand>
{
    public async Task Handle(UpdatePassengerPaymentStatusCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.UpdatePassengerPaymentStatus(request.PassengerId, request.PaymentStatus);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
