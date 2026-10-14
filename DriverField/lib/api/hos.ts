import { request } from "./transport";
import type { DayTotals, HosEvaluation, HosViolation } from "../hos/engine";

// ---------------------------------------------------------------------------
// Hours-of-service API client — contract owned by Backend/ (Drivers module,
// HosEndpoints.cs + HosResponses.cs + DriversEndpoints.cs for `/me`). Shapes
// mirror DutyDaySummaryResponse / DutyEventResponse / HosStatusResponse /
// DriverResponse exactly (JSON camelCase; enums as member names; the engine's
// own records nested verbatim so the lib/hos/engine.ts types apply unchanged).
// Do not invent fields — extend only when the backend contract changes.
//
// APP-LOCAL FOR NOW. Dispatcher/lib/api/hos.ts holds the console's client with
// the same type and function names (listDutyDays / getHosStatus /
// recordDutyEvent and their shapes are identical); this file keeps those names
// so it can become a header-copied file once that one settles. Two members are
// this app's alone and are not in the console's: getMyDriver (the Field App's
// first call) and certifyDay (the driver's own act). The compliance members
// (listHosExceptions / declareHosException / withdrawHosException /
// listEldMalfunctions / reportEldMalfunction and their records, mirroring
// ComplianceResponses.cs) are written to the same contract so the console's
// can take the same names. Imports only ./transport and TYPES from
// ../hos/engine.
//
// Every route is under the HosAccess policy, which admits every Driver, so the
// server runs the caller-owns-this-row check on each {driverId} route and
// answers 403 Drivers.NotYourRecord otherwise. Nothing here assumes the id it
// sends is trusted. Errors are { code, message }.
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

/** GET /api/drivers/me (DriverResponse) — the caller's own roster row, resolved from the token's sub. */
export interface DriverRecord {
  id: string;
  userId: string | null;
  name: string;
  phone: string | null;
  licenceClass: string;
  licenceExpiry: string | null; // DateOnly
  source: string;
  hasWorkPermit: boolean;
  status: string;
  credentialCount: number;
  soonestCredentialExpiry: string | null; // DateOnly
  latestDutyStatus: string | null;
  latestDrivingHours: number | null;
  latestHosDate: string | null; // DateOnly
  registeredAtUtc: string;
  updatedAtUtc: string;
}

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
  /** The exceptions in force on the day — the ones the engine honoured (Declared, Acknowledged or Approved). */
  exceptions: HosExceptionRecord[];
  /** An out-of-service declaration overlapping the day, or null. */
  outOfService: OutOfServiceRecord | null;
  label: string;
}

/** The exception kind strings the API emits and accepts (HosWire.ExceptionKind). */
export type HosExceptionKindWire = "Adverse driving conditions" | "Emergency" | "Off-duty deferral (CO approved)";

/** The two kinds a driver (or dispatch for them) may declare; a deferral goes through the compliance route. */
export type DeclarableExceptionKind = "Adverse driving conditions" | "Emergency";

/** HosExceptionStatus member names, as the API spells them. */
export type HosExceptionStatus = "Declared" | "Acknowledged" | "Approved" | "Rejected" | "Withdrawn";

/** An hours-of-service exception as the API shows it (HosExceptionResponse).
 *  `applying` is true while the rule engine honours it (Declared, Acknowledged
 *  or Approved). `extraDrivingMinutes` is set for adverse conditions only,
 *  `deferredOffDutyMinutes` for a deferral only. */
export interface HosExceptionRecord {
  id: string;
  driverId: string;
  kind: HosExceptionKindWire;
  status: HosExceptionStatus;
  applying: boolean;
  tripId: string | null;
  declaredAtUtc: string;
  declaredByUserId: string;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
  reason: string;
  location: string | null;
  notKnownBeforeDeparture: boolean;
  extraDrivingMinutes: number | null;
  deferredOffDutyMinutes: number | null;
  eldRemarkRecorded: boolean;
  dispatchAcknowledgedAtUtc: string | null;
  dispatchAcknowledgedByUserId: string | null;
  approvedAtUtc: string | null;
  approvedByUserId: string | null;
  closedAtUtc: string | null;
  closedByUserId: string | null;
  closureNote: string | null;
}

/** The out-of-service declaration in force (status) or overlapping a day
 *  (summaries): its id, span and reason (OutOfServiceResponse). untilUtc null
 *  = until lifted. */
