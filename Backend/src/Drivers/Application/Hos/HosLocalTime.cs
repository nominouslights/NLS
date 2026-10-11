using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Application.Hos;

/// <summary>
/// Local wall-clock ⇄ instant conversion for the ledger's zone (<see cref="HosCalendar.DefaultTimeZoneId"/>).
/// The engine's calendar maps instants to days; a transcription arrives as "HH:mm on a date", so
/// the other direction lives here. A time inside a spring-forward gap moves to the first valid
/// instant after it; an ambiguous fall-back time takes its first occurrence (daylight offset).
/// </summary>
public static class HosLocalTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(HosCalendar.DefaultTimeZoneId);

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var offset = Zone.IsAmbiguousTime(local)
            ? Zone.GetAmbiguousTimeOffsets(local).Max()
            : Zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>Parses a transcription's "HH:mm" (or "HH:mm:ss"); anything else is <see cref="HosErrors.InvalidTime"/>.</summary>
    public static Result<TimeOnly> ParseTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "HH:mm:ss", "H:mm"], null, System.Globalization.DateTimeStyles.None, out var time)
            ? Result.Success(time)
            : Result.Failure<TimeOnly>(HosErrors.InvalidTime);
}
