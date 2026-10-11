import { describe, expect, it } from "vitest";
import { BLOCKING_REASONS, EVENT_SOURCES, HosWire } from "./engine";
import {
  blockingShort,
  blockingText,
  dutyStateFromWire,
  formatClock,
  formatDateOnly,
  formatRemaining,
  HOS_CALENDAR,
  localTimeToUnix,
  previewDay,
  remainingKind,
  SOURCE_GLYPH,
  sourceGlyphFor,
} from "./display";

// The console's HOS display helpers: the band thresholds the gauge and roster share, the
// local-time arithmetic the entry and amendment modals rely on (DST-safe), and the
// transcription preview that runs the engine mirror on one day's rows.

describe("remainingKind", () => {
  it("is Over at 0, Soon at or under 2h, Ontime above", () => {
    expect(remainingKind(0)).toBe("over");
    expect(remainingKind(-5)).toBe("over");
    expect(remainingKind(7200)).toBe("soon");
    expect(remainingKind(7201)).toBe("ontime");
  });
});

describe("formatting", () => {
  it("formatRemaining never goes negative and zero-pads minutes", () => {
    expect(formatRemaining(8 * 3600 + 5 * 60)).toBe("8h 05m");
    expect(formatRemaining(-100)).toBe("0h 00m");
  });

  it("formatDateOnly never slides a yyyy-MM-dd by a day", () => {
    expect(formatDateOnly("2026-10-09")).toBe("Fri, Oct 9");
    expect(formatDateOnly("2026-01-01")).toBe("Thu, Jan 1");
  });

  it("formatClock renders America/Winnipeg wall time", () => {
    expect(formatClock(HOS_CALENDAR.dayStartUnix("2026-10-09") + 14 * 3600 + 5 * 60)).toBe("14:05");
  });
});

describe("localTimeToUnix", () => {
  it("round-trips through the calendar on an ordinary day", () => {
    expect(localTimeToUnix("2026-10-09", "06:30")).toBe(HOS_CALENDAR.dayStartUnix("2026-10-09") + 6 * 3600 + 1800);
  });

  it("is DST-aware: an afternoon on the fall-back day is 25h-day arithmetic, not 24h", () => {
    // 2026-11-01 is 25 hours long in Winnipeg; 14:00 is 15h after the day start.
    expect(localTimeToUnix("2026-11-01", "14:00")).toBe(HOS_CALENDAR.dayStartUnix("2026-11-01") + 15 * 3600);
    // and on the spring-forward day, 14:00 is 13h after the start.
    expect(localTimeToUnix("2026-03-08", "14:00")).toBe(HOS_CALENDAR.dayStartUnix("2026-03-08") + 13 * 3600);
  });

  it("rejects malformed times", () => {
    expect(localTimeToUnix("2026-10-09", "")).toBeNull();
    expect(localTimeToUnix("2026-10-09", "25:00")).toBeNull();
  });
});

describe("wire helpers", () => {
  it("every source has its own glyph", () => {
    expect(new Set(EVENT_SOURCES.map((s) => SOURCE_GLYPH[s])).size).toBe(EVENT_SOURCES.length);
    for (const s of EVENT_SOURCES) {
      expect(sourceGlyphFor(HosWire.source(s))).toBe(SOURCE_GLYPH[s]);
    }
  });

  it("every blocking reason has prose, and a short roster form that fits a chip", () => {
    for (const r of BLOCKING_REASONS) {
      expect(blockingText(r).trim().length).toBeGreaterThan(0);
      expect(blockingShort(r).trim().length).toBeGreaterThan(0);
      expect(blockingShort(r).length, r).toBeLessThanOrEqual(20);
    }
  });

  it("falls back to Off Duty for an unknown duty string", () => {
    expect(dutyStateFromWire("Personal Conveyance")).toBe("Personal Conveyance");
    expect(dutyStateFromWire("sleeping")).toBe("Off Duty");
  });
});

describe("previewDay", () => {
  it("derives a compliant shift's totals with no violation and no records noise", () => {
    const ev = previewDay("2026-10-09", [
      { time: "06:00", status: "On Duty" },
      { time: "06:30", status: "Driving" },
      { time: "14:00", status: "On Duty" },
      { time: "14:30", status: "Off Duty" },
    ]);
    const day = ev?.days.find((d) => d.date === "2026-10-09");
    expect(day?.drivingSeconds).toBe(7.5 * 3600);
    expect(day?.onDutySeconds).toBe(8.5 * 3600);
    expect(ev?.violations.filter((v) => !v.suppressed)).toEqual([]);
  });

  it("surfaces a daily driving breach with the server's sentence", () => {
    const ev = previewDay("2026-10-09", [
      { time: "05:00", status: "Driving" },
      { time: "19:00", status: "Off Duty" },
    ]);
    const rules = ev?.violations.map((v) => v.rule);
    expect(rules).toContain("DailyDriving13h");
    expect(ev?.violations.find((v) => v.rule === "DailyDriving13h")?.message).toBe("Driving 14h 0m exceeds the 13h 0m daily limit (s.12(1)).");
  });

  it("is null for a malformed row", () => {
    expect(previewDay("2026-10-09", [{ time: "x", status: "Driving" }])).toBeNull();
  });
});
