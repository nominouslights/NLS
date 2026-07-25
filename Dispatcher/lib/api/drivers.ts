import { request, requestBlob } from "./transport";
import { docStatusFor } from "../maintenanceStore";
import type { StatusKind } from "../theme";

// ---------------------------------------------------------------------------
// Drivers API client — contract owned by Backend/ (Drivers module,
// DriversEndpoints.cs). Shapes mirror the backend's DriverResponse /
// DriverCredentialResponse / DriverClearanceResponse / HosEntryResponse exactly
// (JSON camelCase). Do not invent fields — extend only when the backend
// contract changes. Leave records have NO backend — they stay mocked in
// lib/data.ts.
// ---------------------------------------------------------------------------

export type DriverStatus = "Active" | "Inactive" | "Deactivated";

export interface DriverRecord {
  id: string;
  name: string;
  phone: string | null;
  licenceClass: string;
  licenceExpiry: string | null; // DateOnly, "2027-03-15"
  source: string;
  hasWorkPermit: boolean;
  status: DriverStatus;
  credentialCount: number;
  soonestCredentialExpiry: string | null; // DateOnly
  // Denormalized HOS rollup on the driver read model — the latest duty entry's
  // status and driving hours, so the roster row's duty chip + "HOS left" column
  // render without a per-driver HOS call. Null when the driver has no entries.
  latestDutyStatus: string | null; // friendly duty string — "Off Duty" / "On Duty" / "Driving"
  latestDrivingHours: number | null;
  registeredAtUtc: string;
  updatedAtUtc: string;
}

/** Fields the dispatcher supplies when registering or editing a driver
 *  (RegisterDriverRequest / UpdateDriverRequest — same body). */
export interface DriverInput {
  name: string;
  phone?: string | null;
  licenceClass: string;
  licenceExpiry?: string | null;
  source: string;
  hasWorkPermit: boolean;
}

export interface DriverCredentialRecord {
  id: string;
  driverId: string;
  type: string;
  label: string;
  issued: string; // DateOnly — backend defaults to today (UTC) when omitted on POST
  expiry: string | null;
  optional: boolean;
  note: string | null;
  hasImage: boolean;
}

/** POST /api/drivers/{driverId}/credentials body (DriverCredentialRequest). */
export interface DriverCredentialInput {
  type: string;
  label: string;
  issued?: string | null;
  expiry?: string | null;
  optional: boolean;
  note?: string | null;
}

export interface DriverClearanceRecord {
  id: string;
  driverId: string;
  title: string;
  clientName: string;
  expiry: string | null;
  grantedAtUtc: string;
}

/** POST /api/drivers/{driverId}/clearances body (DriverClearanceRequest). */
export interface DriverClearanceInput {
  title: string;
  clientName: string;
  expiry?: string | null;
}

/** One hours-of-service duty entry (HosEntryResponse). Duty and source arrive
 *  as the friendly display strings the console renders directly (dutyMeta /
 *  SourceChip); the backend maps its enums ⇄ these strings at the boundary. */
export interface HosEntryRecord {
  id: string;
  driverId: string;
  date: string; // DateOnly, "2026-07-17"
  duty: string; // friendly — "Off Duty" / "On Duty" / "Driving"
  onDutyH: number;
  drivingH: number;
  offDutyH: number;
  source: string; // friendly — "Driver App" / "Manual (paper backup)"
  enteredBy: string | null; // dispatcher name for a paper backup, null for the app
  note: string | null;
  recordedAtUtc: string;
}

/** POST /api/drivers/{driverId}/hos body (HosEntryRequest). Source is set
 *  server-side (a dispatcher entry is always a paper backup) — do NOT send it. */
