import { describe, expect, it } from "vitest";
import {
  MAX_DRIVING_HOURS_PER_DAY,
  MAX_ON_DUTY_HOURS_PER_DAY,
  MIN_OFF_DUTY_HOURS_PER_DAY,
  hosRemainingFor,
  hosViolationsFor,
  violationsForEntry,
} from "./hosRules";
import type { HosEntryRecord } from "./api/drivers";

// Hours-of-Service is the one place in this console where a silent regression is a
// COMPLIANCE problem, not a cosmetic one: the module comment notes the backend persists
// duty entries but does NOT reject over-limit days, so these client-side rules are the
// only thing that surfaces a violation to a dispatcher. The CVDHS numbers (13h driving /
// 14h on-duty / 10h off-duty) are pinned as literals below deliberately — a test that
// reused the exported constants would pass no matter what they were changed to.

const CVDHS_DRIVING_MAX = 13;
const CVDHS_ON_DUTY_MAX = 14;
const CVDHS_OFF_DUTY_MIN = 10;

function entry(fields: Partial<HosEntryRecord> = {}): HosEntryRecord {
  return {
    id: "hos-1",
    driverId: "driver-1",
    date: "2026-07-20",
    duty: "Driving",
    onDutyH: 12,
    drivingH: 10,
    offDutyH: 11,
    source: "Driver App",
    enteredBy: null,
    note: null,
    recordedAtUtc: "2026-07-20T18:00:00Z",
    ...fields,
  };
}

describe("CVDHS thresholds", () => {
  it("pins the daily cycle numbers", () => {
    expect(MAX_DRIVING_HOURS_PER_DAY).toBe(CVDHS_DRIVING_MAX);
    expect(MAX_ON_DUTY_HOURS_PER_DAY).toBe(CVDHS_ON_DUTY_MAX);
    expect(MIN_OFF_DUTY_HOURS_PER_DAY).toBe(CVDHS_OFF_DUTY_MIN);
  });

  it("keeps driving under on-duty and the three limits internally consistent", () => {
    // Driving is a subset of on-duty time, so its cap can never exceed the on-duty cap;
    // and on-duty + minimum off-duty must fit in a 24h day.
    expect(MAX_DRIVING_HOURS_PER_DAY).toBeLessThan(MAX_ON_DUTY_HOURS_PER_DAY);
    expect(MAX_ON_DUTY_HOURS_PER_DAY + MIN_OFF_DUTY_HOURS_PER_DAY).toBeLessThanOrEqual(24);
  });
});

describe("violationsForEntry — driving limit", () => {
  it("passes a compliant day clean", () => {
    expect(violationsForEntry(entry())).toEqual([]);
  });

  it("does not flag exactly 13h driven — the limit is a maximum, not an exclusive bound", () => {
    expect(violationsForEntry(entry({ drivingH: CVDHS_DRIVING_MAX }))).toEqual([]);
  });

  it("flags the first fraction over 13h", () => {
    const violations = violationsForEntry(entry({ drivingH: CVDHS_DRIVING_MAX + 0.1 }));

    expect(violations).toHaveLength(1);
    expect(violations[0].kind).toBe("driving");
    expect(violations[0].date).toBe("2026-07-20");
    expect(violations[0].message).toContain("13h max");
  });
});

describe("violationsForEntry — on-duty limit", () => {
  it("does not flag exactly 14h on duty", () => {
    expect(violationsForEntry(entry({ onDutyH: CVDHS_ON_DUTY_MAX }))).toEqual([]);
  });

  it("flags 14.1h on duty", () => {
    const violations = violationsForEntry(entry({ onDutyH: CVDHS_ON_DUTY_MAX + 0.1 }));

    expect(violations).toHaveLength(1);
    expect(violations[0].kind).toBe("onDuty");
    expect(violations[0].message).toContain("14h max");
  });
});

