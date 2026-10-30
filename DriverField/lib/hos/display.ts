// ---------------------------------------------------------------------------
// HOS display helpers — the ONE band set, and the strings next to the figures.
//
// Every status a screen derives from an evaluation comes through here, so Today, Hours, the
// top bar and the DutyControl cannot disagree about what a figure means. The band itself is the
// engine's (`HosEvaluation.band`: Over when the driver may not drive, Soon inside the policy's
// soon window — 2h by default — Ontime otherwise, Off with no ledger); this file only maps it
// onto the design system's StatusKind so colour, glyph and label travel together. Nothing here
// re-derives a limit: the three local threshold sets this replaced (lib/data.ts `hosRemaining`,
// the Hours tiles, the 7-day bar) are gone.
//
// Colour never stands alone. Each kind carries its glyph through statusMeta()/dutyMeta(), and
// every helper that returns a kind returns a label with it.
// ---------------------------------------------------------------------------

import type { HosExceptionKindWire, HosExceptionStatus } from "../api/hos";
import { formatDurationH } from "../data";
import type { DutyStatus as DutyState, StatusKind } from "../theme";
import {
  DEFAULT_HOS_BANDS,
  HOS_LIMITS,
  HosCalendar,
  HosWire,
  type BlockingReason,
  type DateOnly,
  type DayTotals,
  type DutyStatus,
  type HosBand,
  type HosEvaluation,
  type HosRule,
} from "./engine";

// --- bands -----------------------------------------------------------------

const BAND_KIND: Record<HosBand, StatusKind> = {
  Ontime: "ontime",
  Soon: "soon",
  Over: "over",
  Off: "off",
};

const BAND_LABEL: Record<HosBand, string> = {
  Ontime: "OK",
  Soon: "Low",
  Over: "Do not drive",
  Off: "No ledger",
};

export interface Banded {
  kind: StatusKind;
  label: string;
}

/** The evaluation's band as the design system renders it: kind + the label that goes with it. */
export function band(evaluation: HosEvaluation): Banded {
  return { kind: BAND_KIND[evaluation.band], label: BAND_LABEL[evaluation.band] };
}

/**
 * One past day on the 7-day bar. Over when the engine recorded an unsuppressed violation on
 * that day; Soon when the day's driving or on-duty total came within the soon window of its
 * daily limit; Off for a day with no on-duty time; Ontime otherwise. The soon window is the
 * engine's (DEFAULT_HOS_BANDS), not a local number.
 */
export function dayBand(day: DayTotals, evaluation: HosEvaluation): Banded {
  const breached = evaluation.violations.some((v) => !v.suppressed && v.date === day.date);
  if (breached) return { kind: "over", label: "Breach" };
  if (day.onDutySeconds <= 0) return { kind: "off", label: "Off" };
  const soon = DEFAULT_HOS_BANDS.soonSeconds;
  const nearDriving = HOS_LIMITS.dailyDrivingSeconds - day.drivingSeconds <= soon;
  const nearOnDuty = HOS_LIMITS.dailyOnDutySeconds - day.onDutySeconds <= soon;
  if (nearDriving || nearOnDuty) return { kind: "soon", label: "Near limit" };
  return { kind: "ontime", label: "OK" };
}

// --- duty status <-> the design system's four strings -----------------------

/**
 * Engine member → the display/wire string `dutyMeta()` keys on. Typed in both directions rather
 * than cast from HosWire.status(), and pinned equal to it in lib/wire.test.ts.
 */
const DUTY_STATE_OF: Record<DutyStatus, DutyState> = {
  OffDuty: "Off Duty",
  OnDuty: "On Duty",
  Driving: "Driving",
  PersonalConveyance: "Personal Conveyance",
};

const ENGINE_STATUS_OF: Record<DutyState, DutyStatus> = {
  "Off Duty": "OffDuty",
  "On Duty": "OnDuty",
  Driving: "Driving",
  "Personal Conveyance": "PersonalConveyance",
};

export function dutyStateOf(status: DutyStatus): DutyState {
  return DUTY_STATE_OF[status];
}

export function engineStatusOf(state: DutyState): DutyStatus {
  return ENGINE_STATUS_OF[state];
}

/** The four states in the order the control offers them, with their wire strings. */
export const DUTY_STATES: readonly DutyState[] = ["Off Duty", "On Duty", "Driving", "Personal Conveyance"];

