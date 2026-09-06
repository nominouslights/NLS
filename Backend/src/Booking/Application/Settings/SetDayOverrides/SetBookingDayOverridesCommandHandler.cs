using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.BookingDays;

namespace NorthernLink.Booking.Application.Settings.SetDayOverrides;

public sealed class SetBookingDayOverridesCommandHandler(IBookingDayRepository repository)
    : ICommandHandler<SetBookingDayOverridesCommand>
{
    public async Task<Result> Handle(SetBookingDayOverridesCommand command, CancellationToken cancellationToken)
    {
        var day = await repository.GetByIdAsync(command.BookingDayId, cancellationToken);
        if (day is null)
        {
            return Result.Failure(BookingDayErrors.NotFound);
        }

        var result = day.SetOverrides(command.PassengerMinimum, command.SeatCapacity);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
