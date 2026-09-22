// NL-BP-01 Booking Pass — composes the printable HTML from the section
// builders: ONE PASS PER PAGE so each traveller gets a hand-able sheet
// (reference large in mono, traveller + seat, date, corridor, pickup → drop-off,
// payment line, "present at boarding", footer).
//
// It renders the same BookingPassSheet the BookingDetail screen shows and the
// passes email posts (lib/booking/passes.ts) — one derivation, so the three can
// never disagree. The print tab's "Print / Save as PDF" toolbar is the download
// path; there is no PDF library.
//
// Gating (Confirmed only) is the caller's job via passIssuance — these
// functions render whatever sheet they are handed.

import type { BookingPassSheet } from "@/lib/booking/passes";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { BOOKING_PASS_STYLES } from "./styles";
import { passPage } from "./sections";

/** All passes, or only the passes at the given indexes (order preserved). */
export function bookingPassesHtml(sheet: BookingPassSheet, company: CompanyInfo, only?: number[]): string {
  const passes = only ? sheet.passes.filter((p) => only.includes(p.index)) : sheet.passes;
  return `
<style>${BOOKING_PASS_STYLES}</style>
<div class="bp">
  <div class="sheet">
    ${passes.map((p) => passPage(company, sheet, p)).join("")}
  </div>
</div>`;
}

export function printBookingPasses(sheet: BookingPassSheet): void {
  const title = `Booking Passes — ${sheet.reference} — ${sheet.corridorName} — ${sheet.serviceDateLabel}`;
  openPrintDocument(title, bookingPassesHtml(sheet, COMPANY));
}

export function printBookingPass(sheet: BookingPassSheet, index: number): void {
  const pass = sheet.passes.find((p) => p.index === index);
  const who = pass ? ` — ${pass.travellerName} (${pass.seq})` : "";
  const title = `Booking Pass — ${sheet.reference}${who}`;
  openPrintDocument(title, bookingPassesHtml(sheet, COMPANY, [index]));
}
