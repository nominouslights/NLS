import { request } from "./transport";
import type { BlockingReason, DayTotals, HosBand, HosEvaluation, HosViolation } from "../hos/engine";

// ---------------------------------------------------------------------------
// Hours-of-service API client — contract owned by Backend/ (Drivers module,
// HosEndpoints.cs + HosResponses.cs). Shapes mirror DutyDaySummaryResponse /
// DutyEventResponse / HosStatusResponse / HosRosterStatusResponse exactly
// (JSON camelCase; enums as member names; the engine's own records nested
// verbatim so the lib/hos/engine.ts types apply unchanged). Do not invent
// fields — extend only when the backend contract changes.
//
// Imports only ./transport and TYPES from ../hos/engine, so this file can be
// copied into DriverField/lib/api/ unchanged.
// ---------------------------------------------------------------------------

/** The duty strings the API emits and accepts (HosWire.Status). */
export type DutyStatusWire = "Off Duty" | "On Duty" | "Driving" | "Personal Conveyance";

/** The source strings the API emits and accepts (HosWire.Source). */
export type EventSourceWire =
  | "Driver App"
  | "ELD transcription"
  | "Manual (paper backup)"
  | "Other carrier (declared)"
  | "System";

/** The two sources a transcription may carry — never "Driver App" from a console. */
export type TranscriptionSource = "ELD transcription" | "Manual (paper backup)";

/** One duty event as the API shows it (DutyEventResponse). Voided events are
 *  included so the timeline can render the amendment history; the replacement
 *  points back via amendsEventId. */
export interface DutyEventRecord {
  eventId: string;
  sequence: number;
  atUtc: string;
  recordedAtUtc: string;
  status: DutyStatusWire;
  source: EventSourceWire;
  recordedByUserId: string | null;
  enteredBy: string | null;
  carrierName: string | null;
  note: string | null;
  odometerStartKm: number | null;
  odometerEndKm: number | null;
  pcNoPassengersOrCargo: boolean;
  pcNoTrailer: boolean;
  pcNoWork: boolean;
  amendsEventId: string | null;
  amendmentReason: string | null;
  isVoided: boolean;
}

/** A stretch of one status on a day's timeline — effective events only (DutySegmentResponse). */
export interface DutySegmentRecord {
  status: DutyStatusWire;
  source: EventSourceWire;
  fromUtc: string;
  toUtc: string;
  durationSeconds: number;
  eventId: string;
}

/** One day of a driver's ledger (DutyDaySummaryResponse). `totals` is the
 *  engine's DayTotals for that date; `violations` the engine's breaches dated
 *  that day. hasRecords=false is a day in the requested range with no DutyDay
 *  on file — unknown, never "hours unused". */
export interface DutyDaySummary {
  driverId: string;
  date: string; // DateOnly, "2026-10-09"
  hasRecords: boolean;
  dutyDayId: string | null;
  totals: DayTotals;
  personalConveyanceKm: number;
  certified: boolean;
  certifiedAtUtc: string | null;
  recertificationRequired: boolean;
  segments: DutySegmentRecord[];
  events: DutyEventRecord[];
  violations: HosViolation[];
  label: string;
}

/** Reserved for the out-of-service declaration (next PR); always null today. */
export interface OutOfServiceRecord {
  fromUtc: string;
  untilUtc: string | null;
  reason: string;
}

/** A driver's standing right now (HosStatusResponse). `evaluation` is the
 *  engine's HosEvaluation verbatim. recordsOnFile=false means the ledger does
 *  not cover the regulation's 14-day window (not dispatchable). */
export interface HosStatus {
  driverId: string;
  label: string;
  currentDuty: DutyStatusWire;
  currentDutySinceUtc: string;
  recordsOnFile: boolean;
  knownFromUtc: string | null;
  outOfService: OutOfServiceRecord | null;
  eldMalfunctionOpen: boolean;
  evaluation: HosEvaluation;
}

/** One roster row of GET /api/drivers/hos/status (HosRosterStatusResponse). */
export interface HosRosterStatus {
  driverId: string;
  band: HosBand;
  tightestRemainingSeconds: number;
  canDriveNow: boolean;
  blockingReasons: BlockingReason[];
  currentDuty: DutyStatusWire;
}

/** POST /api/drivers/{driverId}/hos/duty-events body (DutyEventRequest).
 *  `eventId` is the client's idempotency key and is also sent as the
 *  Idempotency-Key header; a replay returns 200 with the same body. Source
 *  defaults to Manual (paper backup) server-side when omitted. */
