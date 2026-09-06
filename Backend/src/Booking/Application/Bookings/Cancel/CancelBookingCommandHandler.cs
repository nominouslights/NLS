using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Cancel;

/// <summary>
/// Cancels a booking (revert-not-delete — the record stays listed, read-only), then
/// recomputes the day's threshold in the SAME transaction: dropping a Confirmed day below
/// its minimum applies the window rule (revert + notify outside the cancellation window,
/// stay Confirmed inside it; a Gift-a-Seat guarantee suppresses reverting). The policy's
/// cancellation penalty is still stored/displayed only — enforcement lands with the public
/// PWA batch. See <see cref="BookingDayThresholdService"/>.
/// </summary>
public sealed class CancelBookingCommandHandler(
    IBookingRepository repository,
    BookingDayThresholdService thresholds)
    : ICommandHandler<CancelBookingCommand>
{
    public async Task<Result> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Cancel();
        if (result.IsFailure)
        {
            return result;
        }

        await thresholds.ApplyAfterCancelAsync(booking, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
