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
/// (<see cref="BookingPolicy.DefaultSeatHoldMinutes"/> until a policy row exists). It also
/// allocates the customer-facing <see cref="BookingReference"/>: up to
/// <see cref="ReferenceAttempts"/> random draws checked against the tenant's existing rows,
/// then <see cref="BookingErrors.ReferenceExhausted"/> (a retryable conflict). The
/// <c>(tenant_id, reference)</c> unique index is the backstop for an in-flight race.
/// </summary>
public sealed class CreateBookingCommandHandler(
    IBookingRepository bookings,
    IBookingDayRepository bookingDays,
    ICustomerRepository customers,
    ICorridorLookupRepository corridors,
    IBookingPolicyRepository policies)
    : ICommandHandler<CreateBookingCommand, Guid>
{
    /// <summary>Random draws before giving up — with ~729M values per tenant, one is the norm.</summary>
    public const int ReferenceAttempts = 3;

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

        var reference = await AllocateReferenceAsync(cancellationToken);
        if (reference is null)
        {
            return Result.Failure<Guid>(BookingErrors.ReferenceExhausted);
        }

        // Materialize the calendar cell before the booking so the day exists even if the
        // booking save fails (an empty day is harmless; a day-less booking is not).
        await bookingDays.GetOrCreateAsync(
            command.CorridorId,
            command.ServiceDate,
            () => BookingDay.Create(command.TenantId, command.CorridorId, command.ServiceDate),
            cancellationToken);

        var bookingResult = Domain.Bookings.Booking.Create(
            command.TenantId,
            reference,
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

    /// <summary>A reference no booking in this tenant carries yet, or null after the attempt cap.</summary>
    private async Task<BookingReference?> AllocateReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ReferenceAttempts; attempt++)
        {
            var candidate = BookingReference.Generate();
            if (!await bookings.ReferenceExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}
