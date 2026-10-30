import type { DeclarableExceptionKind, DeclareExceptionInput, ReportEldMalfunctionInput } from "../api/hos";
import { HOS_LIMITS, HosCalendar, HosWire, type DateOnly, type DutyStatus } from "./engine";

// ---------------------------------------------------------------------------
// The compliance commands' payloads — `hos.exception` and `hos.eld-malfunction` — and the
// other-carrier day laid out as three `hos.duty` events. Same contract as lib/hos/dutyCommand.ts:
// one place builds each payload, one place maps it onto the endpoint body at send time, and
// nothing stored on the device is trusted on the way out.
//
// Each sheet on the Hours screen runs the *DraftError function before it enqueues, so the
// driver reads the refusal on the tablet rather than as a parked command an hour later. The
// rules are the aggregates' own (named on each function) and the server remains the one that
// decides — a 400 still parks the command with the server's message.
// ---------------------------------------------------------------------------

// --- hos.exception -----------------------------------------------------------

/** HosException.ReasonMaxLength / LocationMaxLength / NoteMaxLength. */
export const EXCEPTION_REASON_MAX = 500;
export const EXCEPTION_LOCATION_MAX = 200;
export const EXCEPTION_NOTE_MAX = 1000;

/** HosException.MinimumExtension (1 min) and HosLimits.AdverseMaxExtensionSeconds (2h), in minutes. */
export const ADVERSE_EXTENSION_MIN_MINUTES = 1;
export const ADVERSE_EXTENSION_MAX_MINUTES = HOS_LIMITS.adverseMaxExtensionSeconds / 60;

/** HosException.DeclarationWindow (s.76): the conditions must have arisen inside the last 24 hours. */
export const EXCEPTION_DECLARATION_WINDOW_SECONDS = 24 * 3600;

/** The two kinds a driver may declare from the tablet (lib/api/hos.ts DeclarableExceptionKind); a deferral is the compliance officer's. */
export type { DeclarableExceptionKind };

export const DRIVER_EXCEPTION_KINDS: readonly DeclarableExceptionKind[] = [HosWire.AdverseDrivingConditionsKind, HosWire.EmergencyKind] as DeclarableExceptionKind[];

export interface DeclareExceptionCommandPayload {
  action: "declare";
  kind: DeclarableExceptionKind;
  effectiveFromUtc: string;
  reason: string;
  location: string | null;
  notKnownBeforeDeparture: boolean;
  extraDrivingMinutes: number | null;
  eldRemarkRecorded: boolean;
  tripId: string | null;
}

export interface WithdrawExceptionCommandPayload {
  action: "withdraw";
  exceptionId: string;
  note: string | null;
}

export type ExceptionCommandPayload = DeclareExceptionCommandPayload | WithdrawExceptionCommandPayload;

/** What the exception sheet collects. */
export interface ExceptionDraft {
  kind: DeclarableExceptionKind;
  /** When the conditions arose, Unix seconds — the tablet's clock at the time of the tap. */
  effectiveFromUnix: number;
  reason: string;
  location: string;
  /** Adverse only: the s.76 attestation. */
  notKnownBeforeDeparture: boolean;
  /** Adverse only: 1–120 minutes, as typed. */
  extraDrivingMinutes: string;
  eldRemarkRecorded: boolean;
}

function clean(value: string | null | undefined, max: number): string | null {
  const trimmed = (value ?? "").trim();
  return trimmed.length === 0 ? null : trimmed.slice(0, max);
}

/** The integer minutes a typed extension means, or null when it is not a whole number. */
export function extensionMinutes(typed: string): number | null {
  const trimmed = typed.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  return Number(trimmed);
}

/**
 * Why a draft would be refused, in the driver's words, or null when it may be declared.
 * Mirrors HosException.Declare: a reason (≤ 500), an effective-from inside the last 24 hours
 * and not more than five minutes ahead, and for adverse conditions the knowability attestation
 * and a 1–120 minute extension (AdverseKnowabilityRequired, AdverseExtensionRequired,
 * AdverseExtensionTooLong). Checked on this device so the refusal reads on screen first.
 */
export function exceptionDraftError(draft: ExceptionDraft, nowUnix: number): string | null {
  if (!DRIVER_EXCEPTION_KINDS.includes(draft.kind)) return "Choose adverse driving conditions or an emergency.";
  if (draft.reason.trim().length === 0) return draft.kind === HosWire.EmergencyKind ? "Say why you had to stop." : "A reason is required.";
  if (draft.reason.trim().length > EXCEPTION_REASON_MAX) return `The reason must be at most ${EXCEPTION_REASON_MAX} characters.`;
  if (draft.location.trim().length > EXCEPTION_LOCATION_MAX) return `The location must be at most ${EXCEPTION_LOCATION_MAX} characters.`;
  if (draft.effectiveFromUnix > nowUnix + HOS_LIMITS.futureSkewSeconds) return "The exception's start is in the future.";
  if (draft.effectiveFromUnix < nowUnix - EXCEPTION_DECLARATION_WINDOW_SECONDS) {
    return "An adverse-conditions or emergency exception must be declared within 24 hours of the conditions arising.";
  }
  if (draft.kind === HosWire.AdverseDrivingConditionsKind) {
    if (!draft.notKnownBeforeDeparture) {
      return "Adverse driving conditions can only be declared when they could not reasonably have been known before departure (s.76).";
    }
    const minutes = extensionMinutes(draft.extraDrivingMinutes);
    if (minutes === null || minutes < ADVERSE_EXTENSION_MIN_MINUTES) {
      return `Say how much extra driving the conditions need — at least ${ADVERSE_EXTENSION_MIN_MINUTES} minute.`;
    }
    if (minutes > ADVERSE_EXTENSION_MAX_MINUTES) {
      return `Adverse driving conditions extend the limits by at most ${ADVERSE_EXTENSION_MAX_MINUTES} minutes (two hours, s.76).`;
    }
  }
  return null;
}

