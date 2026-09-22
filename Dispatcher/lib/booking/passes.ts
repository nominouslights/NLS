// Booking passes — the ONE derivation that turns a booking detail into the pass
// sheet the BookingDetail screen renders, the NL-BP-01 document prints, and the
// passes email posts. All three consume the same BookingPassSheet so they can
// never disagree about a traveller, a seat number, or the payment line. Same
// contract as lib/billing/accruals.ts.
//
// Ground rules:
//   - One pass per BookingPassenger, in the order the booking lists them; the
//     seat label is "n of N" by position. Passes are keyed by INDEX, not
//     passenger id — MARK AS PAID is a full-replacement PUT that re-creates the
//     passenger rows with new ids, and the sheet must survive that.
//   - Passes may be issued (printed / emailed) ONLY once the booking is
//     Confirmed. Unconfirmed → not yet; Cancelled → never. Payment never gates
//     issuance — that rule lives here in passIssuance and nowhere else.
//   - Everything the email needs travels as pre-formatted strings: the
//     Notifications module never reads Booking data (integration-events-only).

import {
  locationLabel,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  type BookingDetailRecord,
  type BookingPaymentStatus,
  type BookingStatus,
  type BookingUpdateInput,
} from "@/lib/api/booking";
import type { BookingPassSheetPayload } from "@/lib/api/notifications";

// ---------------------------------------------------------------------------
// Shapes
// ---------------------------------------------------------------------------

export interface BookingPass {
  /** Position in the booking's passenger list (0-based) — the pass's identity. */
  index: number;
  /** "1 of 3" */
  seq: string;
  travellerName: string;
  phone: string | null;
  isBillingCustomer: boolean;
}

export interface BookingPassSheet {
  bookingId: string;
  reference: string;
  status: BookingStatus;
  paymentStatus: BookingPaymentStatus;
  paymentMethodLabel: string;
  paymentStatusLabel: string;
  /** "Tuesday, September 15, 2026" */
  serviceDateLabel: string;
  corridorName: string;
  pickupLabel: string;
  dropoffLabel: string;
  /** From the customer row when present, else the booking's name snapshot. */
  customer: { name: string; phone: string | null; email: string | null };
  notes: string | null;
  passes: BookingPass[];
  /** When this sheet was derived — printed in the document footer. */
  issuedAtLabel: string;
}

// ---------------------------------------------------------------------------
// Labels
// ---------------------------------------------------------------------------

/** "Tuesday, September 15, 2026" from a "YYYY-MM-DD" service date. Parsed at
 *  local midnight so the day never shifts across a UTC boundary. */
export function longServiceDateLabel(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-CA", { weekday: "long", year: "numeric", month: "long", day: "numeric" });
}

function issuedAtLabel(now: Date): string {
  return now.toLocaleString("en-CA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });
}

// ---------------------------------------------------------------------------
// Assembly
// ---------------------------------------------------------------------------

export function buildBookingPassSheet(detail: BookingDetailRecord, now: Date = new Date()): BookingPassSheet {
  const b = detail.booking;
  const total = b.passengers.length;
  return {
    bookingId: b.id,
    reference: b.reference,
    status: b.status,
    paymentStatus: b.paymentStatus,
    paymentMethodLabel: PAYMENT_METHOD_LABELS[b.paymentMethod],
    paymentStatusLabel: PAYMENT_STATUS_LABELS[b.paymentStatus],
    serviceDateLabel: longServiceDateLabel(b.serviceDate),
    corridorName: b.corridorName,
    pickupLabel: locationLabel(b.pickup),
    dropoffLabel: locationLabel(b.dropoff),
    customer: {
      name: detail.customer?.name ?? b.customerName,
      phone: detail.customer?.phone ?? null,
      email: detail.customer?.email ?? null,
    },
    notes: b.notes,
    passes: b.passengers.map((p, i) => ({
      index: i,
      seq: `${i + 1} of ${total}`,
      travellerName: p.name,
      phone: p.phone,
      isBillingCustomer: p.isBillingCustomer,
    })),
    issuedAtLabel: issuedAtLabel(now),
  };
}

// ---------------------------------------------------------------------------
// The gating rule — one place. The screen disables its PRINT / EMAIL buttons
// with `reason` as the adjacent hint; the print and email entry points never
// need to re-derive it.
// ---------------------------------------------------------------------------

export function passIssuance(detail: BookingDetailRecord): { allowed: boolean; reason: string | null } {
  switch (detail.booking.status) {
    case "Confirmed":
      return { allowed: true, reason: null };
    case "Cancelled":
      return { allowed: false, reason: "Cancelled — passes are void" };
    case "Unconfirmed":
    default:
      return { allowed: false, reason: "Confirm the booking to issue passes" };
  }
}

// ---------------------------------------------------------------------------
// Email payload — the wire sheet for POST /api/notifications/emails/
// booking-passes (and its preview). Strings only: the backend renders them
// verbatim into one bordered block per traveller.
// ---------------------------------------------------------------------------

export function passesEmailPayload(sheet: BookingPassSheet): BookingPassSheetPayload {
  return {
    reference: sheet.reference,
    serviceDate: sheet.serviceDateLabel,
    corridorName: sheet.corridorName,
    pickup: sheet.pickupLabel,
    dropoff: sheet.dropoffLabel,
    customerName: sheet.customer.name,
    paymentMethod: sheet.paymentMethodLabel,
    paymentStatus: sheet.paymentStatusLabel,
    notes: sheet.notes,
    travellers: sheet.passes.map((p) => ({ name: p.travellerName, phone: p.phone, seat: p.seq })),
  };
}

// ---------------------------------------------------------------------------
// MARK AS PAID — the backend flips payment only through the full-replacement
// PUT, so the update body echoes the booking as it stands with paymentStatus
// set to Paid. BookingLocation → BookingLocationInput drops nothing (same
// three fields); passenger rows are re-created server-side (new ids).
// ---------------------------------------------------------------------------

export function markPaidInput(detail: BookingDetailRecord): BookingUpdateInput {
  const b = detail.booking;
  return {
    pickup: { stopId: b.pickup.stopId, stopName: b.pickup.stopName, addressDetail: b.pickup.addressDetail },
    dropoff: { stopId: b.dropoff.stopId, stopName: b.dropoff.stopName, addressDetail: b.dropoff.addressDetail },
    passengers: b.passengers.map((p) => ({ name: p.name, phone: p.phone, isBillingCustomer: p.isBillingCustomer })),
    paymentMethod: b.paymentMethod,
    paymentStatus: "Paid",
    notes: b.notes,
  };
}
