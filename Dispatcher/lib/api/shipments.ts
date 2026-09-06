import { ApiError, request } from "./transport";
import type { StatusKind } from "../theme";

// ---------------------------------------------------------------------------
// Shipments API client — contract owned by Backend/ (Trips module,
// ShipmentEndpoints.cs under /api/trips/shipments). Shapes mirror the
// backend's ShipmentResponse / ShipmentLegResponse / ShipmentPageResponse and
// the request records exactly (JSON camelCase, enums as PascalCase strings,
// DateOnly as "yyyy-MM-dd"). Do not invent fields — extend only when the
// backend contract changes.
//
// A shipment's clientId/clientName are the shipment's OWN payer; each leg
// carries the TRIP's client. They routinely differ (a run for Alamos full of
// Incline Group's freight) — show both, never derive one from the other.
// ---------------------------------------------------------------------------

export type ShipmentStatus =
  | "Registered"
  | "Assigned"
  | "InTransit"
  | "Delivered"
  | "ReadyForBilling"
  | "Invoiced"
  | "Settled"
  | "Cancelled"
  | "WrittenOff";

export type ShipmentKind = "Parcel" | "Grocery" | "Freight";
export type ShipmentLegStatus = "Planned" | "PickedUp" | "Dropped";
export type ShipmentPaymentMethod = "Cash" | "Online" | "Waived";
export type ShipmentSource = "App" | "Dispatcher";

export const SHIPMENT_KINDS: ShipmentKind[] = ["Parcel", "Grocery", "Freight"];

export const SHIPMENT_KIND_LABELS: Record<ShipmentKind, string> = {
  Parcel: "Parcel",
  Grocery: "Grocery",
  Freight: "Freight",
};

/** One trip the shipment rides (ShipmentLegResponse). tripClientName is the
 *  TRIP's client, joined at read time — the shipment's payer lives on the
 *  shipment itself. */
export interface ShipmentLegRecord {
  id: string;
  sequence: number;
  tripId: string;
  tripNumber: string;
  tripServiceDate: string; // DateOnly, "2026-08-27"
  tripClientId: string | null;
  tripClientName: string | null;
  fromStopId: string | null;
  fromName: string;
  toStopId: string | null;
  toName: string;
  status: ShipmentLegStatus;
  isFinalLeg: boolean;
  onwardTripNumber: string | null;
  assignedAtUtc: string;
  pickedUpAtUtc: string | null;
  pickedUpBy: string | null;
  droppedAtUtc: string | null;
  droppedBy: string | null;
}

