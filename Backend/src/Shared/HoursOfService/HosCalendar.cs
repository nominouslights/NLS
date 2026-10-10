namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// Maps Unix instants to the regulation's "day" (s.1: a 24-hour period starting at the time the
/// carrier designates, local time). Resolves the IANA zone through the runtime's ICU data — never
/// the machine's own zone. DST days are 23 or 25 hours long; the engine splits pieces at the
/// boundaries this calendar reports, so nothing else needs to know.
/// </summary>
public sealed class HosCalendar
{
    public const string DefaultTimeZoneId = "America/Winnipeg";

    private readonly TimeZoneInfo _zone;

    public HosCalendar(string timeZoneId = DefaultTimeZoneId, int dayStartSecondsLocal = 0)
    {
        if (dayStartSecondsLocal is < 0 or >= 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(dayStartSecondsLocal), "Day start must be within one local day.");
        }

        TimeZoneId = timeZoneId;
        DayStartSecondsLocal = dayStartSecondsLocal;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public string TimeZoneId { get; }

    public int DayStartSecondsLocal { get; }

    /// <summary>The regulation day containing <paramref name="unix"/>.</summary>
    public DateOnly DayOf(long unix)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unix), _zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var secondsIntoLocalDay = (int)local.TimeOfDay.TotalSeconds;
        return secondsIntoLocalDay < DayStartSecondsLocal ? date.AddDays(-1) : date;
    }

    /// <summary>The instant <paramref name="date"/> begins.</summary>
    public long DayStartUnix(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddSeconds(DayStartSecondsLocal);
        if (_zone.IsInvalidTime(local))
        {
            // The day start falls inside a spring-forward gap: the first valid instant after it.
            local = local.AddHours(1);
        }

        var offset = _zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUnixTimeSeconds();
    }

    /// <summary>The instant <paramref name="date"/> ends (the next day's start).</summary>
    public long DayEndUnix(DateOnly date) => DayStartUnix(date.AddDays(1));
}
