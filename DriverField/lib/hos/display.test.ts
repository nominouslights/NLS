import { describe, expect, it } from "vitest";
import { statusMeta } from "../theme";
import { DEFAULT_HOS_BANDS, HOS_LIMITS, HosCalendar, type DayTotals, type HosEvaluation, type HosViolation } from "./engine";
import {
  ALERT_LIMIT_LABEL,
  ALERT_THRESHOLDS_SECONDS,
  alertThresholdCrossed,
  band,
  blockingText,
  clockLabel,
  dayBand,
  durationLabel,
  exceptionKindShort,
  exceptionStatusMeta,
  kmLabel,
  limitRemaining,
  localIsoToUnix,
  OUT_OF_SERVICE_GLYPH,
  PAPER_LOG_GLYPH,
  PC_WARN_DECIKM,
  pcDayVerdict,
  ruleShort,
  whenLabel,
} from "./display";
import { BLOCKING_REASONS, HOS_BANDS, HOS_RULES } from "./engine";

// The one band set, both sides of its boundary, and the strings next to the figures. The band
// itself is the engine's — here we pin that the design system renders each band as colour +
// glyph + label, that the 7-day bar's per-day banding uses the engine's soon window rather
// than a local number, and that the local-time helpers agree with HosCalendar.

const H = 3600;
const CAL = new HosCalendar();

function day(fields: Partial<DayTotals>): DayTotals {
  return {
    date: "2026-09-10",
    known: true,
    complete: true,
    drivingSeconds: 0,
    onDutySeconds: 0,
    otherCarrierOnDutySeconds: 0,
    offDutySeconds: 24 * H,
    rawOffDutySeconds: 24 * H,
    pcDecikm: 0,
    hasOwnEvents: true,
    certified: true,
    eightHourConsecutiveMet: true,
    dailyOffRequiredSeconds: 10 * H,
    offDutyShortfallSeconds: 0,
    dailyOffMet: true,
    ...fields,
  };
}

function violation(date: string, suppressed = false): HosViolation {
  return { rule: "DailyDriving13h", date, atUnix: 0, figureSeconds: 0, limitSeconds: 0, suppressed, suppressedByExceptionId: null, message: "m" };
}

function evaluationWith(fields: Partial<HosEvaluation>): HosEvaluation {
  return { violations: [], band: "Ontime", blockingReasons: [], ...fields } as HosEvaluation;
}

describe("band", () => {
  it("maps every engine band onto a StatusKind that carries a glyph, with a label", () => {
    const expected = { Ontime: "ontime", Soon: "soon", Over: "over", Off: "off" } as const;
    for (const b of HOS_BANDS) {
      const got = band(evaluationWith({ band: b }));
      expect(got.kind).toBe(expected[b]);
      expect(got.label.trim().length).toBeGreaterThan(0);
      expect(statusMeta(got.kind).g.trim().length).toBeGreaterThan(0);
    }
  });

  it("reads Over as 'Do not drive' — the band the engine gives whenever canDriveNow is false", () => {
    expect(band(evaluationWith({ band: "Over" }))).toEqual({ kind: "over", label: "Do not drive" });
    expect(band(evaluationWith({ band: "Soon" }))).toEqual({ kind: "soon", label: "Low" });
  });
});

describe("dayBand — the 7-day bar", () => {
  const e = evaluationWith({ violations: [violation("2026-09-08"), violation("2026-09-09", true)] });

  it("is Over on a day with an unsuppressed violation, and not on one whose violation was suppressed", () => {
    expect(dayBand(day({ date: "2026-09-08", onDutySeconds: 10 * H }), e)).toEqual({ kind: "over", label: "Breach" });
    expect(dayBand(day({ date: "2026-09-09", onDutySeconds: 10 * H, drivingSeconds: 8 * H }), e).kind).toBe("ontime");
  });

  it("is Off on a day with no on-duty time", () => {
    expect(dayBand(day({ onDutySeconds: 0 }), e)).toEqual({ kind: "off", label: "Off" });
  });

  it("turns Soon exactly at the engine's soon window from the daily driving limit, not a second before", () => {
    const soon = DEFAULT_HOS_BANDS.soonSeconds;
    // On duty 11h keeps the on-duty total 3h clear of its own limit, so only driving decides.
    const atWindow = HOS_LIMITS.dailyDrivingSeconds - soon;
    expect(dayBand(day({ onDutySeconds: 11 * H, drivingSeconds: atWindow }), e).kind).toBe("soon");
    expect(dayBand(day({ onDutySeconds: 11 * H, drivingSeconds: atWindow - 1 }), e).kind).toBe("ontime");
  });

  it("turns Soon at the engine's soon window from the daily on-duty limit too", () => {
    const soon = DEFAULT_HOS_BANDS.soonSeconds;
    const atWindow = HOS_LIMITS.dailyOnDutySeconds - soon;
    expect(dayBand(day({ onDutySeconds: atWindow, drivingSeconds: 2 * H }), e)).toEqual({ kind: "soon", label: "Near limit" });
    expect(dayBand(day({ onDutySeconds: atWindow - 1, drivingSeconds: 2 * H }), e).kind).toBe("ontime");
  });
});

