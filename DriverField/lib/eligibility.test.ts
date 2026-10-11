import { beforeEach, describe, expect, it } from "vitest";
import { trips } from "./data";
import { eligibility } from "./eligibility";
import { addDays, type DateOnly, type DutyEvent } from "./hos/engine";
import { CALENDAR, installLedgerForTests, resetLedgerForTests } from "./hos/ledger";
import type { Trip } from "./types";

// The five §5.4 eligibility rules. This is a CLIENT-SIDE MIRROR of a rule the server must own,
// so the test's job is not to prove the rule is enforced — it isn't, anywhere, yet — but to
// prove each rule can fail independently and that the verdict is conjunctive.
//
// The hours-of-service rule is now the engine's `project` (HosEngine.Project in
// Backend/src/Shared/HoursOfService — the gate Trips' DriverFitnessGate will run) on the
// planning ledger, with the default margin (1h + 10%). The ledger is live in the app, so the
// test installs a known week on the pinned mock day (the mock trips are dated 2026-09-12): the
// driver has 2h45m driving and 3h30m on duty logged from the day start, evaluated as they went
// off duty at 03:30, so the open shift's 16h window closes at 16:00.
//
// When the backend's claim endpoint and fitness preview ship, this file becomes the pin that
// catches the two drifting apart, exactly as Budgeting's previewPeriod tests name the C# method
// they mirror.

const H = 3600;

/** The week on the pinned day, as per-day totals: [date, on-duty hours, driving hours]. */
const WEEK: [DateOnly, number, number][] = [
  ["2026-09-06", 7, 5.5],
  ["2026-09-07", 0, 0],
  ["2026-09-08", 10, 8.5],
  ["2026-09-09", 4, 0],
  ["2026-09-10", 8, 6],
  ["2026-09-11", 9.5, 7.25],
  ["2026-09-12", 3.5, 2.75],
];

/**
 * Lays the totals onto a ledger the conservative way (driving first, then the rest of the
 * on-duty time, then off), with twenty certified days off before the first — the same layout
 * the server's assembler gives a declared other-carrier day. Evaluated as the driver went off
 * duty on the last day.
 */
function installWeek(): void {
  const firstDate = addDays(WEEK[0][0], -20);
  const lastDate = WEEK[WEEK.length - 1][0];
  const byDate = new Map(WEEK.map(([date, on, drv]) => [date, { on, drv }]));
  const events: DutyEvent[] = [];
  const certifiedDays: DateOnly[] = [];
  let sequence = 1;
  let asOfUnix = 0;
  const push = (atUnix: number, status: DutyEvent["status"]) => {
    events.push({ atUnix, status, source: "PaperLog", sequence: sequence++, odometerDecikm: null, note: null });
  };
  for (let date = firstDate; date <= lastDate; date = addDays(date, 1)) {
    const dayStart = CALENDAR.dayStartUnix(date);
    const row = byDate.get(date) ?? { on: 0, drv: 0 };
    const onDuty = row.on * H;
    const driving = row.drv * H;
    if (driving > 0) push(dayStart, "Driving");
    if (onDuty > driving) push(dayStart + driving, "OnDuty");
    push(dayStart + onDuty, "OffDuty");
    certifiedDays.push(date);
    asOfUnix = dayStart + onDuty;
  }
  installLedgerForTests({ knownFromUnix: CALENDAR.dayStartUnix(firstDate), events, certifiedDays, asOfUnix });
}

beforeEach(() => {
  resetLedgerForTests();
  installWeek();
});

const base: Trip = {
  id: "TRP-TEST",
  tripNumber: "T-TEST",
  mode: "Open",
  serviceType: "Community",
  clientName: "Community Service",
  origin: "Thompson",
  destination: "Leaf Rapids",
  // Well clear of the seeded assigned trips (06:30–09:15 and 17:00–19:45), so the schedule
  // rule passes unless a test deliberately overlaps.
  startsAt: "2026-09-12T11:00:00",
  endsAt: "2026-09-12T13:00:00",
  estimatedHours: 2,
  vehicleId: "VEH-11",
  requiredLicenceClass: "4",
  requiresClearance: null,
  seats: 7,
  status: "Open",
  tk: "info",
};

function ruleFor(trip: Trip, name: string) {
  const rule = eligibility(trip).rules.find((r) => r.rule === name);
  if (!rule) throw new Error(`no rule named ${name}`);
  return rule;
}

