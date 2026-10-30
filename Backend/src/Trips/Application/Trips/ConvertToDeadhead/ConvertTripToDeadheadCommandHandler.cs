using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ConvertToDeadhead;

/// <summary>
/// Loads everything outside the Trip aggregate that decides whether it may run empty — all of
/// its manifests (by <c>ManifestId</c> AND by trip number, since linking is lazy and the
/// trip-number index is not unique), the live Bookeo ledger rows placed on it, and its round-trip
/// partner(s) — and hands them to <see cref="Trip.ConvertToDeadhead"/>. On success the empty
/// manifests are deleted in the same save as the flag flip: one DbContext sits behind every
/// repository, so a passenger added to one of them after it was loaded moves its version token
/// and the whole save comes back as <see cref="TripErrors.ChangedConcurrently"/> instead of
/// stranding that passenger on a deadhead.
/// </summary>
public sealed class ConvertTripToDeadheadCommandHandler(
    ITripRepository tripRepository,
    ITripManifestRepository manifestRepository,
    IBookeoImportRepository bookeoRepository)
    : ICommandHandler<ConvertTripToDeadheadCommand>
{
    public async Task<Result> Handle(ConvertTripToDeadheadCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        var manifests = new List<TripManifest>();
        if (trip.ManifestId is { } manifestId
            && await manifestRepository.GetByIdAsync(manifestId, cancellationToken) is { } linked)
        {
            manifests.Add(linked);
        }

        foreach (var byNumber in await manifestRepository.GetByTripNumberAsync(trip.TripNumber, cancellationToken))
        {
            if (manifests.All(m => m.Id != byNumber.Id))
            {
                manifests.Add(byNumber);
            }
        }

        var externalBookings = await bookeoRepository.CountBookingsOnTripAsync(trip.Id, cancellationToken);

        IReadOnlyList<Trip> partners = trip.RoundTripKey is { } key
            ? [.. (await tripRepository.GetByRoundTripKeyAsync(key, cancellationToken)).Where(t => t.Id != trip.Id)]
            : [];

        var converted = trip.ConvertToDeadhead(manifests, externalBookings, partners);
        if (converted.IsFailure)
        {
            return converted;
        }

        foreach (var manifest in manifests)
        {
            manifestRepository.Remove(manifest);
        }

        return await tripRepository.TrySaveChangesAsync(cancellationToken)
            ? Result.Success()
            : Result.Failure(TripErrors.ChangedConcurrently);
    }
}
