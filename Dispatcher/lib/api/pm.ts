import { request } from "./transport";

// ---------------------------------------------------------------------------
// Preventative-maintenance contract (Backend Fleet module — FleetEndpoints.Pm).
// Shapes mirror the backend responses exactly (JSON camelCase, enums as their
// backend names, DateOnly as "yyyy-MM-dd", timestamps as UTC ISO):
//   MaintenancePlanSummaryResponse / MaintenancePlanResponse (+ item/overhaul)
//   VehiclePmStatusResponse / PmEntryStatusResponse
//   PmDueResponse / FleetPmDueResponse
//   PmOverhaulsResponse / OverhaulStatusResponse / RelatedMeasurementResponse
//   PmCompletionResponse
// Do not invent fields — extend only when the backend contract changes.
// Wire→display mapping (chip kinds, labels, formatters) lives in
// lib/pmDisplay.ts, mirroring the workOrderDisplay.ts pattern.
// ---------------------------------------------------------------------------

/** PmEntryKind enum names. */
export type PmEntryKindWire = "Item" | "Overhaul";
/** PmDueState enum names — the computed due status of one plan line on one vehicle. */
export type PmDueStateWire = "NotYetRecorded" | "Ok" | "DueSoon" | "Overdue";
/** ComponentTier enum names. */
export type PmTierWire = "Primary" | "Secondary";
/** MaintenanceTask enum names. */
export type PmTaskWire = "Inspect" | "Service" | "Test" | "Replace";

