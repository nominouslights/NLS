import { ApiError, request } from "./transport";
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

export type TripStatus =
  | "Scheduled"
  | "InProgress"
  | "ReadyForBilling"
  | "Invoiced"
  | "Completed"
  | "Cancelled"
  | "WrittenOff";
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
  /** When the run itself ended (the old meaning of completedAtUtc). */
  operationsFinishedAtUtc: string | null;
  /** Now means "the money arrived" (payment confirmed) — or run end for clientless trips. */
  completedAtUtc: string | null;
  cancelledReason: string | null;
  /** Reason recorded when the trip was written off (closed without billing / invoice written off). */
  writtenOffReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  /** Billing state replicated from the Billing module; null when no worksheet claims it. */
  billing: TripBilling | null;
}

/** Wire values of TripBillingResponse.state (backend TripBillingStates). */
export type TripBillingState = "OnWorksheet" | "Invoiced" | "Paid" | "WrittenOff";

/** Mirrors the backend TripBillingResponse — a trip's claim by a billing worksheet. */
export interface TripBilling {
  state: TripBillingState;
  invoiceId: string;
  invoiceNumber: string;
  qboInvoiceId: string | null;
  qboEnteredDate: string | null;
  paymentConfirmedDate: string | null;
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
  /** A driver is on the trip — the counterpart of openOnly. */
  assignedOnly?: boolean;
  excludeCancelled?: boolean;
  /** 1-based. Paging is all-or-nothing: send both page and pageSize, or neither. */
  page?: number;
  pageSize?: number;
}

/**
 * The `GET /api/trips` envelope (backend `PagedResponse<TripResponse>`).
 * `page`/`pageSize` are null when the caller did not ask for a page — `items` is
 * then the complete match and `totalCount` is simply its length.
 */