/** Mirrors ShipmentResponse — list and detail share the same shape. */
export interface ShipmentRecord {
  id: string;
  shipmentNumber: string;
  status: ShipmentStatus;
  kind: ShipmentKind;
  clientId: string | null;
  clientName: string | null;
  poNumber: string | null;
  consignorName: string | null;
  consignorContact: string | null;
  consigneeName: string | null;
  consigneeContact: string | null;
  originStopId: string | null;
  originName: string;
  destinationStopId: string | null;
  destinationName: string;
  description: string;
  pieces: number;
  weightKg: number | null;
  lengthCm: number | null;
  widthCm: number | null;
  heightCm: number | null;
  hazmat: boolean;
  declaredValueCad: number | null;
  specialHandling: string | null;
  secured: boolean;
  chargeCad: number | null;
  paymentMethod: ShipmentPaymentMethod | null;
  paymentCollectedAtUtc: string | null;
  readyDate: string | null;
  requiredByDate: string | null;
  legs: ShipmentLegRecord[];
  /** Freight dropped at a hub with an onward leg still planned — real,
   *  locatable inventory a dispatcher must be able to see. */
  awaitingTransfer: boolean;
  deliveredAtUtc: string | null;
  receivedBy: string | null;
  deliveryNote: string | null;
  cancelledReason: string | null;
  writtenOffReason: string | null;
  source: ShipmentSource;
  enteredBy: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** The GET /api/trips/shipments envelope (ShipmentPageResponse). */
export interface ShipmentPage {
  items: ShipmentRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** GET /api/trips/shipments query filters (the endpoint's query args). */
export interface ShipmentListParams {
  status?: ShipmentStatus;
  kind?: ShipmentKind;
  unassignedOnly?: boolean;
  tripId?: string;
  tripNumber?: string;
  clientId?: string;
  clientless?: boolean;
  awaitingDelivery?: boolean;
  awaitingTransfer?: boolean;
  billable?: boolean;
  from?: string; // DateOnly, "yyyy-MM-dd"
  to?: string;
  q?: string;
  page?: number;
  pageSize?: number;
}

/** POST /api/trips/shipments and PUT /{id} body (RegisterShipmentRequest).
 *  clientId is the shipment's own payer — the client NAME is never sent; the
 *  backend snapshots it from the client lookup replica. */
export interface ShipmentInput {
  description: string;
  kind?: ShipmentKind | null; // defaults to Parcel server-side
  pieces?: number | null; // defaults to 1 server-side
  weightKg?: number | null;
  lengthCm?: number | null;
  widthCm?: number | null;
  heightCm?: number | null;
  hazmat?: boolean | null;
  declaredValueCad?: number | null;
  specialHandling?: string | null;
  consignorName?: string | null;
  consignorContact?: string | null;
  consigneeName?: string | null;
  consigneeContact?: string | null;
  originStopId?: string | null;
  originName?: string | null;
  destinationStopId?: string | null;
  destinationName?: string | null;
  readyDate?: string | null;
  requiredByDate?: string | null;
  clientId?: string | null;
  poNumber?: string | null;
  chargeCad?: number | null;
  paymentMethod?: ShipmentPaymentMethod | null;
  source: ShipmentSource;
  enteredBy?: string | null;
}

/** POST /{id}/legs body (AddShipmentLegRequest). A leg says which run moves
 *  the goods — it deliberately carries no client field. */
export interface ShipmentLegInput {
  tripId: string;
  fromStopId?: string | null;
  fromName?: string | null;
  toStopId?: string | null;
  toName?: string | null;
}

/** POST /bulk-assign response (BulkAssignResult) — deliberately not atomic:
 *  each failure is reported with the error the domain gave. */
export interface BulkAssignResult {
  assigned: number;
  failures: { shipmentId: string; code: string; message: string }[];
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export async function listShipments(params?: ShipmentListParams): Promise<ShipmentPage> {
  const q = new URLSearchParams();
  if (params?.status) q.set("status", params.status);
  if (params?.kind) q.set("kind", params.kind);
  if (params?.unassignedOnly) q.set("unassignedOnly", "true");
  if (params?.tripId) q.set("tripId", params.tripId);
  if (params?.tripNumber) q.set("tripNumber", params.tripNumber);
  if (params?.clientId) q.set("clientId", params.clientId);
  if (params?.clientless) q.set("clientless", "true");
  if (params?.awaitingDelivery) q.set("awaitingDelivery", "true");
  if (params?.awaitingTransfer) q.set("awaitingTransfer", "true");
  if (params?.billable) q.set("billable", "true");
  if (params?.from) q.set("from", params.from);
  if (params?.to) q.set("to", params.to);
  if (params?.q) q.set("q", params.q);
  if (params?.page) q.set("page", String(params.page));
  if (params?.pageSize) q.set("pageSize", String(params.pageSize));
  const qs = q.toString();
  const res = await request<ShipmentPage>(`/api/trips/shipments${qs ? `?${qs}` : ""}`);

  // Boundary guard, mirroring listTrips: catch a wrong envelope here rather
  // than letting it surface as an undefined-length crash in a screen.
  if (!res || !Array.isArray(res.items)) {
    throw new ApiError(
      "Trips.Shipments.UnexpectedShape",
      "The shipments API returned an unexpected response. If it is running from an older build, restart it.",
      500,
    );
  }
  return res;
}

export function getShipment(id: string): Promise<ShipmentRecord> {
  return request<ShipmentRecord>(`/api/trips/shipments/${id}`);
}

/** POST → 201 (ShipmentCreatedResponse — { id } only; the row lands on the
 *  next projection read). */
export async function registerShipment(input: ShipmentInput): Promise<string> {
  const res = await request<{ id: string }>("/api/trips/shipments", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateShipment(id: string, input: ShipmentInput): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** POST /{id}/legs → 204. One leg per trip; a second leg is a hub transfer. */
export function addShipmentLeg(id: string, input: ShipmentLegInput): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/legs`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** DELETE /{id}/legs/{sequence} → 204. */
export function removeShipmentLeg(id: string, sequence: number): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/legs/${sequence}`, {
    method: "DELETE",
  });
}

/** POST /{id}/legs/{sequence}/pickup → 204 (LegEventRequest — both optional;
 *  the backend stamps its own clock when atUtc is null). */
export function recordShipmentLegPickup(
  id: string,
  sequence: number,
  by?: string | null,
): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/legs/${sequence}/pickup`, {
    method: "POST",
    body: JSON.stringify({ atUtc: null, by: by ?? null }),
  });
}

/** POST /{id}/legs/{sequence}/drop → 204. */
export function recordShipmentLegDrop(
  id: string,
  sequence: number,
  by?: string | null,
): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/legs/${sequence}/drop`, {
    method: "POST",
    body: JSON.stringify({ atUtc: null, by: by ?? null }),
  });
}

/** POST /bulk-assign → 200 BulkAssignResult (per-shipment failures reported,
 *  not atomic). No UI yet — deferred deliberately; the call exists so the
 *  contract is covered. */
export function bulkAssignShipments(tripId: string, shipmentIds: string[]): Promise<BulkAssignResult> {
  return request<BulkAssignResult>("/api/trips/shipments/bulk-assign", {
    method: "POST",
    body: JSON.stringify({ tripId, shipmentIds }),
  });
}

/** POST /{id}/deliver → 204 (DeliverShipmentRequest — all fields optional). */
export function deliverShipment(
  id: string,
  opts?: { receivedBy?: string | null; note?: string | null },
): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/deliver`, {
    method: "POST",
    body: JSON.stringify({ atUtc: null, receivedBy: opts?.receivedBy ?? null, note: opts?.note ?? null }),
  });
}

/** POST /{id}/secured → 204 (SetSecuredRequest). */
export function setShipmentSecured(id: string, secured: boolean): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/secured`, {
    method: "POST",
    body: JSON.stringify({ secured }),
  });
}

