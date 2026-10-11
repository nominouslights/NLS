namespace NorthernLink.Compliance.Infrastructure.Retention;

/// <summary>
/// Bound from the <c>Compliance:Retention</c> configuration section (non-secret tuning values only).
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Compliance:Retention";

    /// <summary>Local wall-clock time of the nightly sweep, "HH:mm".</summary>
    public string RunAtLocalTime { get; init; } = "02:00";

    /// <summary>IANA time zone the sweep is scheduled in — the carrier's, not the host's.</summary>
    public string TimeZone { get; init; } = "America/Winnipeg";

    /// <summary>How far ahead the due-for-disposal list looks by default.</summary>
    public int ReviewWindowDays { get; init; } = 30;

    /// <summary>Delay before the worker computes its first schedule — skips the boot race like every other worker.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(15);

    public TimeOnly RunAt => TimeOnly.TryParseExact(RunAtLocalTime, "HH:mm", out var time) ? time : new TimeOnly(2, 0);

    public TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

/// <summary>Pure scheduling arithmetic for the nightly sweep — testable without a clock.</summary>
public static class RetentionSchedule
{
    /// <summary>
    /// The next instant strictly after <paramref name="nowUtc"/> at which the local wall clock in
    /// <paramref name="timeZone"/> reads <paramref name="runAtLocal"/>. Today's if it has not passed
    /// yet, otherwise tomorrow's; DST transitions resolve through the zone's own offset at that date.
    /// </summary>
    public static DateTimeOffset NextRunAfter(DateTimeOffset nowUtc, TimeOnly runAtLocal, TimeZoneInfo timeZone)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var candidateLocalDate = nowLocal.TimeOfDay < runAtLocal.ToTimeSpan() ? nowLocal.Date : nowLocal.Date.AddDays(1);

        var candidateUnspecified = DateTime.SpecifyKind(candidateLocalDate.Add(runAtLocal.ToTimeSpan()), DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(candidateUnspecified))
        {
            // Spring-forward gap: run an hour later in the same zone.
            candidateUnspecified = candidateUnspecified.AddHours(1);
        }

        var offset = timeZone.GetUtcOffset(candidateUnspecified);
        return new DateTimeOffset(candidateUnspecified, offset).ToUniversalTime();
    }

    /// <summary>Today's date in the sweep's time zone — the "as of" the sweep judges retention against.</summary>
    public static DateOnly TodayIn(DateTimeOffset nowUtc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).DateTime);
}
