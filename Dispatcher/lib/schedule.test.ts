import { describe, expect, it } from "vitest";
import { templateOccursOn } from "./schedule";
import type { DayName, ScheduleRecurrenceKind, ScheduleTemplateRecord } from "./api/trips";

/**
 * PARITY SUITE — the frontend half of a rule that is implemented twice.
 *
 * `lib/schedule.ts` declares itself "a pure, client-side mirror of the backend
 * TripGenerator's recurrence branch (TripGenerator.MatchingDates)". That is duplicated
 * business logic across a language boundary: the backend half is covered by
 * Backend/tests/NorthernLink.Trips.Tests/TripGeneratorTests.cs, and until now the
 * frontend half was not covered at all — so a divergence would surface as a Special
 * Dates calendar quietly marking the wrong days, with nothing failing.
 *
 * Every case below whose name ends in "(parity)" mirrors a named backend fact, on the
 * SAME dates, so a future change to either side fails on one side and not the other.
 * The backend's own tests assert over a horizon window and return TripDrafts; the
 * frontend exposes a per-date predicate with no horizon at all (by design — the calendar
 * previews any month), so a backend "these dates were generated" assertion becomes
 * "exactly these dates in that range predicate true".
 */

type RecurrenceFields = Pick<
  ScheduleTemplateRecord,
  "recurrenceKind" | "daysOfWeek" | "intervalDays" | "anchorDate" | "daysOfMonth"
>;

function tmpl(
  recurrenceKind: ScheduleRecurrenceKind,
  fields: Partial<RecurrenceFields> = {},
): RecurrenceFields {
  return {
    recurrenceKind,
    daysOfWeek: [],
    intervalDays: null,
    anchorDate: null,
    daysOfMonth: [],
    ...fields,
  };
}

/** "yyyy-MM-dd" strings for `count` consecutive days starting at `startIso` (UTC math,
 *  matching the module under test — no local-timezone day shift). */
function range(startIso: string, count: number): string[] {
  const [y, m, d] = startIso.split("-").map(Number);
  const start = Date.UTC(y, m - 1, d);
  return Array.from({ length: count }, (_, i) => {
    const date = new Date(start + i * 86_400_000);
    const p = (n: number) => String(n).padStart(2, "0");
    return `${date.getUTCFullYear()}-${p(date.getUTCMonth() + 1)}-${p(date.getUTCDate())}`;
  });
}

/** The dates in `dates` the template fires on — the frontend analogue of the backend's
 *  OutboundDates(drafts) helper. */
function firesOn(t: RecurrenceFields, dates: string[]): string[] {
  return dates.filter((date) => templateOccursOn(t, date));
}

// The backend suite's TestPlanning.Monday.
const MONDAY = "2026-07-20";

// ----- DaysOfWeek -----

