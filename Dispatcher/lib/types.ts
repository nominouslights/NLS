import type { StatusKind } from "./theme";

// Trip and Driver records now come from the real APIs (lib/api/trips.ts,
// lib/api/drivers.ts) — the old mock interfaces are gone.

export type DutyStatus = "Off Duty" | "On Duty" | "Driving";

// Driver compliance mocks — HOS and leave only. Driver records, credentials,
// and clearances moved to the real Drivers API (lib/api/drivers.ts); HOS/leave
// has no backend domain and stays keyed by the numeric mock `driverId`.

export type LeaveType = "Vacation" | "Sick" | "Leave Without Pay";

export interface DriverLeave {
  id: string;
  driverId: number;
  type: LeaveType;
  startDate: string; // ISO date, inclusive
  endDate: string; // ISO date, inclusive
  hours?: number;
  note?: string;
}

export type HosLogSource = "Driver App" | "Manual (paper backup)";

export interface HosLogEntry {
  id: string;
  driverId: number;
  date: string; // ISO date
  duty: DutyStatus; // status recorded for this span
  onDutyH: number;
  drivingH: number;
  offDutyH: number;
  source: HosLogSource;
  enteredBy?: string; // dispatcher name when a Manual (paper backup) entry
  note?: string;
}

export interface FleetVehicle {
  id: number;
  unit: string;
  model: string;
  seats: number;
  status: string;
  sk: StatusKind;
  dvir: string;
  dk: StatusKind;
  insp: string;
  ik: StatusKind;
  plate: string;
  vin: string;
  licReq: string;
  periodic: boolean;
}

// --- Maintenance & Asset Management previews (mock only — no backend yet) ---

export interface PmReminder {
  unit: string;
  task: string;
  basis: "mileage" | "hours" | "time";
  due: string;
  k: StatusKind;
}

export interface DtcAlert {
  unit: string;
  code: string;
  desc: string;
  severity: string;
  k: StatusKind;
  raised: string;
}

export interface PartItem {
  sku: string;
  name: string;
  onHand: number;
  min: number;
  k: StatusKind;
  loc: string;
}

export interface FuelRouteStat {
  unit: string;
  l100: string;
  idlePct: string;
  routeAdherence: string;
  k: StatusKind;
}

// --- Fleet & Maintenance prototype domain (mock only — no backend yet) ---
// Keyed by `unit` (the vehicle's unitNumber) so per-vehicle tabs slice by it,
// matching the existing pmReminders / dtcAlerts convention.

export type DocumentType =
  | "Registration"
  | "Insurance / MPI"
  | "NSC Safety Certificate"
  | "Emissions"
  | "Bill of Sale"
  | "Warranty"
  | "Other";

export interface VehicleDocument {
  id: string;
  unit: string;
  type: DocumentType;
  fileName: string;
  fileSizeKb: number;
  uploadedBy: string;
  uploadedAt: string; // ISO date
  expiry: string | null; // ISO date, or null when the document has no expiry
  k: StatusKind; // ontime / soon / over — derived from expiry
  note?: string;
}

export type ServiceCategory = "Preventive" | "Repair" | "Inspection Fix" | "Recall";

export interface ServicePart {
  sku: string;
  qty: number;
}

export interface ServiceRecord {
  id: string;
  unit: string;
  date: string; // ISO date
  performedBy: string; // WHO — technician or vendor
  category: ServiceCategory;
  odometerKm: number;
  itemsChanged: string[]; // WHAT was changed
  reason: string; // WHY it was changed
  partsUsed: ServicePart[];
  laborHours?: number;
  costCad?: number;
  workOrderId?: string; // the work order this service closes, if any
  notes?: string;
}

export type WorkOrderSource =
  | "Manual"
  | "Pre-Trip Inspection"
  | "Post-Trip Inspection"
  | "DTC Alert"
  | "PM Reminder";

export type WorkOrderStatus =
  | "Open"
  | "In Progress"
  | "Awaiting Parts"
  | "Completed"
  | "Cancelled";

export type WorkOrderPriority = "Low" | "Medium" | "High" | "Critical";

