using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Cancel;

/// <summary>
/// Cancels a booking (revert-not-delete — the record stays listed, read-only). This batch
/// records the cancellation only; the cancellation window/penalty from the policy are
/// stored and displayed but not yet enforced (that lands with the public PWA batch).
/// </summary>
public sealed class CancelBookingCommandHandler(IBookingRepository repository)
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

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
