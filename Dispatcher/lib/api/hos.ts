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
  /** The exceptions in force on the day — the ones the engine honoured (Declared, Acknowledged or Approved). */
  exceptions: HosExceptionRecord[];
  /** An out-of-service declaration overlapping the day, or null. */
  outOfService: OutOfServiceRecord | null;
  label: string;
}

/** The out-of-service declaration in force (status) or overlapping a day (summaries) — OutOfServiceResponse. */
export interface OutOfServiceRecord {
  id: string;
  fromUtc: string;
  untilUtc: string | null;
  reason: string;
}

/** The compliance officer's prior-records attestation (PriorRecordsResponse, s.82(1)(f)). */
export interface PriorRecordsRecord {
  coveredThroughDate: string; // DateOnly
  attestedAtUtc: string;
  attestedByUserId: string;
  note: string | null;
  worksForOtherCarriers: boolean;
}

/** A driver's standing right now (HosStatusResponse). `evaluation` is the
 *  engine's HosEvaluation verbatim. recordsOnFile=false means the ledger does
 *  not cover the regulation's 14-day window (not dispatchable). `outOfService`
 *  is the declaration in force (null when none); `eldMalfunctionOpen` is
 *  paper-log mode (a warning, never a block); `priorRecords` is null until a
 *  compliance officer attests. */
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

// ---------------------------------------------------------------------------
// Compliance records (ComplianceResponses.cs) — exceptions, out-of-service,
// ELD malfunctions, findings, the audit. Status strings are enum member names.
// ---------------------------------------------------------------------------

/** The kind strings the API emits and accepts (HosWire exception kinds). */
export type HosExceptionKindWire = "Adverse driving conditions" | "Emergency" | "Off-duty deferral (CO approved)";

/** The two kinds a driver (or dispatch for them) may declare; a deferral goes through the compliance route. */
export type DeclarableExceptionKind = "Adverse driving conditions" | "Emergency";

export type HosExceptionStatus = "Declared" | "Acknowledged" | "Approved" | "Rejected" | "Withdrawn";

/** An hours-of-service exception as the API shows it (HosExceptionResponse).
 *  `applying` is true while the rule engine honours it; `extraDrivingMinutes`
 *  is set for adverse conditions only, `deferredOffDutyMinutes` for a deferral only. */
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

/** POST …/hos/exceptions body (DeclareHosExceptionRequest). For adverse
 *  conditions `notKnownBeforeDeparture` must be true and `extraDrivingMinutes`
 *  is required (1–120). `effectiveFromUtc` is within the last 24 hours. */
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

/** POST …/hos/exceptions/deferral body (ApproveDeferralRequest) — compliance
 *  roles only. `tripId` is required; `effectiveFromUtc` must be in the future
 *  and within seven days; `deferredOffDutyMinutes` is 1–120. */
export interface ApproveDeferralInput {
  tripId: string;
  effectiveFromUtc: string;
  deferredOffDutyMinutes: number;
  reason: string;
  location?: string | null;
  eldRemarkRecorded?: boolean;
}

export type OutOfServiceStatus = "Active" | "Lifted";

/** An out-of-service declaration as the API shows it (OutOfServiceRecordResponse).
 *  `active` is whether it is in force right now; `effectiveUntilUtc` is the lift
 *  instant once lifted, else `untilUtc` (null = until lifted). */
export interface OutOfServiceDeclaration {
  id: string;
  driverId: string;
  status: OutOfServiceStatus;
  active: boolean;
  declaredAtUtc: string;
  recordedAtUtc: string;
  recordedByUserId: string;
  untilUtc: string | null;
  effectiveUntilUtc: string | null;
  reason: string;
  evidence: string | null;
  liftedAtUtc: string | null;
  liftedByUserId: string | null;
  liftReason: string | null;
}

/** POST …/out-of-service body (RecordOutOfServiceRequest). `untilUtc` omitted
 *  means 72 hours after `declaredAtUtc`; `openEnded` true means until lifted. */
