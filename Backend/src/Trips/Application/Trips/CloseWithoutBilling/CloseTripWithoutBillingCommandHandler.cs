using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.CloseWithoutBilling;

public sealed class CloseTripWithoutBillingCommandHandler(
    ITripRepository tripRepository,
    ITripBillingRepository tripBillingRepository)
    : ICommandHandler<CloseTripWithoutBillingCommand>
{
    public async Task<Result> Handle(CloseTripWithoutBillingCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        // A worksheet claiming the trip means someone is actively billing it — closing it out
        // from under the draft would leave the two modules telling different stories. The right
        // move then is on the Billing side: void the draft, or write the invoice off.
        var claims = await tripBillingRepository.GetByTripIdsAsync(
            trip.TenantId, [trip.Id], cancellationToken);
        if (claims.Count > 0)
        {
            return Result.Failure(TripErrors.OnWorksheetCannotCloseWithoutBilling);
        }

        var result = trip.CloseWithoutBilling(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