export interface OutOfServiceRecord {
  id: string;
  fromUtc: string;
  untilUtc: string | null;
  reason: string;
}

export type EldMalfunctionStatus = "Open" | "Resolved";

/** An ELD malfunction report as the API shows it (EldMalfunctionResponse). */
export interface EldMalfunctionRecord {
  id: string;
  driverId: string;
  vehicleId: string | null;
  code: string;
  status: EldMalfunctionStatus;
  noticedAtUtc: string;
  reportedAtUtc: string;
  reportedByUserId: string;
  description: string;
  resolvedAtUtc: string | null;
  resolvedByUserId: string | null;
  resolutionNote: string | null;
}

/** The driver's prior-records attestation (PriorRecordsResponse, s.82(1)(f));
 *  null on a status until a compliance officer records one. */
export interface PriorRecordsRecord {
  coveredThroughDate: string; // DateOnly
  attestedAtUtc: string;
  attestedByUserId: string;
  note: string | null;
  worksForOtherCarriers: boolean;
}

/** A driver's standing right now (HosStatusResponse). `evaluation` is the
 *  engine's HosEvaluation verbatim. recordsOnFile=false means the ledger does
 *  not cover the regulation's 14-day window (not dispatchable).
 *  `outOfService` is the declaration in force right now; `eldMalfunctionOpen`
 *  is true while any ELD malfunction report is open (paper-log mode — a
 *  warning, never a block); `priorRecords` is the compliance officer's
 *  attestation, null until one exists. */
export interface HosStatus {
  driverId: string;
  label: string;
  currentDuty: DutyStatusWire;
  currentDutySinceUtc: string;
  recordsOnFile: boolean;
  knownFromUtc: string | null;
  outOfService: OutOfServiceRecord | null;
  eldMalfunctionOpen: boolean;
  priorRecords: PriorRecordsRecord | null;
  evaluation: HosEvaluation;
}

/** POST /api/drivers/{driverId}/hos/duty-events body (DutyEventRequest).
 *  `eventId` is the client's idempotency key and is also sent as the
 *  Idempotency-Key header; a replay returns 200 with the same body. Source
 *  defaults to Manual (paper backup) server-side when omitted — this app
 *  ALWAYS sends "Driver App" (lib/hos/dutyCommand.ts). */
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

/** POST /api/drivers/{driverId}/hos/exceptions body (DeclareHosExceptionRequest).
 *  `kind` is "Adverse driving conditions" or "Emergency" — a deferral goes
 *  through the compliance route. `effectiveFromUtc` is when the conditions
 *  arose (within the last 24 hours, not more than five minutes ahead). For
 *  adverse conditions `notKnownBeforeDeparture` must be true and
 *  `extraDrivingMinutes` is required (1–120). */
export interface DeclareExceptionInput {
  kind: DeclarableExceptionKind;
  effectiveFromUtc: string;
  reason: string;
  effectiveToUtc?: string | null;
  location?: string | null;
  tripId?: string | null;
  notKnownBeforeDeparture?: boolean;
  extraDrivingMinutes?: number | null;
  eldRemarkRecorded?: boolean;
}

/** POST /api/drivers/{driverId}/hos/eld-malfunctions body (ReportEldMalfunctionRequest). `code` is what the device displayed (≤ 32 chars). */
export interface ReportEldMalfunctionInput {
  code: string;
  noticedAtUtc: string;
  description: string;
  vehicleId?: string | null;
}

/** Per-request options shared by the compliance writes. The exception and
 *  malfunction routes do not honour `Idempotency-Key` yet; the client sends it
 *  from day one so each can start honouring it with no client change. */
export interface WriteOptions {
  idempotencyKey?: string;
}

function idempotencyHeaders(options?: WriteOptions): Record<string, string> | undefined {
  return options?.idempotencyKey ? { "Idempotency-Key": options.idempotencyKey } : undefined;
}

// ---------------------------------------------------------------------------
// Endpoints — all under /api/drivers; errors are { code, message }, and a
// 403 Drivers.NotYourRecord is the own-record check failing.
// ---------------------------------------------------------------------------

/** GET /api/drivers/me — 404 Drivers.NotLinked when this account has no roster row. */
export function getMyDriver(): Promise<DriverRecord> {
  return request<DriverRecord>("/api/drivers/me");
}