/** The queue's capture shape for a declaration. Throws on a draft exceptionDraftError refuses. */
export function declareExceptionPayload(draft: ExceptionDraft, nowUnix: number): DeclareExceptionCommandPayload {
  const error = exceptionDraftError(draft, nowUnix);
  if (error !== null) throw new Error(error);
  const adverse = draft.kind === HosWire.AdverseDrivingConditionsKind;
  return {
    action: "declare",
    kind: draft.kind,
    effectiveFromUtc: new Date(draft.effectiveFromUnix * 1000).toISOString(),
    reason: draft.reason.trim().slice(0, EXCEPTION_REASON_MAX),
    location: clean(draft.location, EXCEPTION_LOCATION_MAX),
    notKnownBeforeDeparture: adverse ? draft.notKnownBeforeDeparture : false,
    extraDrivingMinutes: adverse ? extensionMinutes(draft.extraDrivingMinutes) : null,
    eldRemarkRecorded: draft.eldRemarkRecorded,
    tripId: null,
  };
}

export function withdrawExceptionPayload(exceptionId: string, note?: string | null): WithdrawExceptionCommandPayload {
  return { action: "withdraw", exceptionId, note: clean(note, EXCEPTION_NOTE_MAX) };
}

export function isExceptionCommandPayload(payload: unknown): payload is ExceptionCommandPayload {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  if (p.action === "withdraw") return typeof p.exceptionId === "string" && p.exceptionId.length > 0;
  if (p.action !== "declare") return false;
  return (
    typeof p.kind === "string" &&
    (DRIVER_EXCEPTION_KINDS as readonly string[]).includes(p.kind) &&
    typeof p.effectiveFromUtc === "string" &&
    typeof p.reason === "string"
  );
}

/** The endpoint body for a captured declaration (DeclareHosExceptionRequest). Throws on anything else. */
export function exceptionInputFrom(payload: unknown): DeclareExceptionInput {
  if (!isExceptionCommandPayload(payload) || payload.action !== "declare") {
    throw new Error("Not an exception declaration payload.");
  }
  const adverse = payload.kind === HosWire.AdverseDrivingConditionsKind;
  return {
    kind: payload.kind,
    effectiveFromUtc: payload.effectiveFromUtc,
    reason: payload.reason,
    location: payload.location ?? null,
    tripId: payload.tripId ?? null,
    notKnownBeforeDeparture: adverse ? payload.notKnownBeforeDeparture === true : false,
    extraDrivingMinutes: adverse ? (payload.extraDrivingMinutes ?? null) : null,
    eldRemarkRecorded: payload.eldRemarkRecorded === true,
  };
}

// --- hos.eld-malfunction ------------------------------------------------------

/** EldMalfunctionReport.CodeMaxLength / DescriptionMaxLength. */
export const ELD_CODE_MAX = 32;
export const ELD_DESCRIPTION_MAX = 1000;

export interface EldMalfunctionCommandPayload {
  code: string;
  noticedAtUtc: string;
  description: string;
  vehicleId: string | null;
}

export interface EldMalfunctionDraft {
  code: string;
  /** When the driver noticed it, Unix seconds (today, local). */
  noticedAtUnix: number;
  description: string;
}

/**
 * Mirrors EldMalfunctionReport.Report: a code (≤ 32), a description, and a noticed-at not more
 * than five minutes ahead (EldCodeRequired, EldCodeTooLong, EldDescriptionRequired,
 * EldNoticedInFuture).
 */
export function eldMalfunctionDraftError(draft: EldMalfunctionDraft, nowUnix: number): string | null {
  if (draft.code.trim().length === 0) return "The malfunction code the ELD displayed is required.";
  if (draft.code.trim().length > ELD_CODE_MAX) return `The malfunction code must be at most ${ELD_CODE_MAX} characters.`;
  if (draft.description.trim().length === 0) return "A description of the malfunction is required.";
  if (!Number.isFinite(draft.noticedAtUnix)) return "Say when you noticed it.";
  if (draft.noticedAtUnix > nowUnix + HOS_LIMITS.futureSkewSeconds) return "The malfunction cannot have been noticed in the future.";
  return null;
}

