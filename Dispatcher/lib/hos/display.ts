import type { DutyStatus as DutyState, StatusKind } from "../theme";
import type { HosExceptionKindWire, HosExceptionStatus } from "../api/hos";
import {
  addDays,
  DEFAULT_HOS_BANDS,
  evaluate,
  HOS_LIMITS,
  HosCalendar,
  HosWire,
  type BlockingReason,
  type DateOnly,
  type DutyEvent,
  type DutyLedger,
  type DutyStatus,
  type EventSource,
  type HosBand,
  type HosEvaluation,
} from "./engine";

// ---------------------------------------------------------------------------
// HOS display helpers for the Dispatch Console — the one place a figure from
// the engine becomes a StatusKind + label, so the gauge, the roster, the
// timeline and the entry modal cannot disagree about what a number means.
//
// Colour never stands alone: every helper that returns a kind is rendered by a
// StatusChip (glyph + label) or next to dutyMeta()'s glyph. Nothing here
// re-derives a limit — the band is the engine's, the limits are HOS_LIMITS.
// ---------------------------------------------------------------------------

/** The regulation's day calendar (America/Winnipeg) — the server uses the same zone. */
export const HOS_CALENDAR = new HosCalendar();

export const HOS_TIME_ZONE = HosCalendar.defaultTimeZoneId;

// ---- Bands -----------------------------------------------------------------

export const BAND_KIND: Record<HosBand, StatusKind> = {
  Ontime: "ontime",
  Soon: "soon",
  Over: "over",
  Off: "off",
};

/** A remaining figure banded the way the engine bands: Over at 0, Soon inside the window. */
export function remainingKind(seconds: number, soonSeconds: number = DEFAULT_HOS_BANDS.soonSeconds): StatusKind {
  if (seconds <= 0) return "over";
  if (seconds <= soonSeconds) return "soon";
  return "ontime";
}

// ---- Durations and clocks ------------------------------------------------------

