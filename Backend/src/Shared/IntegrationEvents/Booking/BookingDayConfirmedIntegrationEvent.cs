using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Booking;

/// <summary>
/// Published when a booking day crosses its passenger minimum and confirms — routing key
/// <c>booking.booking-day-confirmed</c>. This is the platform's first CHAIN-REACTION event:
/// it must trigger a command in the Trips module (create the community trip), so it is
/// designated in <c>BusPublicationRegistry</c> and travels over RabbitMQ rather than the
/// outbox-polling path. Consumers must be idempotent on <see cref="BookingDayId"/> — Trips
/// enforces this with a unique index on (tenant_id, booking_day_id), so a redelivery (or a
/// re-confirm after a revert) creates nothing twice.
/// <para>
/// <see cref="CorridorId"/> IS the Trips RouteId (a booking corridor is a Trips route), which
/// is how the consumer resolves the route snapshot without a library reference. Seat numbers
/// are the day's derived math at confirmation time; <see cref="TenantId"/> is part of the
/// payload because handlers run outside any HTTP request.
/// </para>
/// </summary>
public sealed record BookingDayConfirmedIntegrationEvent(
    Guid BookingDayId,
    Guid TenantId,
    Guid CorridorId,
    string CorridorName,
    string Origin,
    string Destination,
    DateOnly ServiceDate,
    int SeatsSold,
    int SeatsMinimum,
    int SeatCapacity) : IntegrationEvent;
