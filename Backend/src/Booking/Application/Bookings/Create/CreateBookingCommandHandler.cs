using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Customers;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Bookings.Create;

/// <summary>
/// Creates a booking: validates the corridor against the replica and the customer against
/// the roster (snapshotting both names), lazily materializes the corridor+date's
/// <see cref="BookingDay"/> (DB-atomic get-or-create — the repository catches the unique
/// violation and re-reads), and stamps the seat hold from the tenant policy
/// (<see cref="BookingPolicy.DefaultSeatHoldMinutes"/> until a policy row exists).
/// </summary>
public sealed class CreateBookingCommandHandler(
    IBookingRepository bookings,
    IBookingDayRepository bookingDays,
    ICustomerRepository customers,
    ICorridorLookupRepository corridors,
    IBookingPolicyRepository policies)
    : ICommandHandler<CreateBookingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(command.CorridorId, cancellationToken);
        if (corridor is null)
        {
            return Result.Failure<Guid>(BookingErrors.CorridorNotFound);
        }

        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<Guid>(CustomerErrors.NotFound);
        }

        var pickup = BookingLocation.Create(
            command.Pickup.StopId, command.Pickup.StopName, command.Pickup.AddressDetail);
        if (pickup.IsFailure)
        {
            return Result.Failure<Guid>(pickup.Error);
        }

        var dropoff = BookingLocation.Create(
            command.Dropoff.StopId, command.Dropoff.StopName, command.Dropoff.AddressDetail);
        if (dropoff.IsFailure)
        {
            return Result.Failure<Guid>(dropoff.Error);
        }

        var policy = await policies.GetAsync(cancellationToken);
        var seatHoldMinutes = policy?.SeatHoldMinutes ?? BookingPolicy.DefaultSeatHoldMinutes;

        // Materialize the calendar cell before the booking so the day exists even if the
        // booking save fails (an empty day is harmless; a day-less booking is not).
        await bookingDays.GetOrCreateAsync(
            command.CorridorId,
            command.ServiceDate,
            () => BookingDay.Create(command.TenantId, command.CorridorId, command.ServiceDate),
            cancellationToken);

        var bookingResult = Domain.Bookings.Booking.Create(
            command.TenantId,
            command.CustomerId,
            customer.Name,
            command.CorridorId,
            corridor.Name,
            command.ServiceDate,
            pickup.Value,
            dropoff.Value,
            [.. command.Passengers.Select(p => new BookingPassengerDetails(p.Name, p.Phone, p.IsBillingCustomer))],
            command.PaymentMethod,
            TimeSpan.FromMinutes(seatHoldMinutes),
            command.Notes);

        if (bookingResult.IsFailure)
        {
            return Result.Failure<Guid>(bookingResult.Error);
        }

        bookings.Add(bookingResult.Value);
        await bookings.SaveChangesAsync(cancellationToken);
        return Result.Success(bookingResult.Value.Id);
    }
}
