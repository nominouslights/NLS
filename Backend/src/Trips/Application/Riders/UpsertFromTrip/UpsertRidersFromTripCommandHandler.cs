using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Riders.UpsertFromTrip;

/// <summary>
/// Worker-semantics handler: resolves trip → manifest and hands off to
/// <see cref="ManifestRiderUpserter"/>. No-ops successfully when the trip isn't visible
/// under the ambient tenant, carries no manifest, or the manifest is gone.
/// </summary>
public sealed class UpsertRidersFromTripCommandHandler(
    ITripRepository tripRepository,
    ITripManifestRepository manifestRepository,
    ManifestRiderUpserter upserter)
    : ICommandHandler<UpsertRidersFromTripCommand>
{
    public async Task<Result> Handle(UpsertRidersFromTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip?.ManifestId is not { } manifestId)
        {
            return Result.Success();
        }

        var manifest = await manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            return Result.Success();
        }

        return await upserter.UpsertAsync(manifest, trip.ServiceType, cancellationToken);
    }
}
