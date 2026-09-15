import { afterEach, describe, expect, it, vi } from "vitest";
import {
  currentPeriod,
  periodContaining,
  periodContains,
  periodLabel,
  stepPeriod,
  withGranularity,
  type Period,
} from "./period";

// Pure month/quarter math for the Trips period navigator. Two reasons this is worth
// pinning: (1) the file deliberately avoids `toISOString()` because it would shift the
// service day for anyone west of UTC — Northern Link runs in Manitoba, UTC-6/-5, so that
// regression would be live in production and silent in code review; (2) `Dispatcher/
// lib/period.ts` is copied verbatim into `Budgeting/lib/period.ts`, so this one suite
// guards both apps.

afterEach(() => {
  vi.useRealTimers();
});

describe("periodContaining — month", () => {
  it("brackets the month containing the date", () => {
    expect(periodContaining("2026-08-14", "month")).toEqual({
      granularity: "month",
      start: "2026-08-01",
      end: "2026-08-31",
    });
  });

  it("handles 30-day months, February, and a leap February", () => {
    expect(periodContaining("2026-09-15", "month").end).toBe("2026-09-30");
    expect(periodContaining("2026-02-15", "month").end).toBe("2026-02-28");
    expect(periodContaining("2028-02-15", "month").end).toBe("2028-02-29");
  });

  it("zero-pads single-digit months and days", () => {
    expect(periodContaining("2026-01-05", "month")).toEqual({
      granularity: "month",
      start: "2026-01-01",
      end: "2026-01-31",
    });
  });

  it("is idempotent on its own boundaries", () => {
    const august = periodContaining("2026-08-14", "month");

    expect(periodContaining(august.start, "month")).toEqual(august);
    expect(periodContaining(august.end, "month")).toEqual(august);
  });
});

describe("periodContaining — quarter", () => {
  it("buckets each month into the right calendar quarter", () => {
    const cases: Array<[string, string, string]> = [
      ["2026-01-15", "2026-01-01", "2026-03-31"], // Q1
      ["2026-03-31", "2026-01-01", "2026-03-31"], // Q1 boundary
      ["2026-04-01", "2026-04-01", "2026-06-30"], // Q2 boundary
      ["2026-08-14", "2026-07-01", "2026-09-30"], // Q3
      ["2026-12-31", "2026-10-01", "2026-12-31"], // Q4 boundary
    ];

    for (const [date, start, end] of cases) {
      expect(periodContaining(date, "quarter"), date).toEqual({
        granularity: "quarter",
        start,
        end,
      });
    }
  });
});

describe("stepPeriod", () => {
  it("steps a month forward and back", () => {
    const august: Period = periodContaining("2026-08-14", "month");

    expect(stepPeriod(august, 1)).toEqual({
      granularity: "month",
      start: "2026-09-01",
      end: "2026-09-30",
    });
    expect(stepPeriod(august, -1)).toEqual({
      granularity: "month",
      start: "2026-07-01",
      end: "2026-07-31",
    });
  });

  it("rolls the year at both ends", () => {
    const december = periodContaining("2026-12-10", "month");
    const january = periodContaining("2026-01-10", "month");

    expect(stepPeriod(december, 1).start).toBe("2027-01-01");
    expect(stepPeriod(january, -1)).toEqual({
      granularity: "month",
      start: "2025-12-01",
      end: "2025-12-31",
    });
  });

  it("steps a quarter by three months, rolling the year", () => {
    const q4 = periodContaining("2026-11-02", "quarter");

    expect(stepPeriod(q4, 1)).toEqual({
      granularity: "quarter",
      start: "2027-01-01",
      end: "2027-03-31",
    });
    expect(stepPeriod(q4, -1)).toEqual({
      granularity: "quarter",
      start: "2026-07-01",
      end: "2026-09-30",
    });
  });

  it("does not overflow when stepping from a 31-day month into a 30-day one", () => {
    // The classic Date.setMonth bug: stepping from Jan 31 lands on Mar 3. This builds
    // from day 1 of the period, so it cannot happen — pin it.
    const january = periodContaining("2026-01-31", "month");

    expect(stepPeriod(january, 1)).toEqual({
      granularity: "month",
      start: "2026-02-01",
      end: "2026-02-28",
    });
  });

  it("round-trips forward then back", () => {
    for (const granularity of ["month", "quarter"] as const) {
      const period = periodContaining("2026-08-14", granularity);
      expect(stepPeriod(stepPeriod(period, 1), -1)).toEqual(period);
    }
  });
});

describe("withGranularity", () => {
  it("keeps the anchor month when switching month to quarter", () => {
    // The documented behaviour: switching while viewing August lands on THAT August's
    // quarter, not on the quarter containing today.
    const august = periodContaining("2026-08-14", "month");

    expect(withGranularity(august, "quarter")).toEqual({
      granularity: "quarter",
      start: "2026-07-01",
      end: "2026-09-30",
    });
  });

  it("narrows a quarter back to its first month", () => {
    // Quarter periods start on the quarter's first month, so the reverse switch lands
    // there rather than back on the month the user came from.
    const q3 = periodContaining("2026-08-14", "quarter");

    expect(withGranularity(q3, "month")).toEqual({
      granularity: "month",
      start: "2026-07-01",
      end: "2026-07-31",
    });
  });

  it("is a no-op at the same granularity", () => {
    const august = periodContaining("2026-08-14", "month");

    expect(withGranularity(august, "month")).toEqual(august);
  });
});