/** PUT /{id}/billing → 204 (SetShipmentBillingRequest). No UI yet — billing
 *  edits are deferred deliberately; the call exists so the contract is covered. */
export function setShipmentBilling(
  id: string,
  input: {
    clientId?: string | null;
    poNumber?: string | null;
    chargeCad?: number | null;
    paymentMethod?: ShipmentPaymentMethod | null;
  },
): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/billing`, {
    method: "PUT",
    body: JSON.stringify({
      clientId: input.clientId ?? null,
      poNumber: input.poNumber ?? null,
      chargeCad: input.chargeCad ?? null,
      paymentMethod: input.paymentMethod ?? null,
    }),
  });
}

/** POST /{id}/cancel → 204 (ReasonRequest — reason optional). */
export function cancelShipment(id: string, reason?: string | null): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/cancel`, {
    method: "POST",
    body: JSON.stringify({ reason: reason ?? null }),
  });
}

/** POST /{id}/close-without-billing → 204 (reason required). No UI yet —
 *  deferred deliberately alongside billing edits. */
export function closeShipmentWithoutBilling(id: string, reason: string): Promise<void> {
  return request<void>(`/api/trips/shipments/${id}/close-without-billing`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible (same backend pattern as trips).
export { refetchUntil } from "./drivers";

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs
// the colour with a glyph and text label).
// ---------------------------------------------------------------------------

/** Human label for every persisted shipment status — screens must never render
 *  a raw enum name. */
export function shipmentStatusLabel(s: ShipmentStatus): string {
  switch (s) {
    case "Registered":
      return "Registered";
    case "Assigned":
      return "Assigned";
    case "InTransit":
      return "In transit";
    case "Delivered":
      return "Delivered";
    case "ReadyForBilling":
      return "Ready for billing";
    case "Invoiced":
      return "Invoiced";
    case "Settled":
      return "Settled";
    case "Cancelled":
      return "Cancelled";
    case "WrittenOff":
      return "Written off";
    default:
      return s satisfies never;
  }
}

/** Status chip (kind + label travel together): registered → gold (waiting for
 *  routing), assigned → blue, in transit → teal ("moving" = good), delivered /
 *  settled → teal, ready for billing → gold, invoiced → blue, cancelled →
 *  gray, written off → vermillion. */
export function shipmentChip(s: ShipmentRecord): { kind: StatusKind; label: string } {
  if (s.awaitingTransfer) {
    // At-a-hub inventory outranks the raw status — invisible freight is the
    // dispatcher's nightmare scenario.
    return { kind: "soon", label: "At hub — awaiting transfer" };
  }
  switch (s.status) {
    case "Registered":
      return { kind: "soon", label: "Registered — needs a trip" };
    case "Assigned":
      return { kind: "info", label: "Assigned" };
    case "InTransit":
      return { kind: "ontime", label: "In transit" };
    case "Delivered":
      return { kind: "ontime", label: "Delivered" };
    case "ReadyForBilling":
      return { kind: "soon", label: "Ready for billing" };
    case "Invoiced":
      return { kind: "info", label: "Invoiced" };
    case "Settled":
      return { kind: "ontime", label: "Settled" };
    case "Cancelled":
      return { kind: "off", label: "Cancelled" };
    case "WrittenOff":
      return { kind: "over", label: "Written off" };
    default:
      // An unknown status must show itself, never masquerade as another state.
      return { kind: "off", label: s.status };
  }
}

/** Leg chip: planned → gold (still to be picked up), picked up → blue (on the
 *  vehicle), dropped → teal. */
export function shipmentLegChip(l: ShipmentLegRecord): { kind: StatusKind; label: string } {
  switch (l.status) {
    case "PickedUp":
      return { kind: "info", label: "Picked up" };
    case "Dropped":
      return { kind: "ontime", label: "Dropped" };
    case "Planned":
    default:
      return { kind: "soon", label: "Planned" };
  }
}

/** True while the shipment is still operationally open (routable/deliverable). */
export function isShipmentOpen(s: ShipmentRecord): boolean {
  return s.status === "Registered" || s.status === "Assigned" || s.status === "InTransit";
}

/** "12 kg · 40×30×20 cm" summary line; empty string when nothing is recorded. */
export function shipmentDimsLabel(s: ShipmentRecord): string {
  const parts: string[] = [];
  if (s.weightKg != null) parts.push(`${s.weightKg} kg`);
  if (s.lengthCm != null || s.widthCm != null || s.heightCm != null) {
    parts.push(`${s.lengthCm ?? "—"}×${s.widthCm ?? "—"}×${s.heightCm ?? "—"} cm`);
  }
  return parts.join(" · ");
}
