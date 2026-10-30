// BOOKING PASS (NL-BP-01) section builders. Pure string functions — no
// framework, no state. Each pass renders from the SAME BookingPassSheet the
// detail screen and the passes email consume (lib/booking/passes.ts), so the
// printed page can never disagree with the screen about a traveller or seat.

import type { BookingPass, BookingPassSheet } from "@/lib/booking/passes";
import type { CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";

// ---- letterhead (repeated on every pass — each page stands alone) -----------

export function header(company: CompanyInfo, sheet: BookingPassSheet): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">BOOKING PASS</div>
      <div class="s">${esc(sheet.corridorName)} · ${esc(sheet.serviceDateLabel)} · Form NL-BP-01</div>
    </div>
  </div>
  <div class="rule"></div>`;
}

// ---- the reference + seat block ----------------------------------------------

export function referenceBlock(sheet: BookingPassSheet, pass: BookingPass): string {
  return `
  <div class="refblock">
    <div>
      <div class="lbl">Booking reference</div>
      <div class="ref">${esc(sheet.reference)}</div>
    </div>
    <div class="seat">
      <div class="lbl">Seat</div>
      <div class="n">${esc(pass.seq)}</div>
    </div>
  </div>
  <div class="traveller">${esc(pass.travellerName)}</div>
  <div class="traveller-sub">${
    [pass.isBillingCustomer ? "Billing customer" : `Booked by ${sheet.customer.name}`, pass.phone]
      .filter(Boolean)
      .map(esc)
      .join(" &nbsp;•&nbsp; ")
  }</div>`;
}

// ---- trip details --------------------------------------------------------------

export function tripBlock(sheet: BookingPassSheet): string {
  return (
    sectionBar("Trip") +
    grid([
      field("Service date", sheet.serviceDateLabel, { wide: true }),
    ]) +
    grid([
      field("Corridor", sheet.corridorName),
      field("Pickup", sheet.pickupLabel),
      field("Drop-off", sheet.dropoffLabel),
      field("Travellers on booking", String(sheet.passes.length), { mono: true }),
    ])
  );
}

// ---- payment line --------------------------------------------------------------

export function paymentLine(sheet: BookingPassSheet): string {
  const paid = sheet.paymentStatus === "Paid";
  const glyph = paid ? "✓" : "◐";
  return `<div class="pay ${paid ? "paid" : "unpaid"}">${glyph} PAYMENT ${esc(
    sheet.paymentStatusLabel.toUpperCase(),
  )} &nbsp;·&nbsp; ${esc(sheet.paymentMethodLabel)}${
    paid ? "" : " — settle with the driver or office before boarding"
  }</div>`;
}

// ---- boarding + notes --------------------------------------------------------------

export function boardingBlock(sheet: BookingPassSheet): string {
  const notes = sheet.notes
    ? `<div class="notes"><b>Booking notes:</b> ${esc(sheet.notes)}</div>`
    : "";
  return `
  <div class="board">PRESENT THIS PASS AT BOARDING — one pass per traveller</div>
  ${notes}`;
}

// ---- footer --------------------------------------------------------------------------

export function footer(company: CompanyInfo, sheet: BookingPassSheet): string {
  return `
  <div class="foot">
    <b>${esc(company.name)}</b> · ${esc(company.phone)} · ${esc(company.email)}<br/>
    Issued ${esc(sheet.issuedAtLabel)} · Booking ${esc(sheet.reference)} · Form NL-BP-01.
    Community departures run once the passenger minimum is met; this pass confirms the seat
    on the booking above. Questions or changes: contact the office with the booking reference.
  </div>`;
}

/** One full pass page. */
export function passPage(company: CompanyInfo, sheet: BookingPassSheet, pass: BookingPass): string {
  return `
  <div class="pass">
    ${header(company, sheet)}
    ${referenceBlock(sheet, pass)}
    ${tripBlock(sheet)}
    ${paymentLine(sheet)}
    ${boardingBlock(sheet)}
    ${footer(company, sheet)}
  </div>`;
}
