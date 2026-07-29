using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.MergeRoundTrip;

/// <summary>
/// Loads both legs through the tenant-filtered repository (a foreign-tenant "other" trip
/// simply reads as NotFound — the API half of dual tenant enforcement), resolves the leg
/// direction from the trips' manifests (a manifest-declared direction outranks the
/// chronological fallback; two manifests declaring the SAME direction is a data conflict
/// the dispatcher must fix, not something to guess around), and delegates the whole
/// validation matrix plus key minting to <see cref="Trip.MergeRoundTrip"/>. One save
/// covers both tracked aggregates and their round-trip-changed events.
/// </summary>
public sealed class MergeRoundTripCommandHandler(
    ITripRepository tripRepository,
    ITripManifestRepository manifestRepository)
    : ICommandHandler<MergeRoundTripCommand>
{
    public async Task<Result> Handle(MergeRoundTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var other = await tripRepository.GetByIdAsync(command.OtherTripId, cancellationToken);
        if (other is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var tripDeclared = await ManifestDirectionAsync(trip, cancellationToken);
        var otherDeclared = await ManifestDirectionAsync(other, cancellationToken);

        if (tripDeclared is not null && tripDeclared == otherDeclared)
        {
            return Result.Failure(TripErrors.RoundTripManifestDirectionConflict);
        }

        // One declared leg fixes both: the undeclared leg takes the opposite.
        var firstDirection = tripDeclared ?? Opposite(otherDeclared);

        var result = Trip.MergeRoundTrip(trip, other, command.AllowMismatch, firstDirection);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<TripDirection?> ManifestDirectionAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.ManifestId is not { } manifestId)
        {
            return null;
        }

        var manifest = await manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        return manifest?.Direction;
    }

    private static TripDirection? Opposite(TripDirection? direction) => direction switch
    {
        TripDirection.Outbound => TripDirection.Inbound,
        TripDirection.Inbound => TripDirection.Outbound,
        _ => null,
    };
}
