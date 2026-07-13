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

// TEMPORARY: dev tenant header until Identity/OpenIddict lands — the backend's
// Development-only ITenantContext reads X-Tenant-Id. Replaced by token claims
// once real auth ships.
const DEV_TENANT_ID = "00000000-0000-0000-0000-000000000001";

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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Id": DEV_TENANT_ID, // TEMPORARY — see note above
        ...init?.headers,
      },
    });
  } catch {
    throw new ApiError(
      "Network.Unreachable",
      `Cannot reach the Fleet API at ${API_BASE}. Is the backend running?`,
      0,
    );
  }

  if (!res.ok) {
    let code = `Http.${res.status}`;
    let message = res.statusText || `Request failed with status ${res.status}`;
    try {
      const body = (await res.json()) as { code?: string; message?: string };
      if (body?.code) code = body.code;
      if (body?.message) message = body.message;
    } catch {
      // no structured error body — keep the HTTP fallback
    }
    throw new ApiError(code, message, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
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
