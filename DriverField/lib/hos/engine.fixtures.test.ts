import { describe, expect, it } from "vitest";
import fixtures from "./hos-fixtures.json";
import {
  DEFAULT_PLANNING_MARGIN,
  evaluate,
  HosCalendar,
  project,
  type BlockingReason,
  type DutyLedger,
  type DutyStatus,
  type HosBand,
  type HosEvaluation,
  type HosProjection,
  type HosViolation,
  type PlanningMargin,
  type TripPlan,
} from "./engine";

// THE MIRROR GUARANTEE, on the tablet. Every case in hos-fixtures.json must satisfy its
// hand-written `expect` block AND reproduce the C# engine's committed `snapshot` exactly — the
// evaluation, and the projection when the case has a plan. This is the same theory as
// Dispatcher/lib/hos/engine.fixtures.test.ts (and the TypeScript half of
// Backend/tests/NorthernLink.Shared.Tests/HoursOfService/HosFixtureTests.cs), run here against
// THIS app's copy of the engine and THIS app's copy of the fixtures. The copy tests prove the
// files are identical; this proves the engine the tablet actually ships agrees with the server
// that enforces, case by case, one minute each side of every limit.
//
// This file is app-local, not a design-system copy — a near-copy of Dispatcher's is fine and
// expected. Never make a case pass by editing the JSON. Regenerate it on the backend and
// re-copy.

interface FixtureCalendar {
  timeZoneId: string;
  dayStartSecondsLocal: number;
}

interface FixtureDayExpect {
  date: string;
  drivingSeconds: number | null;
  offDutySeconds: number | null;
  dailyOffRequiredSeconds: number | null;
}

interface FixtureProjectionExpect {
  feasible: boolean;
  firstBreach: string | null;
  requiredOffBeforeStartSeconds: number | null;
  requiredOffIsNull: boolean | null;
  marginAppliedSeconds: number | null;
  dailyOffStillNeededAfterPlanSeconds: number | null;
  dailyOffAchievableAfterPlan: boolean | null;
  problems: string[] | null;
}

interface FixtureExpect {
  violations: string[];
  suppressedViolations: string[] | null;
  canDriveNow: boolean;
  blockingReasons: BlockingReason[];
  band: HosBand;
  currentStatus: DutyStatus | null;
  remainingDrivingTodaySeconds: number | null;
  remainingOnDutyTodaySeconds: number | null;
  tightestRemainingSeconds: number | null;
  todayDrivingSeconds: number | null;
  todayOffDutySeconds: number | null;
  todayOffDutyShortfallSeconds: number | null;
  shiftStartUnix: number | null;
  betweenShifts: boolean | null;
  shiftDrivingCeilingSeconds: number | null;
  cycleOnDutySeconds: number | null;
  cycleLastResetEndUnix: number | null;
  cycleRecordsIncomplete: boolean | null;
  rest24ExpiresAtUnix: number | null;
  pcTodayDecikm: number | null;
  ledgerProblems: string[] | null;
  day: FixtureDayExpect | null;
  projection: FixtureProjectionExpect | null;
}

interface FixtureCase {
  id: string;
  title: string;
  regulation: string;
  calendar: FixtureCalendar;
  asOfUnix: number;
  ledger: DutyLedger;
  plan: TripPlan | null;
  expect: FixtureExpect;
  snapshot: { evaluation: unknown; projection?: unknown };
}

const cases = fixtures as unknown as FixtureCase[];

function calendarOf(c: FixtureCase): HosCalendar {
  return new HosCalendar(c.calendar.timeZoneId, c.calendar.dayStartSecondsLocal);
}

/** HosFixtureFile.Snapshot: the evaluation, plus the projection when the case has a plan. */
function snapshotOf(c: FixtureCase): { evaluation: HosEvaluation; projection?: HosProjection } {
  const cal = calendarOf(c);
  const node: { evaluation: HosEvaluation; projection?: HosProjection } = {
    evaluation: evaluate(c.ledger, c.asOfUnix, cal),
  };
  if (c.plan !== null) {
    node.projection = project(c.ledger, c.asOfUnix, c.plan, cal, DEFAULT_PLANNING_MARGIN);
  }

  return node;
}