/** "8h 05m" — never negative; what the gauge, roster and timeline all show. */
export function formatRemaining(seconds: number): string {
  const s = Math.max(0, Math.trunc(seconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return `${h}h ${String(m).padStart(2, "0")}m`;
}

const clockFmt = new Intl.DateTimeFormat("en-CA", {
  timeZone: HOS_TIME_ZONE,
  hourCycle: "h23",
  hour: "2-digit",
  minute: "2-digit",
});

const dayClockFmt = new Intl.DateTimeFormat("en-CA", {
  timeZone: HOS_TIME_ZONE,
  hourCycle: "h23",
  weekday: "short",
  hour: "2-digit",
  minute: "2-digit",
});

/** Local (America/Winnipeg) "HH:MM" for a Unix instant. */
export function formatClock(unix: number): string {
  return clockFmt.format(new Date(unix * 1000));
}

/** "HH:MM" when the instant falls on `onDate`; "Sat 06:00" when it falls on another day. */
export function formatClockOn(unix: number, onDate: DateOnly): string {
  return HOS_CALENDAR.dayOf(unix) === onDate ? formatClock(unix) : dayClockFmt.format(new Date(unix * 1000)).replace(",", "");
}

/** Local "HH:MM" for an ISO UTC timestamp from the API. */
export function formatClockUtc(iso: string): string {
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? "—" : formatClock(Math.floor(ms / 1000));
}

const dateFmt = new Intl.DateTimeFormat("en-CA", { timeZone: "UTC", weekday: "short", month: "short", day: "numeric" });

/** "Thu, Oct 9" for a DateOnly — formatted in UTC so a yyyy-MM-dd never slides a day. */
export function formatDateOnly(date: DateOnly): string {
  const [y, m, d] = date.split("-").map(Number);
  if (!y || !m || !d) return date;
  return dateFmt.format(new Date(Date.UTC(y, m - 1, d)));
}

export function unixOfUtc(iso: string): number {
  return Math.floor(Date.parse(iso) / 1000);
}

/** ISO UTC timestamp (what the API accepts) for a Unix instant. */
export function isoOfUnix(unix: number): string {
  return new Date(unix * 1000).toISOString();
}

// en-US rather than en-CA: en-CA abbreviates months with a trailing period ("Oct."), which
// reads as a sentence break inside a chip label.
const dateTimeFmt = new Intl.DateTimeFormat("en-US", {
  timeZone: HOS_TIME_ZONE,
  hourCycle: "h23",
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

/** Local (America/Winnipeg) "Oct 12, 14:00" for an ISO UTC timestamp; "—" when malformed. */
export function formatDateTimeUtc(iso: string): string {
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? "—" : dateTimeFmt.format(new Date(ms));
}

/** The local date ("yyyy-MM-dd") and wall clock ("HH:MM") of a Unix instant in the HOS zone — the modals' default "now". */
export function localDateTimeOf(unix: number): { date: DateOnly; time: string } {
  return { date: HOS_CALENDAR.dayOf(unix), time: formatClock(unix) };
}

/** Wall seconds "as if UTC" of a Unix instant in the HOS zone. */
function wallSecondsAt(unix: number): number {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: HOS_TIME_ZONE,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).formatToParts(new Date(unix * 1000));
  const get = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
  return Date.UTC(get("year"), get("month") - 1, get("day"), get("hour"), get("minute"), get("second")) / 1000;
}

/**
 * A local wall-clock "HH:mm" on `date` → Unix seconds in the HOS zone. Two-pass offset
 * resolution handles DST days; inside a spring-forward gap the instant after the gap wins.
 */
export function localTimeToUnix(date: DateOnly, time: string): number | null {
  const m = /^(\d{1,2}):(\d{2})$/.exec(time.trim());
  if (!m) return null;
  const [y, mo, d] = date.split("-").map(Number);
  const h = Number(m[1]);
  const mi = Number(m[2]);
  if (!y || !mo || !d || h > 23 || mi > 59) return null;
  const wall = Date.UTC(y, mo - 1, d, h, mi) / 1000;
  const guess = wall - (wallSecondsAt(wall) - wall);
  const offset = wallSecondsAt(guess) - guess;
  return wall - offset;
}

/** "HH:mm" → minutes since midnight, or null when malformed. */
export function minutesOf(time: string): number | null {
  const m = /^(\d{1,2}):(\d{2})$/.exec(time.trim());
  if (!m) return null;
  const h = Number(m[1]);
  const mi = Number(m[2]);
  return h > 23 || mi > 59 ? null : h * 60 + mi;
}

// ---- Duty and source strings ---------------------------------------------------

/** Engine member → the design system's duty state (the wire string). */
export function dutyStateOf(status: DutyStatus): DutyState {
  return HosWire.status(status) as DutyState;
}

/** A wire duty string → DutyState, falling back to Off Duty for anything unknown. */
export function dutyStateFromWire(wire: string | null | undefined): DutyState {
  const status = HosWire.statusFrom(wire);
  return status === null ? "Off Duty" : dutyStateOf(status);
}

export const DUTY_STATES: readonly DutyState[] = ["Off Duty", "On Duty", "Driving", "Personal Conveyance"];

/** One glyph per source, so two sources sharing the info colour still read apart. */
export const SOURCE_GLYPH: Record<EventSource, string> = {
  DriverApp: "◈",
  EldTranscription: "≡",
  PaperLog: "✎",
  OtherCarrier: "⇄",
  System: "⚙",
};

/** Glyph for a wire source string; "●" (the info kind's own glyph) when unknown. */
export function sourceGlyphFor(wire: string | null | undefined): string {
  const source = HosWire.sourceFrom(wire);
  return source === null ? "●" : SOURCE_GLYPH[source];
}

// ---- Blocking reasons ------------------------------------------------------------

const BLOCKING_TEXT: Record<BlockingReason, string> = {
  NoLedger: "No duty records on file",
  RecordsIncomplete: "Records for the preceding 14 days are incomplete",
  OutOfService: HosWire.OutOfServiceLabel,
  Rest24Expired: "No 24h off-duty period in the preceding 14 days",
  CycleExhausted: "70h on-duty cycle used up",
  DailyDrivingExhausted: "13h daily driving used up",
  DailyOnDutyExhausted: "14h daily on-duty used up",
  ShiftDrivingExhausted: "13h shift driving used up",
  ShiftOnDutyExhausted: "14h shift on-duty used up",
  ShiftWindowClosed: "16h shift window closed",
};

export function blockingText(reason: BlockingReason): string {
  return BLOCKING_TEXT[reason] ?? String(reason);
}

/** The roster cell's short form of each reason; the full sentence travels as the title. */
const BLOCKING_SHORT: Record<BlockingReason, string> = {
  NoLedger: "No records",
  RecordsIncomplete: "Records incomplete",
  OutOfService: HosWire.OutOfServiceLabel,
  Rest24Expired: "No 24h off",
  CycleExhausted: "Cycle used up",
  DailyDrivingExhausted: "Daily driving used",
  DailyOnDutyExhausted: "Daily on-duty used",
  ShiftDrivingExhausted: "Shift driving used",
  ShiftOnDutyExhausted: "Shift on-duty used",
  ShiftWindowClosed: "Shift window closed",
};

export function blockingShort(reason: BlockingReason): string {
  return BLOCKING_SHORT[reason] ?? String(reason);
}

// ---- Compliance records: exceptions, personal conveyance -------------------------------

/** A chip spec: colour kind + glyph + label, always all three. */
export interface ChipSpec {
  kind: StatusKind;
  glyph: string;
  label: string;
}

/**
 * One chip per exception status. Declared and Acknowledged are both "in force" to the engine,
 * so each carries its own glyph as well as its kind: ◐ awaiting dispatch, ● acknowledged,
 * ✓ approved (a deferral), ▲ rejected, — withdrawn.
 */
export const EXCEPTION_STATUS_CHIP: Record<HosExceptionStatus, { kind: StatusKind; glyph: string }> = {
  Declared: { kind: "soon", glyph: "◐" },
  Acknowledged: { kind: "info", glyph: "●" },
  Approved: { kind: "ontime", glyph: "✓" },
  Rejected: { kind: "over", glyph: "▲" },
  Withdrawn: { kind: "off", glyph: "—" },
};

export function exceptionStatusChip(status: HosExceptionStatus): ChipSpec {
  const c = EXCEPTION_STATUS_CHIP[status] ?? { kind: "off" as StatusKind, glyph: "—" };
  return { ...c, label: status };
}

/** The wire kind string is already the label the regulation uses; "Off-duty deferral (CO approved)" reads as "Off-duty deferral". */
export function exceptionKindLabel(kind: HosExceptionKindWire): string {
  return kind === HosWire.OffDutyDeferralKind ? "Off-duty deferral" : kind;
}

/** Statuses the engine honours — the exception is in force. */
export function exceptionInForce(status: HosExceptionStatus): boolean {
  return status === "Declared" || status === "Acknowledged" || status === "Approved";
}

/** The regulation's daily personal-conveyance allowance in km (s.1: 75.0 km). */
export const PC_DAILY_LIMIT_KM = HOS_LIMITS.pcDailyLimitDecikm / 10;

/** Where the PC caution band begins — the tablet's ◐ at 60 km (owner default). */
export const PC_CAUTION_KM = 60;

/**
 * One day's personal-conveyance distance as a verdict: ✓ under 60 km, ◐ "n km left" from
 * 60 to 74.9, ▲ at 75 and beyond (the remainder is reclassified on-duty by the engine).
 */
export function pcDayVerdict(km: number): ChipSpec {
  const shown = `${km.toFixed(1)} km`;
  if (km >= PC_DAILY_LIMIT_KM) {
    return { kind: "over", glyph: "▲", label: `${shown} — ${PC_DAILY_LIMIT_KM} km reached, remainder counts as on-duty` };
  }
  if (km >= PC_CAUTION_KM) {
    return { kind: "soon", glyph: "◐", label: `${shown} — ${(PC_DAILY_LIMIT_KM - km).toFixed(1)} km left` };
  }
  return { kind: "ontime", glyph: "✓", label: shown };
}

// ---- Gauge figures -----------------------------------------------------------------

export interface GaugeBar {
  key: "driving" | "onDuty" | "window" | "cycle";
  title: string;
  remainingSeconds: number;
  limitSeconds: number;
  kind: StatusKind;
  label: string;
}

/** The four remaining figures the gauge shows, each banded by the engine's rule. */
export function gaugeBars(evaluation: HosEvaluation, soonSeconds: number = DEFAULT_HOS_BANDS.soonSeconds): GaugeBar[] {
  const windowRemaining = evaluation.shift.startUnix === null ? evaluation.shift.elapsedCeilingSeconds : Math.max(0, evaluation.shift.elapsedCeilingSeconds - evaluation.shift.elapsedSeconds);
  const bars: Omit<GaugeBar, "kind" | "label">[] = [
    { key: "driving", title: "Driving today", remainingSeconds: evaluation.remainingDrivingTodaySeconds, limitSeconds: Math.max(1, evaluation.shift.drivingCeilingSeconds) },
    { key: "onDuty", title: "On duty today", remainingSeconds: evaluation.remainingOnDutyTodaySeconds, limitSeconds: Math.max(1, evaluation.shift.onDutyCeilingSeconds) },
    { key: "window", title: "16h shift window", remainingSeconds: windowRemaining, limitSeconds: Math.max(1, evaluation.shift.elapsedCeilingSeconds) },
    { key: "cycle", title: "70h cycle (7 days)", remainingSeconds: evaluation.cycle.remainingSeconds, limitSeconds: Math.max(1, evaluation.cycle.limitSeconds) },
  ];
  return bars.map((b) => ({ ...b, kind: remainingKind(b.remainingSeconds, soonSeconds), label: `${formatRemaining(b.remainingSeconds)} left` }));
}

export function barPercent(bar: GaugeBar): number {
  return Math.max(0, Math.min(100, Math.round((bar.remainingSeconds / bar.limitSeconds) * 100)));
}

// ---- Transcription preview ----------------------------------------------------------

export interface PreviewEntry {
  time: string;
  status: DutyState;
}

/**
 * The engine's view of one transcribed day on its own: a synthetic ledger with fourteen
 * certified days off before it (so the records-keeping rules read as complete and only the
 * day's own breaches surface), evaluated at the day's end. Null when any time is malformed.
 */
export function previewDay(date: DateOnly, entries: readonly PreviewEntry[]): HosEvaluation | null {
  const events: DutyEvent[] = [];
  const certifiedDays: DateOnly[] = [];
  let sequence = 1;
  const push = (atUnix: number, status: DutyStatus, source: EventSource): void => {
    events.push({ atUnix, status, source, sequence: sequence++, odometerDecikm: null, note: null });
  };

  const LEAD_DAYS = 14;
  const firstDate = addDays(date, -LEAD_DAYS);
  for (let d = firstDate; d < date; d = addDays(d, 1)) {
    push(HOS_CALENDAR.dayStartUnix(d), "OffDuty", "PaperLog");
    certifiedDays.push(d);
  }

  certifiedDays.push(date);
  for (const e of entries) {
    const at = localTimeToUnix(date, e.time);
    const status = HosWire.statusFrom(e.status);
    if (at === null || status === null) return null;
    push(at, status, "PaperLog");
  }

  const ledger: DutyLedger = {
    driverId: "preview",
    knownFromUnix: HOS_CALENDAR.dayStartUnix(firstDate),
    events,
    otherCarrierDays: [],
    exceptions: [],
    certifiedDays,
    outOfService: [],
    malfunctions: [],
  };
  return evaluate(ledger, HOS_CALENDAR.dayEndUnix(date), HOS_CALENDAR);
}
