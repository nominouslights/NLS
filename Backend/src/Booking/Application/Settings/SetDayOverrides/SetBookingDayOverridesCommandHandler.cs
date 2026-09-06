using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Domain.BookingDays;

namespace NorthernLink.Booking.Application.Settings.SetDayOverrides;

/// <summary>
/// Sets one day's minimum/capacity overrides, then recomputes the day's threshold in the
/// SAME transaction — a lowered minimum can confirm the day, a raised one can revert it,
/// under exactly the cancellation-window and Gift-a-Seat rules a booking change follows (a
/// raised minimum inside the window does NOT revert a confirmed day). No in-flight booking
/// exists here, so the recompute reads the saved rows as-is.
/// See <see cref="BookingDayThresholdService"/>.
/// </summary>
public sealed class SetBookingDayOverridesCommandHandler(
    IBookingDayRepository repository,
    BookingDayThresholdService thresholds)
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

        await thresholds.RecomputeAsync(day, inFlight: null, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
