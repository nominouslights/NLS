using NorthernLink.Shared.HoursOfService;
using static NorthernLink.Shared.HoursOfService.BlockingReason;
using static NorthernLink.Shared.HoursOfService.DutyStatus;

namespace NorthernLink.Shared.Tests.HoursOfService;

/// <summary>
/// The boundary matrix, authored in code and written out to <c>hos-fixtures.json</c> by the
/// update mode. The JSON is the contract (the TS mirror reads it); this file is how the JSON is
/// produced and the place the expectations were worked out by hand against the regulation.
///
/// Conventions: D = 2026-09-14 (Monday, CDT). Base ledger = KnownFrom D-20 with one certified
/// off-duty event at each day start D-20..D-1. Times are seconds elapsed since the local day
/// start ("HH:MM[:SS]"), not wall clock, so DST days read unambiguously.
/// </summary>
public static class HosFixtureCatalogue
{
    public static readonly DateOnly D = new(2026, 9, 14);

    public static IReadOnlyList<FixtureCase> Build()
    {
        var cases = new List<FixtureCase>();
        cases.AddRange(DailyDriving());
        cases.AddRange(DailyOnDuty());
        cases.AddRange(Shift());
        cases.AddRange(DailyOff());
        cases.AddRange(Rest24());
        cases.AddRange(Cycle());
        cases.AddRange(PcCases());
        cases.AddRange(Adverse());
        cases.AddRange(EmergencyCases());
        cases.AddRange(DeferralCases());
        cases.AddRange(OutOfServiceCases());
        cases.AddRange(Records());
        cases.AddRange(Events());
        cases.AddRange(Dst());
        cases.AddRange(Projection());
        cases.AddRange(Bands());
        return cases;
    }

    private static string At(HosRule rule, int dayOffset, DateOnly? baseDay = null) =>
        $"{rule}@{(baseDay ?? D).AddDays(dayOffset):yyyy-MM-dd}";

    // ---- s.12(1) daily driving ---------------------------------------------------------------

