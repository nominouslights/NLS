using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Records every trip whose run has ended into the <c>billable_trips</c> replica, insert-if-absent
/// keyed on TripId — idempotent under at-least-once delivery, backstopped by the primary key. The
/// row lands uninvoiced (<c>invoice_id</c> null) and is never re-upserted here (a run ends once);
/// the only post-insert mutation is <see cref="TripRoundTripChangedIntegrationEventHandler"/>
/// re-keying an uninvoiced row's pairing.
/// <para>
/// Replaces <see cref="TripCompletedIntegrationEventHandler"/>, which fed off trip completion.
/// Under the billing-driven trip lifecycle a trip only reaches Completed once payment lands, so
/// waiting for that would mean waiting for an invoice that no one had drafted yet. Both handlers
/// run side by side for one release so any in-flight <c>trips.trip-completed</c> rows still
/// drain; they insert the same row if-absent, so an overlap is harmless.
/// </para>
/// <para>
/// Runs outside any HTTP request — tenant from the payload, ambient push for RLS, explicit tenant
/// filter on the existence check (Fleet-consumer pattern).
/// </para>
/// </summary>
public sealed class TripReadyForBillingIntegrationEventHandler(
    IBillableTripRepository repository,
    ILogger<TripReadyForBillingIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripReadyForBillingIntegrationEvent>
{
    public async Task Handle(TripReadyForBillingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        if (await repository.ExistsAsync(integrationEvent.TenantId, integrationEvent.TripId, cancellationToken))
        {
            logger.LogDebug(
                "Billing already recorded billable trip {TripId} ({EventId}); skipping",
                integrationEvent.TripId, integrationEvent.EventId);
            return;
        }

        repository.Add(new BillableTrip
        {
            Id = integrationEvent.TripId,
            TenantId = integrationEvent.TenantId,
            TripNumber = integrationEvent.TripNumber,
            ClientId = integrationEvent.ClientId,
            ClientName = integrationEvent.ClientName,
            ServiceType = integrationEvent.ServiceType,
            RouteName = integrationEvent.RouteName,
            Origin = integrationEvent.Origin,
            Destination = integrationEvent.Destination,
            DistanceKm = integrationEvent.DistanceKm,
            ServiceDate = integrationEvent.ServiceDate,
            RoundTripKey = integrationEvent.RoundTripKey,
            Direction = integrationEvent.Direction,
            IsEmptyLeg = integrationEvent.IsEmptyLeg,
            PoNumber = integrationEvent.PoNumber,
            CompletedAtUtc = integrationEvent.OperationsFinishedAtUtc,
            InvoiceId = null,
        });

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing recorded billable trip {TripNumber} ({EventId}): client {ClientName}, route {RouteName}, service date {ServiceDate}",
            integrationEvent.TripNumber,
            integrationEvent.EventId,
            integrationEvent.ClientName ?? "none",
            integrationEvent.RouteName,
            integrationEvent.ServiceDate);
    }
}
