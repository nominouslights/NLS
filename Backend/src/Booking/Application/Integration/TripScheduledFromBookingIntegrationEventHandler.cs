using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Abstractions;

namespace NorthernLink.Booking.Application.Integration;

/// <summary>
/// The backlink half of the booking-day chain reaction: when Trips materializes the
/// community trip, this stamps <c>BookingDay.TripId</c>/<c>TripNumber</c> so the calendar
/// can link straight to it. Storing/projecting semantics, so it arrives by outbox polling
/// (the polling consumer pushes the row's tenant before resolving this handler's scope;
/// the inner push is defence in depth). Idempotent: re-delivery of the same trip is a
/// domain-level no-op; a DIFFERENT trip for an already-linked day is logged as an error
/// and dropped — at most one trip ever exists per booking day, so that indicates an
/// upstream fault a retry cannot fix.
/// </summary>
public sealed class TripScheduledFromBookingIntegrationEventHandler(
    IBookingDayRepository bookingDays,
    ILogger<TripScheduledFromBookingIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripScheduledFromBookingIntegrationEvent>
{
    public async Task Handle(
        TripScheduledFromBookingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var day = await bookingDays.GetByIdAsync(integrationEvent.BookingDayId, cancellationToken);
            if (day is null)
            {
                // Should be impossible (the day published the confirmation that created the
                // trip), but a replayed history against a reset schema must not park the queue.
                logger.LogWarning(
                    "Booking day {BookingDayId} not found for trip backlink {TripNumber} ({EventId}); dropping",
                    integrationEvent.BookingDayId, integrationEvent.TripNumber, integrationEvent.EventId);
                return;
            }

            var result = day.LinkTrip(integrationEvent.TripId, integrationEvent.TripNumber);
            if (result.IsFailure)
            {
                logger.LogError(
                    "Booking day {BookingDayId} rejected trip backlink {TripId} ({TripNumber}): {Error}",
                    day.Id, integrationEvent.TripId, integrationEvent.TripNumber, result.Error.Code);
                return;
            }

            await bookingDays.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Booking day {BookingDayId} linked to trip {TripNumber} ({TripId}) for tenant {TenantId} ({EventId})",
            integrationEvent.BookingDayId, integrationEvent.TripNumber, integrationEvent.TripId,
            integrationEvent.TenantId, integrationEvent.EventId);
    }
}
