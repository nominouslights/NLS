// TERMINUS SUMMARY (NL-TRM-01) section builders. Pure string functions — no
// framework, no state. The sheet renders the SAME TerminusReport the Reports
// screen and the clipboard export consume (lib/reports/terminus.ts), so the
// printed page can never disagree with the screen about a leg, a kilometre or
// a seat.
//
// This sheet is handed to a counterparty during a negotiation, so the rules it
// prints are restated where they apply rather than assumed: a measure with no
// basis prints NOT AVAILABLE and never a zero, deadhead legs are named where
// they are excluded, and the header says plainly which legs the sheet does not
// represent at all. What is absent is what a reader would otherwise assume.

import { stopTypeLabel } from "@/lib/api/stops";
import { corridorLabel, shortDateLabel, type TripRecord } from "@/lib/api/trips";
import {
  averageKmLabel,
  distanceLabel,
  flowLabel,
  utilizationLabel,
  utilizationRateLabel,
  type TerminusCorridor,
  type TerminusReport,
} from "@/lib/reports/terminus";
import { periodLabel } from "@/lib/period";
import type { CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";

/** A right-aligned monospace figure cell. */
function num(value: string | number): string {
  return `<td class="num">${esc(value)}</td>`;
}

/** A figure that has no basis. Spelled out in words on a tinted ground so it
 *  survives a monochrome print, and never rendered as 0 — a zero is a real and
 *  much worse number than "we did not measure this". */
function na(): string {
  return `<td class="na">NOT AVAILABLE</td>`;
}

// ---- header -----------------------------------------------------------------

export function header(company: CompanyInfo, report: TerminusReport): string {
  const banner =
    `Terminus summary for ${report.terminus.name} — a record of service operated to and from ` +
    "this venue, NOT an invoice and not a statement of amounts owing. Figures cover legs that " +
    "began or ended here and had already run when the sheet was prepared. Legs that served the " +
    "same corridor from a different endpoint are not represented, and cancelled, not-yet-run " +
    "and pass-through legs are listed separately and counted in nothing.";
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">TERMINUS SUMMARY</div>
      <div class="s">${esc(report.terminus.name)} · ${esc(report.terminus.place)}<br/>${esc(
        periodLabel(report.period),
      )} · Form NL-TRM-01</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">${esc(banner)}</div>`;
}

/** The report's own notes — each names a class of leg the figures deliberately
 *  leave out, printed up front so nothing below has to be taken on trust. */
export function notesBlock(report: TerminusReport): string {
  return report.notes.map((n) => `<div class="note">${esc(n)}</div>`).join("");
}

// ---- report details ---------------------------------------------------------

export function detailsBlock(report: TerminusReport): string {
  return (
    sectionBar("Report Details") +
    grid([field("Terminus", report.terminus.label, { wide: true })]) +
    grid([
      field("Period", periodLabel(report.period), { mono: true }),
      field("Prepared", report.today, { mono: true }),
      field("Stop type", stopTypeLabel(report.terminus.stopType)),
      field("Corridors served", String(report.corridors.length), { mono: true }),
    ])
  );
}

// ---- summary ----------------------------------------------------------------

/** The tile row of the screen as one label/value table. Every figure here is a
 *  total over legs that actually ran. */
export function summaryBlock(report: TerminusReport): string {
  const u = report.utilization;
  const rows: [string, string][] = [
    ["Operated legs", String(report.operatedLegs)],
    ["Flow", flowLabel(report)],
    ["Total distance", distanceLabel(report.distance)],
    ["Average per leg", averageKmLabel(report.distance)],
    ["Seat utilization", utilizationLabel(u)],
  ];
  return (
    `<div class="blk">` +
    sectionBar("Summary — Distance & Seat Utilization") +
    `<table>
       <tbody>${rows
         .map(([label, value]) => `<tr><td>${esc(label)}</td><td class="num">${esc(value)}</td></tr>`)
         .join("")}</tbody>
     </table></div>`
  );
}

// ---- by corridor ------------------------------------------------------------

function corridorRow(c: TerminusCorridor): string {
  const sub = [
    c.farEndIsTerminus ? null : "not a terminus venue",
    c.routeNames.length > 0 ? c.routeNames.join(" · ") : null,
  ]
    .filter(Boolean)
    .join(" — ");
  return `<tr>
    <td>${esc(c.label)}${sub ? `<div class="sub">${esc(sub)}</div>` : ""}</td>
    ${num(c.legs.length)}
    ${num(c.arrivals)}
    ${num(c.departures)}
    ${num(c.turnarounds || "—")}
    ${num(distanceLabel(c.distance))}
    ${c.distance.averageKm === null ? na() : num(averageKmLabel(c.distance))}
    ${num(c.utilization.seatsFilled)}
    ${num(c.utilization.seatsOffered)}
    ${c.utilization.rate === null ? na() : num(utilizationRateLabel(c.utilization))}
  </tr>`;
}

export function corridorsBlock(report: TerminusReport): string {
  const t = report.utilization;
  const body =
    report.corridors.length === 0
      ? `<tr><td class="why" colspan="10">No leg began or ended at this terminus in the period.</td></tr>`
      : report.corridors.map(corridorRow).join("");
  return `<div class="blk">
    ${sectionBar("By Corridor — Where The Traffic Went")}
    <table>
      <thead>
        <tr>
          <th>Corridor (far end)</th>
          <th class="num">Legs</th>
          <th class="num">In</th>
          <th class="num">Out</th>
          <th class="num">Turn</th>
          <th class="num">Distance</th>
          <th class="num">Avg per leg</th>
          <th class="num">Seats filled</th>
          <th class="num">Seats offered</th>
          <th class="num">Utilization</th>
        </tr>
      </thead>
      <tbody>${body}</tbody>
      <tfoot>
        <tr class="total">
          <td class="lbl2">Totals</td>
          ${num(report.operatedLegs)}
          ${num(report.arrivals)}
          ${num(report.departures)}
          ${num(report.turnarounds || "—")}
          ${num(distanceLabel(report.distance))}
          ${report.distance.averageKm === null ? na() : num(averageKmLabel(report.distance))}
          ${num(t.seatsFilled)}
          ${num(t.seatsOffered)}
          ${t.rate === null ? na() : num(utilizationRateLabel(t))}
        </tr>
      </tfoot>
    </table>
  </div>`;
}

// ---- not counted ------------------------------------------------------------

function dispositionRow(disposition: string, why: string, t: TripRecord): string {
  return `<tr>
    <td>${esc(disposition)}</td>
    <td class="ref">${esc(shortDateLabel(t.serviceDate))}</td>
    <td class="ref">${esc(t.tripNumber)}</td>
    <td>${esc(corridorLabel(t))}</td>
    <td class="why">${esc(why)}</td>
  </tr>`;
}

/**
 * Everything the figures deliberately exclude, in one table, so the period
 * reconciles. Printed even when empty — an explicit "nothing was excluded" is
 * worth more to a counterparty than a section that silently disappears.
 */
export function reconciliationBlock(report: TerminusReport): string {
  const rows = [
    ...report.passedThrough.map((t) =>
      dispositionRow("Passed through", "called here but neither began nor ended the leg", t),
    ),
    ...report.upcoming.map((t) => dispositionRow("Not yet run", `scheduled after ${report.today}`, t)),
    ...report.cancelled.map((t) =>
      dispositionRow("Cancelled", t.cancelledReason ?? "no reason recorded", t),
    ),
  ];
  const body =
    rows.length === 0
      ? `<tr><td class="why" colspan="5">Nothing excluded — every leg touching this terminus in the period is counted above.</td></tr>`
      : rows.join("");
  return `<div class="blk">
    ${sectionBar("Not Counted — Listed So The Period Reconciles")}
    <table>
      <thead>
        <tr>
          <th>Disposition</th>
          <th>Date</th>
          <th>Trip</th>
          <th>Corridor</th>
          <th>Why</th>
        </tr>
      </thead>
      <tbody>${body}</tbody>
    </table>
  </div>`;
}

// ---- footer -----------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    Only legs that had already run are counted; a cancelled leg never ran and a scheduled one had
    not yet. Deadhead legs run empty by design — their distance counts, their seats do not.
    A leg with no seating capacity on file is excluded from utilization rather than counted as
    empty, and any figure without a basis prints NOT AVAILABLE, never zero.<br/>
    Distances are the planned corridor distance for each leg, not odometer readings.
  </div>`;
}