/** Sanity: the Record above and HosWire agree. Exported for the wire pin, not for screens. */
export function dutyStateWire(status: DutyStatus): string {
  return HosWire.status(status);
}

// --- reasons the driver reads ----------------------------------------------

/**
 * Why Driving is disabled, in the driver's words. These are display strings for the engine's
 * BlockingReason members (which have no server prose — HosMessages covers violations, not
 * blocks); the member names themselves are the contract and are pinned in lib/wire.test.ts.
 */
const BLOCKING_TEXT: Record<BlockingReason, string> = {
  NoLedger: "No hours on record for the last 14 days",
  RecordsIncomplete: "Records for the last 14 days are incomplete",
  OutOfService: "Out of service",
  Rest24Expired: "No 24h off-duty period in the last 14 days",
  CycleExhausted: "70h cycle used up",
  DailyDrivingExhausted: "13h daily driving used up",
  DailyOnDutyExhausted: "14h daily on-duty used up",
  ShiftDrivingExhausted: "13h shift driving used up",
  ShiftOnDutyExhausted: "14h shift on-duty used up",
  ShiftWindowClosed: "16h shift window closed",
};

export function blockingText(reason: BlockingReason): string {
  return BLOCKING_TEXT[reason];
}

/** The first blocking reason as prose, or null when the driver may drive. */
export function cannotDriveBecause(evaluation: HosEvaluation): string | null {
  const first = evaluation.blockingReasons[0];
  return first === undefined ? null : BLOCKING_TEXT[first];
}

/** Short names for the rules a "next required off duty" can cite. Display only. */
const RULE_SHORT: Record<HosRule, string> = {
  DailyDriving13h: "13h daily driving",
  DailyOnDuty14h: "14h daily on-duty",
  ShiftDriving13h: "13h shift driving",
  ShiftOnDuty14h: "14h shift on-duty",
  ShiftElapsed16h: "16h shift window",
  DailyOffDuty10h: "10h daily off-duty",
  Rest24hIn14d: "24h off in 14 days",
  Cycle1_70hIn7d: "70h in 7 days",
  DeferralConditions: "off-duty deferral",
  PcExceeds75km: "75 km personal conveyance",
  PcOdometerMissing: "personal conveyance odometer",
  PcWhileOutOfService: "personal conveyance while out of service",
  DroveWhileOutOfService: "drove while out of service",
  RodsNotCertified: "record not certified",
  RecordsIncomplete14d: "14-day records",
  AdverseDeclaredOutsideShift: "adverse conditions outside a shift",
  ConflictingSources: "conflicting sources",
};

export function ruleShort(rule: HosRule): string {
  return RULE_SHORT[rule];
}

// --- personal conveyance: today's kilometres against the 75 km allowance ---------

/**
 * Where the gold band begins on the day's personal-conveyance kilometres: 60.0 km, tenths of a
 * km like every odometer figure in the engine. The limit itself is the engine's
 * (HOS_LIMITS.pcDailyLimitDecikm = 75.0 km; DutyDay refuses to start PC at or past it —
 * PcAllowanceExhausted — and reclassifies the remainder of a segment that crosses it as on-duty
 * driving). The 60 is the owner's "warn at" figure and lives here as one constant; pinned in
 * display.test.ts on both sides of each boundary.
 */
export const PC_WARN_DECIKM = 600;

export interface PcVerdict extends Banded {
  usedDecikm: number;
  /** Tenths of a km left before the allowance is used up; never below zero. */
  leftDecikm: number;
  /** True at or past 75.0 km: no further PC today, and a segment that got here is on-duty time past it. */
  overLimit: boolean;
}

/** "12.5 km" from tenths of a km. */
export function kmLabel(decikm: number): string {
  const abs = Math.abs(decikm);
  return `${decikm < 0 ? "−" : ""}${Math.trunc(abs / 10)}.${abs % 10} km`;
}

/**
 * The band and words for a day's personal-conveyance kilometres. Teal under 60.0 km, gold ◐
 * from 60.0 km, vermillion ▲ from 75.0 km — the tablet tells the driver the allowance is gone
 * at the figure the server refuses the next segment at.
 */
