using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.RecordDemand;

public sealed class RecordTripDemandCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<RecordTripDemandCommand>
{
    public async Task<Result> Handle(RecordTripDemandCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var result = trip.RecordDemand(command.SeatsConfirmed, command.DemandGuaranteed);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
