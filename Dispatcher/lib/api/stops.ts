import { request } from "./transport";

// ---------------------------------------------------------------------------
// Stops API client — contract owned by Backend/ (Trips module, new Stop
// aggregate; TripPlanningEndpoints.cs). Shapes mirror the backend's
// StopResponse and the StopInput request record exactly (JSON camelCase). A
// Stop is a reusable catalog entry with a structured address + lat/lng that
// routes snapshot and (later) the Live Map consumes. Do not invent fields —
// extend only when the backend contract changes.
// ---------------------------------------------------------------------------

/** Backend stores stopType as a free-text string (a small enum server-side).
 *  This list is the console's dropdown vocabulary, not a wire enum. */
export const STOP_TYPES: string[] = [
  "Hub",
  "Community",
  "Airport",
  "MineSite",
  "PickupPoint",
  "Terminus",
];

/** Friendly labels for the stop-type vocabulary. Unknown values render as-is.
 *  Terminus is the odd one out: the others say what KIND of place a stop is,
 *  while Terminus says we have a standing business relationship with the venue.
 *  It is what the terminus summary report selects on, so it is set deliberately
 *  rather than inferred — an airport we merely pick up from stays an Airport. */
export const STOP_TYPE_LABELS: Record<string, string> = {
  Hub: "Hub",
  Community: "Community",
  Airport: "Airport",
  MineSite: "Mine site",
  PickupPoint: "Pickup point",
  Terminus: "Terminus (partner venue)",
};

export function stopTypeLabel(type: string | null): string {
  if (!type) return "Unspecified";
  return STOP_TYPE_LABELS[type] ?? type;
}

/** Mirrors StopResponse — list and detail share the same shape. City, province
 *  and country are required server-side; street and postal code are optional
 *  (Address.Create) and can come back null. */
export interface StopRecord {
  id: string;
  name: string;
  stopType: string | null;
  street: string | null;
  city: string;
  province: string;
  postalCode: string | null;
  country: string;
  latitude: number;
  longitude: number;
  notes: string | null;
  active: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST/PUT /api/trips/stops body (StopInput). Active is NOT here — it changes
 *  via /activate and /deactivate, like schedule templates. */
export interface StopInput {
  name: string;
  stopType?: string | null;
  street?: string | null;
  city: string;
  province: string;
  postalCode?: string | null;
  country: string;
  latitude: number;
  longitude: number;
  notes?: string | null;
}

export function listStops(): Promise<StopRecord[]> {
  return request<StopRecord[]>("/api/trips/stops");
}

/** POST → 201 { id } (id only; the row lands on the next projection read). */
export async function createStop(input: StopInput): Promise<string> {
  const res = await request<{ id: string }>("/api/trips/stops", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateStop(id: string, input: StopInput): Promise<void> {
  return request<void>(`/api/trips/stops/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setStopActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/trips/stops/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible. Shared helper lives in
// lib/api/drivers.ts (same backend pattern), mirroring lib/api/trips.ts.
export { refetchUntil } from "./drivers";

/** List ordering — alphabetical by name (catalog convention). */
export function sortStops(rows: StopRecord[]): StopRecord[] {
  return [...rows].sort((a, b) => a.name.localeCompare(b.name));
}

/** One-line address summary for list rows / detail headers. */
export function stopAddressLine(s: StopRecord): string {
  return [s.street, s.city, s.province, s.postalCode, s.country].filter(Boolean).join(", ");
}
