using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Calendar;
using NorthernLink.Booking.Domain.BookingDays;

namespace NorthernLink.Booking.Application.BookingDays.Guarantee;

/// <summary>
/// Handles <see cref="GuaranteeBookingDayCommand"/>: sets <c>MinimumGuaranteed</c> and, if
/// the day is currently Reverted, re-confirms it — which republishes the confirmation event
/// (Trips' unique booking-day index absorbs the duplicate, so no second trip appears).
/// The seat math is computed here only to stamp an honest snapshot onto the confirmation
/// event; guaranteeing has no threshold precondition of its own.
/// </summary>
public sealed class GuaranteeBookingDayCommandHandler(
    IBookingDayRepository bookingDays,
    IBookingReadService bookingReads,
    ICorridorSettingsRepository corridorSettings,
    IBookingPolicyRepository policies,
    ICorridorLookupRepository corridors,
    TimeProvider clock)
    : ICommandHandler<GuaranteeBookingDayCommand>
{
    public async Task<Result> Handle(GuaranteeBookingDayCommand command, CancellationToken cancellationToken)
    {
        var day = await bookingDays.GetByIdAsync(command.BookingDayId, cancellationToken);
        if (day is null)
        {
            return Result.Failure(BookingDayErrors.NotFound);
        }

        var settings = await corridorSettings.GetByCorridorAsync(day.CorridorId, cancellationToken);
        var policy = await policies.GetAsync(cancellationToken);
        var rows = await bookingReads.GetSeatRowsAsync(
            day.CorridorId, day.ServiceDate, day.ServiceDate, cancellationToken);

        var capacity = SeatMath.ResolveCapacity(day.SeatCapacityOverride, settings?.SeatCapacity, policy);
        var minimum = SeatMath.ResolveMinimum(day.PassengerMinimumOverride, settings?.PassengerMinimum, policy);
        var seats = SeatMath.Compute(rows, clock.GetUtcNow(), capacity, minimum);

        var corridor = await corridors.GetAsync(day.CorridorId, cancellationToken);
        var result = day.Guarantee(new BookingDayConfirmationSnapshot(
            CorridorName: corridor?.Name ?? string.Empty,
            Origin: corridor?.Origin ?? string.Empty,
            Destination: corridor?.Destination ?? string.Empty,
            SeatsSold: seats.Sold,
            SeatsMinimum: minimum,
            SeatCapacity: capacity));

        if (result.IsFailure)
        {
            return result;
        }

        await bookingDays.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
