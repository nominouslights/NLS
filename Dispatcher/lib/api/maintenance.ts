import { request } from "./transport";

// ---------------------------------------------------------------------------
// Vehicle Inspection contract (Backend Fleet module — VehicleInspectionResponse).
// Inspections are entered directly (driver app or dispatcher), tagged with
// tripNumber + vehicleId, and advance the vehicle odometer. Pre-trip carries
// weather/road/fuel; post-trip carries issues/attestations/signature/fuel-added
// — the sections that USED to live on the trip manifest. Distinct from the mock
// DVIR store in lib/maintenanceStore.ts.
// ---------------------------------------------------------------------------

export type InspectionResultWire = "Pass" | "PassWithDefects" | "Fail";
export type InspectionType = "PreTrip" | "PostTrip";
export type InspectionSourceWire = "DriverApp" | "Dispatcher";
export type DefectSeverityWire = "Minor" | "Major" | "OutOfService";
export type InspectionWeather = "Clear" | "Cloudy" | "Rain" | "Snow" | "Fog" | "ExtremeCold";
export type InspectionRoadCondition = "Dry" | "Wet" | "Icy" | "SnowCovered" | "Muddy";
export type InspectionVisibility = "Good" | "Reduced" | "Poor";
export type InspectionFuelLevel = "Full" | "ThreeQuarters" | "Half" | "Quarter";

export interface InspectionChecklistItemWire {
  group: string | null;
  item: string;
  passed: boolean;
}

export interface InspectionDefectWire {
  item: string;
  severity: DefectSeverityWire;
  note: string | null;
}

export interface VehicleInspection {
  id: string;
  type: InspectionType;
  source: InspectionSourceWire;
  tripNumber: string | null;
  vehicleId: string | null;
  unit: string;
  driverName: string;
  enteredBy: string | null;
  performedAt: string;
  odometerKm: number | null;
  result: InspectionResultWire;
  checklist: InspectionChecklistItemWire[];
  defects: InspectionDefectWire[];
  // Pre-trip sections
  weather: InspectionWeather[];
  temperatureC: string | null;
  roadConditions: InspectionRoadCondition[];
  visibility: InspectionVisibility | null;
  roadAdvisories: string | null;
  fuelLevel: InspectionFuelLevel | null;
  // Post-trip sections
  issues: string[];
  attestations: boolean[];
  driverSignatureName: string | null;
  certifiedAt: string | null;
  fuelAdded: boolean;
  fuelLitres: number | null;
  fuelCostCad: number | null;
  createdAtUtc: string;
}

/** GET /api/fleet/inspections — filter by unit and/or trip number. */
export function listInspections(params?: { unit?: string; tripNumber?: string }): Promise<VehicleInspection[]> {
  const q = new URLSearchParams();
  if (params?.unit) q.set("unit", params.unit);
  if (params?.tripNumber) q.set("tripNumber", params.tripNumber);
  const qs = q.toString();
  return request<VehicleInspection[]>(`/api/fleet/inspections${qs ? `?${qs}` : ""}`);
}

/** POST /api/fleet/inspections body. `source` defaults to Dispatcher server-side;
 *  pre-trip sends the weather/road/fuel fields, post-trip the issues/attestations/
 *  signature/fuel-added fields. Returns the new inspection id. */
export interface InspectionInput {
  type: InspectionType;
  source?: InspectionSourceWire;
  tripNumber?: string | null;
  vehicleId?: string | null;
  unit: string;
  driverName: string;
  enteredBy?: string | null;
  performedAt?: string;
  odometerKm?: number | null;
  checklist: { group?: string | null; item: string; passed: boolean }[];
  defects: { item: string; severity: DefectSeverityWire; note?: string | null }[];
  // Pre-trip
  weather?: InspectionWeather[];
  temperatureC?: string | null;
  roadConditions?: InspectionRoadCondition[];
  visibility?: InspectionVisibility | null;
  roadAdvisories?: string | null;
  fuelLevel?: InspectionFuelLevel | null;
  // Post-trip
  issues?: string[];
  attestations?: boolean[];
  driverSignatureName?: string | null;
  certifiedAt?: string | null;
  fuelAdded?: boolean;
  fuelLitres?: number | null;
  fuelCostCad?: number | null;
}

export async function createInspection(input: InspectionInput): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/inspections", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT /api/fleet/inspections/{id} — body is the same InspectionInput shape as
 *  create; `type` and `tripNumber` are immutable and ignored server-side. */
export function updateInspection(id: string, input: InspectionInput): Promise<void> {
  return request<void>(`/api/fleet/inspections/${id}`, { method: "PUT", body: JSON.stringify(input) });
}

/** DELETE /api/fleet/inspections/{id} — hard delete. Removing a post-trip
 *  inspection asynchronously flips the trip's HasPostTripInspection back to
 *  false (re-gating completion). */
export function deleteInspection(id: string): Promise<void> {
  return request<void>(`/api/fleet/inspections/${id}`, { method: "DELETE" });
}

// ---------------------------------------------------------------------------
// Maintenance contract (Backend Fleet module) — Shops, Documents, Service
// records, Work orders. Wire enums travel as their backend enum names (e.g.
// "InsuranceMpi", "InProgress"); the UI maps them to display labels and derives
// the status chip from the underlying dates/priority. Named with a "Wire" suffix
// to avoid colliding with the mock types in lib/types.ts during the migration.
// ---------------------------------------------------------------------------

export type DocumentTypeWire =
  | "Registration" | "InsuranceMpi" | "NscSafetyCertificate"
  | "Emissions" | "BillOfSale" | "Warranty" | "Other";