/** GET …/hos/days?from&to — defaults server-side to the last 14 days; at most 92. */
export function listDutyDays(driverId: string, range?: { from?: string; to?: string }): Promise<DutyDaySummary[]> {
  const q = new URLSearchParams();
  if (range?.from) q.set("from", range.from);
  if (range?.to) q.set("to", range.to);
  const qs = q.toString();
  return request<DutyDaySummary[]>(`/api/drivers/${driverId}/hos/days${qs ? `?${qs}` : ""}`);
}

export function getHosStatus(driverId: string): Promise<HosStatus> {
  return request<HosStatus>(`/api/drivers/${driverId}/hos/status`);
}

/** POST → 201 first time, 200 on replay. The eventId travels in the body AND as Idempotency-Key. */
export function recordDutyEvent(driverId: string, input: DutyEventInput): Promise<DutyEventRecorded> {
  return request<DutyEventRecorded>(`/api/drivers/${driverId}/hos/duty-events`, {
    method: "POST",
    headers: { "Idempotency-Key": input.eventId },
    body: JSON.stringify(input),
  });
}

/** POST …/hos/days/{date}/certify — no body, driver only, 204. Re-certifying an
 *  already-certified day is a server no-op (also 204), so a replay is safe. 400
 *  Drivers.Hos.PcSegmentOpen | DayInFuture | NothingToCertify; 404 DayNotFound;
 *  403 Drivers.Hos.CertifyDriverOnly for anyone but the driver. */
export function certifyDay(driverId: string, date: string): Promise<void> {
  return request<void>(`/api/drivers/${driverId}/hos/days/${date}/certify`, { method: "POST" });
}

/** GET …/hos/exceptions?from&to — every status, defaults server-side to the 14-day window ending today. */
export function listHosExceptions(driverId: string, range?: { from?: string; to?: string }): Promise<HosExceptionRecord[]> {
  const q = new URLSearchParams();
  if (range?.from) q.set("from", range.from);
  if (range?.to) q.set("to", range.to);
  const qs = q.toString();
  return request<HosExceptionRecord[]>(`/api/drivers/${driverId}/hos/exceptions${qs ? `?${qs}` : ""}`);
}

/** POST …/hos/exceptions → 201 with the record. 400 Drivers.Hos.ExceptionKindInvalid |
 *  ExceptionReasonRequired | ExceptionBackdated | ExceptionInFuture |
 *  AdverseKnowabilityRequired | AdverseExtensionRequired | AdverseExtensionTooLong;
 *  403 Drivers.Hos.DeferralRequiresComplianceOfficer for a deferral. */
export function declareHosException(driverId: string, input: DeclareExceptionInput, options?: WriteOptions): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions`, {
    method: "POST",
    headers: idempotencyHeaders(options),
    body: JSON.stringify(input),
  });
}

/** POST …/hos/exceptions/{id}/withdraw → 200 with the record. The driver may
 *  withdraw only while it is Declared (403
 *  Drivers.Hos.ExceptionWithdrawRequiresComplianceOfficer otherwise); a
 *  withdrawn one is a no-op, a rejected one 409 Drivers.Hos.ExceptionClosed. */
export function withdrawHosException(driverId: string, exceptionId: string, note?: string | null, options?: WriteOptions): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions/${exceptionId}/withdraw`, {
    method: "POST",
    headers: idempotencyHeaders(options),
    body: JSON.stringify({ note: note ?? null }),
  });
}

/** GET …/hos/eld-malfunctions — every report for the driver, open and resolved. */
export function listEldMalfunctions(driverId: string): Promise<EldMalfunctionRecord[]> {
  return request<EldMalfunctionRecord[]>(`/api/drivers/${driverId}/hos/eld-malfunctions`);
}

/** POST …/hos/eld-malfunctions → 201 with the record. 400 Drivers.Hos.EldCodeRequired |
 *  EldCodeTooLong | EldDescriptionRequired | EldNoticedInFuture. */
export function reportEldMalfunction(driverId: string, input: ReportEldMalfunctionInput, options?: WriteOptions): Promise<EldMalfunctionRecord> {
  return request<EldMalfunctionRecord>(`/api/drivers/${driverId}/hos/eld-malfunctions`, {
    method: "POST",
    headers: idempotencyHeaders(options),
    body: JSON.stringify(input),
  });
}