function tag(v: HosViolation): string {
  return `${v.rule}@${v.date}`;
}

function sorted<T>(xs: readonly T[]): T[] {
  return [...xs].sort();
}

function assertIf<T>(expected: T | null | undefined, actual: T): void {
  if (expected !== null && expected !== undefined) {
    expect(actual).toEqual(expected);
  }
}

/** Deterministic shuffle (mulberry32) so an order-dependence failure reproduces. */
function shuffled<T>(xs: readonly T[], seed: number): T[] {
  let a = seed >>> 0;
  const next = (): number => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const out = [...xs];
  for (let i = out.length - 1; i > 0; i--) {
    const j = Math.floor(next() * (i + 1));
    [out[i], out[j]] = [out[j], out[i]];
  }

  return out;
}

function range(prefix: string, count: number): string[] {
  return Array.from({ length: count }, (_, i) => `${prefix}-${String(i + 1).padStart(2, "0")}`);
}

describe("the fixture matrix", () => {
  it("covers every planned case id (HosFixtureTests.The_matrix_covers_every_planned_case_id)", () => {
    const planned = [
      ...range("dd", 5),
      ...range("od", 3),
      ...range("sh", 6),
      ...range("off", 7),
      ...range("r24", 5),
      ...range("cy", 7),
      ...range("pc", 7),
      ...range("adv", 7),
      ...range("em", 2),
      ...range("def", 4),
      ...range("oos", 2),
      ...range("rk", 3),
      ...range("ev", 4),
      ...range("dst", 2),
      ...range("pj", 7),
      "bd-01",
    ];
    expect(sorted(cases.map((c) => c.id))).toEqual(sorted(planned));
  });

  it("carries a committed snapshot on every case", () => {
    for (const c of cases) {
      expect(c.snapshot, c.id).toBeTruthy();
      expect(c.snapshot.evaluation, c.id).toBeTruthy();
      expect("projection" in c.snapshot, c.id).toBe(c.plan !== null);
    }
  });
});

