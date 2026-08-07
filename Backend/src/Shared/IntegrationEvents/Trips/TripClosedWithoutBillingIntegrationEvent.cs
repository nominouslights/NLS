using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a dispatcher closes out a ReadyForBilling trip that will never be invoiced —
/// routing key <c>trips.trip-closed-without-billing</c>. The counterpart to
/// <c>TripReadyForBillingIntegrationEvent</c>: that one puts a trip into Billing's billable
/// pool, this one takes it back out, so a closed trip can never be drafted onto a worksheet.
/// Billing deletes the uninvoiced <c>billable_trips</c> row (absent row = no-op, so delivery is
/// idempotent); a row already claimed by a draft is skipped with a warning, though the producer
/// side refuses to close a claimed trip in the first place.
/// <see cref="TenantId"/> is in the payload because handlers run outside any HTTP request.
/// </summary>
public sealed record TripClosedWithoutBillingIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string TripNumber,
    string Reason) : IntegrationEvent;