describe("DaysOfWeek", () => {
  it("generates exactly the configured weekdays over a week (parity)", () => {
    // Backend: DaysOfWeek_generates_exactly_the_configured_weekdays_over_the_horizon.
    // Window [Mon 2026-07-20, +7) => 20..26; Mon/Wed/Fri => 20, 22, 24.
    const t = tmpl("DaysOfWeek", { daysOfWeek: ["Monday", "Wednesday", "Friday"] });

    expect(firesOn(t, range(MONDAY, 7))).toEqual(["2026-07-20", "2026-07-22", "2026-07-24"]);
  });

  it("skips the days the template does not run (parity)", () => {
    // Backend: Skips_days_the_template_does_not_run — weekdays only, Mon to Fri of that week.
    const weekdays: DayName[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
    const t = tmpl("DaysOfWeek", { daysOfWeek: weekdays });

    expect(firesOn(t, range(MONDAY, 7))).toEqual([
      "2026-07-20",
      "2026-07-21",
      "2026-07-22",
      "2026-07-23",
      "2026-07-24",
    ]);
    expect(templateOccursOn(t, "2026-07-25")).toBe(false); // Saturday
    expect(templateOccursOn(t, "2026-07-26")).toBe(false); // Sunday
  });

  it("fires every day when all seven days are selected (parity)", () => {
    // Backend: Expands_every_matching_day_across_the_horizon.
    const all: DayName[] = [
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday",
      "Sunday",
    ];
    const t = tmpl("DaysOfWeek", { daysOfWeek: all });

    expect(firesOn(t, range(MONDAY, 7))).toHaveLength(7);
  });

  it("maps Sunday correctly — the index-0 edge of the weekday table", () => {
    // DAY_BY_UTC_INDEX is 0=Sunday while the UI's WEEK_DAYS is Mon-first; an off-by-one
    // between the two tables would shift every weekday by exactly one day.
    const t = tmpl("DaysOfWeek", { daysOfWeek: ["Sunday"] });

    expect(templateOccursOn(t, "2026-07-26")).toBe(true); // Sunday
    expect(templateOccursOn(t, "2026-07-25")).toBe(false); // Saturday
    expect(templateOccursOn(t, "2026-07-27")).toBe(false); // Monday
  });

  it("is unaffected by DST transitions", () => {
    // The module comment promises UTC day numbers precisely so a DST boundary cannot move
    // a weekday. 2026-03-08 (North American spring-forward) is a Sunday; 2026-11-01
    // (fall-back) is also a Sunday.
    const sundays = tmpl("DaysOfWeek", { daysOfWeek: ["Sunday"] });

    expect(templateOccursOn(sundays, "2026-03-08")).toBe(true);
    expect(templateOccursOn(sundays, "2026-11-01")).toBe(true);
    expect(templateOccursOn(sundays, "2026-03-09")).toBe(false);
    expect(templateOccursOn(sundays, "2026-11-02")).toBe(false);
  });

  it("never fires with no days selected", () => {
    expect(templateOccursOn(tmpl("DaysOfWeek"), MONDAY)).toBe(false);
  });

  it("ignores a malformed date rather than throwing", () => {
    const t = tmpl("DaysOfWeek", { daysOfWeek: ["Monday"] });

    expect(templateOccursOn(t, "not-a-date")).toBe(false);
    expect(templateOccursOn(t, "")).toBe(false);
  });
});

// ----- EveryNDays: anchor arithmetic -----

describe("EveryNDays anchor arithmetic", () => {
  it("lands on the anchor and its multiples, never before it (parity)", () => {
    // Backend: EveryNDays_with_future_anchor_lands_on_anchor_and_multiples_and_skips_before_anchor.
    // today Mon 2026-07-20, anchor Wed 2026-07-22, interval 3, window [20, 30).
    const t = tmpl("EveryNDays", { intervalDays: 3, anchorDate: "2026-07-22" });

    expect(firesOn(t, range(MONDAY, 10))).toEqual(["2026-07-22", "2026-07-25", "2026-07-28"]);
    // Dates before the anchor never fire, even ones a whole interval away from it.
    expect(templateOccursOn(t, "2026-07-19")).toBe(false); // anchor minus 3
    expect(templateOccursOn(t, "2026-07-20")).toBe(false);
    expect(templateOccursOn(t, "2026-07-21")).toBe(false);
  });

  it("keeps the interval from a past anchor (parity)", () => {
    // Backend: EveryNDays_with_past_anchor_still_lands_on_the_interval_within_the_window.
    // anchor Tue 2026-07-14, interval 3 => 14,17,20,23,26,29; window [20, 30) keeps 4.
    const t = tmpl("EveryNDays", { intervalDays: 3, anchorDate: "2026-07-14" });

    expect(firesOn(t, range(MONDAY, 10))).toEqual([
      "2026-07-20",
      "2026-07-23",
      "2026-07-26",
      "2026-07-29",
    ]);
  });

  it("skips dates that do not divide evenly (parity)", () => {
    // Backend: EveryNDays_skips_dates_that_do_not_divide_evenly — anchor == today.
    const t = tmpl("EveryNDays", { intervalDays: 3, anchorDate: MONDAY });

    expect(firesOn(t, range(MONDAY, 10))).toEqual([
      "2026-07-20",
      "2026-07-23",
      "2026-07-26",
      "2026-07-29",
    ]);
    const offBeats = [
      "2026-07-21",
      "2026-07-22",
      "2026-07-24",
      "2026-07-25",
      "2026-07-27",
      "2026-07-28",
    ];
    for (const offBeat of offBeats) {
      expect(templateOccursOn(t, offBeat), offBeat).toBe(false);
    }
  });

  it("counts intervals across a month boundary", () => {
    // Day-number arithmetic, not day-of-month arithmetic: from 2026-07-29, +3 is Aug 1.
    const t = tmpl("EveryNDays", { intervalDays: 3, anchorDate: MONDAY });

    expect(templateOccursOn(t, "2026-08-01")).toBe(true);
    expect(templateOccursOn(t, "2026-07-31")).toBe(false);
    expect(templateOccursOn(t, "2026-08-02")).toBe(false);
  });

  it("counts intervals across a leap day and a year boundary", () => {
    // 2028-02-29 exists, so the sequence must absorb it: anchor 2028-02-27, interval 2
    // => 27, 29, then Mar 2.
    const t = tmpl("EveryNDays", { intervalDays: 2, anchorDate: "2028-02-27" });

    expect(firesOn(t, range("2028-02-27", 6))).toEqual([
      "2028-02-27",
      "2028-02-29",
      "2028-03-02",
    ]);

    // Year boundary: anchor 2026-12-30, interval 2 => Dec 30, Jan 1, Jan 3.
    const nye = tmpl("EveryNDays", { intervalDays: 2, anchorDate: "2026-12-30" });
    expect(firesOn(nye, range("2026-12-30", 5))).toEqual([
      "2026-12-30",
      "2027-01-01",
      "2027-01-03",
    ]);
  });

  it("fires every day at interval 1", () => {
    const t = tmpl("EveryNDays", { intervalDays: 1, anchorDate: MONDAY });

    expect(firesOn(t, range(MONDAY, 5))).toHaveLength(5);
  });

  it("never fires when misconfigured (validation prevents these server-side)", () => {
    // Backend: MatchingDates yields nothing unless IntervalDays > 0 AND AnchorDate is set.
    expect(templateOccursOn(tmpl("EveryNDays", { intervalDays: 3 }), MONDAY)).toBe(false);
    expect(templateOccursOn(tmpl("EveryNDays", { anchorDate: MONDAY }), MONDAY)).toBe(false);
    expect(
      templateOccursOn(tmpl("EveryNDays", { intervalDays: 0, anchorDate: MONDAY }), MONDAY),
    ).toBe(false);
    expect(
      templateOccursOn(tmpl("EveryNDays", { intervalDays: -3, anchorDate: MONDAY }), MONDAY),
    ).toBe(false);
    expect(
      templateOccursOn(tmpl("EveryNDays", { intervalDays: 3, anchorDate: "junk" }), MONDAY),
    ).toBe(false);
  });
});

// ----- MonthlyDays: the month-end clamp -----

describe("MonthlyDays month-end clamp", () => {
  it("lands on the configured days (parity)", () => {
    // Backend: MonthlyDays_lands_on_the_configured_days — days [1,15] in August 2026.
    const t = tmpl("MonthlyDays", { daysOfMonth: [1, 15] });

    expect(firesOn(t, range("2026-08-01", 20))).toEqual(["2026-08-01", "2026-08-15"]);
  });

  it("clamps 31 to month-end of a 30-day month (parity)", () => {
    // Backend: MonthlyDays_31_clamps_to_month_end_of_a_30_day_month — Sep 2026 => Sep 30.
    const t = tmpl("MonthlyDays", { daysOfMonth: [31] });

    expect(firesOn(t, range("2026-09-01", 30))).toEqual(["2026-09-30"]);
  });

  it("clamps 31 to February 28 in a non-leap year (parity)", () => {
    // Backend: MonthlyDays_31_clamps_to_february_28_in_a_non_leap_year — 2026 is not a leap year.
    const t = tmpl("MonthlyDays", { daysOfMonth: [31] });

    expect(firesOn(t, range("2026-02-01", 28))).toEqual(["2026-02-28"]);
  });

  it("clamps 31 to February 29 in a leap year (parity)", () => {
    // Backend: MonthlyDays_31_clamps_to_february_29_in_a_leap_year — 2028 IS a leap year,
    // so the clamp target moves by one day. A hardcoded 28 passes the test above and
    // fails here.
    const t = tmpl("MonthlyDays", { daysOfMonth: [31] });

    expect(firesOn(t, range("2028-02-01", 29))).toEqual(["2028-02-29"]);
    expect(templateOccursOn(t, "2028-02-28")).toBe(false); // not month-end in 2028
  });

  it("does not clamp in a 31-day month (parity boundary)", () => {
    const t = tmpl("MonthlyDays", { daysOfMonth: [31] });

    expect(firesOn(t, range("2026-07-01", 31))).toEqual(["2026-07-31"]);
    expect(templateOccursOn(t, "2026-07-30")).toBe(false);
  });

  it("clamps 30 and 31 onto the same date without firing twice (parity)", () => {
    // Backend: MonthlyDays_two_days_that_clamp_to_the_same_date_do_not_double_emit.
    // The backend dedupes with a HashSet; the frontend predicate is a boolean, so the
    // equivalent guarantee is that exactly one date in the month matches.
    const t = tmpl("MonthlyDays", { daysOfMonth: [30, 31] });

    expect(firesOn(t, range("2026-02-01", 28))).toEqual(["2026-02-28"]);
  });

  it("keeps a direct hit and a clamped hit distinct within one month", () => {
    // Days [15, 31] in September: 15 is a direct hit, 31 clamps to the 30th.
    const t = tmpl("MonthlyDays", { daysOfMonth: [15, 31] });

    expect(firesOn(t, range("2026-09-01", 30))).toEqual(["2026-09-15", "2026-09-30"]);
  });

  it("clamps every configured day past month-end onto month-end, not onto separate days", () => {
    // Days [29, 30, 31] in Feb 2026 (28 days) all clamp to the 28th — and crucially the
    // 27th does NOT fire.
    const t = tmpl("MonthlyDays", { daysOfMonth: [29, 30, 31] });

    expect(firesOn(t, range("2026-02-01", 28))).toEqual(["2026-02-28"]);
  });

  it("treats day 1 as an ordinary direct hit across month lengths", () => {
    const t = tmpl("MonthlyDays", { daysOfMonth: [1] });

    for (const first of ["2026-01-01", "2026-02-01", "2026-04-01", "2028-02-01"]) {
      expect(templateOccursOn(t, first), first).toBe(true);
    }
    expect(templateOccursOn(t, "2026-01-02")).toBe(false);
  });

  it("never fires with no days configured (parity)", () => {
    expect(templateOccursOn(tmpl("MonthlyDays"), "2026-08-01")).toBe(false);
  });
});

describe("unknown recurrence kind", () => {
  it("never fires rather than throwing", () => {
    const t = tmpl("Fortnightly" as ScheduleRecurrenceKind, { daysOfWeek: ["Monday"] });

    expect(templateOccursOn(t, MONDAY)).toBe(false);
  });
});
