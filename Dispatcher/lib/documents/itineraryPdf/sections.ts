// Trip Itinerary section builders — the stop-by-stop sheet the driver runs the
// day from.
//
// TWO RULES THIS FILE EXISTS TO HOLD:
//
// 1. Scheduled times come from `stopTimeOnTrip()` in lib/timetable.ts and
//    NOWHERE else. A route stores minutes-after-departure offsets, not clock
//    times, and that helper is the single place that resolves an offset against
//    the trip's own departure and direction. No time arithmetic here.
//
// 2. THERE ARE NO PER-STOP TIMESTAMPS IN THIS PLATFORM. Actual arrive, actual
//    depart, passengers on/off and per-stop notes are blank ruled cells on
//    every printed sheet, and the sheet says so out loud. Do not add a column
//    that looks like the system will fill it in, and do not invent a field to
//    back one.

import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripRecord } from "@/lib/api/trips";
import type { CompanyInfo } from "@/lib/company";
import { hasTimetable, stopTimeOnTrip } from "@/lib/timetable";
import { box, check, esc, field, grid, sectionBar } from "../workOrderPdf/html";

/** Blank stop rows on a trip carrying fewer stops than this (a manual trip). */
const MIN_STOP_ROWS = 6;

export const NO_TIMETABLE_NOTE =
  "This route carries no timetable — the trip window above is the only scheduled time.";

export const PAPER_ACTUALS_NOTE =
  "Per-stop actuals are recorded on paper — the platform holds whole-trip timing only.";

// ---- small formatters -------------------------------------------------------

/** "06:30:00" → "06:30". A TimeOnly always leads with HH:MM; anything else
 *  prints as it arrived rather than being reshaped by guesswork. */
function hhmm(time: string | null | undefined): string {
  if (!time) return "";
  return /^\d{2}:\d{2}/.test(time) ? time.slice(0, 5) : time;
}

function tripWindow(trip: TripRecord): string {
  const start = hhmm(trip.windowStart);
  const end = hhmm(trip.windowEnd);
  return end ? `${start}–${end}` : start;
}

function km(v: number | null | undefined): string {
  return v == null ? "" : `${v.toLocaleString("en-CA")} km`;
}

function kg(v: number | null | undefined): string {
  return v == null ? "" : `${v.toLocaleString("en-CA")} kg`;
}

/** An inline run of ☐/☒ option checkboxes inside a labelled field cell. */
function checkField(label: string, checks: string): string {
  return `<div class="fld"><div class="lbl">${esc(label)}</div><div class="val">${checks}</div></div>`;
}

// ---- header ------------------------------------------------------------------

export function header(company: CompanyInfo, trip: TripRecord): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">TRIP ITINERARY</div>
      <div class="s">Trip ${esc(trip.tripNumber)}</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">Carry this sheet on the run. Record every actual time, count and note by hand as it happens.</div>`;
}

export function tripInfoBlock(trip: TripRecord): string {
  const direction =
    check(trip.direction === "Outbound", "Outbound") + check(trip.direction === "Inbound", "Inbound");
  const clientLine = [trip.clientName, trip.poNumber ? `PO ${trip.poNumber}` : null]
    .filter(Boolean)
    .join(" · ");
  return (
    sectionBar("Trip") +
    grid([
      field("Trip #", trip.tripNumber, { mono: true }),
      field("Service Date", trip.serviceDate, { mono: true }),
      field("Window", tripWindow(trip), { mono: true }),
      field("Service Type", trip.serviceType),
    ]) +
    grid([
      field("Client / PO", clientLine),
      field("Driver", trip.driverName),
      field("Unit", trip.vehicleUnit, { mono: true }),
      field("Distance", km(trip.distanceKm), { mono: true }),
    ]) +
    grid([field("Route", trip.routeName), checkField("Direction", direction)], 2) +
    grid([field("Corridor", `${trip.origin} → ${trip.destination}`, { wide: true })])
  );
}

// ---- stops -------------------------------------------------------------------

/**
 * The stop table.
 *
 * Scheduled resolves through `stopTimeOnTrip`; a stop the helper cannot resolve
 * — and every stop on a route with no timetable at all — prints an em dash,
 * which is exactly the trip-level fallback the helper's doc comment specifies.
 * Actual Arrive / Actual Depart / Pax On/Off / Notes are always blank.
 */
export function stopsBlock(trip: TripRecord): string {
  const timetabled = hasTimetable(trip);
  const stops = [...trip.stops].sort((a, b) => a.order - b.order);
  const rowCount = Math.max(MIN_STOP_ROWS, stops.length);

  const rows = Array.from({ length: rowCount }, (_, i) => {
    const s = stops[i] ?? null;
    const scheduled = s && timetabled ? (stopTimeOnTrip(trip, s.stopId, s.name) ?? "—") : s ? "—" : "";
    return `<tr>
      <td class="num">${i + 1}</td>
      <td>${esc(s?.name) || "&nbsp;"}</td>
      <td class="time">${esc(scheduled) || "&nbsp;"}</td>
      <td class="hand blank">&nbsp;</td>
      <td class="hand blank">&nbsp;</td>
      <td class="hand blank">&nbsp;</td>
      <td class="blank">&nbsp;</td>
    </tr>`;
  }).join("");

  const timetableNote = timetabled
    ? ""
    : `<div class="note"><b>No timetable.</b> ${esc(NO_TIMETABLE_NOTE)}</div>`;

  return (
    sectionBar("Stops") +
    `<div class="note">${esc(PAPER_ACTUALS_NOTE)}</div>` +
    timetableNote +
    `<table>
       <thead><tr>
         <th class="num">#</th><th>Stop</th><th class="time">Scheduled</th>
         <th class="hand">Actual Arrive</th><th class="hand">Actual Depart</th>
         <th class="hand">Pax On/Off</th><th>Notes</th>
       </tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

// ---- freight -----------------------------------------------------------------

/**
 * The freight block, from the shipments riding this trip.
 *
 * OMITTED ENTIRELY when there are none — which is the normal case for a
 * passenger run, whose incidental cargo is already on the manifest's §3. An
 * empty freight table on every passenger itinerary would be noise, and worse,
 * would read as "no freight was loaded" rather than "freight does not apply".
 */
export function freightBlock(shipments: ShipmentRecord[]): string {
  if (shipments.length === 0) return "";

  const rows = shipments
    .map(
      (s) => `<tr>
      <td>${esc(s.shipmentNumber)}</td>
      <td>${esc(s.description) || "&nbsp;"}</td>
      <td class="num">${esc(s.pieces)}</td>
      <td>${esc(kg(s.weightKg)) || "&nbsp;"}</td>
      <td>${esc(s.originName)} → ${esc(s.destinationName)}</td>
      <td class="ck">${box(s.hazmat)}</td>
      <td class="hand blank">&nbsp;</td>
      <td class="blank">&nbsp;</td>
    </tr>`,
    )
    .join("");

  return (
    sectionBar("Freight") +
    `<div class="note">Confirm each piece against its waybill at pickup and at delivery. Delivered / Received by are signed on this sheet.</div>
     <table>
       <thead><tr>
         <th>Shipment #</th><th>Description</th><th class="num">Pcs</th><th>Weight</th>
         <th>Origin → Destination</th><th class="ck">Hazmat</th>
         <th class="hand">Delivered</th><th>Received By</th>
       </tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

// ---- footer ------------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    Hand this sheet in with the trip manifest at the end of the run.
  </div>`;
}
