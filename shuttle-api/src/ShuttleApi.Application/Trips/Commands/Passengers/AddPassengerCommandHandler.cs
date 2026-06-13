using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Passengers;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class AddPassengerCommandHandler(
    ITripRepository tripRepository,
    IPassengerProfileRepository passengerProfileRepository)
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

        // Build/refresh passenger profile for charter trips only.
        // Community passengers book through a separate self-service flow.
        if (trip.ServiceType == TripServiceType.Charter && trip.ClientId.HasValue)
        {
            var normalized = request.Name.Trim().ToLowerInvariant();
            var profile = await passengerProfileRepository.FindByNormalizedNameAsync(
                trip.ClientId.Value, normalized, cancellationToken);

            if (profile is null)
                await passengerProfileRepository.AddAsync(
                    PassengerProfile.Create(trip.ClientId.Value, request.Name, request.Phone, request.Email),
                    cancellationToken);
            else
            {
                profile.UpdateLastBooked(request.Phone, request.Email);
                await passengerProfileRepository.UpdateAsync(profile, cancellationToken);
            }

            // Purge profiles with no bookings in over a year.
            // Runs on each add to avoid a separate background job.
            await passengerProfileRepository.PurgeExpiredAsync(
                DateTime.UtcNow.AddYears(-1), cancellationToken);
        }

        return new AddPassengerResult(passenger.Id);
    }
}