export interface TripPage {
  items: TripRecord[];
  page: number | null;
  pageSize: number | null;
  totalCount: number;
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
  /** Ignored when vehicleId is set — the server snapshots the vehicle's
   *  seating capacity onto the trip instead. Manual capacity applies only to
   *  trips created without a fleet vehicle. */
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

/** Omitting page/pageSize returns every match in the same envelope — the server
 *  never applies a default page size, so whole-set callers are not truncated. */
export async function listTrips(params?: TripListParams): Promise<TripPage> {
  const q = new URLSearchParams();
  if (params?.date) q.set("date", params.date);
  if (params?.from) q.set("from", params.from);
  if (params?.to) q.set("to", params.to);
  if (params?.status) q.set("status", params.status);
  if (params?.serviceType) q.set("serviceType", params.serviceType);
  if (params?.clientId) q.set("clientId", params.clientId);
  if (params?.driverId) q.set("driverId", params.driverId);
  if (params?.openOnly) q.set("openOnly", "true");
  if (params?.assignedOnly) q.set("assignedOnly", "true");
  if (params?.excludeCancelled) q.set("excludeCancelled", "true");
  if (params?.page) q.set("page", String(params.page));
  if (params?.pageSize) q.set("pageSize", String(params.pageSize));
  const qs = q.toString();
  const res = await request<TripPage>(`/api/trips${qs ? `?${qs}` : ""}`);

  // Validate the envelope at the boundary rather than letting a wrong shape reach
  // a screen, where it surfaces as an undefined-length crash with no clue why.
  // The shape this catches in practice is a bare array — an API process still
  // running a build from before the paged list contract.
  if (!res || !Array.isArray(res.items)) {
    throw new ApiError(
      "Trips.List.UnexpectedShape",
      "The trips API returned an unexpected response. If it is running from an older build, restart it.",
      500,
    );
  }
  return res;
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
 *  the vehicle (the server snapshots the unit number from the looked-up vehicle).
 *  Assigning a vehicle also re-snapshots the trip's seats capacity from the
 *  vehicle; one seating fewer than the seats already confirmed is rejected
 *  ("Trips.Trip.VehicleCapacityBelowConfirmed"). Unassigning keeps the
 *  last-known capacity. */
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

/** POST /api/trips/{id}/status — the backend accepts ONLY these two transitions
 *  here; "Completed" now 409s (completion is billing-driven — see finishTripOperations). */
export function changeTripStatus(
  id: string,
  status: "InProgress" | "Cancelled",
  reason?: string | null,
): Promise<void> {
  return request<void>(`/api/trips/${id}/status`, {
    method: "POST",
    body: JSON.stringify({ status, reason: reason ?? null }),
  });
}

/** POST /api/trips/{id}/finish → 204 (no body). Ends the run: lands in
 *  ReadyForBilling when the trip has a client, straight in Completed when not. */
export function finishTripOperations(id: string): Promise<void> {
  return request<void>(`/api/trips/${id}/finish`, { method: "POST" });
}

/** POST /api/trips/{id}/close-without-billing → 204. Reason is required; only
 *  legal from ReadyForBilling, and refused with a 409 problem if a billing
 *  worksheet already claims the trip. Lands in WrittenOff. */
export function closeTripWithoutBilling(id: string, reason: string): Promise<void> {
  return request<void>(`/api/trips/${id}/close-without-billing`, {
    method: "POST",
    body: JSON.stringify({ reason }),
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

/** POST /api/trips/{id}/merge-round-trip → 204 — pairs this leg with
 *  `otherTripId` under a shared roundTripKey (Billing prices an
 *  Outbound+Inbound pair sharing a key as one round-trip line).
 *  With `allowMismatch` the backend skips the same-date and mirrored-corridor
 *  checks (same client / not cancelled / unpaired still enforced) and assigns
 *  direction chronologically — the earlier leg becomes Outbound. The flag is
 *  omitted from the body when false to keep the wire shape backward compatible. */
export function mergeRoundTrip(id: string, otherTripId: string, allowMismatch = false): Promise<void> {
  return request<void>(`/api/trips/${id}/merge-round-trip`, {
    method: "POST",
    body: JSON.stringify(allowMismatch ? { otherTripId, allowMismatch: true } : { otherTripId }),
  });
}

/** POST /api/trips/{id}/unpair-round-trip → 204 — clears the pairing on BOTH legs. */
export function unpairRoundTrip(id: string): Promise<void> {
  return request<void>(`/api/trips/${id}/unpair-round-trip`, {
    method: "POST",
  });
}

/** POST /api/trips/{id}/deadhead-return → { id } (same created-id shape as
 *  createTrip) — creates the reversed empty repositioning leg, already paired
 *  to this trip as its return. */
export async function createDeadheadReturn(id: string): Promise<string> {
  const res = await request<{ id: string }>(`/api/trips/${id}/deadhead-return`, {
    method: "POST",
  });
  return res.id;
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

/** How a schedule template recurs (backend RecurrenceKind enum, PascalCase names).
 *  Only the selected kind's fields are read server-side; the rest ride as empty/
 *  null defaults (daysOfWeek:[], intervalDays:null, anchorDate:null, daysOfMonth:[]). */
export type ScheduleRecurrenceKind = "DaysOfWeek" | "EveryNDays" | "MonthlyDays";

/** Frequency-picker labels — the human-facing names for each recurrence kind. */
export const RECURRENCE_LABELS: Record<ScheduleRecurrenceKind, string> = {
  DaysOfWeek: "Specific days of week",
  EveryNDays: "Every N days",
  MonthlyDays: "Monthly (day of month)",
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
  /** How this template recurs; gates which of the fields below are meaningful. */
  recurrenceKind: ScheduleRecurrenceKind;
  daysOfWeek: DayName[];
  /** EveryNDays: interval between generated trips; null for other kinds. */
  intervalDays: number | null;
  /** EveryNDays: DateOnly "yyyy-MM-dd" the interval counts from; null otherwise. */
  anchorDate: string | null;
  /** MonthlyDays: days of month (1–31, month-end clamps); [] for other kinds. */
  daysOfMonth: number[];
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
  /** Selected recurrence kind; only this kind's fields are read server-side. */
  recurrenceKind: ScheduleRecurrenceKind;
  /** DaysOfWeek: the selected weekdays. Send [] for other kinds. */
  daysOfWeek: DayName[];
  /** EveryNDays: interval ≥ 1. Send null for other kinds. */
  intervalDays?: number | null;
  /** EveryNDays: start date "yyyy-MM-dd". Send null for other kinds. */
  anchorDate?: string | null;
  /** MonthlyDays: days of month (1–31). Send [] for other kinds. */
  daysOfMonth: number[];
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

/** Human label for every persisted trip status — screens must never render (or
 *  lowercase) a raw enum name. */
export function tripStatusLabel(s: TripStatus): string {
  switch (s) {
    case "Scheduled":
      return "Scheduled";
    case "InProgress":
      return "In progress";
    case "ReadyForBilling":
      return "Ready for billing";
    case "Invoiced":
      return "Invoiced";
    case "Completed":
      return "Completed";
    case "Cancelled":
      return "Cancelled";
    case "WrittenOff":
      return "Written off";
    default:
      return s satisfies never;
  }
}

/** True once the run itself is over (or never ran) — everything except
 *  Scheduled and InProgress. "The run already happened" gates, not billing. */
export function isOperationallyClosed(t: TripRecord): boolean {
  return t.status !== "Scheduled" && t.status !== "InProgress";
}

/**
 * Billing worksheet chip — the SECOND axis alongside {@link tripChip}. Operational status
 * ("where is the bus / the money") and worksheet detail ("which invoice claims it") are
 * deliberately separate. Ready-for-billing is a real persisted trip status now, covered by
 * the operational chip — this one only speaks when a worksheet claims the trip. Returns
 * null when t.billing is null, so callers render no chip.
 */
export function tripBillingChip(t: TripRecord): { kind: StatusKind; label: string } | null {
  if (!t.billing) return null;
  switch (t.billing.state) {
    case "Paid":
      return { kind: "ontime", label: `Paid · ${t.billing.invoiceNumber}` };
    case "Invoiced":
      return { kind: "info", label: `Invoiced · ${t.billing.qboInvoiceId ?? t.billing.invoiceNumber}` };
    case "WrittenOff":
      return { kind: "over", label: `Written off · ${t.billing.invoiceNumber}` };
    case "OnWorksheet":
    default:
      return { kind: "soon", label: `On worksheet ${t.billing.invoiceNumber}` };
  }
}

/** True when invoicing this trip again would double-bill it. Keyed on the trip's
 *  own status now that the lifecycle is billing-driven; a Completed client trip
 *  is one whose payment already arrived. */
export function isTripBilled(t: TripRecord): boolean {
  return (
    t.status === "Invoiced" ||
    t.status === "WrittenOff" ||
    (t.status === "Completed" && t.clientId !== null)
  );
}

/** Status chip (kind + label travel together): open → gold, empty leg → gray,
 *  in progress → blue, ready for billing → gold, invoiced → blue, completed →
 *  teal, cancelled → gray, written off → vermillion. */
export function tripChip(t: TripRecord): { kind: StatusKind; label: string } {
  switch (t.status) {
    case "InProgress":
      return { kind: "info", label: "In progress" };
    case "ReadyForBilling":
      return { kind: "soon", label: "Ready for billing" };
    case "Invoiced":
      return { kind: "info", label: "Invoiced" };
    case "Completed":
      return { kind: "ontime", label: "Completed" };
    case "Cancelled":
      return { kind: "off", label: "Cancelled" };
    case "WrittenOff":
      return { kind: "over", label: "Written off" };
    case "Scheduled":
      if (t.driverId === null) return { kind: "soon", label: "Open — needs coverage" };
      if (t.isEmptyLeg) return { kind: "off", label: "Empty leg available" };
      return { kind: "ontime", label: "Scheduled" };
    default:
      // An unknown status must show itself, never masquerade as another state.
      return { kind: "off", label: t.status };
  }
}

/** UI mirror of the backend's round-trip pairing eligibility: a client trip,
 *  not cancelled or written off, not already paired. Gates MERGE INTO ROUND
 *  TRIP and (with an extra !isEmptyLeg check) CREATE DEADHEAD RETURN. */
export function canPairRoundTrip(t: TripRecord): boolean {
  return (
    t.clientId !== null &&
    t.roundTripKey === null &&
    t.status !== "Cancelled" &&
    t.status !== "WrittenOff"
  );
}

const normPlace = (s: string) => s.trim().toLowerCase();

/** What the strict matcher would object to about pairing `other` with `trip` —
 *  drives the manual-pairing mismatch warnings (each surfaced as colour + icon
 *  + text, never colour alone). Both false ⇒ `other` is a strict match. */
export function roundTripMismatch(
  trip: TripRecord,
  other: TripRecord,
): { differentDate: boolean; routeNotMirrored: boolean } {
  return {
    differentDate: other.serviceDate !== trip.serviceDate,
    routeNotMirrored:
      normPlace(other.origin) !== normPlace(trip.destination) ||
      normPlace(other.destination) !== normPlace(trip.origin),
  };
}

/** Merge candidates for `trip`, derived client-side from the already-loaded
 *  list: same client, same service date, mirrored corridor (their origin is
 *  our destination and vice versa, case-insensitive/trimmed), unpaired, not
 *  cancelled, and not the trip itself. */
export function roundTripMergeCandidates(trip: TripRecord, all: TripRecord[]): TripRecord[] {
  return sortTrips(
    all.filter((c) => {
      const mm = roundTripMismatch(trip, c);
      return (
        c.id !== trip.id &&
        canPairRoundTrip(c) &&
        c.clientId === trip.clientId &&
        !mm.differentDate &&
        !mm.routeNotMirrored
      );
    }),
  );
}

/** Manual-pairing candidates (allowMismatch merges): every unpaired,
 *  non-cancelled trip for the same client except the trip itself — no date or
 *  corridor requirement. Derived from the already-loaded list, so the reach is
 *  bounded by the screen's fetch window. */
export function roundTripManualCandidates(trip: TripRecord, all: TripRecord[]): TripRecord[] {
  return sortTrips(
    all.filter((c) => c.id !== trip.id && canPairRoundTrip(c) && c.clientId === trip.clientId),
  );
}

/** Human cadence line for a template, switching on its recurrence kind:
 *  - DaysOfWeek → weekday shorts in calendar order, e.g. "MON / WED / FRI".
 *  - EveryNDays → "Every 3 days from 2026-08-24" (singular "day" at 1).
 *  - MonthlyDays → "Monthly on 1, 15, 31" (a 31 clamps to month-end backend-side). */
export function recurrenceSummary(
  t: Pick<
    ScheduleTemplateRecord,
    "recurrenceKind" | "daysOfWeek" | "intervalDays" | "anchorDate" | "daysOfMonth"
  >,
): string {
  switch (t.recurrenceKind) {
    case "EveryNDays": {
      const n = t.intervalDays ?? 0;
      const unit = n === 1 ? "day" : "days";
      const from = t.anchorDate ? ` from ${t.anchorDate}` : "";
      return n > 0 ? `Every ${n} ${unit}${from}` : "Every N days — interval not set";
    }
    case "MonthlyDays": {
      const days = [...t.daysOfMonth].sort((a, b) => a - b);
      return days.length > 0 ? `Monthly on ${days.join(", ")}` : "Monthly — no days selected";
    }
    case "DaysOfWeek":
    default: {
      const ordered = WEEK_DAYS.filter((d) => t.daysOfWeek.includes(d)).map((d) => DAY_SHORT[d]);
      return ordered.length > 0 ? ordered.join(" / ") : "—";
    }
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
export type FarePaymentMethod = "Cash" | "Online" | "Waived";

/** A manifest passenger. Pickup/dropoff reference the trip's route stops by id
 *  (with a snapshot name); free-form trips leave the ids null. Fare fields are
 *  recorded per passenger just after the run — not reconciled to QuickBooks. */
export interface ManifestPassenger {
  name: string;
  email?: string | null;
  phone?: string | null;
  pickupStopId: string | null;
  pickupStopName?: string | null;
  dropoffStopId: string | null;
  dropoffStopName?: string | null;
  idVerified: boolean;
  boardedOn: boolean;
  boardedOff: boolean;
  fareAmountCad: number | null;
  farePaymentMethod: FarePaymentMethod | null;
  farePaidAtUtc: string | null;
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

/** TripManifestResponse = the input fields + id / enteredAt / createdAtUtc,
 *  plus server-computed fare rollups (recorded amounts — not reconciled to QBO). */
export interface TripManifest extends TripManifestInput {
  id: string;
  enteredAt: string | null;
  createdAtUtc: string;
  faresCollectedCad: number;
  faresPaidCount: number;
  faresWaivedCount: number;
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
