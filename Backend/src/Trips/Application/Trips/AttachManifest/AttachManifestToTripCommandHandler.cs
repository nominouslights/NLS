using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.AttachManifest;

/// <summary>
/// Idempotent manifest → trip link. No-ops successfully when the manifest is not visible
/// under the ambient tenant or no trip carries its trip number; re-linking the same
/// manifest converges (the aggregate treats it as a no-op). Linking sets
/// <c>Trip.ManifestId</c> without changing trip status. A different manifest already
/// attached surfaces as a conflict in the worker's log, never an exception.
/// </summary>
public sealed class AttachManifestToTripCommandHandler(
    ITripManifestRepository manifestRepository,
    ITripRepository tripRepository,
    ILogger<AttachManifestToTripCommandHandler> logger)
    : ICommandHandler<AttachManifestToTripCommand>
{
    public async Task<Result> Handle(AttachManifestToTripCommand command, CancellationToken cancellationToken)
    {
        var manifest = await manifestRepository.GetByIdAsync(command.ManifestId, cancellationToken);
        if (manifest is null)
        {
            // Not this tenant's manifest (or deleted) — nothing to attach.
            return Result.Success();
        }

        var trip = await tripRepository.GetByTripNumberAsync(manifest.TripNumber, cancellationToken);
        if (trip is null)
        {
            // Ad-hoc manifest with no planned trip — perfectly normal.
            return Result.Success();
        }

        if (trip.IsEmptyLeg)
        {
            // Defense in depth behind the create-time guard: a deadhead never carries a
            // manifest, so a number-matching manifest is left unlinked rather than attached.
            logger.LogWarning(
                "Trips skipped attaching manifest {ManifestId} to deadhead trip {TripNumber} ({TripId}); empty legs carry no manifest",
                manifest.Id, trip.TripNumber, trip.Id);
            return Result.Success();
        }

        var result = trip.AttachManifest(manifest.Id);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Trips attached manifest {ManifestId} to trip {TripNumber} ({TripId}); status now {Status}",
            manifest.Id, trip.TripNumber, trip.Id, trip.Status);

        return Result.Success();
    }
}