export function pcDayVerdict(usedDecikm: number): PcVerdict {
  const used = Math.max(0, Math.trunc(usedDecikm));
  const limit = HOS_LIMITS.pcDailyLimitDecikm;
  const leftDecikm = Math.max(0, limit - used);
  if (used >= limit) {
    return { kind: "over", label: "Over 75 km — today's personal conveyance is on-duty time", usedDecikm: used, leftDecikm, overLimit: true };
  }
  if (used >= PC_WARN_DECIKM) {
    return { kind: "soon", label: `Nearing the 75 km allowance — ${kmLabel(leftDecikm)} left`, usedDecikm: used, leftDecikm, overLimit: false };
  }
  return { kind: "ontime", label: `${kmLabel(leftDecikm)} left before it counts as on-duty`, usedDecikm: used, leftDecikm, overLimit: false };
}

/** Today's declared personal-conveyance kilometres (closed segments only), from the engine's day totals. */
export function todayPcDecikm(evaluation: HosEvaluation): number {
  const today = new HosCalendar().dayOf(evaluation.asOfUnix);
  return evaluation.days.find((d) => d.date === today)?.pcDecikm ?? 0;
}

// --- exceptions, out of service, paper-log mode: colour + glyph + label ----------

/** How an exception's status reads on a chip. Colour + glyph + the member name, never the tint alone. */
const EXCEPTION_STATUS_META: Record<HosExceptionStatus, { kind: StatusKind; label: string }> = {
  Declared: { kind: "soon", label: "Declared" },
  Acknowledged: { kind: "info", label: "Acknowledged by dispatch" },
  Approved: { kind: "ontime", label: "Approved" },
  Rejected: { kind: "over", label: "Rejected" },
  Withdrawn: { kind: "off", label: "Withdrawn" },
};

export function exceptionStatusMeta(status: HosExceptionStatus): { kind: StatusKind; label: string } {
  return EXCEPTION_STATUS_META[status] ?? { kind: "off", label: status };
}

/** Short names for the wire kinds, for a chip; the wire string itself is what goes to the API. */
const EXCEPTION_KIND_SHORT: Record<HosExceptionKindWire, string> = {
  "Adverse driving conditions": "Adverse conditions",
  Emergency: "Emergency",
  "Off-duty deferral (CO approved)": "Off-duty deferral",
};

export function exceptionKindShort(kind: HosExceptionKindWire): string {
  return EXCEPTION_KIND_SHORT[kind] ?? kind;
}

/**
 * Out of service is `over` with its own ✕, and paper-log mode (an open ELD malfunction) is
 * `soon` with ✎ — glyph overrides on the chips, never new colours: a fifth StatusKind or a
 * new hex is not an option in a copied theme, and sharing a colour is fine so long as the
 * glyph and the label still differ.
 */
export const OUT_OF_SERVICE_GLYPH = "✕";
export const PAPER_LOG_GLYPH = "✎";

export const PAPER_LOG_LABEL = "Paper-log mode";
export const PAPER_LOG_LINE = "Paper-log mode — keep paper RODS until the ELD is fixed. Dispatch transcribes them into this ledger.";

