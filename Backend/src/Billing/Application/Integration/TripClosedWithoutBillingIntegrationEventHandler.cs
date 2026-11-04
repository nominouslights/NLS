using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// The undo of <see cref="TripReadyForBillingIntegrationEventHandler"/>: a dispatcher closed the
/// trip without billing, so it leaves the billable pool for good. Deletes the uninvoiced
/// <c>billable_trips</c> row — deletion, not a flag, because absence is what "not billable"
/// means throughout this table. Idempotent under at-least-once delivery: an absent row is a
/// no-op success.
/// <para>
/// A row already claimed by a worksheet is skipped with a warning rather than stolen back —
/// the producer refuses to close a claimed trip, so hitting this means the close and a draft
/// raced; the claim, which is the stronger fact, wins.
/// </para>
/// <para>
/// Runs outside any HTTP request — tenant from the payload, ambient push for RLS, explicit
/// tenant filter on the fetch (Fleet-consumer pattern).
/// </para>
/// </summary>
public sealed class TripClosedWithoutBillingIntegrationEventHandler(
    IBillableTripRepository repository,
    ILogger<TripClosedWithoutBillingIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripClosedWithoutBillingIntegrationEvent>
{
    public async Task Handle(TripClosedWithoutBillingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var removal = await BillablePoolRemoval.RemoveUninvoicedAsync(
            repository, integrationEvent.TenantId, integrationEvent.TripId, cancellationToken);

        switch (removal.Outcome)
        {
            case BillablePoolRemovalOutcome.Absent:
                logger.LogDebug(
                    "Billing has no billable trip {TripId} to remove ({EventId}); skipping",
                    integrationEvent.TripId, integrationEvent.EventId);
                break;
            case BillablePoolRemovalOutcome.KeptClaimed:
                logger.LogWarning(
                    "Trip {TripNumber} ({TripId}) was closed without billing but invoice {InvoiceId} already claims it ({EventId}); keeping the claim.",
                    integrationEvent.TripNumber, integrationEvent.TripId, removal.ClaimingInvoiceId, integrationEvent.EventId);
                break;
            case BillablePoolRemovalOutcome.Removed:
                logger.LogInformation(
                    "Billing removed billable trip {TripNumber} ({EventId}) — closed without billing: {Reason}",
                    integrationEvent.TripNumber,
                    integrationEvent.EventId,
                    integrationEvent.Reason);
                break;
        }
    }
}
