import type { StatusKind } from "./theme";

// ---------------------------------------------------------------------------
// Fleet API client — contract owned by Backend/ (Fleet module).
// Shapes here mirror the backend's VehicleResponse / RetirementCertificateResponse
// exactly (JSON camelCase). Do not invent fields — extend only when the backend
// contract changes.
// ---------------------------------------------------------------------------

// Relative — requests go to this app's own origin (/api/...) and Next.js's rewrite
// (next.config.ts) proxies them server-side to the real Fleet API. Same-origin means the
// browser never needs CORS. Override only for exotic setups (e.g. a static export) where
// the rewrite proxy isn't available.
export const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export type VehicleStatus =
  | "Active"
  | "InMaintenance"
  | "OutOfService"
  | "Retired"
  | "Sold"
  | "Recycled";

export type DisposalMethod = "Sold" | "Recycled";

export interface Vehicle {
  id: string;
  unitNumber: string;
  vin: string;
  make: string;
  model: string;
  year: number;
  seatingCapacity: number;
  licencePlate: string;
  requiredLicenceClass: string;
  status: VehicleStatus;
  statusReason: string | null;
  odometerKm: number;
  acquisitionCostCad: number;
  endOfLifeKm: number;
  salePriceCad: number | null;
  disposedAtUtc: string | null;
  requiresPeriodicInspection: boolean;
  currentValueCad: number;
  remainingKm: number;
  lifeUsedPct: number;
  registeredAtUtc: string;
  updatedAtUtc: string;
}

export interface RetirementCertificate {
  id: string;
  certificateNumber: string;
  vehicleId: string;
  vin: string;
  unitNumber: string;
  make: string;
  model: string;
  year: number;
  finalOdometerKm: number;
  retirementReason: string;
  retiredAtUtc: string;
  issuedAtUtc: string;
}

/** Fields the dispatcher supplies when registering or editing a vehicle. */
export interface VehicleInput {
  unitNumber: string;
  vin: string;
  make: string;
  model: string;
  year: number;
  seatingCapacity: number;
  licencePlate: string;
  requiredLicenceClass: string;
  acquisitionCostCad: number;
  endOfLifeKm: number;
}

/** Error body shape the backend returns: { code, message }. */
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(code: string, message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.code = code;
    this.status = status;
  }
}

