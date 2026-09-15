import { describe, expect, it } from "vitest";
import {
  MAX_CYCLE_H_PER_7_DAYS,
  MAX_DRIVING_H_PER_DAY,
  MAX_ON_DUTY_H_PER_DAY,
  credentialKind,
  formatDurationH,
  hosRemaining,
  today,
} from "./data";
import type { HosEntry } from "./types";

// Hours of service and expiry banding. Both are thresholds, so both are tested on BOTH SIDES of
// every boundary — an off-by-one in a compliance figure is not a cosmetic bug, and a band that
// is wrong by one hour tells a driver they may legally keep driving when they may not.

function entry(over: Partial<HosEntry> = {}): HosEntry {
  return {
    id: "HOS-T",
    date: today,
    duty: "Driving",
    onDutyH: 0,
    drivingH: 0,
    offDutyH: 0,
    source: "Driver App",
    enteredBy: null,
    note: "",
    ...over,
  };
}

describe("hosRemaining", () => {
  it("subtracts today's hours from the CVDHS daily limits", () => {
    const r = hosRemaining([entry({ drivingH: 5, onDutyH: 6 })]);
    expect(r.drivingRemainingH).toBe(MAX_DRIVING_H_PER_DAY - 5);
    expect(r.onDutyRemainingH).toBe(MAX_ON_DUTY_H_PER_DAY - 6);
  });

  it("sums the rolling 7-day cycle from on-duty hours", () => {
    const week = Array.from({ length: 7 }, (_, i) =>
      entry({ id: `HOS-${i}`, date: `2026-09-0${i + 1}`, onDutyH: 8 }),
    );
    expect(hosRemaining(week).cycleRemainingH).toBe(MAX_CYCLE_H_PER_7_DAYS - 56);
  });

  it("counts only the first seven entries toward the cycle", () => {
    // The limit is a rolling 7 days. An eighth row must not quietly consume cycle hours, or a
    // driver is told they have less time than the law allows and refuses work they could take.
    const eight = Array.from({ length: 8 }, (_, i) =>
      entry({ id: `HOS-${i}`, date: `2026-09-0${i + 1}`, onDutyH: 5 }),
    );
    expect(hosRemaining(eight).cycleRemainingH).toBe(MAX_CYCLE_H_PER_7_DAYS - 35);
  });

  it("treats a day with no entry as no hours used", () => {
    const r = hosRemaining([entry({ date: "2026-01-01", drivingH: 9, onDutyH: 11 })]);
    expect(r.drivingRemainingH).toBe(MAX_DRIVING_H_PER_DAY);
    expect(r.onDutyRemainingH).toBe(MAX_ON_DUTY_H_PER_DAY);
  });

  describe("status banding, on both sides of each boundary", () => {
    it("is ontime at exactly 3h remaining and soon just under", () => {
      expect(hosRemaining([entry({ drivingH: MAX_DRIVING_H_PER_DAY - 3 })]).hk).toBe("ontime");
      expect(hosRemaining([entry({ drivingH: MAX_DRIVING_H_PER_DAY - 2.99 })]).hk).toBe("soon");
    });

    it("is soon at exactly 1h remaining and over just under", () => {
      expect(hosRemaining([entry({ drivingH: MAX_DRIVING_H_PER_DAY - 1 })]).hk).toBe("soon");
      expect(hosRemaining([entry({ drivingH: MAX_DRIVING_H_PER_DAY - 0.99 })]).hk).toBe("over");
    });

    it("bands on the TIGHTEST of the three limits, not driving alone", () => {
      // A driver can be well inside their daily driving limit and still out of cycle. Banding
      // on driving alone would show a reassuring green while the real constraint is breached.
      const week = Array.from({ length: 7 }, (_, i) =>
        entry({ id: `HOS-${i}`, date: `2026-09-0${i + 1}`, onDutyH: 10 }),
      );
      expect(hosRemaining(week).cycleRemainingH).toBe(0);
      expect(hosRemaining(week).hk).toBe("over");
    });
  });
});

describe("credentialKind", () => {
  it("is over once expired, on the day after expiry", () => {
    expect(credentialKind("2026-09-11", "2026-09-12")).toBe("over");
  });

  it("is soon on the expiry day itself, not over", () => {
    // A credential is valid through its expiry date. Showing "expired" a day early would have a
    // driver stand down a shift they were entitled to work.
    expect(credentialKind("2026-09-12", "2026-09-12")).toBe("soon");
  });

  it("is soon at exactly 30 days and ontime at 31", () => {
    expect(credentialKind("2026-10-12", "2026-09-12")).toBe("soon");
    expect(credentialKind("2026-10-13", "2026-09-12")).toBe("ontime");
  });
});

describe("formatDurationH", () => {
  it("writes hours and minutes without leaning on a decimal", () => {
    expect(formatDurationH(2.75)).toBe("2h 45m");
    expect(formatDurationH(3)).toBe("3h");
    expect(formatDurationH(0.5)).toBe("30m");
  });

  it("writes an explicit minus so a negative never rests on colour", () => {
    expect(formatDurationH(-1.5)).toBe("−1h 30m");
  });
});
