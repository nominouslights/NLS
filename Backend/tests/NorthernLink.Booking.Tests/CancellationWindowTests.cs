using NorthernLink.Booking.Application.BookingDays;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The local-time math of the cancellation-window rule: service dates are interpreted in
/// America/Winnipeg (CDT −5 in September, CST −6 in January), departure defaults to local
/// midnight, and the cutoff comparison is strict — exactly at the cutoff is already too late.
/// </summary>
public class CancellationWindowTests
{
    [Fact]
    public void Departure_is_local_midnight_in_daylight_time()
    {
        // 2026-09-15 00:00 CDT (UTC-5) == 05:00Z.
        Assert.Equal(
            new DateTimeOffset(2026, 9, 15, 5, 0, 0, TimeSpan.Zero),
            CancellationWindow.DepartureUtc(new DateOnly(2026, 9, 15)));
    }

    [Fact]
    public void Departure_is_local_midnight_in_standard_time()
    {
        // 2026-01-15 00:00 CST (UTC-6) == 06:00Z.
        Assert.Equal(
            new DateTimeOffset(2026, 1, 15, 6, 0, 0, TimeSpan.Zero),
            CancellationWindow.DepartureUtc(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void An_explicit_departure_time_moves_the_instant()
    {
        // 08:00 CDT == 13:00Z.
        Assert.Equal(
            new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero),
            CancellationWindow.DepartureUtc(new DateOnly(2026, 9, 15), new TimeOnly(8, 0)));
    }

    [Theory]
    [InlineData("2026-09-14T16:59:59Z", true)]  // one second before the cutoff — still allowed
    [InlineData("2026-09-14T17:00:00Z", false)] // exactly at the cutoff — too late (strict <)
    [InlineData("2026-09-14T17:00:01Z", false)] // past the cutoff
    [InlineData("2026-09-13T00:00:00Z", true)]  // well before
    [InlineData("2026-09-15T04:00:00Z", false)] // one hour before departure
    public void The_twelve_hour_boundary_is_strict(string nowUtc, bool beforeCutoff)
    {
        // Departure 2026-09-15T05:00Z (local midnight CDT); 12h window → cutoff 09-14T17:00Z.
        var now = DateTimeOffset.Parse(nowUtc);

        Assert.Equal(
            beforeCutoff,
            CancellationWindow.IsBeforeCutoff(new DateOnly(2026, 9, 15), windowHours: 12, now));
    }

    [Fact]
    public void The_window_length_comes_from_the_caller_not_a_constant()
    {
        // A 200h window (the manual-e2e trick from the plan) puts the cutoff 200h out.
        var serviceDate = new DateOnly(2026, 9, 15);
        var cutoff = CancellationWindow.DepartureUtc(serviceDate) - TimeSpan.FromHours(200);

        Assert.True(CancellationWindow.IsBeforeCutoff(serviceDate, 200, cutoff.AddSeconds(-1)));
        Assert.False(CancellationWindow.IsBeforeCutoff(serviceDate, 200, cutoff));
    }
}