const WHEN = new Intl.DateTimeFormat("en-CA", {
  timeZone: HosCalendar.defaultTimeZoneId,
  hourCycle: "h23",
  weekday: "short",
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

/** "Mon, Oct 12, 14:30" on the regulation's clock, for an instant that may be days away. */
export function whenLabel(unix: number): string {
  return WHEN.format(new Date(unix * 1000));
}

// --- the tablet's alert thresholds ------------------------------------------

/**
 * When the tablet interrupts a driver: at 60 and 30 minutes before a limit, and at the limit.
 * The owner's policy default (plan, "tablet alerts at 60 / 30 / 0 min"); a Compliance Officer
 * may tune these later, which is why they are one exported constant and not three literals.
 * Descending, so a crossing that skips several (a tick after a long freeze) reports the
 * tightest. Pinned in display.test.ts.
 */
export const ALERT_THRESHOLDS_SECONDS: readonly number[] = [3600, 1800, 0];

/**
 * Which threshold a remaining figure crossed between two readings, or null for none. A
 * crossing is `prev > t >= next` — strictly from above — so a figure that sits exactly on a
 * threshold reports it once, when it arrives there, and never again on the next tick. Several
 * crossed at once (61 → 0) report the tightest, which is the one that matters.
 */
export function alertThresholdCrossed(prevSeconds: number, nextSeconds: number): number | null {
  let crossed: number | null = null;
  for (const t of ALERT_THRESHOLDS_SECONDS) {
    if (prevSeconds > t && nextSeconds <= t) crossed = t;
  }
  return crossed;
}

/** The four figures the alerts watch — the four tiles on the Hours screen, by name. */
export type AlertLimit = "driving" | "onDuty" | "window" | "cycle";

export const ALERT_LIMIT_LABEL: Record<AlertLimit, string> = {
  driving: "Driving time",
  onDuty: "14h on-duty",
  window: "16h shift window",
  cycle: "70h cycle",
};

/**
 * Seconds left on each watched figure. `driving` is the engine's own remaining-driving figure
 * — already the minimum of every rule, which is why it is labelled "Driving time" and not
 * "13h driving": whichever rule binds, that is when driving must stop. `window` is null
 * between shifts (no window is open). Nothing is re-derived: every value is an engine field.
 */
export function limitRemaining(evaluation: HosEvaluation): Record<AlertLimit, number | null> {
  const shift = evaluation.shift;
  return {
    driving: evaluation.remainingDrivingTodaySeconds,
    onDuty: evaluation.remainingOnDutyTodaySeconds,
    // The window alone — mustStopDrivingByUnix folds the shift driving and on-duty ceilings
    // in with it, and those two are already watched through `driving` and `onDuty`.
    window: shift.startUnix === null ? null : Math.max(0, shift.elapsedCeilingSeconds - shift.elapsedSeconds),
    cycle: evaluation.cycle.remainingSeconds,
  };
}

// --- time and duration ----------------------------------------------------

/** "10h 15m" from integer seconds. Never leans on a decimal; floors at zero. */
export function durationLabel(seconds: number): string {
  return formatDurationH(Math.max(0, seconds) / 3600);
}

const CLOCK = new Intl.DateTimeFormat("en-CA", {
  timeZone: HosCalendar.defaultTimeZoneId,
  hourCycle: "h23",
  hour: "2-digit",
  minute: "2-digit",
});

/** "HH:MM" on the regulation's clock (America/Winnipeg) — the tablet's local time. */
export function clockLabel(unix: number): string {
  return CLOCK.format(new Date(unix * 1000));
}

/** "09-12" from a regulation day, for the bar's axis. */
export function dayLabel(date: DateOnly): string {
  return date.slice(5);
}

const WALL = new Intl.DateTimeFormat("en-US", {
  timeZone: HosCalendar.defaultTimeZoneId,
  hourCycle: "h23",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
});

/** The local wall clock at `unix`, as seconds "as if UTC" — the same technique HosCalendar uses. */
function wallSeconds(unix: number): number {
  const w = { y: 0, m: 0, d: 0, h: 0, mi: 0, s: 0 };
  for (const part of WALL.formatToParts(new Date(unix * 1000))) {
    const n = Number(part.value);
    if (part.type === "year") w.y = n;
    else if (part.type === "month") w.m = n;
    else if (part.type === "day") w.d = n;
    else if (part.type === "hour") w.h = n;
    else if (part.type === "minute") w.mi = n;
    else if (part.type === "second") w.s = n;
  }

  return Date.UTC(w.y, w.m - 1, w.d, w.h, w.mi, w.s) / 1000;
}

function offsetAt(unix: number): number {
  return wallSeconds(unix) - unix;
}

/**
 * A mock row's local ISO timestamp ("2026-09-12T11:00:00", no zone — lib/data.ts convention)
 * read as America/Winnipeg wall time, to a Unix instant. Resolved exactly as HosCalendar's
 * private fromLocal does: an ambiguous wall time (fall back) takes the standard offset, the
 * smaller of the two; a wall time inside a spring-forward gap takes the first valid instant
 * after it (the previous day's offset). The engine's method is private, so the technique is
 * repeated here rather than the copy edited.
 */
export function localIsoToUnix(iso: string): number {
  const [date, time = "00:00:00"] = iso.split("T");
  const [y, m, d] = date.split("-").map(Number);
  const [h, mi, s = 0] = time.split(":").map(Number);
  const wall = Date.UTC(y, m - 1, d, h, mi, s) / 1000;
  const offsets = new Set<number>([offsetAt(wall - 86400), offsetAt(wall), offsetAt(wall + 86400)]);
  let best: { utc: number; offset: number } | null = null;
  for (const offset of offsets) {
    const utc = wall - offset;
    if (wallSeconds(utc) === wall && (best === null || offset < best.offset)) {
      best = { utc, offset };
    }
  }

  return best === null ? wall - offsetAt(wall - 86400) : best.utc;
}
