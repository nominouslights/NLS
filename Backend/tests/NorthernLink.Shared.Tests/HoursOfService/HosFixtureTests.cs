using System.Text.Json;
using System.Text.Json.Nodes;
using NorthernLink.Shared.HoursOfService;
using Xunit;

namespace NorthernLink.Shared.Tests.HoursOfService;

/// <summary>
/// The fixture suite: every case in <c>hos-fixtures.json</c> must satisfy its hand-written
/// <c>expect</c> block AND reproduce its committed <c>snapshot</c> exactly. Run with
/// <c>HOS_FIXTURES_UPDATE=1</c> to regenerate the file from <see cref="HosFixtureCatalogue"/>
/// (snapshots included), then review the diff and commit it. The TypeScript mirror asserts the
/// same snapshots, so a snapshot change here is a contract change for both frontends.
/// </summary>
public class HosFixtureTests
{
    private static readonly Lazy<List<FixtureCase>> Cases = new(LoadOrUpdate);

    private static List<FixtureCase> LoadOrUpdate()
    {
        if (HosFixtureFile.UpdateRequested)
        {
            var generated = HosFixtureCatalogue.Build().ToList();
            foreach (var c in generated)
            {
                c.Snapshot = HosFixtureFile.Snapshot(c);
            }

            HosFixtureFile.Save(HosFixtureFile.SourcePath, generated);
            return generated;
        }

        return HosFixtureFile.Load(HosFixtureFile.OutputPath);
    }

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases.Value)
        {
            data.Add(c.Id);
        }

        return data;
    }

    private static FixtureCase Find(string id) => Cases.Value.Single(c => c.Id == id);

    [Fact]
    public void The_matrix_covers_every_planned_case_id()
    {
        string[] planned =
        [
            .. Range("dd", 5), .. Range("od", 3), .. Range("sh", 6), .. Range("off", 7), .. Range("r24", 5),
            .. Range("cy", 7), .. Range("pc", 7), .. Range("adv", 7), .. Range("em", 2), .. Range("def", 4),
            .. Range("oos", 2), .. Range("rk", 3), .. Range("ev", 4), .. Range("dst", 2), .. Range("pj", 7), "bd-01",
        ];

        Assert.Equal(planned.OrderBy(x => x), Cases.Value.Select(c => c.Id).OrderBy(x => x));

        static IEnumerable<string> Range(string prefix, int count) =>
            Enumerable.Range(1, count).Select(i => $"{prefix}-{i:00}");
    }

    [Fact]
    public void The_committed_file_matches_the_catalogue()
    {
        // The JSON is the contract; the catalogue is how it is produced. If they drift, someone
        // edited one without regenerating the other.
        var fromCatalogue = HosFixtureCatalogue.Build().ToDictionary(c => c.Id);
        foreach (var onDisk in Cases.Value)
        {
            Assert.True(fromCatalogue.TryGetValue(onDisk.Id, out var expected), $"{onDisk.Id} is not in the catalogue.");
            Assert.True(
                JsonNode.DeepEquals(Inputs(expected!), Inputs(onDisk)),
                $"{onDisk.Id}: inputs or expectations differ from the catalogue. Re-run with HOS_FIXTURES_UPDATE=1.");
        }

        static JsonNode Inputs(FixtureCase c) =>
            JsonSerializer.SerializeToNode(new { c.Title, c.Regulation, c.Calendar, c.AsOfUnix, c.Ledger, c.Plan, c.Expect }, HosFixtureFile.Options)!;
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Case_meets_its_expectations(string id)
    {
        var c = Find(id);
        var cal = HosFixtureFile.CalendarOf(c);
        var e = HosEngine.Evaluate(c.Ledger, c.AsOfUnix, cal, new HosBands());
        var x = c.Expect;

        Assert.Equal(x.Violations.OrderBy(v => v), e.Violations.Where(v => !v.Suppressed).Select(Tag).OrderBy(v => v));
        if (x.SuppressedViolations is not null)
        {
            Assert.Equal(x.SuppressedViolations.OrderBy(v => v), e.Violations.Where(v => v.Suppressed).Select(Tag).OrderBy(v => v));
        }

        Assert.Equal(x.CanDriveNow, e.CanDriveNow);
        Assert.Equal(x.BlockingReasons, e.BlockingReasons);
        Assert.Equal(x.Band, e.Band);
        AssertIf(x.CurrentStatus, e.CurrentStatus);
        AssertIf(x.RemainingDrivingTodaySeconds, e.RemainingDrivingTodaySeconds);
        AssertIf(x.RemainingOnDutyTodaySeconds, e.RemainingOnDutyTodaySeconds);
        AssertIf(x.TightestRemainingSeconds, e.TightestRemainingSeconds);
        AssertIf(x.TodayDrivingSeconds, e.Today.DrivingSeconds);
        AssertIf(x.TodayOffDutySeconds, e.Today.OffDutySeconds);
        AssertIf(x.TodayOffDutyShortfallSeconds, e.Today.OffDutyShortfallSeconds);
        AssertIf(x.ShiftStartUnix, e.Shift.StartUnix);
        AssertIf(x.BetweenShifts, e.Shift.StartUnix is null);
        AssertIf(x.ShiftDrivingCeilingSeconds, e.Shift.DrivingCeilingSeconds);
        AssertIf(x.CycleOnDutySeconds, e.Cycle.OnDutySeconds);
        AssertIf(x.CycleLastResetEndUnix, e.Cycle.LastResetEndUnix);
        AssertIf(x.CycleRecordsIncomplete, e.Cycle.RecordsIncomplete);
        AssertIf(x.Rest24ExpiresAtUnix, e.Rest24.ExpiresAtUnix);
        AssertIf(x.PcTodayDecikm, e.PcTodayDecikm);
        if (x.LedgerProblems is not null)
        {
            Assert.Equal(x.LedgerProblems.OrderBy(p => p), e.LedgerProblems.Select(p => p.Code).OrderBy(p => p));
        }

        if (x.Day is { } day)
        {
            var totals = Assert.Single(e.Days, d => d.Date == day.Date);
            AssertIf(day.DrivingSeconds, totals.DrivingSeconds);
            AssertIf(day.OffDutySeconds, totals.OffDutySeconds);
            AssertIf(day.DailyOffRequiredSeconds, totals.DailyOffRequiredSeconds);
        }

        if (x.Projection is { } px)
        {
            Assert.NotNull(c.Plan);
            var p = HosEngine.Project(c.Ledger, c.AsOfUnix, c.Plan, cal, new PlanningMargin());
            Assert.Equal(px.Feasible, p.Feasible);
            Assert.Equal(px.FirstBreach, p.FirstBreach is null ? null : Tag(p.FirstBreach));
            AssertIf(px.RequiredOffBeforeStartSeconds, p.RequiredOffBeforeStartSeconds);
            if (px.RequiredOffIsNull is true)
            {
                Assert.Null(p.RequiredOffBeforeStartSeconds);
            }

            AssertIf(px.MarginAppliedSeconds, p.MarginAppliedSeconds);
            AssertIf(px.DailyOffStillNeededAfterPlanSeconds, p.DailyOffStillNeededAfterPlanSeconds);
            AssertIf(px.DailyOffAchievableAfterPlan, p.DailyOffAchievableAfterPlan);
            if (px.Problems is not null)
            {
                Assert.Equal(px.Problems.OrderBy(v => v), p.Problems.Select(v => v.Code).OrderBy(v => v));
            }
        }
        else
        {
            Assert.Null(c.Plan);
        }
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Case_reproduces_its_snapshot(string id)
    {
        var c = Find(id);
        Assert.NotNull(c.Snapshot);
        var actual = HosFixtureFile.Snapshot(c);
        Assert.True(
            JsonNode.DeepEquals(c.Snapshot, actual),
            $"{id}: output differs from the committed snapshot.\nExpected:\n{c.Snapshot!.ToJsonString(HosFixtureFile.Options)}\nActual:\n{actual.ToJsonString(HosFixtureFile.Options)}");
    }

    // ---- properties --------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Ids))]
    public void Evaluation_is_independent_of_event_order(string id)
    {
        var c = Find(id);
        var cal = HosFixtureFile.CalendarOf(c);
        var baseline = JsonSerializer.Serialize(HosEngine.Evaluate(c.Ledger, c.AsOfUnix, cal, new HosBands()), HosFixtureFile.Options);
        var random = new Random(id.GetHashCode(StringComparison.Ordinal));
        for (var round = 0; round < 3; round++)
        {
            var shuffled = c.Ledger with { Events = c.Ledger.Events.OrderBy(_ => random.Next()).ToList() };
            var again = JsonSerializer.Serialize(HosEngine.Evaluate(shuffled, c.AsOfUnix, cal, new HosBands()), HosFixtureFile.Options);
            Assert.Equal(baseline, again);
        }
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_larger_margin_never_turns_an_infeasible_plan_feasible(string id)
    {
        var c = Find(id);
        if (c.Plan is null)
        {
            return;
        }

        var cal = HosFixtureFile.CalendarOf(c);
        PlanningMargin[] margins = [new(0, 0), new(900, 5), new(3600, 10), new(7200, 20), new(10800, 50)];
        var feasible = margins.Select(m => HosEngine.Project(c.Ledger, c.AsOfUnix, c.Plan, cal, m).Feasible).ToList();
        for (var i = 1; i < feasible.Count; i++)
        {
            Assert.False(!feasible[i - 1] && feasible[i], $"{id}: margin {margins[i]} made an infeasible plan feasible.");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Evaluate_and_Project_never_throw_on_garbage(int seed)
    {
        var random = new Random(seed);
        var cal = new HosCalendar();
        var asOf = 1_789_000_000L + random.Next(0, 10_000_000);
        for (var round = 0; round < 40; round++)
        {
            var ledger = GarbageLedger(random, asOf);
            var evaluation = HosEngine.Evaluate(ledger, asOf, cal, new HosBands(random.Next(-10, 100_000)));
            Assert.NotNull(evaluation);

            var legs = Enumerable.Range(0, random.Next(0, 4)).Select(_ => new PlannedLeg(
                asOf + random.Next(-100_000, 200_000),
                Enumerable.Range(0, random.Next(0, 4)).Select(_ => new PlannedSegment((DutyStatus)random.Next(-1, 6), random.Next(-100, 60_000))).ToList(),
                null)).ToList();
            var plan = new TripPlan(legs, random.Next(2) == 0 ? null : asOf + random.Next(-1000, 100_000), (DutyStatus)random.Next(0, 4));
            var projection = HosEngine.Project(ledger, asOf, plan, cal, new PlanningMargin(random.Next(-100, 10_000), random.Next(-50, 300)));
            Assert.NotNull(projection);
        }
    }

    private static DutyLedger GarbageLedger(Random random, long asOf)
    {
        long Around() => asOf + random.Next(-40 * 86400, 2 * 86400);
        var events = Enumerable.Range(0, random.Next(0, 60)).Select(_ => new DutyEvent(
            Around(), (DutyStatus)random.Next(-1, 6), (EventSource)random.Next(-1, 7), random.Next(-5, 100),
            random.Next(3) == 0 ? null : random.Next(-10, 200_000), null)).ToList();
        var others = Enumerable.Range(0, random.Next(0, 5)).Select(_ => new OtherCarrierDay(
            DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(Around()).UtcDateTime), random.Next(-100, 90_000), random.Next(-100, 90_000), "x")).ToList();
        var exceptions = Enumerable.Range(0, random.Next(0, 5)).Select<int, HosException>(i => random.Next(3) switch
        {
            0 => new AdverseDrivingConditions($"a{i}", Around(), null, "r", random.Next(-100, 20_000)),
            1 => new Emergency($"e{i}", Around(), null, "r", random.Next(2) == 0 ? null : Around()),
            _ => new OffDutyDeferral($"d{i}", Around(), null, "r", DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(Around()).UtcDateTime), random.Next(-100, 20_000), "co"),
        }).ToList();
        var oos = Enumerable.Range(0, random.Next(0, 3)).Select(_ => new OutOfService(Around(), random.Next(2) == 0 ? null : Around(), "r")).ToList();
        return new DutyLedger("g", Around(), events, others, exceptions, [], oos, []);
    }

    private static string Tag(HosViolation v) => $"{v.Rule}@{v.Date:yyyy-MM-dd}";

    private static void AssertIf<T>(T? expected, T actual) where T : struct
    {
        if (expected is { } e)
        {
            Assert.Equal(e, actual);
        }
    }

    private static void AssertIf<T>(T? expected, T? actual) where T : struct
    {
        if (expected is { } e)
        {
            Assert.Equal(e, actual);
        }
    }
}
