namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// The Cycle 1 limits of M.R. 72/2007 (adopting SOR/2005-313), in integer seconds. All limits are
/// inclusive: exactly 13h00m00s of driving is lawful, one second more is not. Cycle 2, sleeper
/// berths and the 160 km radius exemption are deliberately not modelled.
/// </summary>
public static class HosLimits
{
    /// <summary>s.12(1): driving in a day.</summary>
    public const int DailyDrivingSeconds = 13 * 3600;

    /// <summary>s.12(2): no driving after 14h on duty in a day.</summary>
    public const int DailyOnDutySeconds = 14 * 3600;

    /// <summary>s.13(1): driving in a shift (since the last 8h-consecutive off run).</summary>
    public const int ShiftDrivingSeconds = 13 * 3600;

    /// <summary>s.13(2): no driving after 14h on duty in a shift.</summary>
    public const int ShiftOnDutySeconds = 14 * 3600;

    /// <summary>s.13(3): no driving after 16h have elapsed since the shift began.</summary>
    public const int ShiftElapsedSeconds = 16 * 3600;

    /// <summary>s.14(2): the mandatory consecutive block that ends a shift.</summary>
    public const int MandatoryConsecutiveOffSeconds = 8 * 3600;

    /// <summary>s.14(1): off-duty time required in a day.</summary>
    public const int DailyOffSeconds = 10 * 3600;

    /// <summary>s.14(3): the two hours beyond the mandatory block.</summary>
    public const int ExtraOffSeconds = 2 * 3600;

    /// <summary>s.14(3): off-duty blocks shorter than this do not count toward the 10h.</summary>
    public const int MinOffBlockSeconds = 30 * 60;

    /// <summary>s.25: 24 consecutive hours off…</summary>
    public const int Rest24Seconds = 24 * 3600;

    /// <summary>…within the preceding 14 days (a rolling 336h window).</summary>
    public const int Rest24WindowSeconds = 14 * 24 * 3600;

    /// <summary>s.26: Cycle 1 on-duty ceiling…</summary>
    public const int CycleOnDutySeconds = 70 * 3600;

    /// <summary>…over any 7 consecutive days.</summary>
    public const int CycleDays = 7;

    /// <summary>s.28: 36 consecutive hours off resets the cycle.</summary>
    public const int CycleResetSeconds = 36 * 3600;

    /// <summary>s.76: adverse driving conditions extend driving/on-duty/elapsed by at most 2h.</summary>
    public const int AdverseMaxExtensionSeconds = 2 * 3600;

    /// <summary>s.16: at most 2h of off-duty time may be deferred to the next day.</summary>
    public const int DeferralMaxSeconds = 2 * 3600;

    /// <summary>s.16(1)(c): the two days together must contain at least 20h off.</summary>
    public const int DeferralTwoDayOffMinSeconds = 20 * 3600;

    /// <summary>s.16(1)(b): the two days together may contain at most 26h driving.</summary>
    public const int DeferralTwoDayDrivingMaxSeconds = 26 * 3600;

    /// <summary>s.1 "personal use": 75.0 km per day, in tenths of a kilometre.</summary>
    public const int PcDailyLimitDecikm = 750;

    /// <summary>An event more than this far after "now" is dropped as a clock-skew artefact.</summary>
    public const int FutureSkewSeconds = 300;
}

/// <summary>Where the "soon" band begins. Callers may pass a tenant policy value.</summary>
public sealed record HosBands(int SoonSeconds = 7200);

/// <summary>
/// Planning margin applied by <see cref="HosEngine.Project"/>: every planned driving and on-duty
/// segment is scaled by <paramref name="Percent"/>, and <paramref name="FixedSeconds"/> is added
/// once per leg to its driving time.
/// </summary>
public sealed record PlanningMargin(int FixedSeconds = 3600, int Percent = 10);