export type ServiceCategoryWire = "Preventive" | "Repair" | "InspectionFix" | "Recall";
export type WorkOrderStatusWire = "Open" | "InProgress" | "AwaitingParts" | "Completed" | "Cancelled";
export type WorkOrderPriorityWire = "Low" | "Medium" | "High" | "Critical";
export type WorkOrderSourceWire =
  | "Manual" | "PreTripInspection" | "PostTripInspection" | "DtcAlert" | "PmReminder";

export interface ShopWire {
  id: string;
  number: string;
  name: string;
  contactName: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  gstBusinessNo: string | null;
  mpiAccredited: boolean;
  inspectionStationNo: string | null;
  suppliesParts: boolean;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ShopInput {
  name: string;
  contactName?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  gstBusinessNo?: string | null;
  mpiAccredited: boolean;
  inspectionStationNo?: string | null;
  suppliesParts: boolean;
  notes?: string | null;
}

export function listShops(): Promise<ShopWire[]> {
  return request<ShopWire[]>("/api/fleet/shops");
}

export async function createShop(input: ShopInput): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/shops", { method: "POST", body: JSON.stringify(input) });
  return res.id;
}

export function updateShop(id: string, input: ShopInput): Promise<void> {
  return request<void>(`/api/fleet/shops/${id}`, { method: "PUT", body: JSON.stringify(input) });
}

export interface VehicleDocumentWire {
  id: string;
  vehicleId: string;
  number: string;
  type: DocumentTypeWire;
  fileName: string;
  fileSizeKb: number;
  uploadedBy: string;
  uploadedAt: string;
  expiry: string | null;
  note: string | null;
}

export function listVehicleDocuments(vehicleId: string): Promise<VehicleDocumentWire[]> {
  return request<VehicleDocumentWire[]>(`/api/fleet/vehicles/${vehicleId}/documents`);
}

export function listAllDocuments(): Promise<VehicleDocumentWire[]> {
  return request<VehicleDocumentWire[]>("/api/fleet/documents");
}

export async function addVehicleDocument(vehicleId: string, input: {
  type: DocumentTypeWire;
  fileName: string;
  fileSizeKb: number;
  uploadedBy?: string;
  expiry?: string | null;
  note?: string | null;
}): Promise<string> {
  const res = await request<{ id: string }>(`/api/fleet/vehicles/${vehicleId}/documents`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function removeVehicleDocument(vehicleId: string, documentId: string): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${vehicleId}/documents/${documentId}`, { method: "DELETE" });
}

export interface ServicePartWire { sku: string; qty: number; }

export interface ServiceRecordWire {
  id: string;
  vehicleId: string;
  number: string;
  date: string;
  performedBy: string;
  category: ServiceCategoryWire;
  odometerKm: number;
  itemsChanged: string[];
  reason: string;
  partsUsed: ServicePartWire[];
  laborHours: number | null;
  costCad: number | null;
  workOrderId: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export function listVehicleServiceRecords(vehicleId: string): Promise<ServiceRecordWire[]> {
  return request<ServiceRecordWire[]>(`/api/fleet/vehicles/${vehicleId}/service-records`);
}

export async function addServiceRecord(vehicleId: string, input: {
  date?: string;
  performedBy: string;
  category: ServiceCategoryWire;
  odometerKm: number;
  itemsChanged: string[];
  reason: string;
  partsUsed: ServicePartWire[];
  laborHours?: number | null;
  costCad?: number | null;
  workOrderId?: string | null;
  notes?: string | null;
}): Promise<string> {
  const res = await request<{ id: string }>(`/api/fleet/vehicles/${vehicleId}/service-records`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export interface WorkOrderWire {
  id: string;
  vehicleId: string;
  number: string;
  title: string;
  description: string;
  status: WorkOrderStatusWire;
  priority: WorkOrderPriorityWire;
  source: WorkOrderSourceWire;
  sourceRef: string | null;
  createdBy: string;
  createdAt: string;
  assignedTo: string | null;
  dueDate: string | null;
  lineItems: string[];
  completedAt: string | null;
  resolvingServiceId: string | null;
  shopId: string | null;
  authorizedLimitCad: number | null;
  budgetCode: string | null;
  dateRequiredOrOos: string | null;
}

export function listVehicleWorkOrders(vehicleId: string): Promise<WorkOrderWire[]> {
  return request<WorkOrderWire[]>(`/api/fleet/vehicles/${vehicleId}/work-orders`);
}

export function listAllWorkOrders(): Promise<WorkOrderWire[]> {
  return request<WorkOrderWire[]>("/api/fleet/work-orders");
}

export async function createWorkOrder(input: {
  vehicleId: string;
  title: string;
  description?: string | null;
  priority: WorkOrderPriorityWire;
  source: WorkOrderSourceWire;
  sourceRef?: string | null;
  assignedTo?: string | null;
  dueDate?: string | null;
  lineItems: string[];
  shopId?: string | null;
  authorizedLimitCad?: number | null;
  budgetCode?: string | null;
  dateRequiredOrOos?: string | null;
  inspectionId?: string | null;
}): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/work-orders", { method: "POST", body: JSON.stringify(input) });
  return res.id;
}

export function changeWorkOrderStatus(id: string, status: WorkOrderStatusWire): Promise<void> {
  return request<void>(`/api/fleet/work-orders/${id}/status`, { method: "POST", body: JSON.stringify({ status }) });
}

/** Completes a work order by logging the resolving service record. Returns the service id. */
export async function completeWorkOrder(id: string, input: {
  date?: string;
  performedBy: string;
  category: ServiceCategoryWire;
  odometerKm: number;
  itemsChanged: string[];
  reason: string;
  partsUsed: ServicePartWire[];
  laborHours?: number | null;
  costCad?: number | null;
  notes?: string | null;
}): Promise<string> {
  const res = await request<{ id: string }>(`/api/fleet/work-orders/${id}/complete`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}