describe("the driver's reasons", () => {
  it("has prose for every BlockingReason and a short name for every HosRule", () => {
    for (const r of BLOCKING_REASONS) expect(blockingText(r).trim().length).toBeGreaterThan(0);
    for (const r of HOS_RULES) expect(ruleShort(r).trim().length).toBeGreaterThan(0);
  });
});

describe("alert thresholds", () => {
  it("are 60 / 30 / 0 minutes, descending — the owner's policy default", () => {
    expect([...ALERT_THRESHOLDS_SECONDS]).toEqual([3600, 1800, 0]);
  });

  it("reports a crossing strictly from above, once, on both sides of each threshold", () => {
    expect(alertThresholdCrossed(3601, 3600)).toBe(3600);
    expect(alertThresholdCrossed(3601, 3599)).toBe(3600);
    expect(alertThresholdCrossed(3600, 3599)).toBeNull();
    expect(alertThresholdCrossed(1801, 1800)).toBe(1800);
    expect(alertThresholdCrossed(1800, 1799)).toBeNull();
    expect(alertThresholdCrossed(1, 0)).toBe(0);
    expect(alertThresholdCrossed(0, 0)).toBeNull();
    expect(alertThresholdCrossed(7200, 3601)).toBeNull();
  });

  it("reports the tightest threshold when a jump crosses several", () => {
    expect(alertThresholdCrossed(3601, 0)).toBe(0);
    expect(alertThresholdCrossed(3601, 1799)).toBe(1800);
    expect(alertThresholdCrossed(Number.POSITIVE_INFINITY, 2000)).toBe(3600);
  });

  it("watches the four tiles' figures, with the window alone rather than mustStopDrivingBy, and labels each", () => {
    const e = evaluationWith({
      remainingDrivingTodaySeconds: 100,
      remainingOnDutyTodaySeconds: 200,
      shift: { startUnix: 1000, elapsedSeconds: 10 * H, elapsedCeilingSeconds: 16 * H, mustStopDrivingByUnix: 1100 } as HosEvaluation["shift"],
      cycle: { remainingSeconds: 400 } as HosEvaluation["cycle"],
    });
    expect(limitRemaining(e)).toEqual({ driving: 100, onDuty: 200, window: 6 * H, cycle: 400 });
    const between = evaluationWith({ ...e, shift: { ...e.shift, startUnix: null } });
    expect(limitRemaining(between).window).toBeNull();
    for (const label of Object.values(ALERT_LIMIT_LABEL)) expect(label.trim().length).toBeGreaterThan(0);
  });
});

describe("durationLabel", () => {
  it("writes hours and minutes from seconds, floors at zero, and never leans on a decimal", () => {
    expect(durationLabel(10 * H + 15 * 60)).toBe("10h 15m");
    expect(durationLabel(3 * H)).toBe("3h");
    expect(durationLabel(30 * 60)).toBe("30m");
    expect(durationLabel(-5)).toBe("0m");
  });
});

describe("local time (America/Winnipeg)", () => {
  it("reads a mock row's zoneless ISO as Winnipeg wall time", () => {
    // 11:00 CDT (UTC−5) on 2026-09-12 is 16:00Z.
    expect(localIsoToUnix("2026-09-12T11:00:00")).toBe(Date.UTC(2026, 8, 12, 16, 0, 0) / 1000);
    expect(clockLabel(localIsoToUnix("2026-09-12T11:00:00"))).toBe("11:00");
  });

  it("agrees with HosCalendar on the start of a day, including the DST days", () => {
    for (const d of ["2026-09-12", "2026-03-08", "2026-11-01", "2026-01-15"]) {
      expect(localIsoToUnix(`${d}T00:00:00`), d).toBe(CAL.dayStartUnix(d));
    }
  });

  it("resolves the fall-back ambiguity to the standard offset and a spring-forward gap to the first instant after it", () => {
    // 01:30 on 2026-11-01 happens twice; the standard reading (CST, UTC−6) is 07:30Z — what
    // TimeZoneInfo.GetUtcOffset returns for an ambiguous DateTime, and what the engine does.
    expect(localIsoToUnix("2026-11-01T01:30:00")).toBe(Date.UTC(2026, 10, 1, 7, 30, 0) / 1000);
    // 02:30 on 2026-03-08 does not exist; the previous offset (CST) puts it at 08:30Z = 03:30 CDT.
    expect(localIsoToUnix("2026-03-08T02:30:00")).toBe(Date.UTC(2026, 2, 8, 8, 30, 0) / 1000);
    expect(clockLabel(localIsoToUnix("2026-03-08T02:30:00"))).toBe("03:30");
  });

  it("writes a far-off instant with its day, on the regulation's clock", () => {
    // 2026-10-12T19:30Z is Mon Oct 12, 14:30 CDT.
    expect(whenLabel(Date.UTC(2026, 9, 12, 19, 30) / 1000)).toMatch(/Mon.*Oct.*12.*14:30/);
  });
});

