import { describe, expect, it } from "vitest";
import {
  clampThroughIso,
  endOfNextMonthIso,
  GENERATE_MAX_DAYS_AHEAD,
  maxGenerateThroughIso,
} from "./generation";

// The generate-trips dialog's date window. The backend guard
// (ScheduleTemplate.MaxGenerateAheadDays → InvalidGenerationWindow) is the
// rule; these helpers keep the picker inside it. Dates below are chosen so a
// month-length, year-wrap or leap-day slip fails loudly.

describe("endOfNextMonthIso", () => {
  it("mid-month → last day of the following month", () => {
    expect(endOfNextMonthIso("2026-09-15")).toBe("2026-10-31");
  });

  it("December → the following January (year wraps)", () => {
    expect(endOfNextMonthIso("2026-12-10")).toBe("2027-01-31");
    expect(endOfNextMonthIso("2026-12-31")).toBe("2027-01-31");
  });

  it("January 31 → February 28 in a non-leap year", () => {
    // Not "January 31 + one month" (which would roll into March) — the LAST day
    // of the next month, whatever its length. 2027 is not a leap year.
    expect(endOfNextMonthIso("2027-01-31")).toBe("2027-02-28");
  });

  it("January 31 → February 29 in a leap year", () => {
    // 2028 IS a leap year, so a hardcoded 28 passes the case above and fails here.
    expect(endOfNextMonthIso("2028-01-31")).toBe("2028-02-29");
  });

  it("first of the month and 30-day months behave", () => {
    expect(endOfNextMonthIso("2026-03-01")).toBe("2026-04-30");
    expect(endOfNextMonthIso("2026-10-31")).toBe("2026-11-30");
  });

  it("returns a malformed today unchanged rather than throwing", () => {
    expect(endOfNextMonthIso("")).toBe("");
    expect(endOfNextMonthIso("not-a-date")).toBe("not-a-date");
  });
});

describe("maxGenerateThroughIso", () => {
  it("is exactly GENERATE_MAX_DAYS_AHEAD days after today", () => {
    expect(GENERATE_MAX_DAYS_AHEAD).toBe(366);
    // 2026-09-15 → 2027-09-15 is 365 days (no Feb 29 in between), so +366 lands one day later.
    expect(maxGenerateThroughIso("2026-09-15")).toBe("2027-09-16");
  });

  it("absorbs a leap day inside the span", () => {
    // 2027-09-15 → 2028-09-15 spans 2028-02-29, so it is 366 days exactly.
    expect(maxGenerateThroughIso("2027-09-15")).toBe("2028-09-15");
  });

  it("returns a malformed today unchanged", () => {
    expect(maxGenerateThroughIso("junk")).toBe("junk");
  });
});

describe("clampThroughIso", () => {
  const TODAY = "2026-09-15";
  const CAP = "2027-09-16"; // maxGenerateThroughIso(TODAY)

  it("passes a date inside the window through untouched", () => {
    expect(clampThroughIso(TODAY, "2026-10-31")).toBe("2026-10-31");
    expect(clampThroughIso(TODAY, "2027-03-01")).toBe("2027-03-01");
  });

  it("allows today itself", () => {
    expect(clampThroughIso(TODAY, TODAY)).toBe(TODAY);
  });

  it("clamps a date before today up to today", () => {
    expect(clampThroughIso(TODAY, "2026-09-14")).toBe(TODAY);
    expect(clampThroughIso(TODAY, "2020-01-01")).toBe(TODAY);
  });

  it("clamps a date beyond the cap down to the cap", () => {
    expect(clampThroughIso(TODAY, "2027-09-17")).toBe(CAP);
    expect(clampThroughIso(TODAY, "2030-01-01")).toBe(CAP);
  });

  it("lets exactly the cap pass (the cap is inclusive)", () => {
    expect(clampThroughIso(TODAY, CAP)).toBe(CAP);
  });

  it("falls back to the default for a malformed or empty date", () => {
    const fallback = endOfNextMonthIso(TODAY);
    expect(clampThroughIso(TODAY, "")).toBe(fallback);
    expect(clampThroughIso(TODAY, "not-a-date")).toBe(fallback);
    expect(clampThroughIso(TODAY, "2026-13-01")).toBe(fallback);
    expect(clampThroughIso(TODAY, "2026-02-30")).toBe(fallback); // not a real day
  });

  it("is not fooled by a lexicographic near-miss", () => {
    // Day-number comparison, not string comparison of mixed-width input.
    expect(clampThroughIso(TODAY, "2026-9-20")).toBe(endOfNextMonthIso(TODAY)); // malformed → default
  });
});
