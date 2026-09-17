// Row shapes for the mock layer. Kept out of lib/data.ts so the data file stays readable as
// data, following Budgeting/lib/types.ts.
//
// These are NOT API contracts. The backend owns endpoint shapes; nothing here has been
// promised by a real endpoint, and when a screen goes real its rows come from a
// lib/api/<domain>.ts written against the actual response. See DriverField/CLAUDE.md.

import type { StatusKind } from "./theme";

export type DutyState = "Off Duty" | "On Duty" | "Driving";

/** Matches HosLogEntrySource's wire strings — see lib/wire.test.ts. */
export type HosSource = "Driver App" | "Manual (paper backup)";

export type TripMode = "Open" | "Assigned";

/**
 * DISPLAY strings, not wire values. InspectionDefectSeverity in
 * Backend/src/Fleet/Domain/Inspections/InspectionDefectSeverity.cs is
 * `Minor | Major | OutOfService` — no spaces. Cross the boundary through
 * `severityToWire()` in lib/inspectionGate.ts, never by sending one of these; the default
 * System.Text.Json enum converter would reject "Out of Service". Pinned in lib/wire.test.ts.
 */
export type DefectSeverity = "Minor" | "Major" | "Out of Service";

export type CheckState = "pass" | "defect" | "na";

/**
 * Wire-shaped, matching InspectionType in
 * Backend/src/Fleet/Domain/Inspections/InspectionType.cs (`PreTrip | PostTrip`). Distinct from
 * DvirSubmission.type, which is display prose ("Pre-Trip"). Pinned in lib/wire.test.ts.
 */
export type InspectionMode = "PreTrip" | "PostTrip";

export interface DriverProfile {
  id: string;
  name: string;
  /** Manitoba licence class. Rule 2 of the eligibility engine compares against the vehicle. */
  licenceClass: string;
  licenceExpiresOn: string;
  phone: string;
  homeBase: string;
  employeeNumber: string;
}

export interface Credential {
  id: string;
  kind: string;
  reference: string;
  expiresOn: string;
  /** Derived by credentialKind() — never chosen at the render site. */
  ck: StatusKind;
}

export interface Clearance {
  id: string;
  client: string;
  kind: string;
  grantedOn: string;
  expiresOn: string;
  ck: StatusKind;
}

export interface Vehicle {
  id: string;
  unit: string;
  description: string;
  seats: number;
  odometerKm: number;
  status: string;
  vk: StatusKind;
  /**
   * Encoded per vehicle, never hardcoded to a class. A 7-seat van and a 24-seat bus are both
   * Class 4 under Manitoba's schedule today, but a future larger vehicle requires Class 2 and
   * the rule must not have to be rewritten to notice.
   */
  licenceClassRequired: string;
  /** Set when an inspection failed and the defect is still open — eligibility rule 3. */
  hasFailedDvir: boolean;
}

export interface VehicleDefect {
  id: string;
  vehicleId: string;
  reportedOn: string;
  item: string;
  severity: DefectSeverity;
  note: string;
  dk: StatusKind;
  workOrder: string | null;
}

export interface Trip {
  id: string;
  tripNumber: string;
  mode: TripMode;
  serviceType: string;
  clientName: string;
  origin: string;
  destination: string;
  startsAt: string;
  endsAt: string;
  estimatedHours: number;
  vehicleId: string;
  requiredLicenceClass: string;
  /** Client clearance this trip needs, or null. "Alamos" is the live case. */
  requiresClearance: string | null;
  seats: number;
  status: string;
  tk: StatusKind;
}

export interface ManifestRow {
  id: string;
  tripId: string;
  passenger: string;
  employer: string;
  badgeId: string;
  pickup: string;
  dropoff: string;
  boarded: boolean;
  noShow: boolean;
  bk: StatusKind;
}

export interface CrewBadge {
  id: string;
  badgeId: string;
  passenger: string;
}

export interface HosEntry {
  id: string;
  date: string;
  duty: DutyState;
  onDutyH: number;
  drivingH: number;
  offDutyH: number;
  source: HosSource;
  enteredBy: string | null;
  note: string;
}

/**
 * `id` is what answers, drafts and the resume pointer key on; `label` is both what the driver
 * reads and the `Item` string ChecklistItemInput carries. Separate deliberately: the old code
 * keyed answers by the display string, so two groups sharing an item name collided and the
 * group was lost from the payload even though ChecklistItemInput has a Group field.
 */
export interface ChecklistItem {
  id: string;
  label: string;
}

export interface ChecklistGroup {
  group: string;
  items: ChecklistItem[];
}

export interface DvirSubmission {
  id: string;
  performedAt: string;
  /** Display prose — "Pre-Trip" / "Post-Trip". Read `mode` for anything that branches. */
  type: string;
  /**
   * Wire-shaped, matching InspectionType. Distinct from `type`, which is display prose. The
   * boarding gate branches on this, and prose is not a contract.
   */
  mode: InspectionMode;
  /** Join key for the gate — `unit` is free text and cannot be matched against a Vehicle row. */
  vehicleId: string;
  unit: string;
  odometerKm: number;
  result: string;
  rk: StatusKind;
  defectCount: number;
}

export interface Incident {
  id: string;
  reportedAt: string;
  type: string;
  severity: string;
  location: string;
  narrative: string;
  status: string;
  ik: StatusKind;
}

export interface FuelEntry {
  id: string;
  filledAt: string;
  unit: string;
  litres: number;
  costCad: number;
  odometerKm: number;
  location: string;
}

export interface ClientContract {
  id: string;
  client: string;
  manifestTemplate: string;
  requiresClearance: boolean;
  note: string;
}

/** One rule of the §5.4 eligibility engine, with the reason a driver actually reads. */
export interface EligibilityRule {
  rule: string;
  pass: boolean;
  reason: string;
}

export interface EligibilityVerdict {
  eligible: boolean;
  rules: EligibilityRule[];
}

export interface HosRemaining {
  drivingRemainingH: number;
  onDutyRemainingH: number;
  cycleRemainingH: number;
  hk: StatusKind;
}
