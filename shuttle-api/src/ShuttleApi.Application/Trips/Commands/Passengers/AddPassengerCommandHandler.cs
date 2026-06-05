using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class AddPassengerCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AddPassengerCommand, AddPassengerResult>
{
    public async Task<AddPassengerResult> Handle(AddPassengerCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var passenger = trip.AddPassenger(
            request.Name,
            request.ContactInfo,
            request.SeatNumber,
            request.PaymentStatus,
            phone: request.Phone,
            email: request.Email,
            isAddedAfterDeparture: request.IsAddedAfterDeparture);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        return new AddPassengerResult(passenger.Id);
    }
}
