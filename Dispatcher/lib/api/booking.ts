import { request } from "./transport";
import type { StatusKind } from "../theme";

// ---------------------------------------------------------------------------
// Booking API client — contract owned by Backend/ (Booking module,
// BookingEndpoints). Shapes mirror the backend's responses exactly (JSON
// camelCase, enums as PascalCase strings, DateOnly as "YYYY-MM-DD"). Do not
// invent fields — extend only when the backend contract changes.
//
// All routes live under /api/booking. Corridors are a replica of Trips routes
// (corridorId equals the Trips routeId) fed by the trips.route-changed outbox
// event — the list may be EMPTY until each community route has been re-saved
// once after deploy.
//
// Mutations here are same-module transactional reads: a plain refetch after a
// mutation sees the write immediately (no refetchUntil needed, unlike the
// projection-backed Trips reads).
// ---------------------------------------------------------------------------

// --- customers (US-B.1/2) --------------------------------------------------

export interface CustomerRecord {
  id: string;
  name: string;
  phone: string | null;
  email: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/booking/customers / PUT /api/booking/customers/{id} body. */
export interface CustomerInput {
  name: string;
  phone?: string | null;
  email?: string | null;
  notes?: string | null;
}

/** Blank search = full roster; otherwise name substring OR phone digits substring. */
export function searchCustomers(search: string): Promise<CustomerRecord[]> {
  const qs = search ? `?search=${encodeURIComponent(search)}` : "";
  return request<CustomerRecord[]>(`/api/booking/customers${qs}`);
}

/** POST → 201 { id }. */
export async function createCustomer(input: CustomerInput): Promise<string> {
  const res = await request<{ id: string }>("/api/booking/customers", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function getCustomer(id: string): Promise<CustomerRecord> {
  return request<CustomerRecord>(`/api/booking/customers/${id}`);
}

export function updateCustomer(id: string, input: CustomerInput): Promise<void> {
  return request<void>(`/api/booking/customers/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

// --- corridors -------------------------------------------------------------

export interface CorridorRecord {
  corridorId: string; // equals the Trips routeId
  name: string;
  origin: string;
  destination: string;
  active: boolean;
}

export function listCorridors(): Promise<CorridorRecord[]> {
  return request<CorridorRecord[]>("/api/booking/corridors");
}

// --- calendar (US-B.6/7) ---------------------------------------------------

/** Day lifecycle (US-B.9–11): Unconfirmed until sold reaches the minimum,
 *  Confirmed once it does (a trip is created), Reverted when cancellations
 *  drop it back below minimum outside the cancellation window. An
 *  unmaterialized day reads as Unconfirmed. */
export type BookingDayStatus = "Unconfirmed" | "Confirmed" | "Reverted";

/** One date with booking activity — dates absent from the response are neutral. */
export interface CalendarDaySummary {
  date: string; // "YYYY-MM-DD"
  bookingDayId: string | null;
  tripId: string | null;
  tripNumber: string | null;
  status: BookingDayStatus;
  /** Gift-a-Seat pledge — the day runs even below minimum (never reverts). */
  minimumGuaranteed: boolean;
  bookingCount: number;
  sold: number;
  pending: number;
  capacity: number;
  remaining: number;
  passengerMinimum: number;
  neededToConfirm: number;
  hasOverrides: boolean;
}

export function getCalendarMonth(
  year: number,
  month: number, // 1-based, matching the wire
  corridorId: string,
): Promise<CalendarDaySummary[]> {
  return request<CalendarDaySummary[]>(
    `/api/booking/calendar?year=${year}&month=${month}&corridorId=${encodeURIComponent(corridorId)}`,
  );
}

// --- bookings (US-B.3/4/5/8) ----------------------------------------------

export type BookingStatus = "Unconfirmed" | "Confirmed" | "Cancelled";
export type BookingPaymentMethod = "Square" | "ETransfer" | "Cash";
export type BookingPaymentStatus = "Unpaid" | "Paid";

export interface BookingLocation {
  stopId: string | null;
  stopName: string | null;
  addressDetail: string | null;
}

/** Pickup/dropoff in create/update bodies — stopName required when stopId set;
 *  at least one of stopName/addressDetail present. */
export interface BookingLocationInput {
  stopId?: string | null;
  stopName?: string | null;
  addressDetail?: string | null;
}

/** Spec's "Rider" is BookingPassenger everywhere (the Trips Riders directory is
 *  a different, unrelated concept). */
export interface BookingPassengerRecord {
  id: string;
  name: string;
  phone: string | null;
  isBillingCustomer: boolean;
}

export interface BookingPassengerInput {
  name: string;
  phone?: string | null;
  isBillingCustomer: boolean;
}

export interface BookingRecord {
  id: string;
  customerId: string;
  customerName: string;
  corridorId: string;
  corridorName: string;
  serviceDate: string; // "YYYY-MM-DD"
  status: BookingStatus;
  pickup: BookingLocation;
  dropoff: BookingLocation;
  passengers: BookingPassengerRecord[];
  paymentMethod: BookingPaymentMethod;
  paymentStatus: BookingPaymentStatus;
  holdExpiresAtUtc: string;
  holdExpired: boolean;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** Day-panel detail — tripId/tripNumber stay null until the day confirms and
 *  Trips' backlink lands (the panel shows "no trip yet" meanwhile). */
export interface DayDetail {
  date: string;
  corridorId: string;
  corridorName: string;
  bookingDayId: string | null;
  tripId: string | null;
  tripNumber: string | null;
  status: BookingDayStatus;
  /** Gift-a-Seat pledge — the day runs even below minimum (never reverts). */
  minimumGuaranteed: boolean;
  passengerMinimumOverride: number | null;
  seatCapacityOverride: number | null;
  sold: number;
  pending: number;
  capacity: number;
  remaining: number;
  passengerMinimum: number;
  neededToConfirm: number;
  bookings: BookingRecord[];
}

export function getDayDetail(date: string, corridorId: string): Promise<DayDetail> {
  return request<DayDetail>(
    `/api/booking/days/${date}?corridorId=${encodeURIComponent(corridorId)}`,
  );
}

/** POST /api/booking/bookings body (≥1 passenger). */
export interface BookingInput {
  customerId: string;
  corridorId: string;
  serviceDate: string;
  pickup: BookingLocationInput;
  dropoff: BookingLocationInput;
  passengers: BookingPassengerInput[];
  paymentMethod: BookingPaymentMethod;
  notes?: string | null;
}

/** PUT /api/booking/bookings/{id} body → 204 (409 when cancelled). */
export interface BookingUpdateInput {
  pickup: BookingLocationInput;
  dropoff: BookingLocationInput;
  passengers: BookingPassengerInput[];
  paymentMethod: BookingPaymentMethod;
  paymentStatus: BookingPaymentStatus;
  notes?: string | null;
}

/** POST → 201 { id }. */
export async function createBooking(input: BookingInput): Promise<string> {
  const res = await request<{ id: string }>("/api/booking/bookings", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateBooking(id: string, input: BookingUpdateInput): Promise<void> {
  return request<void>(`/api/booking/bookings/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** 409 AlreadyConfirmed / CancelledIsReadOnly. */
export function confirmBooking(id: string): Promise<void> {
  return request<void>(`/api/booking/bookings/${id}/confirm`, { method: "POST" });
}

/** 409 AlreadyCancelled. */
export function cancelBooking(id: string): Promise<void> {
  return request<void>(`/api/booking/bookings/${id}/cancel`, { method: "POST" });
}

/** Gift-a-Seat (US-B.11): POST /api/booking/days/{id}/guarantee → 204.
 *  Sets MinimumGuaranteed and re-confirms a Reverted day; on a never-confirmed
 *  day it records the pledge only (the day still confirms via the normal
 *  threshold — the pledge then protects it from reverting). Idempotent;
 *  404 for an unknown day. */
export function guaranteeDay(bookingDayId: string): Promise<void> {
  return request<void>(`/api/booking/days/${encodeURIComponent(bookingDayId)}/guarantee`, {
    method: "POST",
  });
}

// --- settings (US-B.22/23) -------------------------------------------------

/** GET never 404s — the backend get-or-creates defaults (isPersisted says which). */
export interface BookingPolicy {
  cancellationWindowHours: number;
  earlyCancellationPenaltyCad: number;
  bookingCutoffHours: number;
  seatHoldMinutes: number;
  defaultPassengerMinimum: number;
  defaultSeatCapacity: number;
  isPersisted: boolean;
}

export interface CorridorSettingsRecord {
  corridorId: string;
  corridorName: string;
  passengerMinimum: number | null; // null = policy default applies
  seatCapacity: number | null;
}

export interface BookingSettings {
  policy: BookingPolicy;
  corridors: CorridorSettingsRecord[];
}

export function getBookingSettings(): Promise<BookingSettings> {
  return request<BookingSettings>("/api/booking/settings");
}

/** PUT /api/booking/settings/policy body — the six editable fields. */
export interface BookingPolicyInput {
  cancellationWindowHours: number;
  earlyCancellationPenaltyCad: number;
  bookingCutoffHours: number;
  seatHoldMinutes: number;
  defaultPassengerMinimum: number;
  defaultSeatCapacity: number;
}

/** [AdminOnly] — a non-Owner account gets a 403. */
export function updateBookingPolicy(input: BookingPolicyInput): Promise<void> {
  return request<void>("/api/booking/settings/policy", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** [AdminOnly] — null clears the per-corridor value back to the policy default. */
export function upsertCorridorSettings(
  corridorId: string,
  input: { passengerMinimum?: number | null; seatCapacity?: number | null },
): Promise<void> {
  return request<void>(`/api/booking/settings/corridors/${encodeURIComponent(corridorId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** [AdminOnly] per-date overrides — only when the day exists (bookingDayId non-null). */
export function setDayOverrides(
  bookingDayId: string,
  input: { passengerMinimum?: number | null; seatCapacity?: number | null },
): Promise<void> {
  return request<void>(`/api/booking/days/${encodeURIComponent(bookingDayId)}/overrides`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

// --- display helpers -------------------------------------------------------

/** Booking status → the protected status palette (colour + glyph + label, never
 *  colour alone): Unconfirmed = Gold, Confirmed = Teal, Cancelled = Vermillion. */
export function bookingStatusKind(status: BookingStatus): StatusKind {
  switch (status) {
    case "Confirmed":
      return "ontime";
    case "Cancelled":
      return "over";
    case "Unconfirmed":
    default:
      return "soon";
  }
}

/** Day lifecycle → the protected status palette (colour + glyph + text label,
 *  never colour alone): Unconfirmed = Gold, Confirmed = Teal, Reverted =
 *  Vermillion. */
export function dayStatusKind(status: BookingDayStatus): StatusKind {
  switch (status) {
    case "Confirmed":
      return "ontime";
    case "Reverted":
      return "over";
    case "Unconfirmed":
    default:
      return "soon";
  }
}

export const PAYMENT_METHOD_LABELS: Record<BookingPaymentMethod, string> = {
  Square: "Square",
  ETransfer: "e-Transfer",
  Cash: "Cash",
};

export const PAYMENT_STATUS_LABELS: Record<BookingPaymentStatus, string> = {
  Unpaid: "Unpaid",
  Paid: "Paid",
};

/** "Pickup → Dropoff" line for booking rows (stop name, else address detail). */
export function locationLabel(loc: BookingLocation): string {
  return loc.stopName ?? loc.addressDetail ?? "—";
}
