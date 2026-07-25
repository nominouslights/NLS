using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Records every completed trip into the <c>billable_trips</c> replica, insert-if-absent
/// keyed on TripId — idempotent under at-least-once delivery, backstopped by the primary
/// key. The row lands uninvoiced (<c>invoice_id</c> null) and stays immutable afterwards:
/// draft generation claims it, it is never re-upserted (a trip completes once). Runs
/// outside any HTTP request — tenant from the payload, ambient push for RLS, explicit
/// tenant filter on the existence check (Fleet-consumer pattern).
/// </summary>
public sealed class TripCompletedIntegrationEventHandler(
    IBillableTripRepository repository,
    ILogger<TripCompletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripCompletedIntegrationEvent>
{
    public async Task Handle(TripCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
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
            PoNumber = integrationEvent.PoNumber,
            CompletedAtUtc = integrationEvent.CompletedAtUtc,
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
