using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Confirm;

public sealed class ConfirmBookingCommandHandler(IBookingRepository repository)
    : ICommandHandler<ConfirmBookingCommand>
{
    public async Task<Result> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Confirm();
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
