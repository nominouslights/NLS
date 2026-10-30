using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ConvertToPassengerTrip;

public sealed class ConvertTripToPassengerTripCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<ConvertTripToPassengerTripCommand>
{
    public async Task<Result> Handle(ConvertTripToPassengerTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var converted = trip.ConvertToPassengerTrip();
        if (converted.IsFailure)
        {
            return converted;
        }

        return await tripRepository.TrySaveChangesAsync(cancellationToken)
            ? Result.Success()
            : Result.Failure(TripErrors.ChangedConcurrently);
    }
}
