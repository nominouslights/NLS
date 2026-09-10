using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Update;

/// <summary>
/// Edits a booking, then recomputes the day's threshold in the SAME transaction — edits
/// change passenger counts, so a Confirmed booking gaining or losing seats can move the day
/// across its minimum exactly like a confirm/cancel does. The day is ensured BEFORE the
/// booking is mutated — the get-or-create may save mid-flow, and it must flush nothing but
/// the day-create. See <see cref="BookingDayThresholdService"/>.
/// </summary>
public sealed class UpdateBookingCommandHandler(
    IBookingRepository repository,
    BookingDayThresholdService thresholds)
    : ICommandHandler<UpdateBookingCommand>
{
    public async Task<Result> Handle(UpdateBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var pickup = BookingLocation.Create(
            command.Pickup.StopId, command.Pickup.StopName, command.Pickup.AddressDetail);
        if (pickup.IsFailure)
        {
            return Result.Failure(pickup.Error);
        }

        var dropoff = BookingLocation.Create(
            command.Dropoff.StopId, command.Dropoff.StopName, command.Dropoff.AddressDetail);
        if (dropoff.IsFailure)
        {
            return Result.Failure(dropoff.Error);
        }

        var day = await thresholds.EnsureDayAsync(
            booking.TenantId, booking.CorridorId, booking.ServiceDate, cancellationToken);

        var result = booking.Update(
            pickup.Value,
            dropoff.Value,
            [.. command.Passengers.Select(p => new BookingPassengerDetails(p.Name, p.Phone, p.IsBillingCustomer))],
            command.PaymentMethod,
            command.PaymentStatus,
            command.Notes);

        if (result.IsFailure)
        {
            return result;
        }

        await thresholds.RecomputeAsync(day, booking, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
