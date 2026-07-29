using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.UnpairRoundTrip;

/// <summary>
/// Clears the pairing on every leg sharing the target trip's RoundTripKey (normally two —
/// the merge/deadhead pair), each clear raising its own round-trip-changed event so
/// Billing re-keys both replica rows. One save commits the whole unpair atomically.
/// </summary>
public sealed class UnpairRoundTripCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<UnpairRoundTripCommand>
{
    public async Task<Result> Handle(UnpairRoundTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        if (trip.RoundTripKey is not { } roundTripKey)
        {
            return Result.Failure(TripErrors.RoundTripNotPaired);
        }

        var legs = await tripRepository.GetByRoundTripKeyAsync(roundTripKey, cancellationToken);
        foreach (var leg in legs)
        {
            var cleared = leg.ClearRoundTrip();
            if (cleared.IsFailure)
            {
                return cleared;
            }
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
