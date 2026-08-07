using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Applies Billing's worksheet state to the trips it claims. Two things happen here, in one
/// transaction: the claimed trips' own <see cref="TripStatus"/> advances (ReadyForBilling →
/// Invoiced → Completed, or → WrittenOff), and the <c>trip_billing</c> replica records the
/// worksheet detail behind it — invoice number, QBO reference, dates.
/// <para>
/// This is the second half of the billing-driven lifecycle: a trip stops when the bus stops but
/// only completes when the money arrives, and this handler is where that fact crosses the module
/// boundary. Billing's <c>billable_trips</c> claim is still the thing that actually enforces
/// "don't invoice it twice" — Trips reflects the outcome, it doesn't police it.
/// </para>
/// <para>
/// The event carries the invoice's complete claim set, so this reconciles rather than patches:
/// listed trips take the event's state and any trip still pointing at the invoice but missing
/// from the list has been released. That makes it idempotent under at-least-once delivery and
/// self-healing after a missed event.
/// </para>
/// <para>
/// Runs outside any HTTP request, so the event's tenant is pushed as ambient for the write
/// (RLS session variable) and both repositories take it explicitly.
/// </para>
/// </summary>
public sealed class InvoiceBillingStateChangedIntegrationEventHandler(
    ITripBillingRepository repository,
    ITripRepository tripRepository,
    ILogger<InvoiceBillingStateChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<InvoiceBillingStateChangedIntegrationEvent>
{
    public async Task Handle(
        InvoiceBillingStateChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // Released is expressed as an empty claim set, never as a stored state — a trip with
        // no row is unclaimed, so "is this billable" stays one existence check.
        var tripIds = integrationEvent.State == TripBillingStates.Released
            ? []
            : integrationEvent.TripIds.Distinct().ToList();

        var claimed = tripIds
            .Select(tripId => new TripBilling
            {
                TripId = tripId,
                TenantId = integrationEvent.TenantId,
                InvoiceId = integrationEvent.InvoiceId,
                InvoiceNumber = integrationEvent.InvoiceNumber,
                State = integrationEvent.State,
                QboInvoiceId = integrationEvent.QboInvoiceId,
                QboEnteredDate = integrationEvent.QboEnteredDate,
                PaymentConfirmedDate = integrationEvent.PaymentConfirmedDate,
                WrittenOffReason = integrationEvent.WrittenOffReason,
                UpdatedAtUtc = integrationEvent.OccurredAtUtc,
            })
            .ToList();

        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await ApplyTripStatusAsync(integrationEvent, tripIds, cancellationToken);

            // Saves the trip transitions staged above alongside the replica rows: both
            // repositories share the scoped DbContext, so this is one transaction and the
            // aggregate writes still emit their event-journal rows on the way through.
            await repository.ReconcileAsync(
                integrationEvent.TenantId,
                integrationEvent.InvoiceId,
                claimed,
                integrationEvent.OccurredAtUtc,
                cancellationToken);
        }

        logger.LogInformation(
            "Trips reconciled billing state {State} for invoice {InvoiceNumber} of tenant {TenantId} ({EventId}) across {TripCount} trip(s).",
            integrationEvent.State,
            integrationEvent.InvoiceNumber,
            integrationEvent.TenantId,
            integrationEvent.EventId,
            claimed.Count);
    }

    /// <summary>
    /// Stages the status transition for every claimed trip. Nothing is saved here — the
    /// reconcile that follows commits it.
    /// </summary>
    private async Task ApplyTripStatusAsync(
        InvoiceBillingStateChangedIntegrationEvent integrationEvent,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken)
    {
        if (TargetFor(integrationEvent.State) is not { } target || tripIds.Count == 0)
        {
            return;
        }

        // High-water mark. Same-status no-ops and the transition matrix between them still let a
        // replayed older event walk a paid trip back to Invoiced, because that edge is legal by
        // design (a payment confirmation cleared in error). Comparing against what the replica
        // already recorded is the only thing that catches it.
        var existing = await repository.GetByTripIdsAsync(
            integrationEvent.TenantId, tripIds, cancellationToken);
        var highWater = existing.ToDictionary(b => b.TripId, b => b.UpdatedAtUtc);

        var fresh = tripIds
            .Where(id => !highWater.TryGetValue(id, out var seen) || seen <= integrationEvent.OccurredAtUtc)
            .ToList();

        if (fresh.Count < tripIds.Count)
        {
            logger.LogInformation(
                "Skipping {StaleCount} trip(s) for invoice {InvoiceNumber} ({EventId}): they already reflect a later billing event.",
                tripIds.Count - fresh.Count,
                integrationEvent.InvoiceNumber,
                integrationEvent.EventId);
        }

        var trips = await tripRepository.GetByIdsAsync(integrationEvent.TenantId, fresh, cancellationToken);

        foreach (var trip in trips)
        {
            var result = target switch
            {
                TripStatus.Invoiced => trip.MarkInvoiced(),
                TripStatus.Completed => trip.MarkPaid(),
                TripStatus.WrittenOff => trip.WriteOff(integrationEvent.WrittenOffReason),
                _ => throw new InvalidOperationException($"Unhandled billing-driven target {target}."),
            };

            if (result.IsFailure)
            {
                // Logged, never thrown. Throwing fails the outbox row, and a failing head row
                // blocks the whole producer schema for that poll — one cancelled trip in a claim
                // set would stall every tenant's billing reconcile until the backoff cleared.
                logger.LogWarning(
                    "Trip {TripNumber} ({TripId}) could not move to {Target} for invoice {InvoiceNumber}: {Error}",
                    trip.TripNumber, trip.Id, target, integrationEvent.InvoiceNumber, result.Error.Message);
            }
        }
    }

    /// <summary>
    /// Which trip status a wire state implies, or null when it implies none.
    /// <para>
    /// <c>OnWorksheet</c> means a draft claims the trip — a draft is not an invoice, so the trip
    /// stays ReadyForBilling. <c>Released</c> is only reachable from a voided draft, which was
    /// never Invoiced, so it must never demote anything: Invoiced → ReadyForBilling is the one
    /// move the lifecycle forbids outright.
    /// </para>
    /// </summary>
    private static TripStatus? TargetFor(string state) => state switch
    {
        TripBillingStates.Invoiced => TripStatus.Invoiced,
        TripBillingStates.Paid => TripStatus.Completed,
        TripBillingStates.WrittenOff => TripStatus.WrittenOff,
        _ => null,
    };
}
