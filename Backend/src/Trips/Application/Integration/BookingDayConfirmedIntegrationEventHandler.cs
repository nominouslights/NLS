using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Booking;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// The platform's first chain-reaction consumer: <c>booking.booking-day-confirmed</c>
/// arrives over RabbitMQ (see <c>BusPublicationRegistry</c>) and this handler creates the
/// community trip via <see cref="Trip.ScheduleFromBooking"/>. The event's CorridorId IS the
/// Trips RouteId, which is how the route snapshot is resolved without a library reference.
/// <para>
/// Idempotency is DB-atomic, not check-then-act: a cheap pre-check by booking day skips
/// obvious replays (sparing a burned trip number), and the real guarantee is the unique
/// (tenant_id, booking_day_id) index — <see cref="ITripRepository.TryAddForBookingDayAsync"/>
/// absorbs the 23505 from a concurrent duplicate and this handler no-ops. Either way the
/// backlink event reaches Booking: the winning insert wrote it to the outbox in the same
/// transaction as the trip.
/// </para>
/// <para>
/// A missing route is logged and dropped rather than retried — the corridor replica came
/// from a route, so its absence is a data-state fault (deleted route, replayed history)
/// no retry can fix, and dead-lettering the whole queue for it would stall live events.
/// </para>
/// </summary>
public sealed class BookingDayConfirmedIntegrationEventHandler(
    ITripRepository trips,
    IRouteRepository routes,
    ITripNumberGenerator tripNumbers,
    ILogger<BookingDayConfirmedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<BookingDayConfirmedIntegrationEvent>
{
    /// <summary>
    /// Provisional departure for a demand-created community trip — the dispatcher sets the
    /// real window on the trip after confirmation (the owner's decision: community trips
    /// have no fixed timetable; departures are set when the day confirms).
    /// </summary>
    public static readonly TimeOnly ProvisionalWindowStart = new(8, 0);

    public async Task Handle(
        BookingDayConfirmedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var existing = await trips.GetByBookingDayIdAsync(
                integrationEvent.TenantId, integrationEvent.BookingDayId, cancellationToken);
            if (existing is not null)
            {
                // Replay (redelivery, or a re-confirm after a revert) — the day's trip
                // already exists and its backlink already went out through the outbox.
                logger.LogInformation(
                    "Booking day {BookingDayId} already has trip {TripNumber}; ignoring duplicate confirmation ({EventId})",
                    integrationEvent.BookingDayId, existing.TripNumber, integrationEvent.EventId);
                return;
            }

            var route = await routes.GetByIdAsync(integrationEvent.CorridorId, cancellationToken);
            if (route is null)
            {
                logger.LogError(
                    "Booking day {BookingDayId} confirmed for corridor {CorridorId}, but no such route exists; dropping ({EventId})",
                    integrationEvent.BookingDayId, integrationEvent.CorridorId, integrationEvent.EventId);
                return;
            }

            var tripNumber = await tripNumbers.NextAsync(integrationEvent.TenantId, cancellationToken);

            var tripResult = Trip.ScheduleFromBooking(
                integrationEvent.TenantId,
                tripNumber,
                integrationEvent.BookingDayId,
                integrationEvent.ServiceDate,
                ProvisionalWindowStart,
                route.Id,
                route.Name,
                route.Origin,
                route.Destination,
                route.Stops,
                route.DistanceKm,
                seatsConfirmed: integrationEvent.SeatsSold,
                seatsCapacity: integrationEvent.SeatCapacity,
                seatsMinimum: integrationEvent.SeatsMinimum);

            if (tripResult.IsFailure)
            {
                // Validation failures are permanent for this payload — throwing would only
                // dead-letter it after three identical attempts.
                logger.LogError(
                    "Booking day {BookingDayId} confirmation could not become a trip: {Error} ({EventId})",
                    integrationEvent.BookingDayId, tripResult.Error.Code, integrationEvent.EventId);
                return;
            }

            var added = await trips.TryAddForBookingDayAsync(tripResult.Value, cancellationToken);
            if (!added)
            {
                logger.LogInformation(
                    "Booking day {BookingDayId} raced a concurrent trip creation; ignoring duplicate confirmation ({EventId})",
                    integrationEvent.BookingDayId, integrationEvent.EventId);
                return;
            }

            logger.LogInformation(
                "Scheduled community trip {TripNumber} ({TripId}) from booking day {BookingDayId} on {ServiceDate} — {Sold}/{Minimum} seats, no driver/vehicle yet",
                tripNumber, tripResult.Value.Id, integrationEvent.BookingDayId,
                integrationEvent.ServiceDate, integrationEvent.SeatsSold, integrationEvent.SeatsMinimum);
        }
    }
}