export interface WorkOrder {
  id: string; // "WO-1042"
  unit: string;
  title: string;
  description: string;
  status: WorkOrderStatus;
  k: StatusKind; // priority-derived chip colour
  priority: WorkOrderPriority;
  source: WorkOrderSource;
  sourceRef?: string; // inspection id / DTC code / PM task
  createdBy: string;
  createdAt: string; // ISO date
  assignedTo?: string;
  dueDate?: string;
  lineItems: string[];
  completedAt?: string;
  resolvingServiceId?: string;
  // NL-WO-01 printable work-order fields (optional)
  shopId?: string;
  authorizedLimitCad?: number;
  budgetCode?: string;
  dateRequiredOrOos?: string;
}

/** Seed values for a new work order, e.g. generated from an inspection's defects. */
export interface WorkOrderPrefill {
  source: WorkOrderSource;
  sourceRef?: string;
  title?: string;
  description?: string;
  lineItems?: string[];
  priority?: WorkOrderPriority;
  inspectionId?: string; // link back once the WO is created
}

/** A shop or partner the dispatcher registers once and reuses on work orders. */
export interface Shop {
  id: string; // "SHOP-01"
  name: string;
  contactName?: string;
  phone?: string;
  email?: string;
  address?: string;
  gstBusinessNo?: string;
  mpiAccredited: boolean; // NL-WO-01 §2
  inspectionStationNo?: string;
  suppliesParts: boolean; // partner flag for parts ordering
  notes?: string;
}

export type InspectionType = "Pre-Trip" | "Post-Trip";
export type InspectionSource = "Driver App" | "Dispatcher Entry (paper backup)";
export type DefectSeverity = "Minor" | "Major" | "Out-of-Service";
export type InspectionResult = "Pass" | "Pass with defects" | "Fail";

export interface InspectionDefect {
  item: string;
  severity: DefectSeverity;
  note?: string;
}

/** One line of the full DVIR checklist — lets the detail view show passes too. */
export interface InspectionChecklistItem {
  item: string;
  status: "Pass" | "Defect";
  severity?: DefectSeverity;
  note?: string;
}

export interface Inspection {
  id: string; // "DVIR-2210"
  unit: string;
  type: InspectionType;
  driver: string;
  performedAt: string; // ISO date
  source: InspectionSource;
  enteredBy?: string; // dispatcher name when a paper-backup entry
  tripId?: string;
  odometerKm: number;
  result: InspectionResult;
  k: StatusKind;
  defects: InspectionDefect[]; // failed items (derived from checklist)
  checklist?: InspectionChecklistItem[]; // full pass/fail record
  generatedWorkOrderId?: string;
}

// Client CRM — contact roster + interaction (touchpoint) log. Prototype/mock
// only (no backend CRM domain yet); the mutable records live in a module
// store (lib/clientStore.ts), mirroring the Fleet & Maintenance store pattern.
// The client roster, contracts, and purchase orders now come from the real
// Clients API (lib/api/clients.ts); these CRM rows are still keyed by the old
// numeric prototype client ids (see the shim in
// components/screens/clients/shared.tsx).
// NOTE: deliberately NO relationship health / happiness / satisfaction field —
// relationship quality is a written business methodology at Northern Link, not
// a tracked signal. Contract renewal chips are contract-expiry status (a
// date-derived signal), not a relationship sentiment.

export interface ClientContact {
  id: string;
  clientId: number;
  name: string;
  title: string; // free-text / tagged role, e.g. "Operations", "AP", "Compliance"
  email?: string;
  phone?: string;
  notes?: string;
  primary: boolean; // exactly one primary per client, enforced by the store
}

export type InteractionType = "Call" | "Meeting" | "Email" | "Site Visit" | "Other";

export interface ClientInteraction {
  id: string;
  clientId: number;
  date: string; // ISO date the touchpoint happened
  type: InteractionType;
  summary: string;
  participantContactIds: string[]; // many-to-many → ClientContact.id
  followUpDate?: string; // ISO date a follow-up is due
  followUpNote?: string;
}

// Invoice records now come from the real Billing API (lib/api/billing.ts).

export interface Rider {
  id: number;
  name: string;
  home: string;
  prog: "NIHB" | "Community";
  last: string;
  phone: string;
  pc: string;
  voucher: string;
  escort: string;
  noshow: string;
  needs: string;
}

export interface Incident {
  id: string;
  sev: string;
  sk: StatusKind;
  status: string;
  date: string;
  driver: string;
  vehicle: string;
  trip: string;
  summary: string;
}