/** Low-level fetch: JSON headers + optional bearer token. Throws only on network failure. */
async function send(path: string, init: RequestInit | undefined, token: string | null): Promise<Response> {
  const headers: Record<string, string> = {
    ...(init?.headers as Record<string, string> | undefined),
  };
  // Only default to JSON content-type if body is not FormData (FormData needs browser-set multipart boundary)
  if (!(init?.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }
  if (token) headers.Authorization = `Bearer ${token}`;
  try {
    return await fetch(`${API_BASE}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(
      "Network.Unreachable",
      `Cannot reach the API at ${API_BASE}. Is the backend running?`,
      0,
    );
  }
}

/** Parses the backend's { code, message } error body, with an HTTP fallback. */
async function parseError(res: Response): Promise<ApiError> {
  let code = `Http.${res.status}`;
  let message = res.statusText || `Request failed with status ${res.status}`;
  try {
    const body = (await res.json()) as { code?: string; message?: string };
    if (body?.code) code = body.code;
    if (body?.message) message = body.message;
  } catch {
    // no structured error body — keep the HTTP fallback
  }
  return new ApiError(code, message, res.status);
}

/** Authenticated fetch with token refresh + 401 retry logic (returns raw Response). */
async function authenticatedFetch(path: string, init?: RequestInit): Promise<Response> {
  const { getAccessToken, getValidAccessToken, refreshAccessToken } = await import("./auth");

  const token = await getValidAccessToken();
  let res = await send(path, init, token);

  if (res.status === 401) {
    let retryToken: string | null = null;
    const current = getAccessToken();
    if (current && current !== token) {
      retryToken = current;
    } else {
      try {
        retryToken = await refreshAccessToken();
      } catch {
        retryToken = null;
      }
    }
    if (retryToken) res = await send(path, init, retryToken);
  }

  return res;
}

/**
 * Unauthenticated POST for the Identity auth endpoints (login/refresh/logout/setup).
 * Used by lib/auth.ts — no bearer header, no 401 retry.
 */
export async function identityRequest<T>(path: string, body: unknown): Promise<T> {
  const res = await send(path, { method: "POST", body: JSON.stringify(body) }, null);
  if (!res.ok) throw await parseError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/**
 * Unauthenticated GET for the Identity first-run setup check. Separate from
 * `identityRequest` (which is POST-only) and from `request` (which attaches a
 * bearer token) — this one runs before any session can exist.
 */
export async function identityGet<T>(path: string): Promise<T> {
  const res = await send(path, { method: "GET" }, null);
  if (!res.ok) throw await parseError(res);
  return (await res.json()) as T;
}

/**
 * Authenticated request. Attaches `Authorization: Bearer <accessToken>`
 * (refreshing proactively when the token is near expiry). On a 401, refreshes
 * once and retries the original request once with the new token; if the
 * refresh fails, lib/auth.ts clears the session (surfacing the login screen)
 * and the original 401 is thrown. Never loops.
 *
 * Exported for lib/auth.ts (admin invite minting needs an authenticated call);
 * endpoint wrappers below remain the preferred surface for feature code.
 */
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await authenticatedFetch(path, init);

  if (!res.ok) throw await parseError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/**
 * Authenticated blob fetch (e.g. image download). Returns raw bytes without parsing.
 * Handles 401 refresh/retry like `request<T>`.
 */
export async function requestBlob(path: string, init?: RequestInit): Promise<Blob> {
  const res = await authenticatedFetch(path, init);

  if (!res.ok) throw await parseError(res);
  return await res.blob();
}

// ---------------------------------------------------------------------------
// Endpoints (Backend Fleet module — FleetEndpoints.cs)
// ---------------------------------------------------------------------------

export function listVehicles(): Promise<Vehicle[]> {
  return request<Vehicle[]>("/api/fleet/vehicles");
}

export function getVehicle(id: string): Promise<Vehicle> {
  return request<Vehicle>(`/api/fleet/vehicles/${id}`);
}

/** POST → 201 + Location header; returns the created vehicle id. */
export async function registerVehicle(input: VehicleInput): Promise<string> {
  const created = await request<string | { id?: string }>("/api/fleet/vehicles", {
    method: "POST",
    body: JSON.stringify(input),
  });
  if (typeof created === "string") return created;
  return created?.id ?? "";
}

export function updateVehicle(id: string, input: VehicleInput): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function changeVehicleStatus(
  id: string,
  status: VehicleStatus,
  reason?: string,
): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${id}/status`, {
    method: "POST",
    body: JSON.stringify({ status, reason }),
  });
}

/** May auto-retire the vehicle as a side effect when odometer ≥ EndOfLifeKm. */
export function recordOdometer(id: string, odometerKm: number): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${id}/odometer`, {
    method: "POST",
    body: JSON.stringify({ odometerKm }),
  });
}

export function disposeVehicle(
  id: string,
  method: DisposalMethod,
  salePriceCad?: number,
  note?: string,
): Promise<void> {
  return request<void>(`/api/fleet/vehicles/${id}/dispose`, {
    method: "POST",
    body: JSON.stringify({ method, salePriceCad, note }),
  });
}

export function getRetirementCertificate(id: string): Promise<RetirementCertificate> {
  return request<RetirementCertificate>(`/api/fleet/vehicles/${id}/retirement-certificate`);
}

// ---------------------------------------------------------------------------
// Display helpers — status colour NEVER stands alone (StatusChip pairs the
// colour with a glyph and text label).
// ---------------------------------------------------------------------------

export function statusKindFor(status: VehicleStatus): StatusKind {
  switch (status) {
    case "Active":
      return "info";
    case "InMaintenance":
      return "soon";
    case "OutOfService":
      return "over";
    case "Retired":
    case "Sold":
    case "Recycled":
    default:
      return "off";
  }
}

export function statusLabelFor(status: VehicleStatus): string {
  switch (status) {
    case "InMaintenance":
      return "In maintenance";
    case "OutOfService":
      return "Out of service";
    default:
      return status;
  }
}

/**
 * Legal status-transition matrix (mirrors the domain's rules — the backend is
 * authoritative; this only drives which action buttons render).
 * Retired is the mandatory gateway to disposal; Sold/Recycled are terminal.
 */
const LEGAL_TRANSITIONS: Record<VehicleStatus, VehicleStatus[]> = {
  Active: ["InMaintenance", "OutOfService", "Retired"],
  InMaintenance: ["Active", "OutOfService", "Retired"],
  OutOfService: ["Active", "InMaintenance", "Retired"],
  Retired: [], // disposal (Sold/Recycled) goes through disposeVehicle, not changeVehicleStatus
  Sold: [],
  Recycled: [],
};

export function canTransition(from: VehicleStatus, to: VehicleStatus): boolean {
  return LEGAL_TRANSITIONS[from]?.includes(to) ?? false;
}

export function isDisposed(status: VehicleStatus): boolean {
  return status === "Sold" || status === "Recycled";
}

/** Only Active vehicles are trip-assignable; disposal requires Retired first. */
export function canDispose(status: VehicleStatus): boolean {
  return status === "Retired";
}

/** End-of-service-life meter kind: <75% ontime, 75–90% soon, ≥90% over. */
export function lifeKindFor(lifeUsedPct: number): StatusKind {
  if (lifeUsedPct >= 90) return "over";
  if (lifeUsedPct >= 75) return "soon";
  return "ontime";
}

const cadFmt = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  maximumFractionDigits: 0,
});

export function formatCad(value: number): string {
  return cadFmt.format(value);
}

export function formatKm(value: number): string {
  return `${value.toLocaleString("en-CA")} km`;
}

export function formatUtcDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-CA", { year: "numeric", month: "short", day: "numeric" });
}

// ---------------------------------------------------------------------------
// Trip Manifest contract (Backend Trips module — TripManifestResponse /
// CreateTripManifestRequest, TripsEndpoints.cs). Shapes mirror the backend
// wire contract exactly (JSON camelCase, enums as PascalCase strings) — do
// not invent fields.
// ---------------------------------------------------------------------------

export type ManifestSource = "App" | "Paper";
export type ManifestFuelLevel = "Full" | "ThreeQuarters" | "Half" | "Quarter";
export type ManifestDirection = "Inbound" | "Outbound";
export type ManifestWeather = "Clear" | "Cloudy" | "Rain" | "Snow" | "Fog" | "ExtremeCold";
export type ManifestRoadCondition = "Dry" | "Wet" | "Icy" | "SnowCovered" | "Muddy";
export type ManifestVisibility = "Good" | "Reduced" | "Poor";
export type ManifestSeverity = "Minor" | "Major" | "OutOfService";
export type ManifestCargoSecured = "Yes" | "NotApplicable";

export interface ManifestPreTripItem {
  /** Backend group label, e.g. "Exterior & Mechanical" (tripManifestChecklist key). */
  group: string;
  /** Backend item string, e.g. "Tires" (tripManifestChecklist key). */
  item: string;
  status: "Ok" | "Fail";
  severity: ManifestSeverity | null;
  note: string | null;
}

export interface ManifestPassenger {
  name: string;
  contact: string | null;
  pickup: string | null;
  dropoff: string | null;
  idVerified: boolean;
  boardedOn: boolean;
  boardedOff: boolean;
}

export interface ManifestCargo {
  description: string;
  ownerRecipient: string | null;
  weightKg: number | null;
  chargeCad: number | null;
  hazmat: boolean;
  secured: boolean;
}

export interface ManifestPostTripItem {
  item: string;
  ok: boolean;
}

export interface TripManifest {
  id: string;
  tripDate: string; // DateOnly, "2026-07-15"
  tripNumber: string;
  route: string;
  direction: ManifestDirection | null;
  client: string | null;
  unit: string;
  driverName: string;
  driverLicenceNo: string | null;
  licencePlate: string | null;
  odometerStartKm: number | null;
  fuelLevel: ManifestFuelLevel | null;
  preTrip: ManifestPreTripItem[];
  weather: ManifestWeather[];
  temperatureC: string | null;
  roadConditions: ManifestRoadCondition[];
  visibility: ManifestVisibility | null;
  roadAdvisories: string | null;
  passengers: ManifestPassenger[];
  allSeatbeltsVerified: boolean;
  cargo: ManifestCargo[];
  allCargoSecured: ManifestCargoSecured | null;
  issues: string[];
  noIssues: boolean;
  departureTime: string | null;
  arrivalTime: string | null;
  odometerEndKm: number | null;
  totalKm: number | null;
  fuelAdded: boolean;
  fuelLitres: number | null;
  fuelCostCad: number | null;
  postTrip: ManifestPostTripItem[];
  attestations: boolean[];
  driverSignatureName: string;
  certifiedAt: string;
  source: ManifestSource;
  enteredBy: string | null;
  enteredAt: string | null;
  createdAtUtc: string;
}

/**
 * POST /api/trips/manifests body — the response shape minus id / enteredAt /
 * createdAtUtc; certifiedAt is optional (backend stamps "now" when omitted).
 * PreTrip rows REQUIRE group + item (400 otherwise).
 */
export interface TripManifestInput {
  tripDate: string;
  tripNumber: string;
  route: string;
  direction: ManifestDirection | null;
  client: string | null;
  unit: string;
  driverName: string;
  driverLicenceNo: string | null;
  licencePlate: string | null;
  odometerStartKm: number | null;
  fuelLevel: ManifestFuelLevel | null;
  preTrip: ManifestPreTripItem[];
  weather: ManifestWeather[];
  temperatureC: string | null;
  roadConditions: ManifestRoadCondition[];
  visibility: ManifestVisibility | null;
  roadAdvisories: string | null;
  passengers: ManifestPassenger[];
  allSeatbeltsVerified: boolean;
  cargo: ManifestCargo[];
  allCargoSecured: ManifestCargoSecured | null;
  issues: string[];
  noIssues: boolean;
  departureTime: string | null;
  arrivalTime: string | null;
  odometerEndKm: number | null;
  totalKm: number | null;
  fuelAdded: boolean;
  fuelLitres: number | null;
  fuelCostCad: number | null;
  postTrip: ManifestPostTripItem[];
  attestations: boolean[];
  driverSignatureName: string;
  certifiedAt?: string;
  source: ManifestSource;
  enteredBy: string | null;
}

/** POST → 201 { id } (Trips module — ManifestCreatedResponse). */
export function createTripManifest(input: TripManifestInput): Promise<{ id: string }> {
  return request<{ id: string }>("/api/trips/manifests", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function listTripManifests(params?: {
  tripNumber?: string;
  unit?: string;
}): Promise<TripManifest[]> {
  const q = new URLSearchParams();
  if (params?.tripNumber) q.set("tripNumber", params.tripNumber);
  if (params?.unit) q.set("unit", params.unit);
  const qs = q.toString();
  return request<TripManifest[]>(`/api/trips/manifests${qs ? `?${qs}` : ""}`);
}

export function getTripManifest(id: string): Promise<TripManifest> {
  return request<TripManifest>(`/api/trips/manifests/${id}`);
}

// ---------------------------------------------------------------------------
// Vehicle Inspection contract (Backend Fleet module — VehicleInspectionResponse).
// Backend-owned inspections derived from submitted manifests (via RabbitMQ),
// distinct from the mock DVIR store in lib/maintenanceStore.ts.
// ---------------------------------------------------------------------------

export type InspectionResultWire = "Pass" | "PassWithDefects" | "Fail";

export interface VehicleInspection {
  id: string;
  unit: string;
  type: "PreTrip" | "PostTrip";
  driverName: string;
  source: "DriverApp" | "PaperTranscription";
  enteredBy: string | null;
  tripNumber: string;
  manifestId: string;
  performedAt: string;
  odometerKm: number | null;
  result: InspectionResultWire;
  checklist: { group: string | null; item: string; passed: boolean }[];
  defects: { item: string; severity: ManifestSeverity; note: string | null }[];
  createdAtUtc: string;
}

export function listInspections(unit?: string): Promise<VehicleInspection[]> {
  const qs = unit ? `?unit=${encodeURIComponent(unit)}` : "";
  return request<VehicleInspection[]>(`/api/fleet/inspections${qs}`);
}

/** Dispatcher paper-backup DVIR entry (POST /api/fleet/inspections). Returns the new id. */
export async function createInspection(input: {
  unit: string;
  type: "PreTrip" | "PostTrip";
  driverName: string;
  enteredBy?: string;
  performedAt?: string;
  odometerKm?: number | null;
  checklist: { group?: string | null; item: string; passed: boolean }[];
  defects: { item: string; severity: "Minor" | "Major" | "OutOfService"; note?: string | null }[];
}): Promise<string> {
  const res = await request<{ id: string }>("/api/fleet/inspections", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
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
