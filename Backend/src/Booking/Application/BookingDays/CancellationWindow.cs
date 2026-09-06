namespace NorthernLink.Booking.Application.BookingDays;

/// <summary>
/// The platform's one piece of local-time math (US-B.10/11's "12-hour rule", though the
/// window is policy-driven, never a hardcoded 12): a Confirmed day that drops below its
/// minimum reverts only while there is still time to tell everyone — strictly before
/// <c>departure − window</c>. Departures are local to Northern Link's service area, so the
/// service date is interpreted in America/Winnipeg (IANA id — resolved via ICU on every OS
/// .NET 10 supports) and converted to UTC for comparison against the injected clock.
/// The departure time-of-day defaults to midnight when unknown: Booking does not store the
/// trip's departure window (the dispatcher sets it on the trip after confirmation), and
/// midnight is the conservative choice — the cutoff can only be earlier than the real one,
/// so a revert is never sent to passengers later than the policy promises.
/// </summary>
public static class CancellationWindow
{
    private static readonly TimeZoneInfo ServiceTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Winnipeg");

    /// <summary>The UTC instant of the (local) departure for a service date.</summary>
    public static DateTimeOffset DepartureUtc(DateOnly serviceDate, TimeOnly? departureTime = null)
    {
        var local = serviceDate.ToDateTime(departureTime ?? TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, ServiceTimeZone), TimeSpan.Zero);
    }

    /// <summary>
    /// True while <paramref name="now"/> is strictly before <c>departure − windowHours</c> —
    /// i.e. a revert is still allowed. At or past the cutoff the day stays Confirmed and the
    /// run happens regardless.
    /// </summary>
    public static bool IsBeforeCutoff(
        DateOnly serviceDate,
        int windowHours,
        DateTimeOffset now,
        TimeOnly? departureTime = null) =>
        now < DepartureUtc(serviceDate, departureTime) - TimeSpan.FromHours(windowHours);
}
