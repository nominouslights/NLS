using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// A dispatcher reopened a trip, so it is back in operation and must not be drafted onto a
/// worksheet while it is. Deletes the uninvoiced <c>billable_trips</c> row — the same shape as
/// <see cref="TripClosedWithoutBillingIntegrationEventHandler"/>, because absence is what "not
/// billable" means throughout this table. When the trip is finished again,
/// <see cref="TripReadyForBillingIntegrationEventHandler"/> finds no row and inserts a fresh one
/// (insert-if-absent), so the reopen → finish cycle needs nothing else here.
/// <para>
/// Acts on every reopen regardless of the source status: only a trip reopened out of
/// ReadyForBilling can have a row, so for the rest the fetch simply finds nothing. Idempotent
/// under at-least-once delivery — an absent row is a no-op success. Outbox polling keeps
/// position order within the trips schema, so ready-for-billing → reopened → ready-for-billing
/// arrive here in that order.
/// </para>
/// <para>
/// A row already claimed by a worksheet is kept with a warning rather than stolen back — the
/// producer refuses to reopen a claimed trip, so reaching this means the reopen raced a draft
/// whose claim had not yet reached Trips; the claim, the stronger fact, wins and someone voids
/// the draft by hand.
/// </para>
/// <para>
/// Runs outside any HTTP request — tenant from the payload, ambient push for RLS, explicit tenant
/// filter on the fetch (Fleet-consumer pattern).
/// </para>
/// </summary>
public sealed class TripReopenedIntegrationEventHandler(
    IBillableTripRepository repository,
    ILogger<TripReopenedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripReopenedIntegrationEvent>
{
    public async Task Handle(TripReopenedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var removal = await BillablePoolRemoval.RemoveUninvoicedAsync(
            repository, integrationEvent.TenantId, integrationEvent.TripId, cancellationToken);

        switch (removal.Outcome)
        {
            case BillablePoolRemovalOutcome.Absent:
                logger.LogDebug(
                    "Billing has no billable trip {TripId} to remove on reopen from {PreviousStatus} ({EventId}); skipping",
                    integrationEvent.TripId, integrationEvent.PreviousStatus, integrationEvent.EventId);
                break;
            case BillablePoolRemovalOutcome.KeptClaimed:
                logger.LogWarning(
                    "Trip {TripNumber} ({TripId}) was reopened from {PreviousStatus} but invoice {InvoiceId} already claims it ({EventId}); keeping the claim — void the draft in Billing.",
                    integrationEvent.TripNumber, integrationEvent.TripId, integrationEvent.PreviousStatus, removal.ClaimingInvoiceId, integrationEvent.EventId);
                break;
            case BillablePoolRemovalOutcome.Removed:
                logger.LogInformation(
                    "Billing removed billable trip {TripNumber} ({EventId}) — reopened from {PreviousStatus} to {NewStatus}: {Reason}",
                    integrationEvent.TripNumber,
                    integrationEvent.EventId,
                    integrationEvent.PreviousStatus,
                    integrationEvent.NewStatus,
                    integrationEvent.Reason);
                break;
        }
    }
}
