namespace NorthernLink.Shared.HoursOfService;

/// <summary>English prose for each rule, built server-side so every app shows the same sentence.</summary>
public static class HosMessages
{
    public static string For(HosRule rule, int figure, int limit) => rule switch
    {
        HosRule.DailyDriving13h =>
            $"Driving {Duration(figure)} exceeds the {Duration(limit)} daily limit (s.12(1)).",
        HosRule.DailyOnDuty14h =>
            $"Drove after {Duration(figure)} on duty in the day; the limit is {Duration(limit)} (s.12(2)).",
        HosRule.ShiftDriving13h =>
            $"Driving {Duration(figure)} in the shift exceeds the {Duration(limit)} limit (s.13(1)).",
        HosRule.ShiftOnDuty14h =>
            $"Drove after {Duration(figure)} on duty in the shift; the limit is {Duration(limit)} (s.13(2)).",
        HosRule.ShiftElapsed16h =>
            $"Drove {Duration(figure)} after the shift began; the window is {Duration(limit)} (s.13(3)).",
        HosRule.DailyOffDuty10h =>
            $"Only {Duration(figure)} off duty in the day; {Duration(limit)} is required (s.14).",
        HosRule.Rest24hIn14d =>
            $"No 24h consecutive off-duty period in the preceding 14 days (longest {Duration(figure)}) (s.25).",
        HosRule.Cycle1_70hIn7d =>
            $"On duty {Duration(figure)} in 7 days exceeds the {Duration(limit)} Cycle 1 limit (s.26).",
        HosRule.DeferralConditions =>
            "The off-duty deferral's day-two conditions were not met (s.16).",
        HosRule.PcExceeds75km =>
            $"Personal conveyance {Km(figure)} km exceeds the {Km(limit)} km daily limit; the remainder counts as driving (s.1).",
        HosRule.PcOdometerMissing =>
            "Personal conveyance without start and end odometer readings counts as driving (s.1).",
        HosRule.PcWhileOutOfService =>
            "Personal conveyance while out of service counts as driving (s.91).",
        HosRule.DroveWhileOutOfService =>
            $"Drove {Duration(figure)} while out of service (s.91).",
        HosRule.RodsNotCertified =>
            "The day's record of duty status has not been certified (s.84).",
        HosRule.RecordsIncomplete14d =>
            "Records for the preceding 14 days are incomplete; driving is not permitted until they are (s.86).",
        HosRule.AdverseDeclaredOutsideShift =>
            "Adverse driving conditions were declared outside a shift and were ignored (s.76).",
        HosRule.ConflictingSources =>
            "Two sources recorded different statuses at the same instant (s.82).",
        _ => rule.ToString(),
    };

    public static string Duration(int seconds)
    {
        var negative = seconds < 0;
        var s = Math.Abs(seconds);
        var hours = s / 3600;
        var minutes = s % 3600 / 60;
        var rest = s % 60;
        var text = rest == 0 ? $"{hours}h {minutes}m" : $"{hours}h {minutes}m {rest}s";
        return negative ? "-" + text : text;
    }

    private static string Km(int decikm) => $"{decikm / 10}.{decikm % 10}";
}
