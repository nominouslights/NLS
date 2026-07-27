import { request } from "./transport";
import type { ServiceType, StatusKind } from "../theme";
import { svcForServiceType, type ClientServiceType } from "./clients";
import type { DriverClearanceRecord } from "./drivers";

// ---------------------------------------------------------------------------
// Trips API client — contract owned by Backend/ (Trips module,
// TripPlanningEndpoints.cs). Shapes mirror the backend's TripResponse /
// RouteResponse / ScheduleTemplateResponse and the request records exactly
// (JSON camelCase, enums as PascalCase strings, TimeOnly as "HH:mm:ss",
// DateOnly as "yyyy-MM-dd"). Do not invent fields — extend only when the
// backend contract changes.
// The trip-manifest surface (listTripManifests, createTripManifest, …) lives
// at the end of this file; lib/api.ts re-exports it for legacy import sites.
// ---------------------------------------------------------------------------

/** Same enum as the Clients module's ServiceType (declared per-module backend-side). */
export type TripServiceType = ClientServiceType;

export type TripStatus = "Scheduled" | "InProgress" | "Completed" | "Cancelled";
export type TripDirection = "Outbound" | "Inbound";

/** One ordered stop on a trip's route snapshot (TripStopResponse / RouteStop).
 *  stopId / latitude / longitude are the enriched snapshot fields carried from
 *  the catalog Stop the route was built from — optional because legacy free-text
 *  stops (and the wizard's manual-trip fallback) have neither an id nor coords. */
export interface TripStop {
  name: string;
  order: number;
  stopId?: string;
  latitude?: number;
  longitude?: number;
}

