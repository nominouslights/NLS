using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Calendar;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Settings;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The derived seat math and its resolution order (day override → corridor setting →
/// policy default → built-in constants).
/// </summary>
public class SeatMathTests
{
    private static readonly DateOnly Date = new(2026, 9, 15);
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static BookingSeatRow Row(BookingStatus status, int passengers, bool holdLive = true) =>
        new(Date, status, holdLive ? Now.AddMinutes(10) : Now.AddMinutes(-10), passengers);

    private static BookingPolicy Policy(int minimum = 4, int capacity = 10)
    {
        var policy = BookingPolicy.CreateDefault(Guid.NewGuid());
        policy.Update(
            BookingPolicy.DefaultCancellationWindowHours,
            BookingPolicy.DefaultEarlyCancellationPenaltyCad,
            BookingPolicy.DefaultBookingCutoffHours,
            BookingPolicy.DefaultSeatHoldMinutes,
            minimum,
            capacity);
        return policy;
    }

    [Fact]
    public void Day_override_wins_over_corridor_and_policy()
    {
        Assert.Equal(12, SeatMath.ResolveCapacity(dayOverride: 12, corridorSetting: 9, Policy(capacity: 10)));
        Assert.Equal(5, SeatMath.ResolveMinimum(dayOverride: 5, corridorSetting: 2, Policy(minimum: 4)));
    }

    [Fact]
    public void Corridor_setting_wins_over_policy_when_no_day_override()
    {
        Assert.Equal(9, SeatMath.ResolveCapacity(dayOverride: null, corridorSetting: 9, Policy(capacity: 10)));
        Assert.Equal(2, SeatMath.ResolveMinimum(dayOverride: null, corridorSetting: 2, Policy(minimum: 4)));
    }

    [Fact]
    public void Policy_default_applies_when_no_overrides()
    {
        Assert.Equal(10, SeatMath.ResolveCapacity(dayOverride: null, corridorSetting: null, Policy(capacity: 10)));
        Assert.Equal(4, SeatMath.ResolveMinimum(dayOverride: null, corridorSetting: null, Policy(minimum: 4)));
    }

    [Fact]
    public void Builtin_constants_apply_when_no_policy_row_exists()
    {
        Assert.Equal(
            BookingPolicy.DefaultSeatCapacityValue,
            SeatMath.ResolveCapacity(dayOverride: null, corridorSetting: null, policy: null));
        Assert.Equal(
            BookingPolicy.DefaultPassengerMinimumValue,
            SeatMath.ResolveMinimum(dayOverride: null, corridorSetting: null, policy: null));
    }

    [Fact]
    public void Sold_counts_confirmed_passengers_only()
    {
        var seats = SeatMath.Compute(
            [Row(BookingStatus.Confirmed, 2), Row(BookingStatus.Confirmed, 1), Row(BookingStatus.Unconfirmed, 3)],
            Now, capacity: 10, passengerMinimum: 3);

        Assert.Equal(3, seats.Sold);
    }

    [Fact]
    public void Pending_counts_unconfirmed_with_live_holds_only()
    {
        var seats = SeatMath.Compute(
            [
                Row(BookingStatus.Unconfirmed, 2, holdLive: true),
                Row(BookingStatus.Unconfirmed, 4, holdLive: false), // expired: listed but not reserving
                Row(BookingStatus.Cancelled, 5),
            ],
            Now, capacity: 10, passengerMinimum: 3);

        Assert.Equal(0, seats.Sold);
        Assert.Equal(2, seats.Pending);
    }

    [Fact]
    public void Remaining_is_capacity_minus_sold_minus_pending()
    {
        var seats = SeatMath.Compute(
            [Row(BookingStatus.Confirmed, 4), Row(BookingStatus.Unconfirmed, 3)],
            Now, capacity: 10, passengerMinimum: 3);

        Assert.Equal(3, seats.Remaining);
    }

    [Fact]
    public void Needed_to_confirm_is_minimum_minus_sold_floored_at_zero()
    {
        var below = SeatMath.Compute(
            [Row(BookingStatus.Confirmed, 1), Row(BookingStatus.Unconfirmed, 5)],
            Now, capacity: 10, passengerMinimum: 3);
        Assert.Equal(2, below.NeededToConfirm); // pending never counts toward the minimum

        var met = SeatMath.Compute(
            [Row(BookingStatus.Confirmed, 5)],
            Now, capacity: 10, passengerMinimum: 3);
        Assert.Equal(0, met.NeededToConfirm);
    }
}
