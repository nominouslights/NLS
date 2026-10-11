// COPIED FROM Dispatcher/lib/hos/engine.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see DriverField/CLAUDE.md.
// TS mirror of Backend/src/Shared/HoursOfService — UX only; the server owns enforcement.
// Regenerate expectations from hos-fixtures.json, never edit by hand.
//
// A line-for-line transliteration of HosEngine.Evaluate.cs / HosEngine.Project.cs and their
// supporting files (DutyLedger.cs, HosCalendar.cs, HosEvaluation.cs, HosLimits.cs,
// HosMessages.cs, HosProjection.cs, HosWire.cs). The same purity contract applies: no clock,
// no I/O, no randomness, no floating point — instants are Unix seconds, durations integer
// seconds, odometers tenths of a kilometre. Every "now" is the asOfUnix argument. Enum member
// spellings are identical to the C# so the fixture JSON reads unchanged on both sides, and
// `engine.fixtures.test.ts` holds this file to every case's committed snapshot.

// ---- Enums (member spellings shared with the C#) --------------------------------------------

export const DUTY_STATUSES = ["OffDuty", "OnDuty", "Driving", "PersonalConveyance"] as const;
export type DutyStatus = (typeof DUTY_STATUSES)[number];

export const EVENT_SOURCES = ["DriverApp", "EldTranscription", "PaperLog", "OtherCarrier", "System"] as const;
export type EventSource = (typeof EVENT_SOURCES)[number];

/** Member names are the violation ids on the wire; their order is the tie-break order. */
export const HOS_RULES = [
  "DailyDriving13h",
  "DailyOnDuty14h",
  "ShiftDriving13h",
  "ShiftOnDuty14h",
  "ShiftElapsed16h",
  "DailyOffDuty10h",
  "Rest24hIn14d",
  "Cycle1_70hIn7d",
  "DeferralConditions",
  "PcExceeds75km",
  "PcOdometerMissing",
  "PcWhileOutOfService",
  "DroveWhileOutOfService",
  "RodsNotCertified",
  "RecordsIncomplete14d",
  "AdverseDeclaredOutsideShift",
  "ConflictingSources",
] as const;
export type HosRule = (typeof HOS_RULES)[number];

/** Why the driver may not drive right now, in the order they are reported. */
export const BLOCKING_REASONS = [
  "NoLedger",
  "RecordsIncomplete",
  "OutOfService",
  "Rest24Expired",
  "CycleExhausted",
  "DailyDrivingExhausted",
  "DailyOnDutyExhausted",
  "ShiftDrivingExhausted",
  "ShiftOnDutyExhausted",
  "ShiftWindowClosed",
] as const;
export type BlockingReason = (typeof BLOCKING_REASONS)[number];

export const HOS_BANDS = ["Ontime", "Soon", "Over", "Off"] as const;
export type HosBand = (typeof HOS_BANDS)[number];

const RULE_ORDINAL: ReadonlyMap<HosRule, number> = new Map(HOS_RULES.map((r, i) => [r, i]));

function ruleOrdinal(rule: HosRule): number {
  return RULE_ORDINAL.get(rule) ?? HOS_RULES.length;
}

function isDefined<T extends string>(members: readonly T[], value: unknown): value is T {
  return typeof value === "string" && (members as readonly string[]).includes(value);
}

// ---- DateOnly ----------------------------------------------------------------------------------

/** C# DateOnly, carried as "yyyy-MM-dd" so it compares lexically and keys a Map directly. */
export type DateOnly = string;

