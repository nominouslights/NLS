using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Riders.UpsertFromManifest;

/// <summary>
/// Worker-semantics handler: resolves manifest → trip by trip number (the
/// <c>AttachManifestToTripCommandHandler</c> resolution shape) and hands off to
/// <see cref="ManifestRiderUpserter"/>. No-ops successfully when the manifest isn't
/// visible under the ambient tenant, no trip carries its trip number, or the number
/// matches a trip whose linked manifest is a different one (edited number pointing at
/// someone else's trip — not this manifest's service type to borrow).
/// </summary>
public sealed class UpsertRidersFromManifestCommandHandler(
    ITripManifestRepository manifestRepository,
    ITripRepository tripRepository,
    ManifestRiderUpserter upserter)
    : ICommandHandler<UpsertRidersFromManifestCommand>
{
    public async Task<Result> Handle(UpsertRidersFromManifestCommand command, CancellationToken cancellationToken)
    {
        var manifest = await manifestRepository.GetByIdAsync(command.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return Result.Success();
        }

        var trip = await tripRepository.GetByTripNumberAsync(manifest.TripNumber, cancellationToken);
        if (trip is null || trip.ManifestId != manifest.Id)
        {
            return Result.Success();
        }

        return await upserter.UpsertAsync(manifest, trip.ServiceType, cancellationToken);
    }
}
