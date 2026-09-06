using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Confirm;

/// <summary>
/// Confirms a booking, then recomputes the day's threshold in the SAME transaction: the
/// booking flip, any resulting <c>BookingDay.Confirm</c> (with its outbox row — the
/// chain-reaction event Trips turns into the community trip), and the audit entries all
/// commit in one SaveChanges. The day is ensured BEFORE the booking is mutated — the
/// get-or-create may save mid-flow, and it must flush nothing but the day-create.
/// See <see cref="BookingDayThresholdService"/>.
/// </summary>
public sealed class ConfirmBookingCommandHandler(
    IBookingRepository repository,
    BookingDayThresholdService thresholds)
    : ICommandHandler<ConfirmBookingCommand>
{
    public async Task<Result> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var day = await thresholds.EnsureDayAsync(
            booking.TenantId, booking.CorridorId, booking.ServiceDate, cancellationToken);

        var result = booking.Confirm();
        if (result.IsFailure)
        {
            return result;
        }

        await thresholds.RecomputeAsync(day, booking, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
