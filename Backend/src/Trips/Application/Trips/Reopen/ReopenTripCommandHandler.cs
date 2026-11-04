using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Reopen;

/// <summary>
/// Reopens a trip after the guards only the application layer can run:
/// <list type="number">
/// <item><b>The optimistic status check.</b> When the caller sends the status it saw
/// (<see cref="ReopenTripCommand.ExpectedStatus"/>) and the trip has moved on since, the reopen is
/// refused rather than applied to a state the dispatcher never looked at — a double click, or two
/// dispatchers reopening the same trip, gets one reopen and one
/// <see cref="TripErrors.ReopenStatusChanged"/>.</item>
/// <item><b>The trip's own claim.</b> Any trip-billing replica row means Billing is working on the
/// trip — a draft worksheet, an invoice in QuickBooks, a payment, or an invoice write-off (whose
/// claim is never released, which is exactly how it is told apart from a dispatcher close-out
/// landing in the same WrittenOff). Reopening under any of them would leave the two modules
/// telling different stories, so it is refused with the reason that matches the claim's
/// state.</item>
/// <item><b>The round-trip partner's claim</b>, on every reopenable status except InProgress
/// (never in the pool, so a reopen changes nothing about how it bills). The draft builder
/// prices each RoundTripKey group at one full round-trip rate, so a partner already drafted —
/// billed as an unpaired group — plus this leg billed once it finishes again would charge the run
/// twice. That holds whatever the leg is reopened from: a Cancelled or closed-out leg would head
/// back into the billable pool it had left, and a reopened ReadyForBilling leg leaves the pool and
/// comes back later as a separate group, behind a partner that may be drafted alone in the
/// meantime. Honestly, a partner drafted alone while this leg is still open is also what already
/// happens to a leg that simply finishes late — reopen does not create that behaviour, it only
/// refuses to step into it once the partner is claimed.</item>
/// </list>
/// A claim that Billing has made but whose event has not yet reached Trips' replica cannot be seen
/// here — the same window close-without-billing has; Billing's reopen consumer keeps (and warns
/// on) a claimed row rather than stealing it back.
/// </summary>
public sealed class ReopenTripCommandHandler(
    ITripRepository tripRepository,
    ITripBillingRepository tripBillingRepository)
    : ICommandHandler<ReopenTripCommand>
{
    public async Task<Result> Handle(ReopenTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        if (command.ExpectedStatus is { } expected && expected != trip.Status)
        {
            return Result.Failure(TripErrors.ReopenStatusChanged);
        }

        var claims = await tripBillingRepository.GetByTripIdsAsync(
            trip.TenantId, [trip.Id], cancellationToken);
        if (claims.Count > 0)
        {
            return Result.Failure(ClaimError(claims));
        }

        // Only reopenable statuses reach the partner lookup — a refused status fails in the
        // aggregate below with its own, more useful, reason. InProgress is exempt: it was never in
        // the billable pool, and leaving it InProgress then finishing it bills exactly what
        // reopening then finishing does, so refusing would block undoing an accidental start
        // without preventing any double bill.
        if (trip.Status != TripStatus.InProgress
            && Trip.ReopenTargetFor(trip.Status).IsSuccess
            && trip.RoundTripKey is { } roundTripKey)
        {
            var partnerError = await PartnerClaimErrorAsync(trip, roundTripKey, cancellationToken);
            if (partnerError is not null)
            {
                return Result.Failure(partnerError);
            }
        }

        var result = trip.Reopen(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> PartnerClaimErrorAsync(
        Trip trip, string roundTripKey, CancellationToken cancellationToken)
    {
        var partners = (await tripRepository.GetByRoundTripKeyAsync(roundTripKey, cancellationToken))
            .Where(leg => leg.Id != trip.Id)
            .ToList();
        if (partners.Count == 0)
        {
            return null;
        }

        var partnerClaims = await tripBillingRepository.GetByTripIdsAsync(
            trip.TenantId, [.. partners.Select(leg => leg.Id)], cancellationToken);
        if (partnerClaims.Count == 0)
        {
            return null;
        }

        var claimed = partners.First(leg => leg.Id == partnerClaims[0].TripId);
        return TripErrors.PartnerLegClaimedCannotReopen(claimed.TripNumber);
    }

    /// <summary>
    /// The refusal that names what Billing is actually doing with the trip, so the dispatcher sees
    /// the right reason: paid wins over everything, then in QuickBooks, then an invoice write-off;
    /// anything else (a draft) points at voiding the draft in Billing first.
    /// </summary>
    private static Error ClaimError(IReadOnlyList<TripBilling> claims)
    {
        if (claims.Any(c => c.State == TripBillingStates.Paid))
        {
            return TripErrors.PaidCannotReopen;
        }

        if (claims.Any(c => c.State == TripBillingStates.Invoiced))
        {
            return TripErrors.InvoicedCannotReopen;
        }

        if (claims.Any(c => c.State == TripBillingStates.WrittenOff))
        {
            return TripErrors.InvoiceWrittenOffCannotReopen;
        }

        return TripErrors.OnWorksheetCannotReopen;
    }
}
