using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.FinishOperations;

public sealed class FinishTripOperationsCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<FinishTripOperationsCommand>
{
    public async Task<Result> Handle(FinishTripOperationsCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        // No manifest guard here — that one gates going en route, not coming back. The gate on
        // finishing is the post-trip inspection, enforced inside the aggregate so it holds on
        // every path including a direct Scheduled → finish.
        var result = trip.FinishOperations();
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
