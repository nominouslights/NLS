using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Booking;
using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.BookingDays.Events;

namespace NorthernLink.Booking.Application.Integration;

/// <summary>
/// Booking's explicit domain-event → integration-event translation — the module's first
/// public contracts, both hanging off the BookingDay lifecycle. The confirmed event is the
/// platform's first chain reaction (designated in <c>BusPublicationRegistry</c>; Trips'
/// RabbitMQ consumer creates the community trip from it); the reverted event is a normal
/// storing event (Notifications polls it to send the "trip at risk" emails). Both domain
/// events already carry the caller-computed snapshots (corridor names, seat math,
/// recipients) because the aggregate does not store derived numbers, so mapping is a
/// straight copy. Everything unmapped stays internal (null) — never auto-publishing.
/// </summary>
public sealed class BookingIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            BookingDayConfirmedDomainEvent confirmed => new BookingDayConfirmedIntegrationEvent(
                confirmed.BookingDayId,
                confirmed.TenantId,
                confirmed.CorridorId,
                confirmed.CorridorName,
                confirmed.Origin,
                confirmed.Destination,
                confirmed.ServiceDate,
                confirmed.SeatsSold,
                confirmed.SeatsMinimum,
                confirmed.SeatCapacity),
            BookingDayRevertedDomainEvent reverted => new BookingDayRevertedIntegrationEvent(
                reverted.BookingDayId,
                reverted.TenantId,
                reverted.CorridorName,
                reverted.ServiceDate,
                reverted.SeatsSold,
                reverted.SeatsNeeded,
                [.. reverted.Recipients.Select(r => new BookingDayRevertRecipient(r.Name, r.Email))],
                reverted.TripId,
                reverted.TripNumber),
            _ => null,
        };
}
