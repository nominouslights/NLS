using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Applies a post-hoc round-trip pairing change (dispatcher merge, deadhead return,
/// unpair) to the <c>billable_trips</c> replica. Only a NOT-yet-invoiced row is re-keyed:
/// no row (the trip never completed, so there is nothing to bill yet — the eventual
/// trip-completed event carries the pairing itself) and already-invoiced rows (the
/// worksheet claimed them; retro-editing a claimed line is a manual act) are skipped with
/// an info log. Idempotent: re-delivery writes the same values. Runs outside any HTTP
/// request — tenant from the payload, ambient push for RLS, explicit tenant filter on the
/// fetch (Fleet-consumer pattern).
/// </summary>
public sealed class TripRoundTripChangedIntegrationEventHandler(
    IBillableTripRepository repository,
    ILogger<TripRoundTripChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripRoundTripChangedIntegrationEvent>
{
    public async Task Handle(TripRoundTripChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var trip = await repository.GetAsync(integrationEvent.TenantId, integrationEvent.TripId, cancellationToken);
        if (trip is null)
        {
            logger.LogInformation(
                "Billing has no billable trip {TripId} ({EventId}); round-trip change not applicable — the pairing will arrive with trip completion",
                integrationEvent.TripId, integrationEvent.EventId);
            return;
        }

        if (trip.InvoiceId is not null)
        {
            logger.LogInformation(
                "Billable trip {TripNumber} ({TripId}, {EventId}) is already claimed by invoice {InvoiceId}; leaving its round-trip pairing alone",
                trip.TripNumber, trip.Id, integrationEvent.EventId, trip.InvoiceId);
            return;
        }

        trip.RoundTripKey = integrationEvent.RoundTripKey;
        trip.Direction = integrationEvent.Direction;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing re-keyed billable trip {TripNumber} ({TripId}, {EventId}): round-trip key {RoundTripKey}, direction {Direction}",
            trip.TripNumber, trip.Id, integrationEvent.EventId,
            integrationEvent.RoundTripKey ?? "none", integrationEvent.Direction ?? "none");
    }
}
