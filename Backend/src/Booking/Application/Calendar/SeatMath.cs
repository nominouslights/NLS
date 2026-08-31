using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Calendar;

/// <summary>One day's derived seat numbers — computed per read, never stored.</summary>
public sealed record DaySeatSummary(
    int Sold,
    int Pending,
    int Capacity,
    int Remaining,
    int PassengerMinimum,
    int NeededToConfirm);

/// <summary>
/// The seat arithmetic, in one pure place so the month and day queries can never drift:
/// sold = passengers on Confirmed bookings; pending = passengers on Unconfirmed bookings
/// whose hold is still live (expired holds stop reserving but the bookings stay listed);
/// remaining = capacity − sold − pending; needed to confirm = max(0, minimum − sold).
/// Capacity and minimum resolve day override → corridor setting → tenant policy, with the
/// <see cref="BookingPolicy"/> Default* constants as the last resort while no policy row
/// exists yet.
/// </summary>
public static class SeatMath
{
    public static int ResolveCapacity(int? dayOverride, int? corridorSetting, BookingPolicy? policy) =>
        dayOverride ?? corridorSetting ?? policy?.DefaultSeatCapacity ?? BookingPolicy.DefaultSeatCapacityValue;

    public static int ResolveMinimum(int? dayOverride, int? corridorSetting, BookingPolicy? policy) =>
        dayOverride ?? corridorSetting ?? policy?.DefaultPassengerMinimum ?? BookingPolicy.DefaultPassengerMinimumValue;

    public static DaySeatSummary Compute(
        IReadOnlyCollection<BookingSeatRow> bookings,
        DateTimeOffset now,
        int capacity,
        int passengerMinimum)
    {
        var sold = bookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Sum(b => b.PassengerCount);

        var pending = bookings
            .Where(b => b.Status == BookingStatus.Unconfirmed && b.HoldExpiresAtUtc > now)
            .Sum(b => b.PassengerCount);

        return new DaySeatSummary(
            Sold: sold,
            Pending: pending,
            Capacity: capacity,
            Remaining: capacity - sold - pending,
            PassengerMinimum: passengerMinimum,
            NeededToConfirm: Math.Max(0, passengerMinimum - sold));
    }
}
