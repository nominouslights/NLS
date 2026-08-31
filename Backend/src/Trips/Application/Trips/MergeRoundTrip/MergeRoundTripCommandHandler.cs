using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
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
/// <para>
/// <b>The closed-leg rule lives here, not in the aggregate.</b> Pairing a ReadyForBilling,
/// Invoiced or Completed leg stays legal — that was always the point of the feature — but it
/// touches work that has already been reported or billed, so this handler additionally requires
/// <see cref="MergeRoundTripCommand.Reason"/> and writes an audit line at Warning naming who did
/// it, to what, and why. Both-open merges are untouched: same validation, same silence.
/// </para>
/// <para>
/// Worth knowing, and deliberately not "fixed" here: Billing's
/// <c>TripRoundTripChangedIntegrationEventHandler</c> skips re-keying a <c>billable_trips</c> row
/// that already carries an <c>invoiceId</c>, on the grounds that an invoiced row is claimed by a
/// document that has left the building. So pairing an already-invoiced trip updates Trips and
/// leaves Billing's replica showing the old (usually absent) pairing. That divergence is the
/// designed behaviour on the Billing side; anyone who wants the replica corrected has to go
/// through Billing (void or re-issue), not through this handler.
/// </para>
/// </summary>
public sealed class MergeRoundTripCommandHandler(
    ITripRepository tripRepository,
    ITripManifestRepository manifestRepository,
    ICurrentActor currentActor,
    ILogger<MergeRoundTripCommandHandler> logger)
    : ICommandHandler<MergeRoundTripCommand>
{
    /// <summary>Upper bound on the free-text reason — long enough for a sentence of context.</summary>
    private const int MaxReasonLength = 500;

    public async Task<Result> Handle(MergeRoundTripCommand command, CancellationToken cancellationToken)
    {
        var reason = command.Reason?.Trim();
        if (reason is { Length: > MaxReasonLength })
        {
            return Result.Failure(TripErrors.RoundTripReasonTooLong);
        }

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

        // "Read-only" in the console's sense: the run is over as far as operations go, so the
        // pairing is being changed on work someone has already reported on or billed.
        var touchesClosedWork = trip.IsOperationallyClosed || other.IsOperationallyClosed;
        if (touchesClosedWork && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(TripErrors.RoundTripReasonRequired);
        }

        var tripDeclared = await ManifestDirectionAsync(trip, cancellationToken);
        var otherDeclared = await ManifestDirectionAsync(other, cancellationToken);

        if (tripDeclared is not null && tripDeclared == otherDeclared)
        {
            return Result.Failure(TripErrors.RoundTripManifestDirectionConflict);
        }

        // One declared leg fixes both: the undeclared leg takes the opposite.
        var firstDirection = tripDeclared ?? Opposite(otherDeclared);

        // Statuses are captured before the merge because AssignRoundTrip does not change them —
        // but reading them here keeps the audit line honest about what was true at decision time.
        var tripStatus = trip.Status;
        var otherStatus = other.Status;

        var result = Trip.MergeRoundTrip(trip, other, command.AllowMismatch, firstDirection);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);

        if (touchesClosedWork)
        {
            // Audit line, not diagnostics: Warning so it survives production log levels and
            // stands out in a review of who re-keyed billed work.
            logger.LogWarning(
                "Round-trip pairing applied to closed trip(s): user {ActorUserId} ({ActorEmail}) paired " +
                "trip {TripNumber} ({TripId}, {TripStatus}) with trip {OtherTripNumber} ({OtherTripId}, " +
                "{OtherTripStatus}); allowMismatch {AllowMismatch}, round-trip key {RoundTripKey}, reason: {Reason}",
                currentActor.UserId,
                currentActor.Email,
                trip.TripNumber,
                trip.Id,
                tripStatus,
                other.TripNumber,
                other.Id,
                otherStatus,
                command.AllowMismatch,
                trip.RoundTripKey,
                reason);
        }

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