function pad2(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

function formatDate(y: number, m: number, d: number): DateOnly {
  return `${y}-${pad2(m)}-${pad2(d)}`;
}

export function addDays(date: DateOnly, days: number): DateOnly {
  const [y, m, d] = date.split("-").map(Number);
  const t = new Date(Date.UTC(y, m - 1, d) + days * 86400000);
  return formatDate(t.getUTCFullYear(), t.getUTCMonth() + 1, t.getUTCDate());
}

function compareDate(a: DateOnly, b: DateOnly): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

function compareNumber(a: number, b: number): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

// ---- Ledger (DutyLedger.cs) --------------------------------------------------------------------

export interface DutyEvent {
  atUnix: number;
  status: DutyStatus;
  source: EventSource;
  /** Breaks ties between events at the same instant (highest wins). */
  sequence: number;
  /** Tenths of a kilometre; required on the events that open and close a PC segment. */
  odometerDecikm: number | null;
  note: string | null;
}

export interface OtherCarrierDay {
  date: DateOnly;
  onDutySeconds: number;
  drivingSeconds: number;
  carrierName: string;
}

interface HosExceptionBase {
  id: string;
  declaredAtUnix: number;
  tripId: string | null;
  reason: string;
}

export interface AdverseDrivingConditions extends HosExceptionBase {
  kind: "AdverseDrivingConditions";
  extensionSeconds: number;
}

export interface Emergency extends HosExceptionBase {
  kind: "Emergency";
  endedAtUnix: number | null;
}

export interface OffDutyDeferral extends HosExceptionBase {
  kind: "OffDutyDeferral";
  dayOne: DateOnly;
  deferredSeconds: number;
  approvedByComplianceOfficer: string;
}

/** The JSON discriminator `kind` carries the C# derived type name. */
export type HosException = AdverseDrivingConditions | Emergency | OffDutyDeferral;

export interface OutOfService {
  fromUnix: number;
  /** null = open. */
  untilUnix: number | null;
  reason: string;
}

export interface EldMalfunction {
  noticedAtUnix: number;
  code: string;
  repairedAtUnix: number | null;
}

export interface DutyLedger {
  driverId: string;
  /** The instant from which the records are complete; time before it is unknown, never "free". */
  knownFromUnix: number;
  events: DutyEvent[];
  otherCarrierDays: OtherCarrierDay[];
  exceptions: HosException[];
  certifiedDays: DateOnly[];
  outOfService: OutOfService[];
  malfunctions: EldMalfunction[];
}

// ---- Limits (HosLimits.cs) ---------------------------------------------------------------------

export const HOS_LIMITS = {
  dailyDrivingSeconds: 13 * 3600,
  dailyOnDutySeconds: 14 * 3600,
  shiftDrivingSeconds: 13 * 3600,
  shiftOnDutySeconds: 14 * 3600,
  shiftElapsedSeconds: 16 * 3600,
  mandatoryConsecutiveOffSeconds: 8 * 3600,
  dailyOffSeconds: 10 * 3600,
  extraOffSeconds: 2 * 3600,
  minOffBlockSeconds: 30 * 60,
  rest24Seconds: 24 * 3600,
  rest24WindowSeconds: 14 * 24 * 3600,
  cycleOnDutySeconds: 70 * 3600,
  cycleDays: 7,
  cycleResetSeconds: 36 * 3600,
  adverseMaxExtensionSeconds: 2 * 3600,
  deferralMaxSeconds: 2 * 3600,
  deferralTwoDayOffMinSeconds: 20 * 3600,
  deferralTwoDayDrivingMaxSeconds: 26 * 3600,
  pcDailyLimitDecikm: 750,
  futureSkewSeconds: 300,
} as const;

/** Where the "soon" band begins. Callers may pass a tenant policy value. */
export interface HosBands {
  soonSeconds: number;
}

export const DEFAULT_HOS_BANDS: HosBands = { soonSeconds: 7200 };

/** Planning margin applied by `project`. */
export interface PlanningMargin {
  fixedSeconds: number;
  percent: number;
}

export const DEFAULT_PLANNING_MARGIN: PlanningMargin = { fixedSeconds: 3600, percent: 10 };

// ---- Evaluation output (HosEvaluation.cs) ------------------------------------------------------

export interface HosViolation {
  rule: HosRule;
  date: DateOnly;
  atUnix: number;
  figureSeconds: number;
  limitSeconds: number;
  suppressed: boolean;
  suppressedByExceptionId: string | null;
  message: string;
}

export interface DayTotals {
  date: DateOnly;
  known: boolean;
  complete: boolean;
  drivingSeconds: number;
  onDutySeconds: number;
  otherCarrierOnDutySeconds: number;
  offDutySeconds: number;
  rawOffDutySeconds: number;
  pcDecikm: number;
  hasOwnEvents: boolean;
  certified: boolean;
  eightHourConsecutiveMet: boolean;
  dailyOffRequiredSeconds: number;
  offDutyShortfallSeconds: number;
  dailyOffMet: boolean;
}

export interface ShiftWindow {
  startUnix: number | null;
  drivingSeconds: number;
  onDutySeconds: number;
  elapsedSeconds: number;
  drivingCeilingSeconds: number;
  onDutyCeilingSeconds: number;
  elapsedCeilingSeconds: number;
  adverseExtensionSeconds: number;
  mustStopDrivingByUnix: number | null;
}

export interface CycleState {
  onDutySeconds: number;
  limitSeconds: number;
  remainingSeconds: number;
  windowStartDate: DateOnly;
  lastResetEndUnix: number | null;
  recordsIncomplete: boolean;
}

export interface Rest24State {
  satisfied: boolean;
  qualifyingRunEndUnix: number | null;
  expiresAtUnix: number | null;
  bestOffSeconds: number;
  recordsIncomplete: boolean;
}

export interface NextRequiredOffDuty {
  mustBeOffByUnix: number;
  reason: HosRule;
  minimumSeconds: number;
  dailyShortfallSeconds: number;
}

export interface HosLedgerProblem {
  code: string;
  message: string;
  atUnix: number | null;
}

export interface HosEvaluation {
  asOfUnix: number;
  currentStatus: DutyStatus;
  currentStatusSinceUnix: number;
  days: DayTotals[];
  today: DayTotals;
  remainingDrivingTodaySeconds: number;
  remainingOnDutyTodaySeconds: number;
  shift: ShiftWindow;
  cycle: CycleState;
  rest24: Rest24State;
  pcTodayDecikm: number;
  pcRemainingDecikm: number;
  violations: HosViolation[];
  canDriveNow: boolean;
  blockingReasons: BlockingReason[];
  nextRequiredOffDuty: NextRequiredOffDuty | null;
  tightestRemainingSeconds: number;
  band: HosBand;
  ledgerProblems: HosLedgerProblem[];
}

// ---- Projection (HosProjection.cs) -------------------------------------------------------------

export interface PlannedSegment {
  status: DutyStatus;
  seconds: number;
}

export interface PlannedLeg {
  startUnix: number;
  segments: PlannedSegment[];
  tripId: string | null;
}

export interface TripPlan {
  legs: PlannedLeg[];
  offDutyFromUnix: number | null;
  /** Status assumed in the gaps between legs (C# default OffDuty). */
  betweenLegs?: DutyStatus;
}

export interface HosProjection {
  feasible: boolean;
  firstBreach: HosViolation | null;
  firstBreachAtUnix: number | null;
  marginAppliedSeconds: number;
  assumedOffDutyFromUnix: number;
  requiredOffBeforeStartSeconds: number | null;
  dailyOffStillNeededAfterPlanSeconds: number;
  dailyOffAchievableAfterPlan: boolean;
  atPlanEnd: HosEvaluation;
  problems: HosLedgerProblem[];
}

// ---- Calendar (HosCalendar.cs) -----------------------------------------------------------------

const MAX_DATE_MS = 8.64e15;

interface Wall {
  y: number;
  m: number;
  d: number;
  h: number;
  mi: number;
  s: number;
}

/**
 * Maps Unix instants to the regulation's "day" (s.1). Resolves the IANA zone through the
 * runtime's ICU data (Intl), never the machine's own zone. DST days are 23 or 25 hours long;
 * the engine splits pieces at the boundaries this calendar reports.
 */
export class HosCalendar {
  static readonly defaultTimeZoneId = "America/Winnipeg";

  readonly timeZoneId: string;
  readonly dayStartSecondsLocal: number;

  private readonly fmt: Intl.DateTimeFormat;
  private readonly dayOfCache = new Map<number, DateOnly>();
  private readonly dayStartCache = new Map<DateOnly, number>();

  constructor(timeZoneId: string = HosCalendar.defaultTimeZoneId, dayStartSecondsLocal = 0) {
    if (!Number.isInteger(dayStartSecondsLocal) || dayStartSecondsLocal < 0 || dayStartSecondsLocal >= 86400) {
      throw new RangeError("Day start must be within one local day.");
    }

    this.timeZoneId = timeZoneId;
    this.dayStartSecondsLocal = dayStartSecondsLocal;
    // hourCycle h23: midnight is "00", never "24" — the one Intl quirk that would shift a day.
    this.fmt = new Intl.DateTimeFormat("en-US", {
      timeZone: timeZoneId,
      hourCycle: "h23",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  }

  /** The regulation day containing `unix`. */
  dayOf(unix: number): DateOnly {
    const cached = this.dayOfCache.get(unix);
    if (cached !== undefined) {
      return cached;
    }

    const w = this.wall(unix);
    const date = formatDate(w.y, w.m, w.d);
    const secondsIntoLocalDay = w.h * 3600 + w.mi * 60 + w.s;
    const result = secondsIntoLocalDay < this.dayStartSecondsLocal ? addDays(date, -1) : date;
    this.dayOfCache.set(unix, result);
    return result;
  }

  /** The instant `date` begins. */
  dayStartUnix(date: DateOnly): number {
    const cached = this.dayStartCache.get(date);
    if (cached !== undefined) {
      return cached;
    }

    const [y, m, d] = date.split("-").map(Number);
    let local = Date.UTC(y, m - 1, d) / 1000 + this.dayStartSecondsLocal;
    let utc = this.fromLocal(local);
    if (utc === null) {
      // The day start falls inside a spring-forward gap: the first valid instant after it
      // (TimeZoneInfo.IsInvalidTime → AddHours(1)).
      local += 3600;
      utc = this.fromLocal(local) ?? local - this.offsetAt(local);
    }

    this.dayStartCache.set(date, utc);
    return utc;
  }

  /** The instant `date` ends (the next day's start). */
  dayEndUnix(date: DateOnly): number {
    return this.dayStartUnix(addDays(date, 1));
  }

  private wall(unix: number): Wall {
    const ms = Math.max(-MAX_DATE_MS, Math.min(MAX_DATE_MS, unix * 1000));
    const w: Wall = { y: 0, m: 0, d: 0, h: 0, mi: 0, s: 0 };
    for (const part of this.fmt.formatToParts(new Date(ms))) {
      switch (part.type) {
        case "year":
          w.y = Number(part.value);
          break;
        case "month":
          w.m = Number(part.value);
          break;
        case "day":
          w.d = Number(part.value);
          break;
        case "hour":
          w.h = Number(part.value);
          break;
        case "minute":
          w.mi = Number(part.value);
          break;
        case "second":
          w.s = Number(part.value);
          break;
        default:
          break;
      }
    }

    return w;
  }

  /** The local wall clock at `unix`, expressed as seconds "as if UTC". */
  private wallSeconds(unix: number): number {
    const w = this.wall(unix);
    return Date.UTC(w.y, w.m - 1, w.d, w.h, w.mi, w.s) / 1000;
  }

  private offsetAt(unix: number): number {
    return this.wallSeconds(unix) - unix;
  }

  /**
   * Local wall seconds → Unix, or null when the wall time does not exist (a spring-forward gap).
   * An ambiguous wall time (fall back) resolves to the standard offset, the smaller of the two,
   * which is what TimeZoneInfo.GetUtcOffset returns for an ambiguous DateTime.
   */
  private fromLocal(localWall: number): number | null {
    const offsets = new Set<number>([
      this.offsetAt(localWall - 86400),
      this.offsetAt(localWall),
      this.offsetAt(localWall + 86400),
    ]);
    let best: { utc: number; offset: number } | null = null;
    for (const offset of offsets) {
      const utc = localWall - offset;
      if (this.wallSeconds(utc) === localWall && (best === null || offset < best.offset)) {
        best = { utc, offset };
      }
    }

    return best === null ? null : best.utc;
  }
}

// ---- Messages (HosMessages.cs) -----------------------------------------------------------------

/** English prose for each rule, identical to the server's so every app shows the same sentence. */
export const HosMessages = {
  for(rule: HosRule, figure: number, limit: number): string {
    const d = HosMessages.duration;
    switch (rule) {
      case "DailyDriving13h":
        return `Driving ${d(figure)} exceeds the ${d(limit)} daily limit (s.12(1)).`;
      case "DailyOnDuty14h":
        return `Drove after ${d(figure)} on duty in the day; the limit is ${d(limit)} (s.12(2)).`;
      case "ShiftDriving13h":
        return `Driving ${d(figure)} in the shift exceeds the ${d(limit)} limit (s.13(1)).`;
      case "ShiftOnDuty14h":
        return `Drove after ${d(figure)} on duty in the shift; the limit is ${d(limit)} (s.13(2)).`;
      case "ShiftElapsed16h":
        return `Drove ${d(figure)} after the shift began; the window is ${d(limit)} (s.13(3)).`;
      case "DailyOffDuty10h":
        return `Only ${d(figure)} off duty in the day; ${d(limit)} is required (s.14).`;
      case "Rest24hIn14d":
        return `No 24h consecutive off-duty period in the preceding 14 days (longest ${d(figure)}) (s.25).`;
      case "Cycle1_70hIn7d":
        return `On duty ${d(figure)} in 7 days exceeds the ${d(limit)} Cycle 1 limit (s.26).`;
      case "DeferralConditions":
        return "The off-duty deferral's day-two conditions were not met (s.16).";
      case "PcExceeds75km":
        return `Personal conveyance ${km(figure)} km exceeds the ${km(limit)} km daily limit; the remainder counts as driving (s.1).`;
      case "PcOdometerMissing":
        return "Personal conveyance without start and end odometer readings counts as driving (s.1).";
      case "PcWhileOutOfService":
        return "Personal conveyance while out of service counts as driving (s.91).";
      case "DroveWhileOutOfService":
        return `Drove ${d(figure)} while out of service (s.91).`;
      case "RodsNotCertified":
        return "The day's record of duty status has not been certified (s.84).";
      case "RecordsIncomplete14d":
        return "Records for the preceding 14 days are incomplete; driving is not permitted until they are (s.86).";
      case "AdverseDeclaredOutsideShift":
        return "Adverse driving conditions were declared outside a shift and were ignored (s.76).";
      case "ConflictingSources":
        return "Two sources recorded different statuses at the same instant (s.82).";
      default:
        return String(rule);
    }
  },

  duration(seconds: number): string {
    const negative = seconds < 0;
    const s = Math.abs(seconds);
    const hours = Math.trunc(s / 3600);
    const minutes = Math.trunc((s % 3600) / 60);
    const rest = s % 60;
    const text = rest === 0 ? `${hours}h ${minutes}m` : `${hours}h ${minutes}m ${rest}s`;
    return negative ? `-${text}` : text;
  },
};

function km(decikm: number): string {
  return `${Math.trunc(decikm / 10)}.${decikm % 10}`;
}

// ---- Wire strings (HosWire.cs) -----------------------------------------------------------------

/** The strings the API emits and accepts for HOS enums; violation ids are HosRule names verbatim. */
export const HosWire = {
  OffDuty: "Off Duty",
  OnDuty: "On Duty",
  Driving: "Driving",
  PersonalConveyance: "Personal Conveyance",

  DriverAppSource: "Driver App",
  EldTranscriptionSource: "ELD transcription",
  /** Legacy string for EventSource.PaperLog; kept verbatim. */
  PaperLogSource: "Manual (paper backup)",
  OtherCarrierSource: "Other carrier (declared)",
  SystemSource: "System",

  AdverseDrivingConditionsKind: "Adverse driving conditions",
  EmergencyKind: "Emergency",
  OffDutyDeferralKind: "Off-duty deferral (CO approved)",

  OutOfServiceLabel: "Out of service",

  status(status: DutyStatus): string {
    switch (status) {
      case "OffDuty":
        return HosWire.OffDuty;
      case "OnDuty":
        return HosWire.OnDuty;
      case "Driving":
        return HosWire.Driving;
      case "PersonalConveyance":
        return HosWire.PersonalConveyance;
      default:
        return String(status);
    }
  },

  statusFrom(wire: string | null | undefined): DutyStatus | null {
    switch (wire?.trim()) {
      case HosWire.OffDuty:
        return "OffDuty";
      case HosWire.OnDuty:
        return "OnDuty";
      case HosWire.Driving:
        return "Driving";
      case HosWire.PersonalConveyance:
        return "PersonalConveyance";
      default:
        return null;
    }
  },

  source(source: EventSource): string {
    switch (source) {
      case "DriverApp":
        return HosWire.DriverAppSource;
      case "EldTranscription":
        return HosWire.EldTranscriptionSource;
      case "PaperLog":
        return HosWire.PaperLogSource;
      case "OtherCarrier":
        return HosWire.OtherCarrierSource;
      case "System":
        return HosWire.SystemSource;
      default:
        return String(source);
    }
  },

  sourceFrom(wire: string | null | undefined): EventSource | null {
    switch (wire?.trim()) {
      case HosWire.DriverAppSource:
        return "DriverApp";
      case HosWire.EldTranscriptionSource:
        return "EldTranscription";
      case HosWire.PaperLogSource:
        return "PaperLog";
      case HosWire.OtherCarrierSource:
        return "OtherCarrier";
      case HosWire.SystemSource:
        return "System";
      default:
        return null;
    }
  },

  exceptionKind(exception: HosException): string {
    switch (exception.kind) {
      case "AdverseDrivingConditions":
        return HosWire.AdverseDrivingConditionsKind;
      case "Emergency":
        return HosWire.EmergencyKind;
      case "OffDutyDeferral":
        return HosWire.OffDutyDeferralKind;
      default:
        return String((exception as { kind: string }).kind);
    }
  },

  ruleId(rule: HosRule): string {
    return rule;
  },

  ruleFrom(id: string | null | undefined): HosRule | null {
    return isDefined(HOS_RULES, id) ? id : null;
  },
};

// ---- Engine internals --------------------------------------------------------------------------

const INT_MAX = 2147483647;
const INT_MIN = -2147483648;
const LONG_MAX = Number.MAX_SAFE_INTEGER;
const LONG_MIN = Number.MIN_SAFE_INTEGER;

/** C# `Sec(long)`: clamp to int. */
function sec(value: number): number {
  return value >= INT_MAX ? INT_MAX : value <= INT_MIN ? INT_MIN : value;
}

function overlap(aStart: number, aEnd: number, bStart: number, bEnd: number): number {
  return Math.max(0, Math.min(aEnd, bEnd) - Math.max(aStart, bStart));
}

/** C# integer division (truncates toward zero). */
function idiv(a: number, b: number): number {
  return Math.trunc(a / b);
}

function minOf(...values: number[]): number {
  let min = INT_MAX;
  for (const v of values) {
    min = Math.min(min, v);
  }

  return min;
}

interface Segment {
  start: number;
  end: number;
  status: DutyStatus;
  source: EventSource;
  startOdo: number | null;
  endOdo: number | null;
  hasClosingEvent: boolean;
  synthetic: boolean;
}

interface Piece {
  segment: Segment;
  start: number;
  end: number;
  status: DutyStatus;
  date: DateOnly;
  decikm: number;
}

function pieceLength(p: Piece): number {
  return p.end - p.start;
}

function isOff(p: Piece): boolean {
  return p.status === "OffDuty" || p.status === "PersonalConveyance";
}

function isOnDuty(p: Piece): boolean {
  return p.status === "OnDuty" || p.status === "Driving";
}

function isDriving(p: Piece): boolean {
  return p.status === "Driving";
}

interface Run {
  start: number;
  end: number;
}

function runLength(r: Run): number {
  return r.end - r.start;
}

interface Pending {
  rule: HosRule;
  date: DateOnly;
  atUnix: number;
  figure: number;
  limit: number;
}

function pendingKey(rule: HosRule, key: string): string {
  return `${rule}\u0000${key}`;
}

class Evaluation {
  private readonly ledger: DutyLedger;
  private readonly asOf: number;
  private readonly cal: HosCalendar;
  private readonly bands: HosBands;
  private readonly cutoff: number;
  private knownFrom: number;
  private readonly today: DateOnly;

  private readonly problems: HosLedgerProblem[] = [];
  private readonly pending = new Map<string, Pending>();
  private readonly events: DutyEvent[] = [];
  private readonly ownEventDays = new Set<DateOnly>();
  private readonly otherCarrier = new Map<DateOnly, OtherCarrierDay>();

  /** Other-carrier days rendered as synthetic pieces (declared hours at the day start), so their totals are not added twice. */
  private readonly otherCarrierOnTimeline = new Set<DateOnly>();
  private readonly certified = new Set<DateOnly>();
  private readonly oos: { from: number; until: number }[] = [];
  private readonly segments: Segment[] = [];
  private readonly pieces: Piece[] = [];
  private readonly runs: Run[] = [];
  private readonly adverseByShift = new Map<number, number>();
  private readonly adverseByDay = new Map<DateOnly, number>();
  private readonly deferrals = new Map<DateOnly, OffDutyDeferral>();
  private readonly emergencies: { exception: Emergency; from: number; to: number }[] = [];
  private readonly pcDeclared = new Map<DateOnly, number>();
  private readonly deferralFailedDayOne = new Set<DateOnly>();

  private noLedger = false;
  private currentStatus: DutyStatus = "OffDuty";
  private currentSince: number;
  private records14dIncomplete = false;

  constructor(ledger: DutyLedger, asOf: number, cal: HosCalendar, bands: HosBands, cutoff: number) {
    this.ledger = ledger;
    this.asOf = asOf;
    this.cal = cal;
    this.bands = bands;
    this.cutoff = cutoff;
    this.today = cal.dayOf(asOf);
    this.knownFrom = ledger.knownFromUnix;
    if (this.knownFrom > asOf) {
      this.problem("KnownFromAfterAsOf", "The ledger's known-from instant is after the evaluation instant; nothing is known.", this.knownFrom);
      this.knownFrom = asOf;
    }

    this.currentSince = this.knownFrom;
  }

  run(): HosEvaluation {
    this.normaliseEvents();
    this.loadReferenceData();
    this.buildSegments();
    this.buildPieces();
    this.reclassifyPersonalConveyance();
    this.buildRuns();
    this.loadExceptions();
    const days = this.buildDays();
    this.evaluateDrivingPieces();
    this.evaluateDailyOff(days);
    this.evaluateRecords(days);
    return this.assemble(days);
  }

  // ---- 1. Normalise ----------------------------------------------------------------

  private normaliseEvents(): void {
    const kept: DutyEvent[] = [];
    for (const e of this.ledger.events ?? []) {
      if (!isDefined(DUTY_STATUSES, e.status) || !isDefined(EVENT_SOURCES, e.source)) {
        this.problem("InvalidEvent", "An event carries an unknown status or source and was ignored.", e.atUnix);
        continue;
      }

      if (e.atUnix > this.asOf + HOS_LIMITS.futureSkewSeconds) {
        this.problem("FutureEvent", "An event is more than five minutes in the future and was ignored.", e.atUnix);
        continue;
      }

      kept.push(e);
    }

    kept.sort((a, b) => (a.atUnix !== b.atUnix ? compareNumber(a.atUnix, b.atUnix) : compareNumber(a.sequence, b.sequence)));

    for (let i = 0; i < kept.length; ) {
      let j = i;
      while (j + 1 < kept.length && kept[j + 1].atUnix === kept[i].atUnix) {
        j++;
      }

      const winner = kept[j];
      for (let k = i; k < j; k++) {
        if (kept[k].source !== winner.source && kept[k].status !== winner.status) {
          const date = this.cal.dayOf(winner.atUnix);
          this.record("ConflictingSources", `${winner.atUnix}`, date, winner.atUnix, 0, 0);
        }
      }

      this.events.push(winner);
      i = j + 1;
    }

    for (const e of this.events) {
      if (e.atUnix <= this.asOf) {
        this.ownEventDays.add(this.cal.dayOf(e.atUnix));
      }
    }
  }

  private loadReferenceData(): void {
    const firstOwn = this.events.length > 0 ? this.events[0].atUnix : LONG_MAX;
    for (const day of this.ledger.otherCarrierDays ?? []) {
      if (day === null || day === undefined || day.onDutySeconds < 0 || day.drivingSeconds < 0 || day.drivingSeconds > day.onDutySeconds) {
        this.problem("InvalidOtherCarrierDay", "An other-carrier day has impossible hours and was ignored.", null);
        continue;
      }

      if (this.otherCarrier.has(day.date)) {
        this.problem("DuplicateOtherCarrierDay", `Two other-carrier declarations exist for ${day.date}; the first was kept.`, this.cal.dayStartUnix(day.date));
        continue;
      }

      if (this.cal.dayEndUnix(day.date) > firstOwn) {
        this.problem(
          "UntimedOtherCarrierHoursAfterOwnRecords",
          `Other-carrier hours for ${day.date} are declared after the driver's own records begin; they are counted conservatively.`,
          this.cal.dayStartUnix(day.date),
        );
      }

      this.otherCarrier.set(day.date, day);
    }

    this.noLedger = this.events.length === 0 && this.otherCarrier.size === 0;

    for (const date of this.ledger.certifiedDays ?? []) {
      this.certified.add(date);
    }

    for (const span of this.ledger.outOfService ?? []) {
      if (span === null || span === undefined) {
        continue;
      }

      const until = span.untilUnix ?? LONG_MAX;
      if (until <= span.fromUnix) {
        this.problem("InvalidOutOfService", "An out-of-service span ends before it starts and was ignored.", span.fromUnix);
        continue;
      }

      this.oos.push({ from: span.fromUnix, until });
    }
  }

  // ---- 2. Segments and pieces -------------------------------------------------------

  private buildSegments(): void {
    let current: Segment | null = null;
    let pendingEndOdo: number | null = null;
    for (let i = 0; i < this.events.length; i++) {
      const e = this.events[i];
      const end = i + 1 < this.events.length ? this.events[i + 1].atUnix : Math.max(this.asOf, e.atUnix);
      if (current !== null && current.status === e.status) {
        current.end = end;
        if (e.odometerDecikm !== null && e.odometerDecikm !== undefined) {
          pendingEndOdo = e.odometerDecikm;
        }

        continue;
      }

      if (current !== null) {
        current.endOdo = e.odometerDecikm ?? pendingEndOdo;
        current.hasClosingEvent = true;
      }

      current = {
        start: e.atUnix,
        end,
        status: e.status,
        source: e.source,
        startOdo: e.odometerDecikm ?? null,
        endOdo: null,
        hasClosingEvent: false,
        synthetic: false,
      };
      pendingEndOdo = null;
      this.segments.push(current);
    }

    if (current !== null) {
      current.endOdo = pendingEndOdo;
    }

    for (const s of this.segments) {
      if (s.start <= this.asOf) {
        this.currentStatus = s.status;
        this.currentSince = s.start;
      }
    }

    if (this.segments.length === 0 || this.currentSince < this.knownFrom) {
      this.currentSince = Math.max(this.currentSince, this.knownFrom);
    }
  }

  private buildPieces(): void {
    const clamped: Segment[] = [];
    for (const s of this.segments) {
      const start = Math.max(s.start, this.knownFrom);
      const end = Math.min(s.end, this.asOf);
      if (start < end) {
        clamped.push({
          start,
          end,
          status: s.status,
          source: s.source,
          startOdo: s.startOdo,
          endOdo: s.endOdo,
          hasClosingEvent: s.hasClosingEvent,
          synthetic: false,
        });
      }
    }

    const firstStart = clamped.length > 0 ? clamped[0].start : this.asOf;
    if (firstStart > this.knownFrom) {
      clamped.unshift(...this.syntheticSegments(this.knownFrom, firstStart));
    }

    for (const s of clamped) {
      const cuts: number[] = [];
      let date = this.cal.dayOf(s.start);
      let boundary = this.cal.dayEndUnix(date);
      while (boundary < s.end) {
        cuts.push(boundary);
        date = addDays(date, 1);
        boundary = this.cal.dayEndUnix(date);
      }

      if (this.cutoff > s.start && this.cutoff < s.end) {
        cuts.push(this.cutoff);
      }

      cuts.sort(compareNumber);
      const totalKm =
        s.status === "PersonalConveyance" && s.startOdo !== null && s.endOdo !== null && s.endOdo >= s.startOdo ? s.endOdo - s.startOdo : 0;
      let allocated = 0;
      let pieceStart = s.start;
      for (let c = 0; c <= cuts.length; c++) {
        const pieceEnd = c < cuts.length ? cuts[c] : s.end;
        if (pieceEnd <= pieceStart) {
          continue;
        }

        const piece: Piece = {
          segment: s,
          start: pieceStart,
          end: pieceEnd,
          status: s.status,
          date: this.cal.dayOf(pieceStart),
          decikm: 0,
        };
        if (totalKm > 0) {
          const share = c === cuts.length ? totalKm - allocated : idiv(totalKm * (pieceEnd - pieceStart), s.end - s.start);
          piece.decikm = sec(share);
          allocated += share;
        }

        this.pieces.push(piece);
        pieceStart = pieceEnd;
      }
    }
  }

  /**
   * Time between known-from and the first own event: off duty, except that a fully covered
   * day with a declared other-carrier row becomes that declaration laid out from the day
   * start (driving, then other on-duty, then off).
   */
  private syntheticSegments(from: number, to: number): Segment[] {
    const list: Segment[] = [];
    const add = (start: number, end: number, status: DutyStatus): void => {
      if (start < end) {
        list.push({ start, end, status, source: "OtherCarrier", startOdo: null, endOdo: null, hasClosingEvent: false, synthetic: true });
      }
    };

    let cursor = from;
    while (cursor < to) {
      const date = this.cal.dayOf(cursor);
      const dayStart = this.cal.dayStartUnix(date);
      const dayEnd = Math.min(this.cal.dayEndUnix(date), to);
      const oc = this.otherCarrier.get(date);
      if (oc !== undefined && oc.onDutySeconds > 0 && dayStart >= from && this.cal.dayEndUnix(date) <= to) {
        this.otherCarrierOnTimeline.add(date);
        const drivingEnd = Math.min(dayStart + oc.drivingSeconds, dayEnd);
        const onDutyEnd = Math.min(dayStart + oc.onDutySeconds, dayEnd);
        add(dayStart, drivingEnd, "Driving");
        add(drivingEnd, onDutyEnd, "OnDuty");
        add(onDutyEnd, dayEnd, "OffDuty");
      } else {
        add(cursor, dayEnd, "OffDuty");
      }

      cursor = dayEnd;
    }

    return list;
  }

  // ---- 3. Personal conveyance ------------------------------------------------------

  private reclassifyPersonalConveyance(): void {
    const used = new Map<DateOnly, number>();
    for (let i = 0; i < this.pieces.length; i++) {
      const p = this.pieces[i];
      if (p.status !== "PersonalConveyance") {
        continue;
      }

      const seg = p.segment;
      const key = `${seg.start}`;
      const segDate = this.cal.dayOf(seg.start);
      const odometerMissing =
        seg.startOdo === null || (seg.hasClosingEvent && seg.endOdo === null) || (seg.endOdo !== null && seg.endOdo < seg.startOdo);
      if (odometerMissing) {
        p.status = "Driving";
        p.decikm = 0;
        this.record("PcOdometerMissing", key, segDate, seg.start, 0, 0);
        continue;
      }

      const oosOverlapFrom = this.oosOverlapStart(seg.start, seg.end);
      if (oosOverlapFrom !== null) {
        p.status = "Driving";
        p.decikm = 0;
        this.record("PcWhileOutOfService", key, segDate, Math.max(seg.start, oosOverlapFrom), 0, 0);
        continue;
      }

      if (!seg.hasClosingEvent && seg.endOdo === null) {
        continue; // in progress: no distance yet, still personal conveyance
      }

      this.pcDeclared.set(p.date, (this.pcDeclared.get(p.date) ?? 0) + p.decikm);
      const allowance = HOS_LIMITS.pcDailyLimitDecikm - (used.get(p.date) ?? 0);
      if (p.decikm <= allowance) {
        used.set(p.date, (used.get(p.date) ?? 0) + p.decikm);
        continue;
      }

      const dayKey = p.date;
      if (allowance <= 0) {
        p.status = "Driving";
        this.record("PcExceeds75km", dayKey, p.date, p.start, this.pcDeclared.get(p.date) ?? 0, HOS_LIMITS.pcDailyLimitDecikm);
        continue;
      }

      const crossing = p.start + idiv(pieceLength(p) * allowance, p.decikm);
      used.set(p.date, HOS_LIMITS.pcDailyLimitDecikm);
      const driving: Piece = {
        segment: seg,
        start: crossing,
        end: p.end,
        status: "Driving",
        date: p.date,
        decikm: p.decikm - allowance,
      };
      p.end = crossing;
      p.decikm = allowance;
      if (pieceLength(p) <= 0) {
        this.pieces[i] = driving;
      } else {
        this.pieces.splice(i + 1, 0, driving);
        i++;
      }

      this.record("PcExceeds75km", dayKey, p.date, crossing, this.pcDeclared.get(p.date) ?? 0, HOS_LIMITS.pcDailyLimitDecikm);
    }
  }

  private oosOverlapStart(start: number, end: number): number | null {
    let best: number | null = null;
    for (const { from, until } of this.oos) {
      if (overlap(start, end, from, until) > 0 && (best === null || from < best)) {
        best = from;
      }
    }

    return best;
  }

  // ---- 4. Runs ----------------------------------------------------------------------

  private buildRuns(): void {
    let runStart: number | null = null;
    let runEnd = 0;
    for (const p of this.pieces) {
      if (isOff(p)) {
        if (runStart === null || p.start !== runEnd) {
          if (runStart !== null) {
            this.runs.push({ start: runStart, end: runEnd });
          }

          runStart = p.start;
        }

        runEnd = p.end;
      } else if (runStart !== null) {
        this.runs.push({ start: runStart, end: runEnd });
        runStart = null;
      }
    }

    if (runStart !== null) {
      this.runs.push({ start: runStart, end: runEnd });
    }
  }

  private insideLongRun(t: number, minimum: number): boolean {
    for (const r of this.runs) {
      if (runLength(r) >= minimum && r.start <= t && t < r.end) {
        return true;
      }
    }

    return false;
  }

  /** End of the most recent run of at least `minimum` ending at or before `t`; null when none. */
  private lastRunEndAtOrBefore(t: number, minimum: number): number | null {
    let best: number | null = null;
    for (const r of this.runs) {
      if (runLength(r) >= minimum && r.end <= t && (best === null || r.end > best)) {
        best = r.end;
      }
    }

    return best;
  }

  private shiftStartAt(t: number): number {
    return this.lastRunEndAtOrBefore(t, HOS_LIMITS.mandatoryConsecutiveOffSeconds) ?? this.knownFrom;
  }

  private betweenShiftsAt(t: number): boolean {
    if (!(this.currentStatus === "OffDuty" || this.currentStatus === "PersonalConveyance")) {
      return false;
    }

    for (const r of this.runs) {
      if (r.start <= t && t <= r.end && t - r.start >= HOS_LIMITS.mandatoryConsecutiveOffSeconds) {
        return true;
      }
    }

    return false;
  }

  // ---- 5. Exceptions ---------------------------------------------------------------

  private loadExceptions(): void {
    const ordered = [...(this.ledger.exceptions ?? [])].sort((a, b) =>
      compareNumber(a?.declaredAtUnix ?? LONG_MAX, b?.declaredAtUnix ?? LONG_MAX),
    );
    for (const ex of ordered) {
      switch (ex?.kind) {
        case "AdverseDrivingConditions":
          this.loadAdverse(ex);
          break;
        case "Emergency":
          this.loadEmergency(ex);
          break;
        case "OffDutyDeferral":
          this.loadDeferral(ex);
          break;
        default:
          this.problem("InvalidException", "An exception of unknown kind was ignored.", null);
          break;
      }
    }
  }

  private loadAdverse(adverse: AdverseDrivingConditions): void {
    const t = adverse.declaredAtUnix;
    const insideShift = t >= this.knownFrom && t <= this.asOf && !this.insideLongRun(t, HOS_LIMITS.mandatoryConsecutiveOffSeconds);
    const date = this.cal.dayOf(t);
    if (!insideShift) {
      this.record("AdverseDeclaredOutsideShift", adverse.id, date, t, 0, 0);
      return;
    }

    if (adverse.extensionSeconds <= 0) {
      this.problem("InvalidException", `Adverse-conditions exception ${adverse.id} declares no extension and was ignored.`, t);
      return;
    }

    let ext = adverse.extensionSeconds;
    if (ext > HOS_LIMITS.adverseMaxExtensionSeconds) {
      this.problem("AdverseExtensionClamped", `Adverse-conditions exception ${adverse.id} asked for more than 2h; it was clamped to 2h.`, t);
      ext = HOS_LIMITS.adverseMaxExtensionSeconds;
    }

    const shift = this.shiftStartAt(t);
    if (this.adverseByShift.has(shift)) {
      this.problem("DuplicateException", `Adverse-conditions exception ${adverse.id} is the second in its shift and was ignored.`, t);
      return;
    }

    this.adverseByShift.set(shift, ext);
    this.adverseByDay.set(date, Math.max(this.adverseByDay.get(date) ?? 0, ext));
  }

  private loadEmergency(emergency: Emergency): void {
    const from = emergency.declaredAtUnix;
    let to: number;
    if (emergency.endedAtUnix !== null && emergency.endedAtUnix !== undefined) {
      const ended = emergency.endedAtUnix;
      if (ended < from) {
        this.problem("InvalidException", `Emergency ${emergency.id} ends before it starts and was ignored.`, from);
        return;
      }

      to = ended;
    } else {
      to = from;
      for (const s of this.segments) {
        if (s.status === "Driving" && s.start <= from && from < s.end) {
          to = s.end;
        }
      }
    }

    this.emergencies.push({ exception: emergency, from, to });
  }

  private loadDeferral(deferral: OffDutyDeferral): void {
    if (deferral.deferredSeconds <= 0 || deferral.deferredSeconds > HOS_LIMITS.deferralMaxSeconds) {
      this.problem("InvalidException", `Deferral ${deferral.id} defers an amount outside 1s..2h and was ignored.`, deferral.declaredAtUnix);
      return;
    }

    if (this.deferrals.has(deferral.dayOne)) {
      this.problem("DuplicateException", `Deferral ${deferral.id} duplicates another for the same day one and was ignored.`, deferral.declaredAtUnix);
      return;
    }

    this.deferrals.set(deferral.dayOne, deferral);
  }

  private adverseForDay(date: DateOnly, at: number): number {
    return at <= this.cutoff ? (this.adverseByDay.get(date) ?? 0) : 0;
  }

  private adverseForShift(shiftStart: number, at: number): number {
    return at <= this.cutoff ? (this.adverseByShift.get(shiftStart) ?? 0) : 0;
  }

  // ---- Figures ------------------------------------------------------------------------

  private sumBetween(from: number, to: number, driving: boolean): number {
    let total = 0;
    for (const p of this.pieces) {
      if (driving ? isDriving(p) : isOnDuty(p)) {
        total += overlap(p.start, p.end, from, to);
      }
    }

    return total;
  }

  /** Declared other-carrier hours not already on the timeline as synthetic pieces. */
  private otherCarrierSeconds(date: DateOnly, driving: boolean): number {
    const oc = this.otherCarrier.get(date);
    return !this.otherCarrierOnTimeline.has(date) && oc !== undefined ? (driving ? oc.drivingSeconds : oc.onDutySeconds) : 0;
  }

  private declaredOtherCarrierOnDuty(date: DateOnly): number {
    return this.otherCarrier.get(date)?.onDutySeconds ?? 0;
  }

  private dailyDrivingAt(date: DateOnly, t: number): number {
    return sec(this.sumBetween(this.cal.dayStartUnix(date), t, true)) + this.otherCarrierSeconds(date, true);
  }

  private dailyOnDutyAt(date: DateOnly, t: number): number {
    return sec(this.sumBetween(this.cal.dayStartUnix(date), t, false)) + this.otherCarrierSeconds(date, false);
  }

  private qualifyingOff(date: DateOnly, minimumRun: number = HOS_LIMITS.minOffBlockSeconds): number {
    const dayStart = this.cal.dayStartUnix(date);
    const dayEnd = this.cal.dayEndUnix(date);
    let total = 0;
    for (const r of this.runs) {
      if (runLength(r) >= minimumRun) {
        total += overlap(r.start, r.end, dayStart, dayEnd);
      }
    }

    return sec(total);
  }

  private runTouches(date: DateOnly, minimumRun: number): boolean {
    const dayStart = this.cal.dayStartUnix(date);
    const dayEnd = this.cal.dayEndUnix(date);
    for (const r of this.runs) {
      if (runLength(r) >= minimumRun && overlap(r.start, r.end, dayStart, dayEnd) > 0) {
        return true;
      }
    }

    return false;
  }

  private cycleAt(t: number): CycleState {
    const date = this.cal.dayOf(t);
    const windowStart = addDays(date, -(HOS_LIMITS.cycleDays - 1));
    const resetEnd = this.lastRunEndAtOrBefore(t, HOS_LIMITS.cycleResetSeconds);
    const from = Math.max(this.cal.dayStartUnix(windowStart), resetEnd ?? LONG_MIN);
    let total = this.sumBetween(from, t, false);
    const resetDate = resetEnd !== null ? this.cal.dayOf(resetEnd) : null;
    for (let d = windowStart; compareDate(d, date) <= 0; d = addDays(d, 1)) {
      if (resetDate === null || compareDate(d, resetDate) >= 0) {
        total += this.otherCarrierSeconds(d, false);
      }
    }

    const incomplete = resetEnd === null && this.cal.dayStartUnix(windowStart) < this.knownFrom;
    const onDuty = sec(total);
    return {
      onDutySeconds: onDuty,
      limitSeconds: HOS_LIMITS.cycleOnDutySeconds,
      remainingSeconds: Math.max(0, HOS_LIMITS.cycleOnDutySeconds - onDuty),
      windowStartDate: windowStart,
      lastResetEndUnix: resetEnd,
      recordsIncomplete: incomplete,
    };
  }

  private rest24At(t: number): Rest24State {
    const windowStart = t - HOS_LIMITS.rest24WindowSeconds;
    let best = 0;
    let bestEnd: number | null = null;
    let expires: number | null = null;
    for (const r of this.runs) {
      const end = Math.min(r.end, t);
      const o = overlap(r.start, end, windowStart, t);
      if (o > best) {
        best = o;
      }

      if (o >= HOS_LIMITS.rest24Seconds) {
        const candidate = end - HOS_LIMITS.rest24Seconds + HOS_LIMITS.rest24WindowSeconds;
        if (expires === null || candidate > expires) {
          expires = candidate;
          bestEnd = end;
        }
      }
    }

    const satisfied = expires !== null;
    const incomplete = !satisfied && windowStart < this.knownFrom;
    return {
      satisfied,
      qualifyingRunEndUnix: bestEnd,
      expiresAtUnix: expires,
      bestOffSeconds: sec(best),
      recordsIncomplete: incomplete,
    };
  }

  // ---- 6. Days ------------------------------------------------------------------------

  private buildDays(): DayTotals[] {
    const days: DayTotals[] = [];
    for (let offset = -13; offset <= 0; offset++) {
      const date = addDays(this.today, offset);
      days.push(this.buildDay(date));
    }

    return days;
  }

  private buildDay(date: DateOnly): DayTotals {
    const dayStart = this.cal.dayStartUnix(date);
    const dayEnd = this.cal.dayEndUnix(date);
    const known = dayEnd > this.knownFrom;
    const complete = compareDate(date, this.today) < 0;
    const end = Math.min(dayEnd, this.asOf);
    const driving = known ? this.dailyDrivingAt(date, end) : 0;
    const onDuty = known ? this.dailyOnDutyAt(date, end) : 0;
    let rawOff = 0;
    for (const p of this.pieces) {
      if (isOff(p)) {
        rawOff += overlap(p.start, p.end, dayStart, dayEnd);
      }
    }

    const qualifying = known ? this.qualifyingOff(date) : 0;
    const required = this.requiredOff(date);
    const shortfall = Math.max(0, required - qualifying);
    return {
      date,
      known,
      complete,
      drivingSeconds: driving,
      onDutySeconds: onDuty,
      otherCarrierOnDutySeconds: this.declaredOtherCarrierOnDuty(date),
      offDutySeconds: qualifying,
      rawOffDutySeconds: sec(rawOff),
      pcDecikm: this.pcDeclared.get(date) ?? 0,
      hasOwnEvents: this.ownEventDays.has(date),
      certified: this.certified.has(date),
      eightHourConsecutiveMet: known && this.runTouches(date, HOS_LIMITS.mandatoryConsecutiveOffSeconds),
      dailyOffRequiredSeconds: required,
      offDutyShortfallSeconds: shortfall,
      dailyOffMet: known && shortfall === 0,
    };
  }

  private requiredOff(date: DateOnly): number {
    let required = HOS_LIMITS.dailyOffSeconds;
    const ext = this.adverseByDay.get(date);
    if (ext !== undefined) {
      required = Math.max(HOS_LIMITS.mandatoryConsecutiveOffSeconds, HOS_LIMITS.dailyOffSeconds - ext);
    }

    const deferral = this.deferrals.get(date);
    if (deferral !== undefined && !this.deferralFailedDayOne.has(date)) {
      required = Math.min(required, HOS_LIMITS.dailyOffSeconds - deferral.deferredSeconds);
    }

    return required;
  }

  // ---- 7. Driving pieces ----------------------------------------------------------

  private evaluateDrivingPieces(): void {
    for (const p of this.pieces) {
      if (!isDriving(p)) {
        continue;
      }

      const t = p.end;
      const date = p.date;
      const dayKey = date;
      const extDay = this.adverseForDay(date, t);
      this.check("DailyDriving13h", dayKey, date, this.dailyDrivingAt(date, t), HOS_LIMITS.dailyDrivingSeconds + extDay, p);
      this.check("DailyOnDuty14h", dayKey, date, this.dailyOnDutyAt(date, t), HOS_LIMITS.dailyOnDutySeconds + extDay, p);

      const shift = this.shiftStartAt(p.start);
      const shiftKey = `${shift}`;
      const extShift = this.adverseForShift(shift, t);
      this.check("ShiftDriving13h", shiftKey, null, sec(this.sumBetween(shift, t, true)), HOS_LIMITS.shiftDrivingSeconds + extShift, p);
      this.check("ShiftOnDuty14h", shiftKey, null, sec(this.sumBetween(shift, t, false)), HOS_LIMITS.shiftOnDutySeconds + extShift, p);
      this.check("ShiftElapsed16h", shiftKey, null, sec(t - shift), HOS_LIMITS.shiftElapsedSeconds + extShift, p);

      const cycle = this.cycleAt(t);
      if (!cycle.recordsIncomplete) {
        this.check("Cycle1_70hIn7d", dayKey, date, cycle.onDutySeconds, HOS_LIMITS.cycleOnDutySeconds, p);
      }

      const rest = this.rest24At(t);
      if (!rest.satisfied && !rest.recordsIncomplete) {
        const at = Math.max(this.rest24ExpiryBefore(p.start) ?? p.start, p.start);
        this.record("Rest24hIn14d", dayKey, date, at, rest.bestOffSeconds, HOS_LIMITS.rest24Seconds);
      }

      for (let i = 0; i < this.oos.length; i++) {
        const { from, until } = this.oos[i];
        const o = overlap(p.start, p.end, from, until);
        if (o > 0) {
          const key = `${i}|${dayKey}`;
          const existing = this.pending.get(pendingKey("DroveWhileOutOfService", key));
          if (existing !== undefined) {
            existing.figure += sec(o);
          } else {
            this.record("DroveWhileOutOfService", key, date, Math.max(from, p.start), sec(o), 0);
          }
        }
      }
    }
  }

  /** The instant the most recent 24h-in-14d entitlement lapsed, if one ever existed. */
  private rest24ExpiryBefore(t: number): number | null {
    let latest: number | null = null;
    for (const r of this.runs) {
      if (r.end <= t && runLength(r) >= HOS_LIMITS.rest24Seconds) {
        const expires = r.end - HOS_LIMITS.rest24Seconds + HOS_LIMITS.rest24WindowSeconds;
        if (latest === null || expires > latest) {
          latest = expires;
        }
      }
    }

    return latest;
  }

  private check(rule: HosRule, key: string, date: DateOnly | null, figure: number, ceiling: number, p: Piece): void {
    if (figure <= ceiling) {
      return;
    }

    const existing = this.pending.get(pendingKey(rule, key));
    if (existing !== undefined) {
      existing.figure = figure;
      existing.limit = ceiling;
      return;
    }

    const at = Math.max(p.end - (figure - ceiling), p.start);
    this.record(rule, key, date ?? this.cal.dayOf(at), at, figure, ceiling);
  }

  private record(rule: HosRule, key: string, date: DateOnly, at: number, figure: number, limit: number): void {
    const k = pendingKey(rule, key);
    if (this.pending.has(k)) {
      return;
    }

    this.pending.set(k, { rule, date, atUnix: at, figure, limit });
  }

  // ---- 8. Daily off, deferral, records ------------------------------------------------

  private evaluateDailyOff(days: DayTotals[]): void {
    for (const deferral of this.deferrals.values()) {
      this.evaluateDeferral(deferral);
    }

    for (let i = 0; i < days.length; i++) {
      let day = days[i];
      if (!day.known || this.cal.dayStartUnix(day.date) < this.knownFrom) {
        continue;
      }

      if (this.deferralFailedDayOne.has(day.date)) {
        // Re-evaluated with the plain 10h by evaluateDeferral; refresh the totals row.
        days[i] = day = this.buildDay(day.date);
      }

      const dayKey = day.date;
      const dayEnd = this.cal.dayEndUnix(day.date);
      if (day.complete) {
        if (day.offDutySeconds < day.dailyOffRequiredSeconds) {
          this.record("DailyOffDuty10h", dayKey, day.date, dayEnd, day.offDutySeconds, day.dailyOffRequiredSeconds);
        }

        if (day.hasOwnEvents && !day.certified) {
          this.record("RodsNotCertified", dayKey, day.date, dayEnd, 0, 0);
        }

        continue;
      }

      // Today: a shortfall is reported, and becomes a violation only once it can no longer be made up.
      const remaining = dayEnd - this.asOf;
      let credit = 0;
      for (const r of this.runs) {
        if (r.end === this.asOf && runLength(r) < HOS_LIMITS.minOffBlockSeconds) {
          credit = overlap(r.start, r.end, this.cal.dayStartUnix(day.date), dayEnd);
        }
      }

      if (day.offDutySeconds + remaining + credit < day.dailyOffRequiredSeconds) {
        this.record("DailyOffDuty10h", dayKey, day.date, this.asOf, day.offDutySeconds, day.dailyOffRequiredSeconds);
      }
    }
  }

  private evaluateDeferral(deferral: OffDutyDeferral): void {
    const d1 = deferral.dayOne;
    const d2 = addDays(d1, 1);
    if (compareDate(d1, this.today) >= 0 || this.cal.dayStartUnix(d1) < this.knownFrom) {
      return;
    }

    const d = deferral.deferredSeconds;
    const off1 = this.qualifyingOff(d1);
    let ok = off1 >= HOS_LIMITS.dailyOffSeconds - d && this.runTouches(d1, HOS_LIMITS.mandatoryConsecutiveOffSeconds);
    let at = this.cal.dayEndUnix(d1);
    let off2 = 0;
    if (ok && compareDate(d2, this.today) < 0) {
      off2 = this.qualifyingOff(d2);
      const driving = this.dailyDrivingAt(d1, this.cal.dayEndUnix(d1)) + this.dailyDrivingAt(d2, this.cal.dayEndUnix(d2));
      ok =
        off2 >= HOS_LIMITS.dailyOffSeconds + d &&
        this.runTouches(d2, HOS_LIMITS.mandatoryConsecutiveOffSeconds + d) &&
        off1 + off2 >= HOS_LIMITS.deferralTwoDayOffMinSeconds &&
        driving <= HOS_LIMITS.deferralTwoDayDrivingMaxSeconds;
      at = this.cal.dayEndUnix(d2);
    }

    if (ok) {
      return;
    }

    this.deferralFailedDayOne.add(d1);
    this.record("DeferralConditions", d2, d2, at, off2, HOS_LIMITS.dailyOffSeconds + d);
    if (off1 < HOS_LIMITS.dailyOffSeconds) {
      this.record("DailyOffDuty10h", d1, d1, this.cal.dayEndUnix(d1), off1, HOS_LIMITS.dailyOffSeconds);
    }
  }

  private evaluateRecords(days: DayTotals[]): void {
    if (this.noLedger) {
      this.records14dIncomplete = true;
      this.record("RecordsIncomplete14d", "ledger", this.today, this.asOf, 0, HOS_LIMITS.rest24WindowSeconds);
      return;
    }

    if (this.knownFrom > this.asOf - HOS_LIMITS.rest24WindowSeconds) {
      this.records14dIncomplete = true;
      this.record("RecordsIncomplete14d", "knownFrom", this.today, this.asOf, sec(this.asOf - this.knownFrom), HOS_LIMITS.rest24WindowSeconds);
    }

    for (const day of days) {
      if (!day.complete || this.cal.dayStartUnix(day.date) < this.knownFrom) {
        continue;
      }

      if (!day.hasOwnEvents && !this.otherCarrier.has(day.date)) {
        this.records14dIncomplete = true;
        this.record("RecordsIncomplete14d", day.date, day.date, this.cal.dayStartUnix(day.date), 0, 0);
      }
    }
  }

  // ---- 9. Current state -----------------------------------------------------------

  private assemble(days: DayTotals[]): HosEvaluation {
    const today = days[days.length - 1];
    const between = this.betweenShiftsAt(this.asOf);
    const qualifyingShiftRun = this.lastRunEndAtOrBefore(this.asOf, HOS_LIMITS.mandatoryConsecutiveOffSeconds);
    const shiftStart = between ? this.asOf : (qualifyingShiftRun ?? this.knownFrom);
    const extDay = this.adverseForDay(this.today, this.asOf);
    const extShift = between ? 0 : this.adverseForShift(shiftStart, this.asOf);

    const drivingToday = this.dailyDrivingAt(this.today, this.asOf);
    const onDutyToday = this.dailyOnDutyAt(this.today, this.asOf);
    const dayDrivingCeil = HOS_LIMITS.dailyDrivingSeconds + extDay;
    const dayOnDutyCeil = HOS_LIMITS.dailyOnDutySeconds + extDay;

    const shiftDriving = between ? 0 : sec(this.sumBetween(shiftStart, this.asOf, true));
    const shiftOnDuty = between ? 0 : sec(this.sumBetween(shiftStart, this.asOf, false));
    const shiftElapsed = between ? 0 : sec(this.asOf - shiftStart);
    const shiftDrivingCeil = HOS_LIMITS.shiftDrivingSeconds + extShift;
    const shiftOnDutyCeil = HOS_LIMITS.shiftOnDutySeconds + extShift;
    const shiftElapsedCeil = HOS_LIMITS.shiftElapsedSeconds + extShift;

    const remDayDriving = Math.max(0, dayDrivingCeil - drivingToday);
    const remDayOnDuty = Math.max(0, dayOnDutyCeil - onDutyToday);
    const remShiftDriving = Math.max(0, shiftDrivingCeil - shiftDriving);
    const remShiftOnDuty = Math.max(0, shiftOnDutyCeil - shiftOnDuty);
    const remWindow = Math.max(0, shiftElapsedCeil - shiftElapsed);

    const cycle = this.cycleAt(this.asOf);
    const rest = this.rest24At(this.asOf);
    const shiftIncomplete = !between && qualifyingShiftRun === null && this.asOf - HOS_LIMITS.shiftElapsedSeconds < this.knownFrom;
    const recordsIncomplete = this.records14dIncomplete || shiftIncomplete || rest.recordsIncomplete || cycle.recordsIncomplete;
    const oosActive = this.oos.some((span) => span.from <= this.asOf && this.asOf < span.until);

    const blocking: BlockingReason[] = [];
    if (this.noLedger) {
      blocking.push("NoLedger");
    }

    if (recordsIncomplete) {
      blocking.push("RecordsIncomplete");
    }

    if (oosActive) {
      blocking.push("OutOfService");
    }

    if (!rest.satisfied && !rest.recordsIncomplete) {
      blocking.push("Rest24Expired");
    }

    if (cycle.remainingSeconds === 0) {
      blocking.push("CycleExhausted");
    }

    if (remDayDriving === 0) {
      blocking.push("DailyDrivingExhausted");
    }

    if (remDayOnDuty === 0) {
      blocking.push("DailyOnDutyExhausted");
    }

    if (!between && remShiftDriving === 0) {
      blocking.push("ShiftDrivingExhausted");
    }

    if (!between && remShiftOnDuty === 0) {
      blocking.push("ShiftOnDutyExhausted");
    }

    if (!between && remWindow === 0) {
      blocking.push("ShiftWindowClosed");
    }

    const remainingDriving = minOf(remDayDriving, remShiftDriving, remDayOnDuty, remShiftOnDuty, remWindow, cycle.remainingSeconds);
    const remainingOnDuty = minOf(remDayOnDuty, remShiftOnDuty, remWindow, cycle.remainingSeconds);
    const restRemaining = rest.satisfied ? sec(Math.max(0, (rest.expiresAtUnix as number) - this.asOf)) : 0;
    const tightest = this.noLedger ? 0 : minOf(remainingDriving, remainingOnDuty, cycle.remainingSeconds, restRemaining);

    const canDrive = blocking.length === 0;
    const band: HosBand = this.noLedger ? "Off" : !canDrive ? "Over" : tightest <= this.bands.soonSeconds ? "Soon" : "Ontime";

    const mustStop: number | null = between
      ? null
      : Math.min(Math.min(this.asOf + remShiftDriving, this.asOf + remShiftOnDuty), shiftStart + shiftElapsedCeil);
    const shift: ShiftWindow = {
      startUnix: between ? null : shiftStart,
      drivingSeconds: shiftDriving,
      onDutySeconds: shiftOnDuty,
      elapsedSeconds: shiftElapsed,
      drivingCeilingSeconds: shiftDrivingCeil,
      onDutyCeilingSeconds: shiftOnDutyCeil,
      elapsedCeilingSeconds: shiftElapsedCeil,
      adverseExtensionSeconds: extShift,
      mustStopDrivingByUnix: mustStop,
    };

    let next: NextRequiredOffDuty | null = null;
    if (!this.noLedger) {
      const candidates: { at: number; rule: HosRule }[] = [
        { at: this.asOf + remDayDriving, rule: "DailyDriving13h" },
        { at: this.asOf + remDayOnDuty, rule: "DailyOnDuty14h" },
        { at: this.asOf + remShiftDriving, rule: "ShiftDriving13h" },
        { at: this.asOf + remShiftOnDuty, rule: "ShiftOnDuty14h" },
        { at: this.asOf + remWindow, rule: "ShiftElapsed16h" },
        { at: this.asOf + cycle.remainingSeconds, rule: "Cycle1_70hIn7d" },
      ];
      if (rest.expiresAtUnix !== null) {
        candidates.push({ at: Math.max(rest.expiresAtUnix, this.asOf), rule: "Rest24hIn14d" });
      } else if (!rest.recordsIncomplete) {
        candidates.push({ at: this.asOf, rule: "Rest24hIn14d" });
      }

      let first = candidates[0];
      for (const c of candidates) {
        if (c.at < first.at || (c.at === first.at && ruleOrdinal(c.rule) < ruleOrdinal(first.rule))) {
          first = c;
        }
      }

      next = {
        mustBeOffByUnix: first.at,
        reason: first.rule,
        minimumSeconds: first.rule === "Rest24hIn14d" ? HOS_LIMITS.rest24Seconds : HOS_LIMITS.mandatoryConsecutiveOffSeconds,
        dailyShortfallSeconds: today.offDutyShortfallSeconds,
      };
    }

    const pcToday = this.pcDeclared.get(this.today) ?? 0;
    return {
      asOfUnix: this.asOf,
      currentStatus: this.currentStatus,
      currentStatusSinceUnix: this.currentSince,
      days,
      today,
      remainingDrivingTodaySeconds: remainingDriving,
      remainingOnDutyTodaySeconds: remainingOnDuty,
      shift,
      cycle,
      rest24: rest,
      pcTodayDecikm: pcToday,
      pcRemainingDecikm: Math.max(0, HOS_LIMITS.pcDailyLimitDecikm - pcToday),
      violations: this.materialiseViolations(),
      canDriveNow: canDrive,
      blockingReasons: blocking,
      nextRequiredOffDuty: next,
      tightestRemainingSeconds: tightest,
      band,
      ledgerProblems: this.problems,
    };
  }

  private materialiseViolations(): HosViolation[] {
    const list: HosViolation[] = [];
    for (const v of this.pending.values()) {
      let suppressedBy: string | null = null;
      for (const { exception, from, to } of this.emergencies) {
        if (from <= v.atUnix && v.atUnix <= to) {
          suppressedBy = exception.id;
          break;
        }
      }

      list.push({
        rule: v.rule,
        date: v.date,
        atUnix: v.atUnix,
        figureSeconds: v.figure,
        limitSeconds: v.limit,
        suppressed: suppressedBy !== null,
        suppressedByExceptionId: suppressedBy,
        message: HosMessages.for(v.rule, v.figure, v.limit),
      });
    }

    list.sort((a, b) => {
      let c = compareNumber(a.atUnix, b.atUnix);
      if (c !== 0) return c;
      c = compareNumber(ruleOrdinal(a.rule), ruleOrdinal(b.rule));
      return c !== 0 ? c : compareDate(a.date, b.date);
    });
    return list;
  }

  private problem(code: string, message: string, at: number | null): void {
    this.problems.push({ code, message, atUnix: at });
  }
}

// ---- Public API: Evaluate ----------------------------------------------------------------------

/**
 * `adverseCutoffUnix`: adverse-conditions extensions apply only to figures evaluated at instants
 * at or before it. `project` passes its as-of so a declared extension never stretches projected
 * time.
 */
function evaluateWithCutoff(ledger: DutyLedger, asOfUnix: number, calendar: HosCalendar, bands: HosBands | null | undefined, adverseCutoffUnix: number): HosEvaluation {
  return new Evaluation(ledger, asOfUnix, calendar, bands ?? DEFAULT_HOS_BANDS, adverseCutoffUnix).run();
}

/** HosEngine.Evaluate: the driver's standing at `asOfUnix`, from the ledger alone. */
export function evaluate(ledger: DutyLedger, asOfUnix: number, calendar: HosCalendar, bands?: HosBands | null): HosEvaluation {
  return evaluateWithCutoff(ledger, asOfUnix, calendar, bands, LONG_MAX);
}

// ---- Public API: Project (HosEngine.Project.cs) -----------------------------------------------

const PROJECTION_BANDS: HosBands = { soonSeconds: 7200 };

interface Leg {
  startUnix: number;
  segments: PlannedSegment[];
  tripId: string | null;
}

interface Simulation {
  feasible: boolean;
  firstBreach: HosViolation | null;
  marginApplied: number;
  stillNeeded: number;
  achievable: boolean;
  atPlanEnd: HosEvaluation;
}

/**
 * HosEngine.Project: synthesises the plan onto the ledger (off duty from `offDutyFromUnix`,
 * default as-of, then each leg with margin applied) and evaluates at the plan's end. Adverse
 * extensions already declared are never applied to projected time. Exceptions declared after
 * as-of are ignored.
 */
export function project(ledger: DutyLedger, asOfUnix: number, plan: TripPlan, calendar: HosCalendar, margin?: PlanningMargin | null): HosProjection {
  const m = margin ?? DEFAULT_PLANNING_MARGIN;

  const problems: HosLedgerProblem[] = [];
  const offFrom = plan.offDutyFromUnix ?? asOfUnix;
  if (offFrom < asOfUnix) {
    problems.push({ code: "InvalidPlan", message: "The assumed off-duty start is before as-of.", atUnix: offFrom });
  }

  const baseLedger: DutyLedger = {
    ...ledger,
    exceptions: (ledger.exceptions ?? []).filter((e) => e !== null && e !== undefined && e.declaredAtUnix <= asOfUnix),
  };

  const legs = validateLegs(plan, offFrom, problems);
  if (problems.length > 0 || legs.length === 0) {
    if (legs.length === 0 && problems.length === 0) {
      problems.push({ code: "InvalidPlan", message: "The plan has no legs.", atUnix: null });
    }

    const evaluation = evaluateWithCutoff(baseLedger, asOfUnix, calendar, PROJECTION_BANDS, asOfUnix);
    return {
      feasible: false,
      firstBreach: null,
      firstBreachAtUnix: null,
      marginAppliedSeconds: 0,
      assumedOffDutyFromUnix: offFrom,
      requiredOffBeforeStartSeconds: null,
      dailyOffStillNeededAfterPlanSeconds: evaluation.today.offDutyShortfallSeconds,
      dailyOffAchievableAfterPlan: false,
      atPlanEnd: evaluation,
      problems,
    };
  }

  const betweenLegs = plan.betweenLegs ?? "OffDuty";
  const core = simulate(baseLedger, asOfUnix, legs, offFrom, betweenLegs, calendar, m, 0);

  let requiredOff: number | null = core.feasible ? 0 : null;
  if (!core.feasible) {
    const now = evaluateWithCutoff(baseLedger, asOfUnix, calendar, PROJECTION_BANDS, asOfUnix);
    const candidateSet = new Set<number>([
      HOS_LIMITS.mandatoryConsecutiveOffSeconds,
      HOS_LIMITS.rest24Seconds,
      HOS_LIMITS.cycleResetSeconds,
    ]);
    if (now.today.offDutyShortfallSeconds > 0) {
      candidateSet.add(now.today.offDutyShortfallSeconds);
    }

    const candidates = [...candidateSet].sort(compareNumber);
    for (const x of candidates) {
      const attempt = simulate(baseLedger, asOfUnix, legs, offFrom, betweenLegs, calendar, m, x);
      if (attempt.feasible) {
        requiredOff = x;
        break;
      }
    }
  }

  return {
    feasible: core.feasible,
    firstBreach: core.firstBreach,
    firstBreachAtUnix: core.firstBreach?.atUnix ?? null,
    marginAppliedSeconds: core.marginApplied,
    assumedOffDutyFromUnix: offFrom,
    requiredOffBeforeStartSeconds: requiredOff,
    dailyOffStillNeededAfterPlanSeconds: core.stillNeeded,
    dailyOffAchievableAfterPlan: core.achievable,
    atPlanEnd: core.atPlanEnd,
    problems,
  };
}

function validateLegs(plan: TripPlan, offFrom: number, problems: HosLedgerProblem[]): Leg[] {
  const legs: Leg[] = [];
  const ordered = (plan.legs ?? []).filter((l) => l !== null && l !== undefined).sort((a, b) => compareNumber(a.startUnix, b.startUnix));
  let previousEnd = LONG_MIN;
  for (const leg of ordered) {
    const segments = (leg.segments ?? []).filter((s) => s !== null && s !== undefined);
    if (segments.length === 0 || segments.some((s) => s.seconds <= 0 || !isDefined(DUTY_STATUSES, s.status))) {
      problems.push({ code: "InvalidPlan", message: "A leg has no segments, or a segment with a non-positive duration.", atUnix: leg.startUnix });
      continue;
    }

    if (leg.startUnix < offFrom) {
      problems.push({ code: "InvalidPlan", message: "A leg starts before the assumed off-duty start.", atUnix: leg.startUnix });
      continue;
    }

    let length = 0;
    for (const s of segments) {
      length += s.seconds;
    }

    if (leg.startUnix < previousEnd) {
      problems.push({ code: "InvalidPlan", message: "Legs overlap.", atUnix: leg.startUnix });
      continue;
    }

    previousEnd = leg.startUnix + length;
    legs.push({ startUnix: leg.startUnix, segments, tripId: leg.tripId ?? null });
  }

  return legs;
}

function simulate(
  ledger: DutyLedger,
  asOf: number,
  legs: Leg[],
  offFrom: number,
  betweenLegs: DutyStatus,
  cal: HosCalendar,
  margin: PlanningMargin,
  counterfactualOffSeconds: number,
): Simulation {
  let events: DutyEvent[] = [...(ledger.events ?? [])];
  let sequence = 1;
  if (events.length > 0) {
    let max = events[0].sequence;
    for (const e of events) {
      max = Math.max(max, e.sequence);
    }

    sequence = max + 1;
  }

  if (counterfactualOffSeconds > 0) {
    // "What if the driver had been off for x ending at the first leg's start?" — replace
    // whatever the ledger says in that span with one off-duty run.
    const offStart = legs[0].startUnix - counterfactualOffSeconds;
    events = events.filter((e) => !(e.atUnix >= offStart && e.atUnix < legs[0].startUnix));
    events.push({ atUnix: offStart, status: "OffDuty", source: "System", sequence: sequence++, odometerDecikm: null, note: "projection: assumed rest" });
  }

  events.push({ atUnix: offFrom, status: "OffDuty", source: "System", sequence: sequence++, odometerDecikm: null, note: "projection: off duty" });

  let marginApplied = 0;
  let cursor = offFrom;
  const firstDrivingInstants: number[] = [];
  for (const leg of legs) {
    cursor = Math.max(cursor, leg.startUnix);
    let fixedApplied = false;
    for (const segment of leg.segments) {
      let seconds = segment.seconds;
      if (segment.status === "Driving" || segment.status === "OnDuty") {
        let scaled = sec(idiv(seconds * (100 + margin.percent), 100));
        if (segment.status === "Driving" && !fixedApplied) {
          scaled += margin.fixedSeconds;
          fixedApplied = true;
        }

        marginApplied += scaled - seconds;
        seconds = scaled;
      }

      if (segment.status === "Driving" && (firstDrivingInstants.length === 0 || firstDrivingInstants[firstDrivingInstants.length - 1] < leg.startUnix)) {
        firstDrivingInstants.push(cursor);
      }

      events.push({ atUnix: cursor, status: segment.status, source: "System", sequence: sequence++, odometerDecikm: null, note: leg.tripId });
      cursor += seconds;
    }

    events.push({ atUnix: cursor, status: betweenLegs, source: "System", sequence: sequence++, odometerDecikm: null, note: leg.tripId });
  }

  const planEnd = cursor;
  const projected: DutyLedger = { ...ledger, events };
  const atEnd = evaluateWithCutoff(projected, planEnd, cal, PROJECTION_BANDS, asOf);

  const breaches: HosViolation[] = [];
  for (const v of atEnd.violations) {
    if (!v.suppressed && v.atUnix >= asOf) {
      breaches.push(v);
    }
  }

  for (const instant of firstDrivingInstants) {
    const atStart = evaluateWithCutoff(projected, instant, cal, PROJECTION_BANDS, asOf);
    if (atStart.blockingReasons.length > 0) {
      breaches.push(breachFor(atStart, instant, cal));
    }
  }

  const stillNeeded = atEnd.today.offDutyShortfallSeconds;
  const achievable = cal.dayEndUnix(atEnd.today.date) - planEnd >= stillNeeded;
  if (!achievable && !breaches.some((b) => b.rule === "DailyOffDuty10h" && b.date === atEnd.today.date)) {
    breaches.push({
      rule: "DailyOffDuty10h",
      date: atEnd.today.date,
      atUnix: planEnd,
      figureSeconds: atEnd.today.offDutySeconds,
      limitSeconds: atEnd.today.dailyOffRequiredSeconds,
      suppressed: false,
      suppressedByExceptionId: null,
      message: HosMessages.for("DailyOffDuty10h", atEnd.today.offDutySeconds, atEnd.today.dailyOffRequiredSeconds),
    });
  }

  let first: HosViolation | null = null;
  for (const b of breaches) {
    if (first === null || b.atUnix < first.atUnix || (b.atUnix === first.atUnix && ruleOrdinal(b.rule) < ruleOrdinal(first.rule))) {
      first = b;
    }
  }

  return { feasible: first === null, firstBreach: first, marginApplied, stillNeeded, achievable, atPlanEnd: atEnd };
}

function breachFor(at: HosEvaluation, instant: number, cal: HosCalendar): HosViolation {
  const reason = at.blockingReasons[0];
  let rule: HosRule;
  let figure: number;
  let limit: number;
  switch (reason) {
    case "NoLedger":
    case "RecordsIncomplete":
      [rule, figure, limit] = ["RecordsIncomplete14d", 0, HOS_LIMITS.rest24WindowSeconds];
      break;
    case "OutOfService":
      [rule, figure, limit] = ["DroveWhileOutOfService", 0, 0];
      break;
    case "Rest24Expired":
      [rule, figure, limit] = ["Rest24hIn14d", at.rest24.bestOffSeconds, HOS_LIMITS.rest24Seconds];
      break;
    case "CycleExhausted":
      [rule, figure, limit] = ["Cycle1_70hIn7d", at.cycle.onDutySeconds, at.cycle.limitSeconds];
      break;
    case "DailyDrivingExhausted":
      [rule, figure, limit] = ["DailyDriving13h", at.today.drivingSeconds, HOS_LIMITS.dailyDrivingSeconds];
      break;
    case "DailyOnDutyExhausted":
      [rule, figure, limit] = ["DailyOnDuty14h", at.today.onDutySeconds, HOS_LIMITS.dailyOnDutySeconds];
      break;
    case "ShiftDrivingExhausted":
      [rule, figure, limit] = ["ShiftDriving13h", at.shift.drivingSeconds, at.shift.drivingCeilingSeconds];
      break;
    case "ShiftOnDutyExhausted":
      [rule, figure, limit] = ["ShiftOnDuty14h", at.shift.onDutySeconds, at.shift.onDutyCeilingSeconds];
      break;
    default:
      [rule, figure, limit] = ["ShiftElapsed16h", at.shift.elapsedSeconds, at.shift.elapsedCeilingSeconds];
      break;
  }

  return {
    rule,
    date: cal.dayOf(instant),
    atUnix: instant,
    figureSeconds: figure,
    limitSeconds: limit,
    suppressed: false,
    suppressedByExceptionId: null,
    message: HosMessages.for(rule, figure, limit),
  };
}