/** Mirrors TripResponse — list and detail share the same shape. */
export interface TripRecord {
  id: string;
  tripNumber: string;
  serviceDate: string; // DateOnly, "2026-07-07"
  windowStart: string; // TimeOnly, "06:30:00"
  windowEnd: string | null;
  serviceType: TripServiceType;
  routeId: string | null;
  routeName: string;
  origin: string;
  destination: string;
  stops: TripStop[];
  distanceKm: number;
  scheduleTemplateId: string | null;
  roundTripKey: string | null;
  direction: TripDirection | null;
  isEmptyLeg: boolean;
  clientId: string | null;
  clientName: string | null;
  poNumber: string | null;
  driverId: string | null;
  driverName: string | null;
  /** Validated Fleet vehicle reference (mirrors driverId). Null when unassigned. */
  vehicleId: string | null;
  /** Server-side snapshot of the vehicle's unit number at assign time. */
  vehicleUnit: string | null;
  seatsCapacity: number | null;
  seatsConfirmed: number;
  seatsMinimum: number | null;
  demandGuaranteed: boolean;
  status: TripStatus;
  manifestId: string | null;
  hasPostTripInspection: boolean;
  completedAtUtc: string | null;
  cancelledReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** GET /api/trips query filters (TripFilter). */
export interface TripListParams {
  date?: string;
  from?: string;
  to?: string;
  status?: TripStatus;
  serviceType?: TripServiceType;
  clientId?: string;
  driverId?: string;
  openOnly?: boolean;
}

/** POST /api/trips body (CreateTripRequest). Supplying routeId snapshots the
 *  route's corridor fields server-side and the free-form ones are ignored;
 *  tripNumber is always generated server-side. */
export interface TripInput {
  serviceDate: string;
  windowStart: string; // "HH:mm"
  windowEnd?: string | null;
  serviceType: TripServiceType;
  routeId?: string | null;
  routeName?: string | null;
  origin?: string | null;
  destination?: string | null;
  stops?: TripStop[] | null;
  distanceKm: number;
  direction?: TripDirection | null;
  isEmptyLeg: boolean;
  clientId?: string | null;
  clientName?: string | null;
  poNumber?: string | null;
  driverId?: string | null;
  /** Validated Fleet vehicle reference; the backend snapshots the unit number. */
  vehicleId?: string | null;
  seatsCapacity?: number | null;
  seatsMinimum?: number | null;
}

/** PUT /api/trips/{id} body (UpdateTripRequest) — editable only while Scheduled. */
export interface TripUpdateInput {
  serviceDate: string;
  windowStart: string;
  windowEnd?: string | null;
  serviceType: TripServiceType;
  routeId?: string | null;
  routeName?: string | null;
  origin?: string | null;
  destination?: string | null;
  stops?: TripStop[] | null;
  distanceKm: number;
  isEmptyLeg: boolean;
  clientId?: string | null;
  clientName?: string | null;
  poNumber?: string | null;
  seatsCapacity?: number | null;
  seatsMinimum?: number | null;
}

// ---------------------------------------------------------------------------
// Trip endpoints
// ---------------------------------------------------------------------------

export function listTrips(params?: TripListParams): Promise<TripRecord[]> {
  const q = new URLSearchParams();
  if (params?.date) q.set("date", params.date);
  if (params?.from) q.set("from", params.from);
  if (params?.to) q.set("to", params.to);
  if (params?.status) q.set("status", params.status);
  if (params?.serviceType) q.set("serviceType", params.serviceType);
  if (params?.clientId) q.set("clientId", params.clientId);
  if (params?.driverId) q.set("driverId", params.driverId);
  if (params?.openOnly) q.set("openOnly", "true");
  const qs = q.toString();
  return request<TripRecord[]>(`/api/trips${qs ? `?${qs}` : ""}`);
}

export function getTrip(id: string): Promise<TripRecord> {
  return request<TripRecord>(`/api/trips/${id}`);
}

/** POST → 201 (TripCreatedResponse — { id } only; tripNumber comes from the
 *  projection on the next read). */
export async function createTrip(input: TripInput): Promise<string> {
  const res = await request<{ id: string }>("/api/trips", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateTrip(id: string, input: TripUpdateInput): Promise<void> {
  return request<void>(`/api/trips/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** POST /api/trips/{id}/assign — null driverId unassigns, null vehicleId clears
 *  the vehicle (the server snapshots the unit number from the looked-up vehicle). */
export function assignTrip(
  id: string,
  driverId: string | null,
  vehicleId: string | null,
): Promise<void> {
  return request<void>(`/api/trips/${id}/assign`, {
    method: "POST",
    body: JSON.stringify({ driverId, vehicleId }),
  });
}

export function changeTripStatus(id: string, status: TripStatus, reason?: string | null): Promise<void> {
  return request<void>(`/api/trips/${id}/status`, {
    method: "POST",
    body: JSON.stringify({ status, reason: reason ?? null }),
  });
}

export function recordTripDemand(
  id: string,
  seatsConfirmed: number,
  demandGuaranteed: boolean,
): Promise<void> {
  return request<void>(`/api/trips/${id}/demand`, {
    method: "POST",
    body: JSON.stringify({ seatsConfirmed, demandGuaranteed }),
  });
}

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------

/** Mirrors RouteResponse — origin/destination derived from first/last stop. */
export interface RouteRecord {
  id: string;
  name: string;
  stops: TripStop[];
  origin: string;
  destination: string;
  distanceKm: number;
  estimatedDurationMinutes: number;
  requiredLicenceClass: string | null;
  active: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/trips/routes body (CreateRouteRequest). Stops are selected from the
 *  Stop catalog by id, in corridor order; the backend loads each Stop, verifies
 *  it resolves and is active, then snapshots name + lat/lng into RouteRecord.stops. */
export interface RouteInput {
  name: string;
  stopIds: string[];
  distanceKm: number;
  estimatedDurationMinutes: number;
  requiredLicenceClass?: string | null;
}

export function listRoutes(): Promise<RouteRecord[]> {
  return request<RouteRecord[]>("/api/trips/routes");
}

/** POST → 201 { id }. */
export async function createRoute(input: RouteInput): Promise<string> {
  const res = await request<{ id: string }>("/api/trips/routes", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT — full row including active (UpdateRouteRequest). */
export function updateRoute(id: string, input: RouteInput & { active: boolean }): Promise<void> {
  return request<void>(`/api/trips/routes/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

// ---------------------------------------------------------------------------
// Schedule templates — a backend worker generates trips from ACTIVE templates
// (~every 30 min, GenerationHorizonDays ahead, default 7). Template edits
// affect not-yet-generated trips only.
// ---------------------------------------------------------------------------

export type DayName =
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday"
  | "Sunday";

/** Calendar order for the weekly grid (Mon-first, operations convention). */
export const WEEK_DAYS: DayName[] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

export const DAY_SHORT: Record<DayName, string> = {
  Monday: "MON",
  Tuesday: "TUE",
  Wednesday: "WED",
  Thursday: "THU",
  Friday: "FRI",
  Saturday: "SAT",
  Sunday: "SUN",
};

/** Mirrors ScheduleTemplateResponse. */
export interface ScheduleTemplateRecord {
  id: string;
  name: string;
  routeId: string;
  routeName: string | null;
  serviceType: TripServiceType;
  clientId: string | null;
  clientName: string | null;
  daysOfWeek: DayName[];
  departureTime: string; // TimeOnly, "06:30:00"
  returnDepartureTime: string | null;
  seatsCapacity: number;
  seatsMinimum: number | null;
  defaultVehicleUnit: string | null;
  defaultDriverId: string | null;
  generationHorizonDays: number;
  cutoffNote: string | null;
  active: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST/PUT /api/trips/schedule-templates body (Create/UpdateScheduleTemplateRequest).
 *  Active is NOT here — it changes via /activate and /deactivate. */
export interface ScheduleTemplateInput {
  name: string;
  routeId: string;
  serviceType: TripServiceType;
  clientId?: string | null;
  clientName?: string | null;
  daysOfWeek: DayName[];
  departureTime: string; // "HH:mm"
  returnDepartureTime?: string | null;
  seatsCapacity: number;
  seatsMinimum?: number | null;
  defaultVehicleUnit?: string | null;
  defaultDriverId?: string | null;
  generationHorizonDays?: number | null;
  cutoffNote?: string | null;
}

export function listScheduleTemplates(): Promise<ScheduleTemplateRecord[]> {
  return request<ScheduleTemplateRecord[]>("/api/trips/schedule-templates");
}

/** POST → 201 { id }. */
export async function createScheduleTemplate(input: ScheduleTemplateInput): Promise<string> {
  const res = await request<{ id: string }>("/api/trips/schedule-templates", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateScheduleTemplate(id: string, input: ScheduleTemplateInput): Promise<void> {
  return request<void>(`/api/trips/schedule-templates/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function setScheduleTemplateActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/trips/schedule-templates/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible. Shared helper lives in
// lib/api/drivers.ts (same backend pattern).
export { refetchUntil } from "./drivers";

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs
// the colour with a glyph and text label). "Open — needs coverage" and
// "Empty leg available" are frontend derivations, never persisted statuses.
// ---------------------------------------------------------------------------

/** Scheduled with no driver = open, needs coverage. */
export function isOpenTrip(t: TripRecord): boolean {
  return t.status === "Scheduled" && t.driverId === null;
}

/** Status chip (kind + label travel together), matching the prototype's chip
 *  semantics: open → gold, empty leg → gray, in progress → blue, completed →
 *  teal, cancelled → gray. */
export function tripChip(t: TripRecord): { kind: StatusKind; label: string } {
  switch (t.status) {
    case "InProgress":
      return { kind: "info", label: "In progress" };
    case "Completed":
      return { kind: "ontime", label: "Completed" };
    case "Cancelled":
      return { kind: "off", label: "Cancelled" };
    case "Scheduled":
    default:
      if (t.driverId === null) return { kind: "soon", label: "Open — needs coverage" };
      if (t.isEmptyLeg) return { kind: "off", label: "Empty leg available" };
      return { kind: "ontime", label: "Scheduled" };
  }
}

/** Backend ServiceType enum → the console's theme service key (svcMeta). */
export function svcForTrip(serviceType: TripServiceType): ServiceType {
  return svcForServiceType(serviceType);
}

/** Stop names in corridor order; falls back to origin → destination. */
export function stopNames(t: { stops: TripStop[]; origin: string; destination: string }): string[] {
  const names = [...t.stops].sort((a, b) => a.order - b.order).map((s) => s.name);
  if (names.length >= 2) return names;
  return [t.origin, t.destination].filter(Boolean);
}

export function corridorLabel(t: { stops: TripStop[]; origin: string; destination: string }): string {
  return stopNames(t).join("  →  ");
}

/** "06:30:00" → "06:30" (tolerates "06:30" input). */
export function hhmm(time: string | null): string {
  if (!time) return "—";
  return time.slice(0, 5);
}

/** "06:30 → 09:55" (or just the start when no end window). */
export function tripWindowLabel(t: { windowStart: string; windowEnd: string | null }): string {
  return t.windowEnd ? `${hhmm(t.windowStart)} → ${hhmm(t.windowEnd)}` : hhmm(t.windowStart);
}

/** Seats line for the manifest panel: "4 / 7" (capacity unknown → "4"). */
export function seatsLabel(t: TripRecord): string {
  return t.seatsCapacity != null ? `${t.seatsConfirmed} / ${t.seatsCapacity}` : String(t.seatsConfirmed);
}

/** True when the driver holds an unexpired clearance for the trip's client.
 *  Matching is by client name (the backend stores clearance clientName as free
 *  text); clearance checks are a UI-side warning in v1, never a hard block. */
export function hasClearanceFor(
  clearances: DriverClearanceRecord[],
  clientName: string | null,
): boolean {
  if (!clientName) return true; // no client on the trip — nothing to clear
  const today = todayIso();
  return clearances.some(
    (c) =>
      c.clientName.trim().toLowerCase() === clientName.trim().toLowerCase() &&
      (c.expiry === null || c.expiry >= today),
  );
}

/** Local-time ISO date (yyyy-MM-dd) — service dates are local operational days. */
export function todayIso(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function isoDaysFromToday(offset: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

/** "2026-07-07" → "Tue Jul 7" (list headers / detail lines). */
export function shortDateLabel(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-CA", { weekday: "short", month: "short", day: "numeric" });
}

/** List ordering — service date, then departure time, then trip number. */
export function sortTrips(rows: TripRecord[]): TripRecord[] {
  return [...rows].sort((a, b) => {
    if (a.serviceDate !== b.serviceDate) return a.serviceDate < b.serviceDate ? -1 : 1;
    if (a.windowStart !== b.windowStart) return a.windowStart < b.windowStart ? -1 : 1;
    return a.tripNumber.localeCompare(b.tripNumber);
  });
}

// ---------------------------------------------------------------------------
// Trip Manifest contract (Backend Trips module — TripManifestResponse /
// TripManifestInput, TripsEndpoints.cs). The manifest is now a SLIM, editable
// passenger + cargo manifest: creating/editing it does NOT change trip status.
// Weather/road/fuel/inspection/certification sections moved to the Fleet
// VehicleInspection records (lib/api/maintenance.ts). Shapes mirror the backend
// wire contract exactly (JSON camelCase, enums as PascalCase strings) — do not
// invent fields.
// ---------------------------------------------------------------------------

export type ManifestSource = "App" | "Dispatcher";
export type ManifestDirection = "Inbound" | "Outbound";
export type ManifestCargoSecured = "Yes" | "NotApplicable";

/** A manifest passenger. Pickup/dropoff reference the trip's route stops by id
 *  (with a snapshot name); free-form trips leave the ids null. */
export interface ManifestPassenger {
  name: string;
  contact?: string | null;
  pickupStopId: string | null;
  pickupStopName?: string | null;
  dropoffStopId: string | null;
  dropoffStopName?: string | null;
  idVerified: boolean;
  boardedOn: boolean;
  boardedOff: boolean;
}

export interface ManifestCargo {
  description: string;
  ownerRecipient?: string | null;
  weightKg?: number | null;
  chargeCad?: number | null;
  hazmat: boolean;
  secured: boolean;
}

/**
 * POST /api/trips/manifests and PUT /api/trips/manifests/{id} body. `enteredBy`
 * is required when `source === "Dispatcher"`.
 */
export interface TripManifestInput {
  tripDate: string; // DateOnly, "2026-07-15"
  tripNumber: string;
  route: string;
  direction: ManifestDirection | null;
  client: string | null;
  passengers: ManifestPassenger[];
  allSeatbeltsVerified: boolean;
  cargo: ManifestCargo[];
  allCargoSecured: ManifestCargoSecured | null;
  source: ManifestSource;
  enteredBy: string | null;
}

/** TripManifestResponse = the input fields + id / enteredAt / createdAtUtc. */
export interface TripManifest extends TripManifestInput {
  id: string;
  enteredAt: string | null;
  createdAtUtc: string;
}

/** POST → 201 { id } (Trips module — ManifestCreatedResponse). */
export function createTripManifest(input: TripManifestInput): Promise<{ id: string }> {
  return request<{ id: string }>("/api/trips/manifests", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** PUT → 204. The manifest is editable any time without changing trip status. */
export function updateTripManifest(id: string, input: TripManifestInput): Promise<void> {
  return request<void>(`/api/trips/manifests/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function listTripManifests(params?: { tripNumber?: string }): Promise<TripManifest[]> {
  const q = new URLSearchParams();
  if (params?.tripNumber) q.set("tripNumber", params.tripNumber);
  const qs = q.toString();
  return request<TripManifest[]>(`/api/trips/manifests${qs ? `?${qs}` : ""}`);
}

export function getTripManifest(id: string): Promise<TripManifest> {
  return request<TripManifest>(`/api/trips/manifests/${id}`);
}

// ---------------------------------------------------------------------------
// Trip activity / audit timeline (Backend Trips module — journaled domain
// events for the trip + its manifest). GET /api/trips/{id}/activity.
// ---------------------------------------------------------------------------

export interface TripActivityEntry {
  occurredAtUtc: string;
  aggregateType: "trip" | "trip-manifest";
  /** Kebab-case event type, e.g. "trip-scheduled", "trip-manifest-updated". */
  eventType: string;
  source: ManifestSource | null;
  enteredBy: string | null;
}

export function listTripActivity(id: string): Promise<TripActivityEntry[]> {
  return request<TripActivityEntry[]>(`/api/trips/${id}/activity`);
}
