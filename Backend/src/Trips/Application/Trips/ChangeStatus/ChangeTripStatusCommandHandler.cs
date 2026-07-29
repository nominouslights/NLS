using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeStatus;

public sealed class ChangeTripStatusCommandHandler(
    ITripRepository tripRepository,
    ITripManifestRepository manifestRepository)
    : ICommandHandler<ChangeTripStatusCommand>
{
    public async Task<Result> Handle(ChangeTripStatusCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        // En-route guard: a trip cannot go InProgress without a manifest carrying >=1 passenger.
        // Deadheads (IsEmptyLeg) are exempt — an empty repositioning run starts and ends
        // without passengers, and manifests can't even be created for it.
        if (command.Status == TripStatus.InProgress && !trip.IsEmptyLeg)
        {
            var guard = await GuardPassengerManifest(trip, cancellationToken);
            if (guard.IsFailure)
            {
                return guard;
            }
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

    private async Task<Result> GuardPassengerManifest(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.ManifestId is not { } manifestId)
        {
            return Result.Failure(TripErrors.PassengerManifestRequired);
        }

        var manifest = await manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null || manifest.Passengers.Count < 1)
        {
            return Result.Failure(TripErrors.PassengerManifestRequired);
        }

        return Result.Success();
    }
}