/** One row of the plan list (MaintenancePlanSummaryResponse). */
export interface PmPlanSummaryWire {
  id: string;
  name: string;
  vehicleModel: string;
  serviceClass: string;
  notes: string | null;
  itemCount: number;
  overhaulCount: number;
  assignedVehicleCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** One routine maintenance line of a plan (MaintenanceItemResponse). Null leads = plan-wide defaults. */
export interface PmPlanItemWire {
  code: string;
  system: string;
  component: string;
  tier: PmTierWire;
  task: PmTaskWire;
  intervalKm: number | null;
  intervalMonths: number | null;
  shopMinutes: number;
  leadKm: number | null;
  leadDays: number | null;
  notes: string | null;
}

/** One major-component overhaul of a plan (OverhaulSpecResponse). No shopMinutes —
 *  overhaul effort travels as labourHours; only computed PM entries carry minutes. */
export interface PmPlanOverhaulWire {
  code: string;
  component: string;
  intervalKm: number | null;
  intervalMonths: number | null;
  labourHours: number;
  partsCad: number;
  leadKm: number | null;
  leadDays: number | null;
  scope: string;
  conditionTriggers: string[];
  relatedItemCodes: string[];
}

/** Full plan incl. every item and overhaul (MaintenancePlanResponse). */
export interface PmPlanWire {
  id: string;
  name: string;
  vehicleModel: string;
  serviceClass: string;
  notes: string | null;
  items: PmPlanItemWire[];
  overhauls: PmPlanOverhaulWire[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * One plan line (item or overhaul) with its computed due status on one vehicle
 * (PmEntryStatusResponse). Overhaul entries carry system "Overhauls", no
 * tier/task, and their labour hours converted to shopMinutes. Last-done and
 * due fields are null when never recorded or when that interval arm does not
 * apply; dates are "yyyy-MM-dd".
 */
export interface PmEntryStatusWire {
  code: string;
  kind: PmEntryKindWire;
  system: string;
  component: string;
  tier: PmTierWire | null;
  task: PmTaskWire | null;
  intervalKm: number | null;
  intervalMonths: number | null;
  leadKm: number | null;
  leadDays: number | null;
  shopMinutes: number;
  lastDoneKm: number | null;
  lastDoneDate: string | null;
  nextDueKm: number | null;
  nextDueDate: string | null;
  kmRemaining: number | null;
  daysRemaining: number | null;
  state: PmDueStateWire;
}

/** The synthetic system heading overhaul entries group under (contract constant). */
export const PM_OVERHAULS_SYSTEM = "Overhauls";

/** Full computed PM schedule of one vehicle (VehiclePmStatusResponse).
 *  assigned=false means no plan: plan fields null, entries empty (200, never
 *  404) — but currentOdometerKm still carries the vehicle's real reading. */
export interface VehiclePmStatusWire {
  assigned: boolean;
  planId: string | null;
  planName: string | null;
  assignedAtUtc: string | null;
  currentOdometerKm: number | null;
  entries: PmEntryStatusWire[];
}

/** The due entries of one system (PmDueGroupResponse). */
export interface PmDueGroupWire {
  system: string;
  entries: PmEntryStatusWire[];
}

/** Shop-visit package for one vehicle (PmDueResponse): everything DueSoon or
 *  Overdue grouped by system; totalShopMinutes sums the due entries' minutes;
 *  notYetRecorded lists codes never logged (no due math possible). */
export interface VehiclePmDueWire {
  assigned: boolean;
  planId: string | null;
  planName: string | null;
  currentOdometerKm: number | null;
  totalShopMinutes: number;
  groups: PmDueGroupWire[];
  notYetRecorded: PmEntryStatusWire[];
}

/** Latest completion of one related Test item (RelatedMeasurementResponse) —
 *  the condition evidence beside an overhaul. Nulls = never logged. */
export interface PmRelatedMeasurementWire {
  itemCode: string;
  component: string;
  measurement: string | null;
  performedAt: string | null;
  odometerKm: number | null;
}

/** One overhaul's computed status (OverhaulStatusResponse). */
export interface PmOverhaulStatusWire {
  code: string;
  component: string;
  intervalKm: number | null;
  intervalMonths: number | null;
  leadKm: number | null;
  leadDays: number | null;
  labourHours: number;
  partsCad: number;
  scope: string;
  conditionTriggers: string[];
  lastDoneKm: number | null;
  lastDoneDate: string | null;
  nextDueKm: number | null;
  nextDueDate: string | null;
  kmRemaining: number | null;
  daysRemaining: number | null;
  state: PmDueStateWire;
  relatedMeasurements: PmRelatedMeasurementWire[];
}

/** Overhaul-early decision view for one vehicle (PmOverhaulsResponse). */
export interface VehiclePmOverhaulsWire {
  assigned: boolean;
  planId: string | null;
  planName: string | null;
  currentOdometerKm: number | null;
  overhauls: PmOverhaulStatusWire[];
}

/** One logged PM completion (PmCompletionResponse) — the append-only per-unit
 *  service record. performedAt is "yyyy-MM-dd". */
export interface PmCompletionWire {
  id: string;
  vehicleId: string;
  planId: string;
  code: string;
  kind: PmEntryKindWire;
  performedAt: string;
  odometerKm: number;
  performedBy: string;
  workOrderId: string | null;
  measurement: string | null;
  notes: string | null;
  createdAtUtc: string;
}

/** One assigned vehicle's due picture on the fleet dashboard (FleetVehiclePmDueResponse). */
export interface FleetVehiclePmDueWire {
  vehicleId: string;
  unitNumber: string;
  currentOdometerKm: number;
  planId: string;
  planName: string;
  dueSoonCount: number;
  overdueCount: number;
  notYetRecordedCount: number;
  dueEntries: PmEntryStatusWire[];
}

/** Fleet-wide PM dashboard package (FleetPmDueResponse): one row per assigned,
 *  non-disposed vehicle — zero-due vehicles included — ordered most urgent
 *  first (overdue desc, due-soon desc, not-yet-recorded desc, unit number). */
export interface FleetPmDueWire {
  vehicles: FleetVehiclePmDueWire[];
}

// ---------------------------------------------------------------------------
// Mutation inputs (PmPlanRequest / PmPlanItemRequest / PmPlanOverhaulRequest /
// AssignPmPlanRequest / PmCompletionRequest in FleetEndpoints.Pm.cs).
// ---------------------------------------------------------------------------

/** One routine line of a plan request. tier/task are REQUIRED (omission is a 400). */
export interface PmPlanItemInput {
  code: string;
  system: string;
  component: string;
  tier: PmTierWire;
  task: PmTaskWire;
  intervalKm?: number | null;
  intervalMonths?: number | null;
  shopMinutes: number;
  leadKm?: number | null;
  leadDays?: number | null;
  notes?: string | null;
}

/** One overhaul of a plan request. Null leads keep the plan-wide defaults. */
export interface PmPlanOverhaulInput {
  code: string;
  component: string;
  intervalKm?: number | null;
  intervalMonths?: number | null;
  labourHours: number;
  partsCad: number;
  leadKm?: number | null;
  leadDays?: number | null;
  scope: string;
  conditionTriggers?: string[];
  relatedItemCodes?: string[];
}

/** Body for POST/PUT /api/fleet/pm-plans. */
export interface PmPlanInput {
  name: string;
  vehicleModel: string;
  serviceClass: string;
  notes?: string | null;
  items: PmPlanItemInput[];
  overhauls: PmPlanOverhaulInput[];
}

/** Body for POST /api/fleet/vehicles/{id}/pm/completions.
 *  performedAt is "yyyy-MM-dd" (backend binds DateOnly); odometerKm is required. */
export interface PmCompletionInput {
  code: string;
  kind: PmEntryKindWire;
  performedAt: string;
  odometerKm: number;
  performedBy: string;
  workOrderId?: string | null;
  measurement?: string | null;
  notes?: string | null;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

/** GET /api/fleet/pm/due — the fleet-wide PM dashboard, ordered most urgent first. */
export function listFleetPmDue(): Promise<FleetPmDueWire> {
  return request<FleetPmDueWire>("/api/fleet/pm/due");
}

/** GET /api/fleet/pm-plans — plan summaries with line counts and assigned-vehicle counts. */
export function listPmPlans(): Promise<PmPlanSummaryWire[]> {
  return request<PmPlanSummaryWire[]>("/api/fleet/pm-plans");
}

/** GET /api/fleet/pm-plans/{id} — full plan incl. items and overhauls. */
export function getPmPlan(id: string): Promise<PmPlanWire> {
  return request<PmPlanWire>(`/api/fleet/pm-plans/${id}`);
}

/** POST /api/fleet/pm-plans → 201; returns the new plan id. */
export async function createPmPlan(input: PmPlanInput): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/pm-plans", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT /api/fleet/pm-plans/{id} → 204. Full replace of the plan definition. */
export function updatePmPlan(id: string, input: PmPlanInput): Promise<void> {
  return request<void>(`/api/fleet/pm-plans/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** POST /api/fleet/pm-plans/seed-defaults → 200 { id }. Idempotent — returns the
 *  same plan id whether this run seeded the default plan or found it already there. */
export async function seedDefaultPmPlan(): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/pm-plans/seed-defaults", {
    method: "POST",
  });
  return res.id;
}

/** GET /api/fleet/vehicles/{id}/pm — full computed schedule.
 *  Unassigned vehicles answer 200 { assigned: false }, never 404. */
export function getVehiclePm(vehicleId: string): Promise<VehiclePmStatusWire> {
  return request<VehiclePmStatusWire>(`/api/fleet/vehicles/${vehicleId}/pm`);
}

/** GET /api/fleet/vehicles/{id}/pm/due — the shop-visit package (due + never-recorded). */
export function getVehiclePmDue(vehicleId: string): Promise<VehiclePmDueWire> {
  return request<VehiclePmDueWire>(`/api/fleet/vehicles/${vehicleId}/pm/due`);
}

/** GET /api/fleet/vehicles/{id}/pm/overhauls — overhaul statuses with condition
 *  triggers and the latest measurement from each related Test item. */
export function getVehiclePmOverhauls(vehicleId: string): Promise<VehiclePmOverhaulsWire> {
  return request<VehiclePmOverhaulsWire>(`/api/fleet/vehicles/${vehicleId}/pm/overhauls`);
}

/** GET /api/fleet/vehicles/{id}/pm/history?limit= — completions newest first
 *  (server default limit applies when omitted). 404 on an unknown vehicle. */
export function listVehiclePmHistory(vehicleId: string, limit?: number): Promise<PmCompletionWire[]> {
  const qs = limit != null ? `?limit=${limit}` : "";
  return request<PmCompletionWire[]>(`/api/fleet/vehicles/${vehicleId}/pm/history${qs}`);
}

/** POST /api/fleet/vehicles/{id}/pm/assign → 204. 400 missing planId,
 *  404 unknown plan, 409 assignment race. */
export function assignPmPlan(vehicleId: string, planId: string): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${vehicleId}/pm/assign`, {
    method: "POST",
    body: JSON.stringify({ planId }),
  });
}

/** DELETE /api/fleet/vehicles/{id}/pm → 204. Removes the plan assignment. */
export function unassignPmPlan(vehicleId: string): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${vehicleId}/pm`, { method: "DELETE" });
}

/** POST /api/fleet/vehicles/{id}/pm/completions → 201; returns the completion id. */
export async function logPmCompletion(vehicleId: string, input: PmCompletionInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/fleet/vehicles/${vehicleId}/pm/completions`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}