describe("pcDayVerdict — today's personal conveyance against the 75 km allowance", () => {
  it("bands at 60.0 km (gold ◐) and 75.0 km (vermillion ▲), one tenth of a km each side", () => {
    expect(PC_WARN_DECIKM).toBe(600);
    expect(HOS_LIMITS.pcDailyLimitDecikm).toBe(750);
    expect(pcDayVerdict(599).kind).toBe("ontime");
    expect(pcDayVerdict(600).kind).toBe("soon");
    expect(pcDayVerdict(749).kind).toBe("soon");
    expect(pcDayVerdict(750).kind).toBe("over");
    expect(pcDayVerdict(751).kind).toBe("over");
    expect(statusMeta(pcDayVerdict(600).kind).g).toBe("◐");
    expect(statusMeta(pcDayVerdict(750).kind).g).toBe("▲");
  });

  it("says what is left, and at 75 km that today's personal conveyance is on-duty time", () => {
    expect(pcDayVerdict(0)).toMatchObject({ usedDecikm: 0, leftDecikm: 750, overLimit: false, label: "75.0 km left before it counts as on-duty" });
    expect(pcDayVerdict(599)).toMatchObject({ leftDecikm: 151, label: "15.1 km left before it counts as on-duty" });
    expect(pcDayVerdict(600).label).toBe("Nearing the 75 km allowance — 15.0 km left");
    expect(pcDayVerdict(749).label).toBe("Nearing the 75 km allowance — 0.1 km left");
    expect(pcDayVerdict(750)).toMatchObject({ leftDecikm: 0, overLimit: true, label: "Over 75 km — today's personal conveyance is on-duty time" });
    expect(pcDayVerdict(900)).toMatchObject({ usedDecikm: 900, leftDecikm: 0, overLimit: true });
    expect(pcDayVerdict(-5).usedDecikm).toBe(0);
  });

  it("writes kilometres from tenths, never leaning on floating point", () => {
    expect(kmLabel(0)).toBe("0.0 km");
    expect(kmLabel(1234567)).toBe("123456.7 km");
    expect(kmLabel(750)).toBe("75.0 km");
    expect(kmLabel(-15)).toBe("−1.5 km");
  });
});

describe("exceptions, out of service and paper-log mode read as colour + glyph + label", () => {
  it("gives each of the five HosExceptionStatus members a band and a label", () => {
    expect(exceptionStatusMeta("Declared")).toEqual({ kind: "soon", label: "Declared" });
    expect(exceptionStatusMeta("Acknowledged")).toEqual({ kind: "info", label: "Acknowledged by dispatch" });
    expect(exceptionStatusMeta("Approved")).toEqual({ kind: "ontime", label: "Approved" });
    expect(exceptionStatusMeta("Rejected")).toEqual({ kind: "over", label: "Rejected" });
    expect(exceptionStatusMeta("Withdrawn")).toEqual({ kind: "off", label: "Withdrawn" });
    for (const s of ["Declared", "Acknowledged", "Approved", "Rejected", "Withdrawn"] as const) {
      expect(statusMeta(exceptionStatusMeta(s).kind).g.trim().length).toBeGreaterThan(0);
    }
  });

  it("shortens the wire kinds for a chip without changing what goes to the API", () => {
    expect(exceptionKindShort("Adverse driving conditions")).toBe("Adverse conditions");
    expect(exceptionKindShort("Emergency")).toBe("Emergency");
    expect(exceptionKindShort("Off-duty deferral (CO approved)")).toBe("Off-duty deferral");
  });

  it("overrides the glyph, not the colour: out of service is `over` with ✕, paper-log mode is `soon` with ✎", () => {
    // Two states on one colour each, told apart from their colour-mates (▲ breach, ◐ near
    // limit) by glyph and label — never a fifth StatusKind, never a new hex.
    expect(OUT_OF_SERVICE_GLYPH).toBe("✕");
    expect(OUT_OF_SERVICE_GLYPH).not.toBe(statusMeta("over").g);
    expect(PAPER_LOG_GLYPH).toBe("✎");
    expect(PAPER_LOG_GLYPH).not.toBe(statusMeta("soon").g);
  });
});
