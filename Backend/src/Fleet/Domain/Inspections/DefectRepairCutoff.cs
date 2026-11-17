namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// "No clearing across the repair": a duplicate is cleared only when its inspection was
/// performed STRICTLY BEFORE the repair, because a report made after it may be the fault coming
/// back. This type owns where that line is drawn.
/// <list type="bullet">
/// <item>Manual <see cref="DefectResolutionReason.PreviouslyRepaired"/> — the resolution instant
/// itself (<see cref="ForManualRepair"/>).</item>
/// <item>Work-order completion — the START of the service record's date in the fleet's
/// operating zone (<see cref="ForServiceDate"/>), so a report made on the day of the repair stays
/// open: the record does not say whether it came before or after the mechanic, and open is the
/// safe direction.</item>
/// </list>
/// The fleet operates in Manitoba only (the Trips module's OperatingArea decision), so the
/// operating zone is <see cref="OperatingTimeZoneId"/>. Resolved through the runtime's ICU data,
/// never the machine's own zone.
/// </summary>
public static class DefectRepairCutoff
{
    /// <summary>The fleet's operating time zone (Manitoba).</summary>
    public const string OperatingTimeZoneId = "America/Winnipeg";

    private static readonly TimeZoneInfo OperatingZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingTimeZoneId);

    /// <summary>A hand-recorded repair: reports made before the resolution instant are its duplicates.</summary>
    public static DateTimeOffset ForManualRepair(DateTimeOffset resolvedAtUtc) => resolvedAtUtc;

    /// <summary>
    /// A work-order repair: the start of <paramref name="serviceDate"/>'s operating day, never
    /// later than <paramref name="completedAtUtc"/> (a service record dated in the future must not
    /// sweep up reports made between now and then).
    ///
    /// <c>ServiceRecord.Date</c> is a <see cref="DateTimeOffset"/> on the wire and in the column,
    /// but what it carries is a DATE: the Dispatcher sends <c>yyyy-MM-dd</c>, which arrives as
    /// midnight in whatever offset the server parsed it with. So a value that is exactly midnight
    /// in its own offset is read as that calendar date as written; any other value (the endpoint's
    /// <c>UtcNow</c> default when no date is sent) is a real instant and is converted to the
    /// operating zone to find its day.
    /// </summary>
    public static DateTimeOffset ForServiceDate(DateTimeOffset serviceDate, DateTimeOffset completedAtUtc)
    {
        var start = StartOfOperatingDay(OperatingDayOf(serviceDate));
        return start < completedAtUtc ? start : completedAtUtc;
    }

    /// <summary>The operating-zone calendar day a service date names (see <see cref="ForServiceDate"/>).</summary>
    public static DateOnly OperatingDayOf(DateTimeOffset serviceDate) =>
        serviceDate.TimeOfDay == TimeSpan.Zero
            ? DateOnly.FromDateTime(serviceDate.DateTime)
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(serviceDate, OperatingZone).DateTime);

    /// <summary>The instant <paramref name="day"/> begins in the operating zone (local midnight; Manitoba's DST shifts at 02:00, so midnight always exists).</summary>
    public static DateTimeOffset StartOfOperatingDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, OperatingZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
