import { request } from "./transport";
import type { StatusKind } from "../theme";
import type { ClientServiceType } from "./clients";

// ---------------------------------------------------------------------------
// Riders API client — contract owned by Backend/ (Trips module, new Rider
// aggregate; RiderEndpoints.cs). Shapes mirror the backend's RiderResponse
// exactly (JSON camelCase, serviceType as a PascalCase string, DateOnly as
// "yyyy-MM-dd"). The directory is populated server-side from saved trip
// manifests (eventually consistent — it refreshes shortly after a manifest is
// saved); the console never creates riders directly. Do not invent fields —
// extend only when the backend contract changes.
// ---------------------------------------------------------------------------

/** Riders exist only for passenger service types — Cargo/Grocery trips never
 *  populate the directory. */
export type RiderServiceType = Extract<
  ClientServiceType,
  "ContractCrew" | "Community" | "Nihb" | "Charter"
>;

/** Backend list ordering — the trip-type separator groups, in order. */
export const RIDER_GROUP_ORDER: RiderServiceType[] = [
  "ContractCrew",
  "Community",
  "Nihb",
  "Charter",
];

/** Crew rotation lengths (days) — ContractCrew riders only. */
export const ROTATION_OPTIONS: number[] = [20, 10, 5];

/** Mirrors RiderResponse — list and detail share the same shape. */
export interface RiderRecord {
  id: string;
  name: string;
  serviceType: RiderServiceType;
  contact: string | null;
  /** 5 | 10 | 20 — ContractCrew only; null when unset / not applicable. */
  rotationDays: number | null;
  lastTripDate: string | null; // DateOnly, "2026-08-22"
  lastTripNumber: string | null;
  /** lastTripDate + rotationDays, computed server-side. */
  nextExpectedTravelDate: string | null;
  tripCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export function listRiders(params?: {
  serviceType?: RiderServiceType;
  search?: string;
}): Promise<RiderRecord[]> {
  const qs = new URLSearchParams();
  if (params?.serviceType) qs.set("serviceType", params.serviceType);
  if (params?.search) qs.set("search", params.search);
  const suffix = qs.toString();
  return request<RiderRecord[]>(`/api/trips/riders${suffix ? `?${suffix}` : ""}`);
}

/** PUT → 204. rotationDays must be 5/10/20 (ContractCrew only) or null to clear. */
export function setRiderRotation(id: string, rotationDays: number | null): Promise<void> {
  return request<void>(`/api/trips/riders/${id}/rotation`, {
    method: "PUT",
    body: JSON.stringify({ rotationDays }),
  });
}

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs the
// colour with a glyph and text label; the protected hexes live in lib/theme).
// ---------------------------------------------------------------------------

/** Group riders in RIDER_GROUP_ORDER, skipping empty groups. Rows within a
 *  group keep the backend's name ordering. */
export function groupRiders(
  rows: RiderRecord[],
): { serviceType: RiderServiceType; riders: RiderRecord[] }[] {
  return RIDER_GROUP_ORDER.map((serviceType) => ({
    serviceType,
    riders: rows.filter((r) => r.serviceType === serviceType),
  })).filter((g) => g.riders.length > 0);
}

/** Client-side mirror of the server's NextExpectedTravelDate computation, for
 *  optimistic updates after a rotation change (before the next refetch). */
export function nextExpectedFrom(
  lastTripDate: string | null,
  rotationDays: number | null,
): string | null {
  if (!lastTripDate || !rotationDays) return null;
  const d = new Date(`${lastTripDate}T00:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  d.setDate(d.getDate() + rotationDays);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export type RotationDueKind = Extract<StatusKind, "over" | "soon" | "ontime">;

/** Chip labels for the rotation due state — the text half of colour+icon+label. */
export const ROTATION_DUE_LABELS: Record<RotationDueKind, string> = {
  over: "Overdue",
  soon: "Due soon",
  ontime: "On rotation",
};

/** Due-state of a rider's crew rotation against today:
 *  "over" = next expected travel date has passed, "soon" = due within 2 days,
 *  "ontime" = comfortably inside the rotation, null = not applicable
 *  (no rotation set, no trips yet, or not a ContractCrew rider). */
export function rotationDueKind(
  rider: Pick<RiderRecord, "serviceType" | "rotationDays" | "nextExpectedTravelDate">,
  todayIso: string,
): RotationDueKind | null {
  if (rider.serviceType !== "ContractCrew") return null;
  if (!rider.rotationDays || !rider.nextExpectedTravelDate) return null;
  const due = new Date(`${rider.nextExpectedTravelDate}T00:00:00`).getTime();
  const today = new Date(`${todayIso}T00:00:00`).getTime();
  if (Number.isNaN(due) || Number.isNaN(today)) return null;
  const diffDays = Math.round((due - today) / 86_400_000);
  if (diffDays < 0) return "over";
  if (diffDays <= 2) return "soon";
  return "ontime";
}
