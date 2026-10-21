using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a dispatcher reopens a trip to undo a lifecycle mistake — routing key
/// <c>trips.trip-reopened</c>. Raised for every reopen (whatever the source status) so the fact
/// is on the wire for any future consumer; today only Billing acts on it: a trip reopened out of
/// ReadyForBilling is back in operation, so Billing deletes its uninvoiced <c>billable_trips</c>
/// row (absent row = no-op, so delivery is idempotent) and keeps, with a warning, a row an invoice
/// already claims. When the trip is finished again the ordinary
/// <c>trips.trip-ready-for-billing</c> re-adds a fresh row (insert-if-absent).
/// <para>
/// A storing/projecting event: it arrives by outbox polling, never RabbitMQ. Statuses are the
/// trip-status enum member names (e.g. "ReadyForBilling", "Scheduled").
/// <see cref="TenantId"/> is in the payload because handlers run outside any HTTP request.
/// </para>
/// </summary>
public sealed record TripReopenedIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string TripNumber,
    string Reason,
    string PreviousStatus,
    string NewStatus) : IntegrationEvent;