export interface HosEntryInput {
  date: string;
  duty: string; // friendly duty string, sent as-is
  onDutyH: number;
  drivingH: number;
  offDutyH: number;
  enteredBy: string;
  note?: string | null;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export function listDrivers(): Promise<DriverRecord[]> {
  return request<DriverRecord[]>("/api/drivers");
}

export function getDriver(id: string): Promise<DriverRecord> {
  return request<DriverRecord>(`/api/drivers/${id}`);
}

/** POST → 201 { id }. */
export async function registerDriver(input: DriverInput): Promise<string> {
  const res = await request<{ id: string }>("/api/drivers", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateDriver(id: string, input: DriverInput): Promise<void> {
  return request<void>(`/api/drivers/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function changeDriverStatus(id: string, status: DriverStatus): Promise<void> {
  return request<void>(`/api/drivers/${id}/status`, {
    method: "POST",
    body: JSON.stringify({ status }),
  });
}

export function listDriverCredentials(driverId: string): Promise<DriverCredentialRecord[]> {
  return request<DriverCredentialRecord[]>(`/api/drivers/${driverId}/credentials`);
}

/** POST → 201 { id }. */
export async function addDriverCredential(
  driverId: string,
  input: DriverCredentialInput,
): Promise<string> {
  const res = await request<{ id: string }>(`/api/drivers/${driverId}/credentials`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function removeDriverCredential(driverId: string, credentialId: string): Promise<void> {
  return request<void>(`/api/drivers/${driverId}/credentials/${credentialId}`, {
    method: "DELETE",
  });
}

/** Upload a credential image (JPEG, PNG, HEIC). Returns void (204 No Content). */
export async function uploadDriverCredentialImage(
  driverId: string,
  credentialId: string,
  file: File,
): Promise<void> {
  const formData = new FormData();
  formData.append("image", file);
  return request<void>(`/api/drivers/${driverId}/credentials/${credentialId}/image`, {
    method: "POST",
    body: formData,
  });
}

/** Fetch a credential's image as a blob (or throws 404 if no image). */
export function driverCredentialImageBlob(driverId: string, credentialId: string): Promise<Blob> {
  return requestBlob(`/api/drivers/${driverId}/credentials/${credentialId}/image`);
}

export function listDriverClearances(driverId: string): Promise<DriverClearanceRecord[]> {
  return request<DriverClearanceRecord[]>(`/api/drivers/${driverId}/clearances`);
}

/** POST → 201 { id }. */
export async function grantDriverClearance(
  driverId: string,
  input: DriverClearanceInput,
): Promise<string> {
  const res = await request<{ id: string }>(`/api/drivers/${driverId}/clearances`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function revokeDriverClearance(driverId: string, clearanceId: string): Promise<void> {
  return request<void>(`/api/drivers/${driverId}/clearances/${clearanceId}`, {
    method: "DELETE",
  });
}

export function listDriverHosEntries(driverId: string): Promise<HosEntryRecord[]> {
  return request<HosEntryRecord[]>(`/api/drivers/${driverId}/hos`);
}

/** POST → 201 { id }. Source is fixed server-side (Manual paper backup). */
export async function addDriverHosEntry(driverId: string, input: HosEntryInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/drivers/${driverId}/hos`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

// Eventual-consistency retry helper now lives in lib/api/shared.ts; re-exported
// here so existing `from "./drivers"` imports (and the clients/trips/billing
// re-exports of this re-export) keep working unchanged.
export { refetchUntil } from "./shared";

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs
// the colour with a glyph and text label).
// ---------------------------------------------------------------------------

/** Credential/clearance/licence expiry chip kind — same thresholds as the
 *  Fleet document chips (ontime / soon within 60d / over). */
export function credentialKindFor(expiry: string | null): StatusKind {
  return docStatusFor(expiry);
}

export function driverStatusKindFor(status: DriverStatus): StatusKind {
  switch (status) {
    case "Active":
      return "info";
    case "Inactive":
      return "off";
    case "Deactivated":
    default:
      return "over";
  }
}

/** Credential type display order — licence first, then the mandatory checks,
 *  then optional creds last. Unknown types sort after the known ones. The
 *  backend stores type as free text; this list is the console's dropdown
 *  vocabulary, not a wire enum. */
export const CREDENTIAL_TYPES: string[] = [
  "Licence Class",
  "Police Record Check",
  "Drug & Alcohol",
  "First Aid",
  "Work Permit",
];

export function sortCredentials(rows: DriverCredentialRecord[]): DriverCredentialRecord[] {
  const orderOf = (type: string) => {
    const i = CREDENTIAL_TYPES.indexOf(type);
    return i === -1 ? CREDENTIAL_TYPES.length : i;
  };
  return [...rows].sort((a, b) => orderOf(a.type) - orderOf(b.type));
}

/** Clearances alphabetical by client name (roster convention). */
export function sortClearances(rows: DriverClearanceRecord[]): DriverClearanceRecord[] {
  return [...rows].sort((a, b) => a.clientName.localeCompare(b.clientName));
}

/** HOS duty entries newest first — the latest entry drives the remaining gauge
 *  and duty status (ISO dates sort lexicographically). */
export function sortHosEntries(rows: HosEntryRecord[]): HosEntryRecord[] {
  return [...rows].sort((a, b) => b.date.localeCompare(a.date));
}