describe.each(cases.map((c) => [c.id, c] as const))("%s", (id, c) => {
  it(`meets its expectations — ${c.title}`, () => {
    const cal = calendarOf(c);
    const e = evaluate(c.ledger, c.asOfUnix, cal);
    const x = c.expect;

    expect(sorted(e.violations.filter((v) => !v.suppressed).map(tag))).toEqual(sorted(x.violations));
    if (x.suppressedViolations !== null) {
      expect(sorted(e.violations.filter((v) => v.suppressed).map(tag))).toEqual(sorted(x.suppressedViolations));
    }

    expect(e.canDriveNow).toBe(x.canDriveNow);
    expect(e.blockingReasons).toEqual(x.blockingReasons);
    expect(e.band).toBe(x.band);
    assertIf(x.currentStatus, e.currentStatus);
    assertIf(x.remainingDrivingTodaySeconds, e.remainingDrivingTodaySeconds);
    assertIf(x.remainingOnDutyTodaySeconds, e.remainingOnDutyTodaySeconds);
    assertIf(x.tightestRemainingSeconds, e.tightestRemainingSeconds);
    assertIf(x.todayDrivingSeconds, e.today.drivingSeconds);
    assertIf(x.todayOffDutySeconds, e.today.offDutySeconds);
    assertIf(x.todayOffDutyShortfallSeconds, e.today.offDutyShortfallSeconds);
    assertIf(x.shiftStartUnix, e.shift.startUnix);
    assertIf(x.betweenShifts, e.shift.startUnix === null);
    assertIf(x.shiftDrivingCeilingSeconds, e.shift.drivingCeilingSeconds);
    assertIf(x.cycleOnDutySeconds, e.cycle.onDutySeconds);
    assertIf(x.cycleLastResetEndUnix, e.cycle.lastResetEndUnix);
    assertIf(x.cycleRecordsIncomplete, e.cycle.recordsIncomplete);
    assertIf(x.rest24ExpiresAtUnix, e.rest24.expiresAtUnix);
    assertIf(x.pcTodayDecikm, e.pcTodayDecikm);
    if (x.ledgerProblems !== null) {
      expect(sorted(e.ledgerProblems.map((p) => p.code))).toEqual(sorted(x.ledgerProblems));
    }

    if (x.day !== null) {
      const matching = e.days.filter((d) => d.date === x.day!.date);
      expect(matching).toHaveLength(1);
      const totals = matching[0];
      assertIf(x.day.drivingSeconds, totals.drivingSeconds);
      assertIf(x.day.offDutySeconds, totals.offDutySeconds);
      assertIf(x.day.dailyOffRequiredSeconds, totals.dailyOffRequiredSeconds);
    }

    if (x.projection !== null) {
      const px = x.projection;
      expect(c.plan).not.toBeNull();
      const p = project(c.ledger, c.asOfUnix, c.plan!, cal, DEFAULT_PLANNING_MARGIN);
      expect(p.feasible).toBe(px.feasible);
      expect(p.firstBreach === null ? null : tag(p.firstBreach)).toBe(px.firstBreach);
      assertIf(px.requiredOffBeforeStartSeconds, p.requiredOffBeforeStartSeconds);
      if (px.requiredOffIsNull === true) {
        expect(p.requiredOffBeforeStartSeconds).toBeNull();
      }

      assertIf(px.marginAppliedSeconds, p.marginAppliedSeconds);
      assertIf(px.dailyOffStillNeededAfterPlanSeconds, p.dailyOffStillNeededAfterPlanSeconds);
      assertIf(px.dailyOffAchievableAfterPlan, p.dailyOffAchievableAfterPlan);
      if (px.problems !== null) {
        expect(sorted(p.problems.map((v) => v.code))).toEqual(sorted(px.problems));
      }
    } else {
      expect(c.plan).toBeNull();
    }
  });

  it("reproduces its committed snapshot exactly", () => {
    // A JSON round trip first: it drops any `undefined` the mirror might emit where the C#
    // serialises `null`, so that difference fails here instead of being masked by toEqual's
    // undefined-equals-missing leniency.
    const actual = JSON.parse(JSON.stringify(snapshotOf(c)));
    expect(actual).toStrictEqual(c.snapshot);
  });

  it("evaluates the same whatever order the events arrive in", () => {
    // HosFixtureTests.Evaluation_is_independent_of_event_order: the normaliser sorts by
    // (atUnix, sequence), so input order must never leak into the result. On the tablet this
    // is the property the optimistic ledger leans on — an appended event lands at the end of
    // the array whatever its instant.
    const cal = calendarOf(c);
    const baseline = JSON.stringify(evaluate(c.ledger, c.asOfUnix, cal));
    for (let round = 0; round < 3; round++) {
      const seed = id.split("").reduce((h, ch) => (h * 31 + ch.charCodeAt(0)) >>> 0, round + 1);
      const reordered: DutyLedger = { ...c.ledger, events: shuffled(c.ledger.events, seed) };
      expect(JSON.stringify(evaluate(reordered, c.asOfUnix, cal))).toBe(baseline);
    }
  });

  it("never lets a larger margin turn an infeasible plan feasible", () => {
    // HosFixtureTests.A_larger_margin_never_turns_an_infeasible_plan_feasible.
    if (c.plan === null) {
      return;
    }

    const cal = calendarOf(c);
    const margins: PlanningMargin[] = [
      { fixedSeconds: 0, percent: 0 },
      { fixedSeconds: 900, percent: 5 },
      { fixedSeconds: 3600, percent: 10 },
      { fixedSeconds: 7200, percent: 20 },
      { fixedSeconds: 10800, percent: 50 },
    ];
    const feasible = margins.map((m) => project(c.ledger, c.asOfUnix, c.plan!, cal, m).feasible);
    for (let i = 1; i < feasible.length; i++) {
      expect(!feasible[i - 1] && feasible[i], `${id}: margin ${JSON.stringify(margins[i])} made an infeasible plan feasible`).toBe(false);
    }
  });
});