export interface DutyEventInput {
  eventId: string;
  atUtc: string;
  status: DutyStatusWire;
  source?: EventSourceWire;
  enteredBy?: string | null;
  carrierName?: string | null;
  note?: string | null;
  odometerStartKm?: number | null;
  odometerEndKm?: number | null;
  pcNoPassengersOrCargo?: boolean;
  pcNoTrailer?: boolean;
  pcNoWork?: boolean;
}

/** 201 first write / 200 replay (DutyEventRecordedResponse). */
export interface DutyEventRecorded {
  eventId: string;
  dutyDayId: string;
  localDate: string;
  created: boolean;
}

/** One transcribed line: local "HH:mm" on the day, wire duty string. */
export interface TranscriptionEntryInput {
  time: string;
  status: DutyStatusWire;
  note?: string | null;
}

/** POST /api/drivers/{driverId}/hos/days/{date}/transcribe body
 *  (TranscribeDutyDayRequest). Times must be strictly increasing; the write is
 *  all-or-nothing. `transcriptionId` keys the entries' ids so a resend is a no-op. */
export interface TranscribeDayInput {
  transcriptionId: string;
  source: TranscriptionSource;
  enteredBy: string;
  carrierName?: string | null;
  entries: TranscriptionEntryInput[];
}

/** 201 when something was appended / 200 when all already recorded (TranscriptionRecordedResponse). */
export interface TranscriptionRecorded {
  dutyDayId: string;
  localDate: string;
  appended: number;
  alreadyRecorded: number;
}

/** POST /api/drivers/{driverId}/hos/duty-events/{eventId}/amend body
 *  (AmendDutyEventRequest): the replacement event plus the mandatory reason.
 *  The replacement must fall on the same local day as the original. */
export interface AmendDutyEventInput {
  replacementEventId?: string;
  atUtc: string;
  status: DutyStatusWire;
  reason: string;
  source?: EventSourceWire;
  enteredBy?: string | null;
  carrierName?: string | null;
  note?: string | null;
  odometerStartKm?: number | null;
  odometerEndKm?: number | null;
  pcNoPassengersOrCargo?: boolean;
  pcNoTrailer?: boolean;
  pcNoWork?: boolean;
}

/** DutyEventAmendedResponse. */
export interface DutyEventAmended {
  originalEventId: string;
  replacementEventId: string;
  dutyDayId: string;
  localDate: string;
  recertificationRequired: boolean;
  created: boolean;
}

// ---------------------------------------------------------------------------
// Endpoints — all under /api/drivers; errors are { code, message }, and a
// 403 Drivers.NotYourRecord is the own-record check failing.
// ---------------------------------------------------------------------------

/** GET …/hos/days?from&to — defaults server-side to the last 14 days; at most 92. */
export function listDutyDays(driverId: string, range?: { from?: string; to?: string }): Promise<DutyDaySummary[]> {
  const q = new URLSearchParams();
  if (range?.from) q.set("from", range.from);
  if (range?.to) q.set("to", range.to);
  const qs = q.toString();
  return request<DutyDaySummary[]>(`/api/drivers/${driverId}/hos/days${qs ? `?${qs}` : ""}`);
}

export function getDutyDay(driverId: string, date: string): Promise<DutyDaySummary> {
  return request<DutyDaySummary>(`/api/drivers/${driverId}/hos/days/${date}`);
}

export function getHosStatus(driverId: string): Promise<HosStatus> {
  return request<HosStatus>(`/api/drivers/${driverId}/hos/status`);
}

/** The roster batch — one call for every driver's band. */
export function listHosStatusBatch(): Promise<HosRosterStatus[]> {
  return request<HosRosterStatus[]>("/api/drivers/hos/status");
}

/** POST → 201 first time, 200 on replay. The eventId travels in the body AND as Idempotency-Key. */
export function recordDutyEvent(driverId: string, input: DutyEventInput): Promise<DutyEventRecorded> {
  return request<DutyEventRecorded>(`/api/drivers/${driverId}/hos/duty-events`, {
    method: "POST",
    headers: { "Idempotency-Key": input.eventId },
    body: JSON.stringify(input),
  });
}

/** POST → 201 when entries were appended, 200 when the same transcription was already recorded. */
export function transcribeDay(driverId: string, date: string, input: TranscribeDayInput): Promise<TranscriptionRecorded> {
  return request<TranscriptionRecorded>(`/api/drivers/${driverId}/hos/days/${date}/transcribe`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 201 first time, 200 on replay of the same replacementEventId. */
export function amendDutyEvent(driverId: string, eventId: string, input: AmendDutyEventInput): Promise<DutyEventAmended> {
  return request<DutyEventAmended>(`/api/drivers/${driverId}/hos/duty-events/${eventId}/amend`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}
