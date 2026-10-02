// En-Route Defect Report section builders — the paper form of
// Man. Reg. 95/2008 s.17(2): a defect a driver finds AFTER the pre-trip is
// recorded and reported — a Major one without delay, a Minor one before the
// next inspection.
//
// A HAND-COMPLETION sheet. Only the trip's identity is prefilled (date, trip #,
// unit, driver, route); every observation, tick, time and signature is blank
// ruled paper. Nothing here reads an inspection record, computes anything, or
// guesses an answer — there is no platform record behind this sheet.

import type { TripRecord } from "@/lib/api/trips";
import type { CompanyInfo } from "@/lib/company";
import { box, esc, field, grid, sectionBar } from "../workOrderPdf/html";

/** Blank ruled defect rows. Enough for a bad day; the sheet stays one page. */
export const DEFECT_REPORT_ROWS = 6;

export const NO_DEFECTS_LABEL = "No defects found during this trip";

export const SEVERITY_INSTRUCTION =
  "Major defect: stop and do not drive until it is repaired — call dispatch immediately. " +
  "Minor defect: report to the carrier before the next inspection.";

function route(trip: TripRecord): string {
  const corridor = `${trip.origin} → ${trip.destination}`;
  return trip.routeName ? `${trip.routeName} (${corridor})` : corridor;
}

export function header(company: CompanyInfo): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">EN-ROUTE DEFECT REPORT</div>
      <div class="s">Form NL-PTI-01 · Defects found after the pre-trip</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">${esc(SEVERITY_INSTRUCTION)}</div>`;
}

/** Trip identity, prefilled; end-of-trip odometer and arrival time by hand. */
export function tripBlock(trip: TripRecord): string {
  return (
    sectionBar("Trip") +
    grid([
      field("Date", trip.serviceDate, { mono: true }),
      field("Trip #", trip.tripNumber, { mono: true }),
      field("Unit", trip.vehicleUnit, { mono: true }),
      field("Driver", trip.driverName),
    ]) +
    grid(
      [
        field("Route", route(trip)),
        field("Odometer at end of trip (km)", "", { mono: true }),
        field("Arrival time", "", { mono: true }),
      ],
      3,
    )
  );
}

/** The tick box and the ruled defect table. Always unticked, always blank. */
export function defectsBlock(): string {
  const rows = Array.from(
    { length: DEFECT_REPORT_ROWS },
    () => `<tr>
      <td class="time">&nbsp;</td>
      <td class="loc">&nbsp;</td>
      <td class="sys">&nbsp;</td>
      <td>&nbsp;</td>
      <td class="sev">${box(false)} Minor<br/>${box(false)} Major</td>
      <td class="rep">&nbsp;</td>
    </tr>`,
  ).join("");
  return (
    sectionBar("Defects found en route") +
    `<div class="none">${box(false)} ${esc(NO_DEFECTS_LABEL)}</div>
     <div class="note">Tick the box above if nothing was found. Otherwise record each defect on its own row, when you notice it.</div>
     <table>
       <thead><tr>
         <th class="time">Time noticed</th>
         <th class="loc">Location (town / highway km)</th>
         <th class="sys">Item / system</th>
         <th>Describe the defect</th>
         <th class="sev">Severity</th>
         <th class="rep">Reported to (name) &amp; time</th>
       </tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

function signLine(left: string, right: string): string {
  return `<div class="sign">
    <div><div class="sigval">&nbsp;</div><div class="sigline">${esc(left)}</div></div>
    <div><div class="sigval">&nbsp;</div><div class="sigline">${esc(right)}</div></div>
  </div>`;
}

export function signatureBlock(): string {
  return (
    sectionBar("Driver") +
    `<div class="certbox">
       I have recorded every defect I found or was told about during this trip.
       ${signLine("Driver signature", "Date / time")}
     </div>` +
    sectionBar("Received by (carrier / dispatch)") +
    `<div class="certbox">
       ${signLine("Received by (carrier / dispatch) — signature", "Date / time")}
     </div>`
  );
}

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    Man. Reg. 95/2008 s.17(2) (defects found after the trip inspection) · National Safety Code Standard 13
  </div>`;
}