describe("violationsForEntry — off-duty minimum", () => {
  it("does not flag exactly 10h off duty — the minimum is inclusive", () => {
    expect(violationsForEntry(entry({ offDutyH: CVDHS_OFF_DUTY_MIN }))).toEqual([]);
  });

  it("flags 9.9h off duty", () => {
    const violations = violationsForEntry(entry({ offDutyH: CVDHS_OFF_DUTY_MIN - 0.1 }));

    expect(violations).toHaveLength(1);
    expect(violations[0].kind).toBe("offDuty");
    expect(violations[0].message).toContain("10h minimum");
  });

  it("flags a zero-rest day", () => {
    const violations = violationsForEntry(entry({ offDutyH: 0 }));

    expect(violations).toHaveLength(1);
    expect(violations[0].kind).toBe("offDuty");
  });
});

describe("violationsForEntry — multiple breaches", () => {
  it("reports all three independently, in driving/onDuty/offDuty order", () => {
    const violations = violationsForEntry(
      entry({ drivingH: 15, onDutyH: 16, offDutyH: 4, date: "2026-07-21" }),
    );

    expect(violations.map((v) => v.kind)).toEqual(["driving", "onDuty", "offDuty"]);
    expect(violations.every((v) => v.date === "2026-07-21")).toBe(true);
  });
});

describe("hosViolationsFor", () => {
  it("returns nothing for an empty log", () => {
    expect(hosViolationsFor([])).toEqual([]);
  });

  it("flattens violations across a multi-day log and keeps clean days out", () => {
    const violations = hosViolationsFor([
      entry({ id: "a", date: "2026-07-20" }), // clean
      entry({ id: "b", date: "2026-07-21", drivingH: 14 }), // driving
      entry({ id: "c", date: "2026-07-22", offDutyH: 6 }), // off-duty
    ]);

    expect(violations).toHaveLength(2);
    expect(violations.map((v) => [v.date, v.kind])).toEqual([
      ["2026-07-21", "driving"],
      ["2026-07-22", "offDuty"],
    ]);
  });
});

describe("hosRemainingFor — the gauge", () => {
  it("shows an em dash and the offline kind when there is no entry", () => {
    expect(hosRemainingFor(null)).toEqual({ label: "—", kind: "off", pct: 0 });
  });

  it("counts down from the 13h driving cap", () => {
    expect(hosRemainingFor(0)).toMatchObject({ label: "13h 00m", pct: 100 });
    expect(hosRemainingFor(5)).toMatchObject({ label: "8h 00m", kind: "ontime" });
    expect(hosRemainingFor(2.5)).toMatchObject({ label: "10h 30m" });
  });

  it("never reports negative remaining hours for an over-limit day", () => {
    // The driver has already blown the cap; the gauge floors at zero rather than
    // showing "-2h 00m".
    expect(hosRemainingFor(15)).toEqual({ label: "0h 00m", kind: "over", pct: 0 });
  });

  it("turns caution at 4h remaining and problem at zero — the status boundaries", () => {
    // Colour is never alone here: kind travels with a label. ontime > 4h, soon <= 4h,
    // over <= 0h.
    expect(hosRemainingFor(8.9).kind).toBe("ontime"); // 4.1h left
    expect(hosRemainingFor(9).kind).toBe("soon"); // exactly 4h left
    expect(hosRemainingFor(12.9).kind).toBe("soon"); // 0.1h left
    expect(hosRemainingFor(13).kind).toBe("over"); // exactly at the cap
  });

  it("always pairs a non-empty label with the status kind", () => {
    for (const driven of [0, 4, 9, 13, 20]) {
      const gauge = hosRemainingFor(driven);
      expect(gauge.label.trim().length, String(driven)).toBeGreaterThan(0);
      expect(["ontime", "soon", "over", "off"]).toContain(gauge.kind);
    }
  });

  it("keeps pct inside 0–100 for every real driving total", () => {
    for (const driven of [0, 6.5, 13, 40]) {
      const { pct } = hosRemainingFor(driven);
      expect(pct, String(driven)).toBeGreaterThanOrEqual(0);
      expect(pct, String(driven)).toBeLessThanOrEqual(100);
    }
  });

  it("rounds minutes rather than truncating them", () => {
    // 12h 20m driven leaves 0h 40m — a truncating implementation would say 0h 39m.
    expect(hosRemainingFor(12 + 20 / 60).label).toBe("0h 40m");
    expect(hosRemainingFor(0.5).label).toBe("12h 30m");
  });
});