describe("eligibility", () => {
  it("passes every rule for a trip this driver can take", () => {
    const verdict = eligibility(base);
    expect(verdict.rules.every((r) => r.pass)).toBe(true);
    expect(verdict.eligible).toBe(true);
  });

  it("checks exactly the five rules §5.4 specifies", () => {
    // Named rather than counted: a rule silently disappearing is the failure mode that matters,
    // and a count alone would be satisfied by a rename.
    expect(eligibility(base).rules.map((r) => r.rule)).toEqual([
      "Hours of service",
      "Licence class",
      "Vehicle status",
      "Schedule conflict",
      "Client clearance",
    ]);
  });

  it("fails on hours of service alone", () => {
    // The seeded driver has 2h45m driving logged against a 13h limit, so a 13-hour trip cannot
    // fit however else it qualifies.
    const trip = { ...base, estimatedHours: 13 };
    expect(ruleFor(trip, "Hours of service").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("names the engine's breach, in the server's sentence, when hours fail", () => {
    // HosMessages.For — identical prose on the server, the console and here. A 13h trip at
    // 11:00 is 7h30m after the seeded as-of, so it is still the shift that began at the day
    // start; the earliest breach in time is the 16h window closing at 16:00 (s.13(3)), ahead
    // of the daily driving limit — and the engine reports the earliest, as the server does.
    const reason = ruleFor({ ...base, estimatedHours: 13 }, "Hours of service").reason;
    expect(reason).toMatch(/after the shift began; the window is 16h 0m \(s\.13\(3\)\)\.$/);
  });

  it("projects the trip WITH the default margin, both sides of the fit", () => {
    // Starting right after the seeded as-of (03:30), 8h of driving scaled by the default
    // margin (×1.10 + 1h = 9h48m) lands at 12h33m driving today — inside 13h. Half an hour
    // more (8.5h → 10h21m) lands at 13h06m: it fails ONLY because the margin is counted, since
    // 8.5h raw is well under the 10h15m raw remaining. A gate that ignored the margin would
    // pass it; the server's gate will not.
    const early = { ...base, startsAt: "2026-09-12T04:00:00", endsAt: "2026-09-12T12:00:00" };
    const fits = ruleFor({ ...early, estimatedHours: 8 }, "Hours of service");
    expect(fits.pass).toBe(true);
    expect(fits.reason).toContain("margin");

    const overByMargin = ruleFor({ ...early, estimatedHours: 8.5 }, "Hours of service");
    expect(overByMargin.pass).toBe(false);
    expect(overByMargin.reason).toContain("s.12(1)");
  });

  it("closes the shift window on a trip that would run past 16h after the shift began — and reopens it after 8h off", () => {
    // The seeded shift began at the day start. A 6h trip at 10:00 (7h36m with margin) ends at
    // 17:36 — 17h36m into a 16h window (s.13(3)) — even though the daily driving total fits.
    const sameShift = { ...base, startsAt: "2026-09-12T10:00:00", endsAt: "2026-09-12T16:00:00", estimatedHours: 6 };
    const closed = ruleFor(sameShift, "Hours of service");
    expect(closed.pass).toBe(false);
    expect(closed.reason).toContain("s.13(3)");

    // The same trip two hours later: 03:30 → 12:00 is 8h30m off, the mandatory 8h consecutive
    // off-duty period, so a NEW shift begins at 12:00 and its window runs to 04:00. The engine
    // projects the rest from the assumed off-duty time — this is s.13's reset, not a loophole.
    const newShift = { ...sameShift, startsAt: "2026-09-12T12:00:00", endsAt: "2026-09-12T18:00:00" };
    expect(ruleFor(newShift, "Hours of service").pass).toBe(true);
  });

  it("fails on licence class alone", () => {
    // Rule 2 reads the requirement off the trip/vehicle, never a hardcoded class — a future
    // vehicle needing Class 2 must be caught without changing this rule.
    const trip = { ...base, requiredLicenceClass: "2", vehicleId: "VEH-14" };
    expect(ruleFor(trip, "Licence class").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on vehicle status alone", () => {
    // VEH-16 is Out of Service with an outstanding failed DVIR.
    const trip = { ...base, vehicleId: "VEH-16", requiredLicenceClass: "4" };
    expect(ruleFor(trip, "Vehicle status").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on a schedule conflict alone", () => {
    // Overlaps the seeded 06:30–09:15 assigned trip.
    const trip = { ...base, startsAt: "2026-09-12T07:00:00", endsAt: "2026-09-12T08:00:00" };
    expect(ruleFor(trip, "Schedule conflict").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on a missing client clearance alone", () => {
    // The rule that traces to a real incident: a driver without active Alamos clearance must
    // never be eligible for an Alamos-routed trip, automatically, rather than depending on a
    // dispatcher remembering to check.
    const trip = { ...base, requiresClearance: "Vale" };
    expect(ruleFor(trip, "Client clearance").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("treats a trip needing a clearance the driver holds as eligible", () => {
    expect(ruleFor({ ...base, requiresClearance: "Alamos" }, "Client clearance").pass).toBe(true);
  });

  it("is conjunctive — eligible only when every rule passes", () => {
    const trip = { ...base, estimatedHours: 13, requiredLicenceClass: "2" };
    const verdict = eligibility(trip);
    expect(verdict.rules.filter((r) => !r.pass)).toHaveLength(2);
    expect(verdict.eligible).toBe(false);
  });

  it("gives every rule a reason, passing or failing", () => {
    // The scaffold greys ineligible trips and names the failing rule rather than hiding them
    // (a deliberate deviation from §5.4 — see lib/eligibility.ts). That only works if every
    // rule can say why, so an empty reason is a bug, not a cosmetic gap.
    for (const trip of trips) {
      for (const rule of eligibility(trip).rules) {
        expect(rule.reason.trim().length).toBeGreaterThan(0);
      }
    }
  });
});