export function eldMalfunctionPayload(draft: EldMalfunctionDraft, nowUnix: number, vehicleId: string | null = null): EldMalfunctionCommandPayload {
  const error = eldMalfunctionDraftError(draft, nowUnix);
  if (error !== null) throw new Error(error);
  return {
    code: draft.code.trim(),
    noticedAtUtc: new Date(draft.noticedAtUnix * 1000).toISOString(),
    description: draft.description.trim().slice(0, ELD_DESCRIPTION_MAX),
    vehicleId,
  };
}

export function isEldMalfunctionCommandPayload(payload: unknown): payload is EldMalfunctionCommandPayload {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  return typeof p.code === "string" && typeof p.noticedAtUtc === "string" && typeof p.description === "string";
}

/** The endpoint body for a captured report (ReportEldMalfunctionRequest). Throws on anything else. */
export function eldMalfunctionInputFrom(payload: unknown): ReportEldMalfunctionInput {
  if (!isEldMalfunctionCommandPayload(payload)) {
    throw new Error("Not an ELD malfunction report payload.");
  }
  return {
    code: payload.code,
    noticedAtUtc: payload.noticedAtUtc,
    description: payload.description,
    vehicleId: typeof payload.vehicleId === "string" && payload.vehicleId.length > 0 ? payload.vehicleId : null,
  };
}

// --- other-carrier hours: three hos.duty events ---------------------------------

/** How far back a day may be declared: the regulation's 14-day record window. */
export const OTHER_CARRIER_LOOKBACK_DAYS = 14;

export interface OtherCarrierDayDraft {
  date: DateOnly;
  carrierName: string;
  /** As typed. On-duty time INCLUDES the driving. */
  onDutyMinutes: string;
  drivingMinutes: string;
}

function wholeMinutes(typed: string): number | null {
  const trimmed = typed.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  return Number(trimmed);
}

/**
 * Why an other-carrier day would be refused, or null. The server's own rule on these events is
 * CarrierNameRequired (DutyDay.Validate); the rest is this tablet's arithmetic: whole minutes,
 * driving inside on-duty, no more than a day, a date inside the last 14 days and not ahead.
 */
export function otherCarrierDraftError(draft: OtherCarrierDayDraft, today: DateOnly): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(draft.date)) return "Choose the day the work was done.";
  if (draft.date > today) return "The day cannot be in the future.";
  const earliest = shiftDate(today, -(OTHER_CARRIER_LOOKBACK_DAYS - 1));
  if (draft.date < earliest) return `Only the last ${OTHER_CARRIER_LOOKBACK_DAYS} days can be declared here; older work goes through dispatch.`;
  if (draft.carrierName.trim().length === 0) return "A carrier name is required for work declared for another carrier.";
  const onDuty = wholeMinutes(draft.onDutyMinutes);
  const driving = wholeMinutes(draft.drivingMinutes);
  if (onDuty === null || driving === null) return "On-duty and driving time are whole minutes.";
  if (onDuty <= 0) return "On-duty time must be at least one minute.";
  if (onDuty > 24 * 60) return "On-duty time cannot exceed 24 hours in one day.";
  if (driving > onDuty) return "Driving time is part of on-duty time and cannot exceed it.";
  return null;
}

function shiftDate(date: DateOnly, days: number): DateOnly {
  const [y, m, d] = date.split("-").map(Number);
  const t = new Date(Date.UTC(y, m - 1, d) + days * 86400000);
  const pad = (n: number) => (n < 10 ? `0${n}` : String(n));
  return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}-${pad(t.getUTCDate())}`;
}

export interface OtherCarrierEvent {
  status: DutyStatus;
  atUnix: number;
  atUtc: string;
}

/**
 * The day's events laid out from the regulation day's start (America/Winnipeg midnight):
 * OnDuty at the start, Driving once the on-duty-not-driving time is spent, OffDuty when the
 * on-duty time is used up. Zero driving minutes lays out OnDuty → OffDuty only — a zero-length
 * Driving segment is a false statement, not a shorter one. Each event becomes its own
 * `hos.duty` command with its own id. Throws on a draft otherCarrierDraftError refuses.
 */
export function otherCarrierEvents(draft: OtherCarrierDayDraft, today: DateOnly, calendar: HosCalendar): OtherCarrierEvent[] {
  const error = otherCarrierDraftError(draft, today);
  if (error !== null) throw new Error(error);
  const onDuty = wholeMinutes(draft.onDutyMinutes)!;
  const driving = wholeMinutes(draft.drivingMinutes)!;
  const start = calendar.dayStartUnix(draft.date);
  const at = (minutes: number): OtherCarrierEvent["atUnix"] => start + minutes * 60;
  const iso = (unix: number) => new Date(unix * 1000).toISOString();
  const events: OtherCarrierEvent[] = [{ status: "OnDuty", atUnix: at(0), atUtc: iso(at(0)) }];
  if (driving > 0) {
    const drivingAt = at(onDuty - driving);
    events.push({ status: "Driving", atUnix: drivingAt, atUtc: iso(drivingAt) });
  }
  events.push({ status: "OffDuty", atUnix: at(onDuty), atUtc: iso(at(onDuty)) });
  return events;
}