    private static IEnumerable<FixtureCase> DailyDriving()
    {
        yield return new Case("dd-01", "13h00m00s driving in a day is lawful but exhausts the day", "s.12(1)")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "19:30", OnDuty).AsOf(0, "19:35")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                RemainingDrivingTodaySeconds = 0, TodayDrivingSeconds = 46800, TightestRemainingSeconds = 0,
            })
            .WithShiftStart(0, "06:00");

        yield return new Case("dd-02", "13h00m01s driving in a day breaches s.12(1) and s.13(1)", "s.12(1)")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "19:30:01", OnDuty).AsOf(0, "19:35")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 46801,
            });

        yield return new Case("dd-03", "A one-hour break does not reset daily driving", "s.12(1)")
            .Base().Ev(0, "05:30", OnDuty).Ev(0, "06:00", Driving).Ev(0, "12:00", OffDuty).Ev(0, "13:00", Driving).Ev(0, "20:00", OnDuty).AsOf(0, "20:05")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 46800, TodayOffDutySeconds = 23400, RemainingDrivingTodaySeconds = 0,
            })
            .WithShiftStart(0, "05:30");

        yield return new Case("dd-04", "13h across midnight: lawful per day, exhausts the shift", "s.12(1) / s.13(1)")
            .Base().Ev(-1, "17:30", OnDuty).Ev(-1, "18:00", Driving).Ev(0, "07:00", OnDuty).AsOf(0, "07:05")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 25200, RemainingDrivingTodaySeconds = 0,
            })
            .WithShiftStart(-1, "17:30");

        yield return new Case("dd-05", "13h01m across midnight breaches the shift limit only", "s.13(1)")
            .Base().Ev(-1, "17:30", OnDuty).Ev(-1, "18:00", Driving).Ev(0, "07:01", OnDuty).AsOf(0, "07:05")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 25260,
            });
    }

    // ---- s.12(2) daily on-duty -----------------------------------------------------------------

    private static IEnumerable<FixtureCase> DailyOnDuty()
    {
        yield return new Case("od-01", "14h00m on duty: lawful, no more driving", "s.12(2)")
            .Base().Ev(0, "05:00", OnDuty).Ev(0, "05:30", Driving).Ev(0, "12:30", OnDuty).AsOf(0, "19:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyOnDutyExhausted, ShiftOnDutyExhausted], Band = HosBand.Over,
                RemainingDrivingTodaySeconds = 0, RemainingOnDutyTodaySeconds = 0,
            });

        yield return new Case("od-02", "Driving after 14h on duty breaches s.12(2) and s.13(2); today's 10h off is also lost", "s.12(2)")
            .Base().Ev(0, "05:00", OnDuty).Ev(0, "05:30", Driving).Ev(0, "12:30", OnDuty).Ev(0, "19:00", Driving).AsOf(0, "19:10")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOnDuty14h, 0), At(HosRule.ShiftOnDuty14h, 0), At(HosRule.DailyOffDuty10h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyOnDutyExhausted, ShiftOnDutyExhausted], Band = HosBand.Over,
            });

        yield return new Case("od-03", "On duty (not driving) beyond 14h is not an on-duty violation (the day's 10h off is lost by 20:00)", "s.12(2)")
            .Base().Ev(0, "05:00", OnDuty).Ev(0, "05:30", Driving).Ev(0, "12:30", OnDuty).AsOf(0, "20:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOffDuty10h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyOnDutyExhausted, ShiftOnDutyExhausted], Band = HosBand.Over,
                RemainingOnDutyTodaySeconds = 0,
            });
    }

    // ---- s.13 shift ------------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> Shift()
    {
        yield return new Case("sh-01", "16h00m elapsed: lawful, window closed", "s.13(3)")
            .Base().Ev(0, "04:00", OnDuty).Ev(0, "04:30", Driving).Ev(0, "08:30", OffDuty).Ev(0, "15:30", Driving).Ev(0, "20:00", OnDuty).AsOf(0, "20:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [ShiftWindowClosed], Band = HosBand.Over,
                RemainingDrivingTodaySeconds = 0, TodayDrivingSeconds = 30600, TodayOffDutySeconds = 39600,
            })
            .WithShiftStart(0, "04:00");

        yield return new Case("sh-02", "16h01m elapsed with driving breaches s.13(3)", "s.13(3)")
            .Base().Ev(0, "04:00", OnDuty).Ev(0, "04:30", Driving).Ev(0, "08:30", OffDuty).Ev(0, "15:30", Driving).Ev(0, "20:01", OnDuty).AsOf(0, "20:01")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.ShiftElapsed16h, 0)],
                CanDriveNow = false, BlockingReasons = [ShiftWindowClosed], Band = HosBand.Over,
            });

        yield return new Case("sh-03", "8h00m off resets the shift (daily limit still applies)", "s.13 / s.14(2)")
            .Base().Ev(0, "00:30", OnDuty).Ev(0, "01:00", Driving).Ev(0, "05:00", OffDuty).Ev(0, "13:00", Driving).Ev(0, "22:00", OnDuty).AsOf(0, "22:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 46800, RemainingDrivingTodaySeconds = 0,
            })
            .WithShiftStart(0, "13:00");

        yield return new Case("sh-04", "7h59m off does not reset the shift", "s.13 / s.14(2)")
            .Base().Ev(0, "00:30", OnDuty).Ev(0, "01:00", Driving).Ev(0, "05:00", OffDuty).Ev(0, "12:59", Driving).Ev(0, "17:00", OnDuty).AsOf(0, "17:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.ShiftElapsed16h, 0)],
                CanDriveNow = false, BlockingReasons = [ShiftWindowClosed], Band = HosBand.Over,
                TodayDrivingSeconds = 28860,
            })
            .WithShiftStart(0, "00:30");

        yield return new Case("sh-05", "An open off run of 8h30m means between shifts", "s.13")
            .Base().Ev(0, "05:30", OnDuty).Ev(0, "06:00", Driving).Ev(0, "10:00", OffDuty).AsOf(0, "18:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, BetweenShifts = true,
                RemainingDrivingTodaySeconds = 32400, RemainingOnDutyTodaySeconds = 34200, CurrentStatus = OffDuty,
            });

        yield return new Case("sh-06", "No 8h run since KnownFrom: shift starts at KnownFrom and records are incomplete", "s.13 / s.86")
            .KnownFrom(0, "00:00").Ev(0, "00:00", OnDuty).Ev(0, "00:30", Driving).AsOf(0, "05:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, 0)],
                CanDriveNow = false, BlockingReasons = [RecordsIncomplete], Band = HosBand.Over,
                CurrentStatus = Driving, TodayDrivingSeconds = 16200, CycleRecordsIncomplete = true,
            })
            .WithShiftStart(0, "00:00");
    }

    // ---- s.14 daily off --------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> DailyOff()
    {
        yield return new Case("off-01", "Exactly 10h off in a completed day", "s.14")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "14:30", OnDuty).Ev(-1, "20:00", OffDuty).AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, BetweenShifts = true,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), DrivingSeconds = 28800, OffDutySeconds = 36000 },
            });

        yield return new Case("off-02", "9h59m off in a completed day breaches s.14", "s.14")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "14:30", OnDuty).Ev(-1, "20:01", OffDuty).AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOffDuty10h, -1)],
                CanDriveNow = true, Band = HosBand.Ontime,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), OffDutySeconds = 35940 },
            });

        yield return new Case("off-03", "A 29-minute break does not count toward the 10h", "s.14(3)")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "10:00", OffDuty).Ev(-1, "10:29", Driving)
            .Ev(-1, "14:00", OnDuty).Ev(-1, "20:30", OffDuty).AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOffDuty10h, -1)],
                CanDriveNow = true, Band = HosBand.Ontime,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), DrivingSeconds = 25260, OffDutySeconds = 34200 },
            });

        yield return new Case("off-04", "A 30-minute break counts toward the 10h", "s.14(3)")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "10:00", OffDuty).Ev(-1, "10:30", Driving)
            .Ev(-1, "14:00", OnDuty).Ev(-1, "20:30", OffDuty).AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), DrivingSeconds = 25200, OffDutySeconds = 36000 },
            });

        yield return new Case("off-05", "An off run spanning midnight is one run; its 15 minutes before midnight count", "s.14")
            .Base().Ev(-1, "09:45", OnDuty).Ev(-1, "10:15", Driving).Ev(-1, "18:00", OnDuty).Ev(-1, "23:45", OffDuty).AsOf(0, "09:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, BetweenShifts = true,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), DrivingSeconds = 27900, OffDutySeconds = 36000 },
            });

        yield return new Case("off-06", "Today's shortfall is reported, not a violation, while it can still be made up", "s.14")
            .Base().Ev(0, "00:00", OnDuty).Ev(0, "00:30", Driving).Ev(0, "08:30", OnDuty).AsOf(0, "10:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                TodayOffDutySeconds = 0, TodayOffDutyShortfallSeconds = 36000, RemainingDrivingTodaySeconds = 14400,
            });

        yield return new Case("off-07", "Today's 10h becomes a violation the moment it can no longer be achieved", "s.14")
            .Base().Ev(0, "02:00", OnDuty).Ev(0, "02:30", Driving).Ev(0, "10:30", OnDuty).Ev(0, "16:00", OffDuty).Ev(0, "19:00", OnDuty).AsOf(0, "19:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOffDuty10h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyOnDutyExhausted, ShiftOnDutyExhausted, ShiftWindowClosed], Band = HosBand.Over,
                TodayOffDutySeconds = 18000, RemainingDrivingTodaySeconds = 0,
            });
    }

    // ---- s.25 24h in 14 days -------------------------------------------------------------------

    private static Case LightDays(Case c, int from, int to)
    {
        for (var k = from; k <= to; k++)
        {
            c.Ev(k, "08:00", OnDuty).Ev(k, "08:30", Driving).Ev(k, "12:30", OnDuty).Ev(k, "14:00", OffDuty).Certify(k);
        }

        return c;
    }

    private static IEnumerable<FixtureCase> Rest24()
    {
        yield return new Case("r24-01", "Exactly 24h of the last off run inside the 336h window: satisfied, expiring now", "s.25")
            .Base(-20, -15).Apply(c => LightDays(c, -14, -2)).Ev(-1, "08:00", OnDuty).AsOf(-1, "08:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Soon, TightestRemainingSeconds = 0,
                Rest24ExpiresAtUnix = Case.T(-1, "08:00"), CurrentStatus = OnDuty, BetweenShifts = false,
            });

        yield return new Case("r24-02", "23h59m of off time in the window: blocked, no violation while not driving", "s.25")
            .Base(-20, -15).Apply(c => LightDays(c, -14, -2)).Ev(-1, "08:00", OnDuty).AsOf(-1, "08:01")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [Rest24Expired], Band = HosBand.Over,
            });

        yield return new Case("r24-03", "Driving once the 24h entitlement has lapsed breaches s.25", "s.25")
            .Base(-20, -15).Apply(c => LightDays(c, -14, -2)).Ev(-1, "07:30", OnDuty).Ev(-1, "08:00", Driving).AsOf(-1, "08:10")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.Rest24hIn14d, -1)],
                CanDriveNow = false, BlockingReasons = [Rest24Expired], Band = HosBand.Over,
            });

        yield return new Case("r24-04", "A fresh 42h off run restores the entitlement and resets the cycle", "s.25 / s.28")
            .Base(-20, -15).Apply(c => LightDays(c, -14, -4)).Ev(-3, "00:00", OffDuty).Certify(-3)
            .Apply(c => LightDays(c, -2, -1)).Ev(0, "08:00", OnDuty).AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Rest24ExpiresAtUnix = Case.T(11, "08:00"), CycleOnDutySeconds = 43200, CycleLastResetEndUnix = Case.T(-2, "08:00"),
            });

        yield return new Case("r24-05", "Window reaching before KnownFrom with no qualifying run: records incomplete, not a s.25 breach", "s.25 / s.86")
            .KnownFrom(-10, "00:00").Apply(c => LightDays(c, -10, -1)).Ev(0, "08:00", OnDuty).Ev(0, "08:30", Driving).AsOf(0, "09:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, 0)],
                CanDriveNow = false, BlockingReasons = [RecordsIncomplete], Band = HosBand.Over,
            });
    }

    // ---- s.26 / s.28 cycle -------------------------------------------------------------------------

    private static Case HeavyDays(Case c, int from, int to)
    {
        for (var k = from; k <= to; k++)
        {
            c.Ev(k, "06:00", OnDuty).Ev(k, "06:30", Driving).Ev(k, "14:30", OnDuty).Ev(k, "16:00", OffDuty).Certify(k);
        }

        return c;
    }

    private static IEnumerable<FixtureCase> Cycle()
    {
        yield return new Case("cy-01", "70h00m on duty in 7 days: lawful, cycle exhausted", "s.26")
            .Base(-20, -7).Apply(c => HeavyDays(c, -6, -1)).Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "14:30", OnDuty).AsOf(0, "16:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [CycleExhausted], Band = HosBand.Over, CycleOnDutySeconds = 252000,
            });

        yield return new Case("cy-02", "Driving past 70h in 7 days breaches s.26", "s.26")
            .Base(-20, -7).Apply(c => HeavyDays(c, -6, -1)).Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "14:30", OnDuty).Ev(0, "16:00", Driving).AsOf(0, "16:05")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.Cycle1_70hIn7d, 0)],
                CanDriveNow = false, BlockingReasons = [CycleExhausted], Band = HosBand.Over, CycleOnDutySeconds = 252300,
            });

        yield return new Case("cy-03", "The oldest day rolls off the 7-day window", "s.26")
            .Base(-20, -8).Apply(c => HeavyDays(c, -7, -1)).Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).AsOf(0, "07:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, CycleOnDutySeconds = 221400, RemainingDrivingTodaySeconds = 30600,
            });

        yield return new Case("cy-04", "36h00m off resets the cycle", "s.28")
            .Base(-20, -10).Apply(c => HeavyDays(c, -9, -4)).Ev(-3, "00:00", OffDuty).Certify(-3)
            .Ev(-2, "04:00", OnDuty).Ev(-2, "04:30", Driving).Ev(-2, "12:30", OnDuty).Ev(-2, "14:00", OffDuty).Certify(-2)
            .Apply(c => HeavyDays(c, -1, -1)).Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "14:30", OnDuty).AsOf(0, "16:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                CycleOnDutySeconds = 108000, CycleLastResetEndUnix = Case.T(-2, "04:00"), RemainingDrivingTodaySeconds = 14400,
            });

        yield return new Case("cy-05", "35h59m off does not reset the cycle", "s.28")
            .Base(-20, -10).Apply(c => HeavyDays(c, -9, -4)).Ev(-3, "00:00", OffDuty).Certify(-3)
            .Ev(-2, "03:59", OnDuty).Ev(-2, "04:30", Driving).Ev(-2, "12:30", OnDuty).Ev(-2, "14:00", OffDuty).Certify(-2)
            .Apply(c => HeavyDays(c, -1, -1)).Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "14:30", OnDuty).AsOf(0, "16:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                CycleOnDutySeconds = 216060, CycleLastResetEndUnix = Case.T(-9, "06:00"), RemainingDrivingTodaySeconds = 14400,
            });

        yield return new Case("cy-06", "Declared other-carrier hours count toward the cycle", "s.26 / s.82(1)(f)")
            .KnownFrom(-20, "00:00")
            .Apply(c =>
            {
                for (var k = -20; k <= -7; k++) c.OtherCarrier(k, 0, 0);
                for (var k = -6; k <= -1; k++) c.OtherCarrier(k, 36000, 28800);
            })
            .Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "14:30", OnDuty).AsOf(0, "16:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [CycleExhausted], Band = HosBand.Over,
                CycleOnDutySeconds = 252000, CycleLastResetEndUnix = Case.T(-6, "00:00"), Rest24ExpiresAtUnix = Case.T(7, "00:00"),
            })
            .WithShiftStart(0, "06:00");

        yield return new Case("cy-07", "Cycle window reaching before KnownFrom: records incomplete", "s.26 / s.86")
            .KnownFrom(-3, "00:00").Apply(c => LightDays(c, -3, -1)).Ev(0, "08:00", OnDuty).Ev(0, "08:30", Driving).AsOf(0, "09:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, 0)],
                CanDriveNow = false, BlockingReasons = [RecordsIncomplete], Band = HosBand.Over, CycleRecordsIncomplete = true,
            });
    }

    // ---- personal conveyance -----------------------------------------------------------------------

    private static Case PcMorning(Case c) =>
        c.Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "10:30", OffDuty);

    private static IEnumerable<FixtureCase> PcCases()
    {
        yield return new Case("pc-01", "74.9 km of personal conveyance stays personal conveyance", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 100749).AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 749, TodayDrivingSeconds = 14400, TodayOffDutySeconds = 28800,
            });

        yield return new Case("pc-02", "75.0 km exactly stays personal conveyance", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 100750).AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 750, TodayDrivingSeconds = 14400,
            });

        yield return new Case("pc-03", "75.1 km: the remainder after the linear crossing instant is driving", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 100751).AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.PcExceeds75km, 0)],
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 751, TodayDrivingSeconds = 14405, TodayOffDutySeconds = 28795,
            });

        yield return new Case("pc-04", "Personal conveyance without an end odometer is driving", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty).AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.PcOdometerMissing, 0)],
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 0, TodayDrivingSeconds = 18000,
            });

        yield return new Case("pc-05", "An end odometer below the start is treated as missing", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 99000).AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.PcOdometerMissing, 0)],
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 0, TodayDrivingSeconds = 18000,
            });

        yield return new Case("pc-06", "Personal conveyance while out of service is driving while out of service", "s.91")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 100749)
            .Oos(0, "10:45", null, "Roadside inspection").AsOf(0, "12:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.PcWhileOutOfService, 0), At(HosRule.DroveWhileOutOfService, 0)],
                CanDriveNow = false, BlockingReasons = [BlockingReason.OutOfService], Band = HosBand.Over, PcTodayDecikm = 0, TodayDrivingSeconds = 18000,
            });

        yield return new Case("pc-07", "Two personal-conveyance segments accumulate; the second crosses 75 km", "s.1")
            .Apply(PcMorning).Ev(0, "11:00", PersonalConveyance, odometer: 100000).Ev(0, "12:00", OffDuty, odometer: 100400)
            .Ev(0, "13:00", PersonalConveyance, odometer: 100400).Ev(0, "14:00", OffDuty, odometer: 100800).AsOf(0, "14:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.PcExceeds75km, 0)],
                CanDriveNow = true, Band = HosBand.Ontime, PcTodayDecikm = 800, TodayDrivingSeconds = 14850,
            });
    }

    // ---- s.76 adverse driving conditions ---------------------------------------------------------

    private static IEnumerable<FixtureCase> Adverse()
    {
        yield return new Case("adv-01", "Adverse conditions: 15h00m driving is lawful", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "12:00", 7200).AsOf(0, "21:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 54000, ShiftDrivingCeilingSeconds = 54000,
            });

        yield return new Case("adv-02", "Adverse conditions: 15h01m driving breaches", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "12:00", 7200).AsOf(0, "21:31")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
            });

        yield return new Case("adv-03", "An extension over 2h is clamped to 2h", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "12:00", 10800).AsOf(0, "21:31")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                LedgerProblems = ["AdverseExtensionClamped"], ShiftDrivingCeilingSeconds = 54000,
            });

        yield return new Case("adv-04", "Declared outside a shift: ignored and reported", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "03:00", 7200).AsOf(0, "19:31")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.AdverseDeclaredOutsideShift, 0), At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                ShiftDrivingCeilingSeconds = 46800,
            });

        yield return new Case("adv-05", "A second declaration in the same shift is ignored", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "12:00", 3600).Adverse("adv-2", 0, "13:00", 7200).AsOf(0, "20:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                LedgerProblems = ["DuplicateException"], ShiftDrivingCeilingSeconds = 50400, TodayDrivingSeconds = 50400,
            });

        yield return new Case("adv-06", "The extension applies to its own day only; 9h off satisfies the 8h floor", "s.76 / s.14")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "21:00", OffDuty).Adverse("adv-1", -1, "12:00", 7200)
            .Ev(0, "07:00", OnDuty).Ev(0, "07:30", Driving).AsOf(0, "20:31")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                ShiftDrivingCeilingSeconds = 46800,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), DrivingSeconds = 52200, OffDutySeconds = 32400, DailyOffRequiredSeconds = 28800 },
            });

        yield return new Case("adv-07", "The extension follows the shift across midnight; 7h30m off does not end it", "s.76 / s.13")
            .Base().Ev(-1, "06:00", OnDuty).Ev(-1, "06:30", Driving).Ev(-1, "21:00", OffDuty).Adverse("adv-1", -1, "12:00", 7200)
            .Ev(0, "04:30", OnDuty).Ev(0, "05:00", Driving).AsOf(0, "06:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.ShiftDriving13h, 0), At(HosRule.ShiftOnDuty14h, 0), At(HosRule.ShiftElapsed16h, 0)],
                CanDriveNow = false, BlockingReasons = [ShiftDrivingExhausted, ShiftOnDutyExhausted, ShiftWindowClosed], Band = HosBand.Over,
                ShiftDrivingCeilingSeconds = 54000,
            })
            .WithShiftStart(-1, "06:00");
    }

    // ---- emergency ---------------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> EmergencyCases()
    {
        yield return new Case("em-01", "Violations inside an open emergency are suppressed; figures unchanged", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).EmergencyAt("em-1", 0, "18:00", null).AsOf(0, "19:40")
            .Expect(new FixtureExpect
            {
                SuppressedViolations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
            });

        yield return new Case("em-02", "An emergency that ended before the breach suppresses nothing", "s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).EmergencyAt("em-1", 0, "18:00", (0, "19:20")).AsOf(0, "19:40")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyDriving13h, 0), At(HosRule.ShiftDriving13h, 0)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
            });
    }

    // ---- s.16 deferral -------------------------------------------------------------------------------

    private static Case DeferralDayOne(Case c) =>
        c.Base().Ev(-1, "04:00", OnDuty).Ev(-1, "04:30", Driving).Ev(-1, "17:30", OnDuty).Ev(-1, "20:00", OffDuty)
            .DeferralAt("def-1", -1, "20:00", -1, 7200);

    private static IEnumerable<FixtureCase> DeferralCases()
    {
        yield return new Case("def-01", "Deferral day pair: 8h + 12h off, 24h30m driving — lawful", "s.16")
            .Apply(DeferralDayOne).Ev(0, "08:00", OnDuty).Ev(0, "08:30", Driving).Ev(0, "20:00", OffDuty).Certify(0).AsOf(1, "08:00")
            .Expect(new FixtureExpect { CanDriveNow = true, Band = HosBand.Ontime, BetweenShifts = true });

        yield return new Case("def-02", "Day two 11h59m off: deferral fails and day one's 10h is re-emitted", "s.16")
            .Apply(DeferralDayOne).Ev(0, "08:00", OnDuty).Ev(0, "08:30", Driving).Ev(0, "20:01", OffDuty).Certify(0).AsOf(1, "08:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DeferralConditions, 0), At(HosRule.DailyOffDuty10h, -1)],
                CanDriveNow = true, Band = HosBand.Ontime,
            });

        yield return new Case("def-03", "Day two short on both counts: deferral fails, both days breach s.14", "s.16")
            .Apply(DeferralDayOne).Ev(0, "05:59", OnDuty).Ev(0, "06:30", Driving).Ev(0, "17:30", OnDuty).Ev(0, "20:00", OffDuty).Certify(0).AsOf(1, "08:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DeferralConditions, 0), At(HosRule.DailyOffDuty10h, -1), At(HosRule.DailyOffDuty10h, 0)],
                CanDriveNow = true, Band = HosBand.Ontime,
            });

        yield return new Case("def-04", "Day two still in progress: day one passes provisionally", "s.16")
            .Apply(DeferralDayOne).Ev(0, "08:00", OnDuty).Ev(0, "08:30", Driving).AsOf(0, "12:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Day = new FixtureDayExpect { Date = D.AddDays(-1), OffDutySeconds = 28800, DailyOffRequiredSeconds = 28800 },
            });
    }

    // ---- s.91 out of service ---------------------------------------------------------------------

    private static IEnumerable<FixtureCase> OutOfServiceCases()
    {
        yield return new Case("oos-01", "Out of service blocks driving while active", "s.91")
            .Base().Oos(-1, "12:00", (2, "12:00"), "72h out of service").AsOf(0, "10:00")
            .Expect(new FixtureExpect { CanDriveNow = false, BlockingReasons = [BlockingReason.OutOfService], Band = HosBand.Over });

        yield return new Case("oos-02", "Driving inside an out-of-service span is a violation", "s.91")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Oos(0, "07:00", null, "Roadside inspection").AsOf(0, "08:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DroveWhileOutOfService, 0)],
                CanDriveNow = false, BlockingReasons = [BlockingReason.OutOfService], Band = HosBand.Over,
            });
    }

    // ---- s.86 records --------------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> Records()
    {
        yield return new Case("rk-01", "No ledger at all: blocked, band Off", "s.86")
            .KnownFrom(-20, "00:00").AsOf(0, "10:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, 0)],
                CanDriveNow = false, BlockingReasons = [NoLedger, RecordsIncomplete], Band = HosBand.Off,
            });

        yield return new Case("rk-02", "KnownFrom exactly 336h ago: records complete", "s.86")
            .KnownFrom(-14, "10:00").Ev(-14, "10:00", OffDuty).Base(-13, -1)
            .Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "09:00", OnDuty).AsOf(0, "10:00")
            .Expect(new FixtureExpect { CanDriveNow = true, Band = HosBand.Ontime, RemainingDrivingTodaySeconds = 36000 });

        yield return new Case("rk-03", "KnownFrom one second inside 336h: records incomplete", "s.86")
            .KnownFrom(-14, "10:00:01").Ev(-14, "10:00", OffDuty).Base(-13, -1)
            .Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "09:00", OnDuty).AsOf(0, "10:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, 0)],
                CanDriveNow = false, BlockingReasons = [RecordsIncomplete], Band = HosBand.Over,
            });
    }

    // ---- event normalisation -----------------------------------------------------------------------

    private static IEnumerable<FixtureCase> Events()
    {
        yield return new Case("ev-01", "Unsorted input; an event 301s in the future is dropped", "normalisation")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "10:05:01", Driving).AsOf(0, "10:00").ReverseEvents()
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, LedgerProblems = ["FutureEvent"], CurrentStatus = Driving, TodayDrivingSeconds = 12600,
            });

        yield return new Case("ev-02", "An event exactly 300s in the future is kept but not yet in effect", "normalisation")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Ev(0, "10:05", OnDuty).AsOf(0, "10:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, LedgerProblems = [], CurrentStatus = Driving, TodayDrivingSeconds = 12600,
            });

        yield return new Case("ev-03", "Same instant, different source and status: highest sequence wins, conflict reported", "s.82")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:00", Driving, EventSource.EldTranscription).AsOf(0, "07:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.ConflictingSources, 0)],
                CanDriveNow = true, Band = HosBand.Ontime, CurrentStatus = Driving, TodayDrivingSeconds = 3600,
            });

        yield return new Case("ev-04", "A completed day with no record in the last 14 blocks driving", "s.86")
            .Base().DropDay(-5).Ev(0, "06:00", OnDuty).AsOf(0, "07:00")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.RecordsIncomplete14d, -5)],
                CanDriveNow = false, BlockingReasons = [RecordsIncomplete], Band = HosBand.Over,
            });
    }

    // ---- DST -----------------------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> Dst()
    {
        var spring = new DateOnly(2026, 3, 8);
        yield return new Case("dst-01", "Spring forward (23h day): 13h of real driving is 13h, not 14h of wall clock; only 9h30m of the short day is left for off duty", "s.12(1)", spring)
            .Base().Ev(0, "01:00", OnDuty).Ev(0, "01:30", Driving).AsOf(0, "14:30")
            .Expect(new FixtureExpect
            {
                Violations = [At(HosRule.DailyOffDuty10h, 0, spring)],
                CanDriveNow = false, BlockingReasons = [DailyDrivingExhausted, ShiftDrivingExhausted], Band = HosBand.Over,
                TodayDrivingSeconds = 46800, RemainingDrivingTodaySeconds = 0,
            });

        var fall = new DateOnly(2026, 11, 1);
        yield return new Case("dst-02", "Fall back (25h day): the extra hour counts toward the day's 10h off", "s.14", fall)
            .Base().Ev(0, "00:00", OnDuty).Ev(0, "00:30", Driving).Ev(0, "13:30", OnDuty).Ev(0, "15:00", OffDuty).Certify(0).AsOf(1, "08:00")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, BetweenShifts = true,
                Day = new FixtureDayExpect { Date = fall, DrivingSeconds = 46800, OffDutySeconds = 36000 },
            });
    }

    // ---- projection --------------------------------------------------------------------------------

    private static PlannedLeg Leg(int day, string time, int drivingSeconds, string? tripId = "trip-1") =>
        new(Case.T(day, time), [new PlannedSegment(OnDuty, 1800), new PlannedSegment(Driving, drivingSeconds), new PlannedSegment(OnDuty, 1200)], tripId);

    private static IEnumerable<FixtureCase> Projection()
    {
        yield return new Case("pj-01", "Rested driver, 10h planned driving: feasible with margin", "projection")
            .Base().AsOf(0, "08:00").Plan(new TripPlan([Leg(0, "08:00", 36000)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Projection = new FixtureProjectionExpect
                {
                    Feasible = true, RequiredOffBeforeStartSeconds = 0, MarginAppliedSeconds = 7500,
                    DailyOffStillNeededAfterPlanSeconds = 7200, DailyOffAchievableAfterPlan = true,
                },
            });

        yield return new Case("pj-02", "11h planned driving exceeds 13h with margin; no rest fixes it", "projection")
            .Base().AsOf(0, "08:00").Plan(new TripPlan([Leg(0, "08:00", 39600)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Projection = new FixtureProjectionExpect { Feasible = false, FirstBreach = At(HosRule.DailyDriving13h, 0), RequiredOffIsNull = true },
            });

        yield return new Case("pj-03", "Margin edge: driving that lands exactly on 13h00m00s is feasible", "projection")
            .Base().AsOf(0, "08:00").Plan(new TripPlan([Leg(0, "08:00", 39273)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Projection = new FixtureProjectionExpect { Feasible = true, RequiredOffBeforeStartSeconds = 0, MarginAppliedSeconds = 7827 },
            });

        yield return new Case("pj-04", "Margin edge: one more second of planned driving breaches", "projection")
            .Base().AsOf(0, "08:00").Plan(new TripPlan([Leg(0, "08:00", 39274)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Projection = new FixtureProjectionExpect { Feasible = false, FirstBreach = At(HosRule.DailyDriving13h, 0), RequiredOffIsNull = true },
            });

        yield return new Case("pj-05", "End-of-day rule: the plan leaves no room for today's 10h off; 8h rest first fixes it", "projection / s.14")
            .Base().Ev(0, "00:00", OnDuty).Ev(0, "00:30", Driving).Ev(0, "03:30", OffDuty).Ev(0, "03:55", Driving).Ev(0, "06:55", OffDuty)
            .Ev(0, "07:20", Driving).Ev(0, "10:20", OffDuty).Ev(0, "10:45", OnDuty).AsOf(0, "11:15")
            .Plan(new TripPlan([Leg(0, "11:15", 3600)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, TodayOffDutySeconds = 0,
                Projection = new FixtureProjectionExpect
                {
                    Feasible = false, FirstBreach = At(HosRule.DailyOffDuty10h, 0), RequiredOffBeforeStartSeconds = 28800,
                    MarginAppliedSeconds = 4260, DailyOffStillNeededAfterPlanSeconds = 36000, DailyOffAchievableAfterPlan = false,
                },
            });

        yield return new Case("pj-06", "Overlapping legs are rejected as a plan problem", "projection")
            .Base().AsOf(0, "08:00").Plan(new TripPlan([Leg(0, "08:00", 3600), Leg(0, "08:30", 3600, "trip-2")], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime,
                Projection = new FixtureProjectionExpect { Feasible = false, Problems = ["InvalidPlan"] },
            });

        yield return new Case("pj-07", "A declared adverse extension is never projected forward (the day's 8h off floor still applies, so 2h rest first suffices)", "projection / s.76")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).Adverse("adv-1", 0, "12:00", 7200).AsOf(0, "14:00")
            .Plan(new TripPlan([Leg(0, "14:00", 18000)], null))
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Ontime, ShiftDrivingCeilingSeconds = 54000,
                Projection = new FixtureProjectionExpect { Feasible = false, FirstBreach = At(HosRule.DailyOnDuty14h, 0), RequiredOffBeforeStartSeconds = 7200 },
            });
    }

    // ---- bands ---------------------------------------------------------------------------------------

    private static IEnumerable<FixtureCase> Bands()
    {
        yield return new Case("bd-01", "Exactly 2h of driving left is the Soon band", "bands")
            .Base().Ev(0, "06:00", OnDuty).Ev(0, "06:30", Driving).AsOf(0, "17:30")
            .Expect(new FixtureExpect
            {
                CanDriveNow = true, Band = HosBand.Soon, RemainingDrivingTodaySeconds = 7200, TightestRemainingSeconds = 7200,
            });
    }

    /// <summary>Fluent builder for one case; <see cref="Build"/> yields the JSON-shaped record.</summary>
    public sealed class Case
    {
        private static readonly HosCalendar Calendar = new();
        private static DateOnly _baseDay = D;

        private readonly string _id;
        private readonly string _title;
        private readonly string _regulation;
        private readonly DateOnly _day;
        private readonly List<DutyEvent> _events = [];
        private readonly List<OtherCarrierDay> _otherCarrier = [];
        private readonly List<HosException> _exceptions = [];
        private readonly SortedSet<DateOnly> _certified = [];
        private readonly List<OutOfService> _oos = [];
        private long _knownFrom;
        private long _asOf;
        private long _sequence = 1;
        private TripPlan? _plan;
        private FixtureExpect _expect = new();

        public Case(string id, string title, string regulation, DateOnly? day = null)
        {
            _id = id;
            _title = title;
            _regulation = regulation;
            _day = day ?? D;
            _baseDay = _day;
            _knownFrom = T(-20, "00:00");
        }

        /// <summary>Unix instant <paramref name="time"/> ("HH:MM[:SS]") after the start of day <paramref name="dayOffset"/>.</summary>
        public static long T(int dayOffset, string time)
        {
            var parts = time.Split(':');
            var seconds = int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + (parts.Length > 2 ? int.Parse(parts[2]) : 0);
            return Calendar.DayStartUnix(_baseDay.AddDays(dayOffset)) + seconds;
        }

        public Case KnownFrom(int day, string time)
        {
            _knownFrom = T(day, time);
            return this;
        }

        /// <summary>One certified off-duty event at the start of each day in the range.</summary>
        public Case Base(int from = -20, int to = -1)
        {
            for (var k = from; k <= to; k++)
            {
                Ev(k, "00:00", OffDuty);
                Certify(k);
            }

            return this;
        }

        public Case Ev(int day, string time, DutyStatus status, EventSource source = EventSource.DriverApp, int? odometer = null)
        {
            _events.Add(new DutyEvent(T(day, time), status, source, _sequence++, odometer, null));
            return this;
        }

        public Case Certify(int day)
        {
            _certified.Add(_day.AddDays(day));
            return this;
        }

        public Case DropDay(int day)
        {
            var date = _day.AddDays(day);
            _events.RemoveAll(e => Calendar.DayOf(e.AtUnix) == date);
            _certified.Remove(date);
            return this;
        }

        public Case OtherCarrier(int day, int onDuty, int driving)
        {
            _otherCarrier.Add(new OtherCarrierDay(_day.AddDays(day), onDuty, driving, "Acme Freight"));
            return this;
        }

        public Case Adverse(string id, int day, string time, int extension)
        {
            _exceptions.Add(new AdverseDrivingConditions(id, T(day, time), null, "Blizzard on PR 391", extension));
            return this;
        }

        public Case EmergencyAt(string id, int day, string time, (int Day, string Time)? ended)
        {
            _exceptions.Add(new Emergency(id, T(day, time), null, "Medical evacuation", ended is { } e ? T(e.Day, e.Time) : null));
            return this;
        }

        public Case DeferralAt(string id, int declaredDay, string declaredTime, int dayOne, int deferred)
        {
            _exceptions.Add(new OffDutyDeferral(id, T(declaredDay, declaredTime), "trip-1", "Late return from Lynn Lake", _day.AddDays(dayOne), deferred, "CO Jane Doe"));
            return this;
        }

        public Case Oos(int day, string time, (int Day, string Time)? until, string reason)
        {
            _oos.Add(new OutOfService(T(day, time), until is { } u ? T(u.Day, u.Time) : null, reason));
            return this;
        }

        public Case AsOf(int day, string time)
        {
            _asOf = T(day, time);
            return this;
        }

        public Case Plan(TripPlan plan)
        {
            _plan = plan;
            return this;
        }

        public Case ReverseEvents()
        {
            _events.Reverse();
            return this;
        }

        public Case Apply(Func<Case, Case> f) => f(this);

        public Case Apply(Action<Case> f)
        {
            f(this);
            return this;
        }

        public Case Expect(FixtureExpect expect)
        {
            _expect = expect;
            return this;
        }

        public Case WithShiftStart(int day, string time)
        {
            _expect.ShiftStartUnix = T(day, time);
            _expect.BetweenShifts = false;
            return this;
        }

        public static implicit operator FixtureCase(Case c) => c.Build();

        public FixtureCase Build() => new()
        {
            Id = _id,
            Title = _title,
            Regulation = _regulation,
            Calendar = new FixtureCalendar(),
            AsOfUnix = _asOf,
            Ledger = new DutyLedger("driver-1", _knownFrom, _events.ToList(), _otherCarrier.ToList(), _exceptions.ToList(), _certified.ToList(), _oos.ToList(), []),
            Plan = _plan,
            Expect = _expect,
        };
    }
}