export interface RecordOutOfServiceInput {
  declaredAtUtc: string;
  reason: string;
  untilUtc?: string | null;
  openEnded?: boolean;
  evidence?: string | null;
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

/** POST …/hos/eld-malfunctions body (ReportEldMalfunctionRequest). `code` is what the device displayed (≤ 32 chars). */
export interface ReportEldMalfunctionInput {
  code: string;
  noticedAtUtc: string;
  description: string;
  vehicleId?: string | null;
}

export type FindingSeverity = "Minor" | "Major" | "Severe";
export type FindingStatus = "Open" | "Corrected" | "Closed";
export type FindingSource = "Audit" | "Enforcement" | "Auto";

/** A compliance finding as the API shows it (ComplianceFindingResponse). `rule`
 *  is an HosRule name or Falsification / Tampering / TwoRecords / OutOfServiceDeclared. */
export interface ComplianceFindingRecord {
  id: string;
  driverId: string;
  localDate: string;
  rule: string;
  severity: FindingSeverity;
  status: FindingStatus;
  source: FindingSource;
  description: string;
  followUpNote: string | null;
  openedAtUtc: string;
  openedByUserId: string;
  closedAtUtc: string | null;
  closedByUserId: string | null;
}

/** POST …/findings body (OpenFindingRequest). `source` defaults to Audit server-side. */
export interface OpenFindingInput {
  localDate: string;
  rule: string;
  severity: FindingSeverity;
  description: string;
  source?: FindingSource | null;
  followUpNote?: string | null;
}

/** PUT …/findings/{id} body (UpdateFindingRequest): Open → Corrected → Closed (or Open → Closed), never backwards. */
export interface UpdateFindingInput {
  status: FindingStatus;
  followUpNote?: string | null;
}

/** POST …/prior-records-attestation body (AttestPriorRecordsRequest). */
export interface AttestPriorRecordsInput {
  coveredThroughDate: string;
  note?: string | null;
  worksForOtherCarriers?: boolean;
}

/** Other-carrier work on an audit day: one row per carrier name (HosAuditOtherCarrierRow). */
export interface HosAuditOtherCarrierRow {
  carrierName: string;
  eventCount: number;
}

/** One America/Winnipeg day of the compliance audit (HosAuditDayResponse). */
export interface HosAuditDay {
  driverId: string;
  date: string;
  hasRecords: boolean;
  eventCount: number;
  sources: EventSourceWire[];
  certified: boolean;
  certifiedAtUtc: string | null;
  recertificationRequired: boolean;
  totals: DayTotals;
  personalConveyanceKm: number;
  violations: HosViolation[];
  exceptions: HosExceptionRecord[];
  outOfService: OutOfServiceDeclaration | null;
  eldMalfunctionOpen: boolean;
  otherCarrier: HosAuditOtherCarrierRow[];
  priorRecordsAttested: boolean;
  findings: ComplianceFindingRecord[];
  label: string;
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

/** HosAlertKind member names, in the order the server lists them (most urgent first). */
export type HosAlertKind = "OutOfService" | "OverLimit" | "NearLimit" | "EldMalfunction" | "ExceptionDeclared" | "Uncertified";

/** One row of the today alerts (HosAlertItemResponse). `message` is the server's sentence, shown verbatim. */
export interface HosAlertItem {
  driverId: string;
  driverName: string;
  kind: HosAlertKind;
  message: string;
}

/** GET /api/drivers/hos/alerts/today (HosAlertsResponse). The six counts summarise `items`:
 *  one per driver for the bands and out-of-service, one per open malfunction, uncertified
 *  yesterday and declared exception. Deactivated drivers are excluded server-side. */
export interface HosAlertsToday {
  asOfUtc: string;
  driversNearLimit: number;
  driversOverLimit: number;
  outOfService: number;
  openMalfunctions: number;
  uncertifiedYesterday: number;
  unassignedExceptions: number;
  items: HosAlertItem[];
  label: string;
}

/** The Dispatch Board's compliance tile and the Compliance screen's today list — DriverRosterAccess. */
export function getHosAlertsToday(): Promise<HosAlertsToday> {
  return request<HosAlertsToday>("/api/drivers/hos/alerts/today");
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

// ---------------------------------------------------------------------------
// Exceptions and ELD malfunctions — the HosAccess group (own-record check;
// dispatch and compliance roles may act on any driver).
// ---------------------------------------------------------------------------

function dateRangeQuery(range?: { from?: string; to?: string }): string {
  const q = new URLSearchParams();
  if (range?.from) q.set("from", range.from);
  if (range?.to) q.set("to", range.to);
  const qs = q.toString();
  return qs ? `?${qs}` : "";
}

/** GET …/hos/exceptions?from&to — defaults server-side to the 14-day window ending today. */
export function listHosExceptions(driverId: string, range?: { from?: string; to?: string }): Promise<HosExceptionRecord[]> {
  return request<HosExceptionRecord[]>(`/api/drivers/${driverId}/hos/exceptions${dateRangeQuery(range)}`);
}

/** POST → 201 with the new exception (Declared). */
export function declareHosException(driverId: string, input: DeclareExceptionInput): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST …/exceptions/{id}/acknowledge — dispatch's act; no body. */
export function acknowledgeHosException(driverId: string, exceptionId: string): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions/${exceptionId}/acknowledge`, { method: "POST" });
}

/** POST …/exceptions/{id}/withdraw {note?} — withdrawing an acknowledged one is compliance's act (the handler draws that line). */
export function withdrawHosException(driverId: string, exceptionId: string, note?: string | null): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions/${exceptionId}/withdraw`, {
    method: "POST",
    body: JSON.stringify({ note: note ?? null }),
  });
}

export function listEldMalfunctions(driverId: string): Promise<EldMalfunctionRecord[]> {
  return request<EldMalfunctionRecord[]>(`/api/drivers/${driverId}/hos/eld-malfunctions`);
}

/** POST → 201 with the new report (Open). */
export function reportEldMalfunction(driverId: string, input: ReportEldMalfunctionInput): Promise<EldMalfunctionRecord> {
  return request<EldMalfunctionRecord>(`/api/drivers/${driverId}/hos/eld-malfunctions`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

// ---------------------------------------------------------------------------
// The ComplianceAccess group (Owner, ComplianceOfficer) — any other role gets
// the server's 403; the client gate in lib/roles.ts is UX only.
// ---------------------------------------------------------------------------

/** POST …/hos/exceptions/deferral → 201 with the Approved deferral. */
export function approveOffDutyDeferral(driverId: string, input: ApproveDeferralInput): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions/deferral`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST …/exceptions/{id}/reject {note?}. */
export function rejectHosException(driverId: string, exceptionId: string, note?: string | null): Promise<HosExceptionRecord> {
  return request<HosExceptionRecord>(`/api/drivers/${driverId}/hos/exceptions/${exceptionId}/reject`, {
    method: "POST",
    body: JSON.stringify({ note: note ?? null }),
  });
}

/** POST …/eld-malfunctions/{id}/resolve {note?}. */
export function resolveEldMalfunction(driverId: string, reportId: string, note?: string | null): Promise<EldMalfunctionRecord> {
  return request<EldMalfunctionRecord>(`/api/drivers/${driverId}/hos/eld-malfunctions/${reportId}/resolve`, {
    method: "POST",
    body: JSON.stringify({ note: note ?? null }),
  });
}

export function listOutOfService(driverId: string): Promise<OutOfServiceDeclaration[]> {
  return request<OutOfServiceDeclaration[]>(`/api/drivers/${driverId}/out-of-service`);
}

/** POST → 201; a 409 Drivers.Hos.AlreadyOutOfService when one is already active. */
export function recordOutOfService(driverId: string, input: RecordOutOfServiceInput): Promise<OutOfServiceDeclaration> {
  return request<OutOfServiceDeclaration>(`/api/drivers/${driverId}/out-of-service`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST …/out-of-service/{id}/lift {reason?} — the reason is required for an early lift. */
export function liftOutOfService(driverId: string, recordId: string, reason?: string | null): Promise<OutOfServiceDeclaration> {
  return request<OutOfServiceDeclaration>(`/api/drivers/${driverId}/out-of-service/${recordId}/lift`, {
    method: "POST",
    body: JSON.stringify({ reason: reason ?? null }),
  });
}

/** GET …/findings?from&to. */
export function listComplianceFindings(driverId: string, range?: { from?: string; to?: string }): Promise<ComplianceFindingRecord[]> {
  return request<ComplianceFindingRecord[]>(`/api/drivers/${driverId}/findings${dateRangeQuery(range)}`);
}

/** POST → 201 with the new finding (Open). */
export function openComplianceFinding(driverId: string, input: OpenFindingInput): Promise<ComplianceFindingRecord> {
  return request<ComplianceFindingRecord>(`/api/drivers/${driverId}/findings`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** PUT …/findings/{id}. */
export function updateComplianceFinding(driverId: string, findingId: string, input: UpdateFindingInput): Promise<ComplianceFindingRecord> {
  return request<ComplianceFindingRecord>(`/api/drivers/${driverId}/findings/${findingId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** POST …/prior-records-attestation → 200 with the attestation. */
export function attestPriorRecords(driverId: string, input: AttestPriorRecordsInput): Promise<PriorRecordsRecord> {
  return request<PriorRecordsRecord>(`/api/drivers/${driverId}/prior-records-attestation`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** GET …/hos/audit?from&to — defaults server-side to the 14-day window ending today. */
export function getHosAudit(driverId: string, range?: { from?: string; to?: string }): Promise<HosAuditDay[]> {
  return request<HosAuditDay[]>(`/api/drivers/${driverId}/hos/audit${dateRangeQuery(range)}`);
}
