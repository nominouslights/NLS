using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeStatus;

public sealed class ChangeTripStatusCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<ChangeTripStatusCommand>
{
    public async Task<Result> Handle(ChangeTripStatusCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var result = command.Status switch
        {
            TripStatus.InProgress => trip.Start(),
            TripStatus.Completed => trip.Complete(),
            TripStatus.Cancelled => trip.Cancel(command.Reason),
            _ => Result.Failure(TripErrors.InvalidStatusTransition(trip.Status, command.Status)),
        };

        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
