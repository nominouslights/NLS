import { describe, expect, it } from "vitest";
import { BLOCKING_REASONS, EVENT_SOURCES, HosWire } from "./engine";
import {
  blockingShort,
  blockingText,
  dutyStateFromWire,
  EXCEPTION_STATUS_CHIP,
  exceptionInForce,
  exceptionKindLabel,
  exceptionStatusChip,
  formatClock,
  formatDateOnly,
  formatDateTimeUtc,
  formatRemaining,
  HOS_CALENDAR,
  isoOfUnix,
  localDateTimeOf,
  localTimeToUnix,
  PC_CAUTION_KM,
  PC_DAILY_LIMIT_KM,
  pcDayVerdict,
  previewDay,
  remainingKind,
  SOURCE_GLYPH,
  sourceGlyphFor,
} from "./display";

describe("pcDayVerdict — the personal-conveyance bands", () => {
  it("reads 75.0 km from the engine's limit and cautions from 60", () => {
    expect(PC_DAILY_LIMIT_KM).toBe(75);
    expect(PC_CAUTION_KM).toBe(60);
  });

  it("is ✓ under 60, ◐ with the km left from 60 to 74.9, ▲ at 75 and beyond — each a different glyph", () => {
    expect(pcDayVerdict(59.9)).toEqual({ kind: "ontime", glyph: "✓", label: "59.9 km" });
    expect(pcDayVerdict(60)).toEqual({ kind: "soon", glyph: "◐", label: "60.0 km — 15.0 km left" });
    expect(pcDayVerdict(74.9)).toEqual({ kind: "soon", glyph: "◐", label: "74.9 km — 0.1 km left" });
    expect(pcDayVerdict(75)).toEqual({ kind: "over", glyph: "▲", label: "75.0 km — 75 km reached, remainder counts as on-duty" });
    expect(pcDayVerdict(75.1).kind).toBe("over");
    expect(pcDayVerdict(0)).toEqual({ kind: "ontime", glyph: "✓", label: "0.0 km" });
  });
});

describe("exception chips", () => {
  it("gives each status its own kind and glyph, with the status as the label", () => {
    expect(exceptionStatusChip("Declared")).toEqual({ kind: "soon", glyph: "◐", label: "Declared" });
    expect(exceptionStatusChip("Acknowledged")).toEqual({ kind: "info", glyph: "●", label: "Acknowledged" });
    expect(exceptionStatusChip("Approved")).toEqual({ kind: "ontime", glyph: "✓", label: "Approved" });
    expect(exceptionStatusChip("Rejected")).toEqual({ kind: "over", glyph: "▲", label: "Rejected" });
    expect(exceptionStatusChip("Withdrawn")).toEqual({ kind: "off", glyph: "—", label: "Withdrawn" });
    const glyphs = Object.values(EXCEPTION_STATUS_CHIP).map((c) => c.glyph);
    expect(new Set(glyphs).size).toBe(glyphs.length);
  });

  it("in force = the statuses the engine honours", () => {
    expect(["Declared", "Acknowledged", "Approved"].map((s) => exceptionInForce(s as "Declared"))).toEqual([true, true, true]);
    expect(["Rejected", "Withdrawn"].map((s) => exceptionInForce(s as "Rejected"))).toEqual([false, false]);
  });

  it("shortens the deferral's wire kind and passes the others through", () => {
    expect(exceptionKindLabel(HosWire.OffDutyDeferralKind as "Off-duty deferral (CO approved)")).toBe("Off-duty deferral");
    expect(exceptionKindLabel("Adverse driving conditions")).toBe("Adverse driving conditions");
    expect(exceptionKindLabel("Emergency")).toBe("Emergency");
  });
});

describe("local date-time helpers", () => {
  it("formatDateTimeUtc renders America/Winnipeg wall time with a short month", () => {
    // 2026-10-12 06:00 CDT = 11:00Z.
    expect(formatDateTimeUtc("2026-10-12T11:00:00Z")).toBe("Oct 12, 06:00");
    expect(formatDateTimeUtc("not a date")).toBe("—");
  });

  it("localDateTimeOf and isoOfUnix round-trip through localTimeToUnix", () => {
    const unix = Date.UTC(2026, 9, 12, 11) / 1000;
    expect(localDateTimeOf(unix)).toEqual({ date: "2026-10-12", time: "06:00" });
    expect(localTimeToUnix("2026-10-12", "06:00")).toBe(unix);
    expect(isoOfUnix(unix)).toBe("2026-10-12T11:00:00.000Z");
  });
});

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