describe("periodLabel", () => {
  it("names a month", () => {
    expect(periodLabel(periodContaining("2026-08-14", "month"))).toBe("August 2026");
    expect(periodLabel(periodContaining("2026-01-01", "month"))).toBe("January 2026");
    expect(periodLabel(periodContaining("2026-12-31", "month"))).toBe("December 2026");
  });

  it("names a quarter with its month span", () => {
    expect(periodLabel(periodContaining("2026-08-14", "quarter"))).toBe("Q3 2026 · Jul – Sep");
    expect(periodLabel(periodContaining("2026-01-15", "quarter"))).toBe("Q1 2026 · Jan – Mar");
    // Q4 reads the third month at index 11 — one past it would be undefined.
    expect(periodLabel(periodContaining("2026-11-02", "quarter"))).toBe("Q4 2026 · Oct – Dec");
  });
});

describe("periodContains", () => {
  it("includes both endpoints and excludes the neighbours", () => {
    const august = periodContaining("2026-08-14", "month");

    expect(periodContains(august, "2026-08-01")).toBe(true);
    expect(periodContains(august, "2026-08-31")).toBe(true);
    expect(periodContains(august, "2026-07-31")).toBe(false);
    expect(periodContains(august, "2026-09-01")).toBe(false);
  });

  it("works across a quarter and a year boundary", () => {
    const q4 = periodContaining("2026-11-02", "quarter");

    expect(periodContains(q4, "2026-10-01")).toBe(true);
    expect(periodContains(q4, "2026-12-31")).toBe(true);
    expect(periodContains(q4, "2027-01-01")).toBe(false);
    expect(periodContains(q4, "2026-09-30")).toBe(false);
  });
});

describe("currentPeriod", () => {
  it("brackets the month containing today", () => {
    // Local-time constructor on purpose: "today" here is the operator's local calendar day.
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 7, 15, 12, 0, 0)); // 2026-08-15 local

    expect(currentPeriod("month")).toEqual({
      granularity: "month",
      start: "2026-08-01",
      end: "2026-08-31",
    });
    expect(currentPeriod("quarter")).toEqual({
      granularity: "quarter",
      start: "2026-07-01",
      end: "2026-09-30",
    });
  });

  it("uses the LOCAL day, not the UTC one, near midnight", () => {
    // 2026-09-01 00:30 local is still 2026-08-31 in UTC for anyone east of UTC and
    // 2026-09-01 06:30 UTC west of it. The navigator must open on the operator's own
    // calendar month, which is what getFullYear()/getMonth() give.
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 1, 0, 30, 0)); // 2026-09-01 00:30 local

    expect(currentPeriod("month").start).toBe("2026-09-01");
  });
});

describe("timezone safety (the reason toISOString is banned in this module)", () => {
  /** Swaps the process timezone for one assertion block. Node re-reads TZ per Date
   *  operation, so this really does move the clock. */
  function withTimeZone(tz: string, body: () => void): void {
    const previous = process.env.TZ;
    process.env.TZ = tz;
    try {
      body();
    } finally {
      if (previous === undefined) delete process.env.TZ;
      else process.env.TZ = previous;
      // Force the runtime to pick the original zone back up before the next test.
      new Date().getTimezoneOffset();
    }
  }

  /** Whether the swap actually took effect on this platform/Node build. */
  function timeZoneSwapWorks(): boolean {
    const previous = process.env.TZ;
    process.env.TZ = "America/Winnipeg";
    const shifted = new Date(2026, 7, 1).getTimezoneOffset();
    process.env.TZ = "UTC";
    const utc = new Date(2026, 7, 1).getTimezoneOffset();
    if (previous === undefined) delete process.env.TZ;
    else process.env.TZ = previous;
    return shifted !== utc;
  }

  it("keeps month boundaries intact west of UTC (America/Winnipeg)", () => {
    // A `toISOString()`-based implementation returns 2026-07-31 for the start of August
    // in any UTC-negative zone. That is the exact regression this test exists to catch.
    if (!timeZoneSwapWorks()) {
      expect(periodContaining("2026-08-01", "month").start).toBe("2026-08-01");
      return;
    }

    withTimeZone("America/Winnipeg", () => {
      expect(periodContaining("2026-08-01", "month")).toEqual({
        granularity: "month",
        start: "2026-08-01",
        end: "2026-08-31",
      });
      expect(periodContaining("2026-01-01", "month").start).toBe("2026-01-01");
      expect(periodContaining("2026-12-31", "month").end).toBe("2026-12-31");
      expect(periodContaining("2026-08-14", "quarter")).toEqual({
        granularity: "quarter",
        start: "2026-07-01",
        end: "2026-09-30",
      });
      expect(stepPeriod(periodContaining("2026-12-10", "month"), 1).start).toBe("2027-01-01");
      expect(periodLabel(periodContaining("2026-08-14", "month"))).toBe("August 2026");
    });
  });

  it("keeps month boundaries intact east of UTC (Pacific/Kiritimati, UTC+14)", () => {
    if (!timeZoneSwapWorks()) {
      expect(periodContaining("2026-08-31", "month").end).toBe("2026-08-31");
      return;
    }

    withTimeZone("Pacific/Kiritimati", () => {
      expect(periodContaining("2026-08-31", "month")).toEqual({
        granularity: "month",
        start: "2026-08-01",
        end: "2026-08-31",
      });
      expect(periodContaining("2028-02-29", "month").end).toBe("2028-02-29");
    });
  });
});
