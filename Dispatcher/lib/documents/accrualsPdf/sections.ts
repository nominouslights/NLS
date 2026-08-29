// ACCRUALS REPORT (NL-ACC-01) section builders. Pure string functions — no
// framework, no state. The sheet renders the SAME AccrualsReport the Reports
// screen and the clipboard export consume (lib/billing/accruals.ts), so the
// printed page can never disagree with the screen about a bucket or a dollar.
//
// Money rules restated where they print: estimates carry an explicit "EST."
// suffix, an unpaired leg prints "UNPAIRED — NOT ESTIMATED" rather than a
// half-rate, and GST appears only in the Invoices Referenced section.

import {
  ACCRUAL_BUCKET_META,
  ACCRUALS_ESTIMATE_NOTE,
  ACCRUALS_GST_NOTE,
  accrualTotals,
  groupAmountLabel,
  groupRefLabel,
  groupRouteLabel,
  type AccrualBucket,
  type AccrualGroup,
  type AccrualsReport,
} from "@/lib/billing/accruals";
import {
  formatInvoiceCad,
  invoiceChip,
  periodLabel as invoicePeriodLabel,
} from "@/lib/api/billing";
import { contractRateLabel } from "@/lib/api/clients";
import { corridorLabel } from "@/lib/api/trips";
import { directionLabel } from "@/lib/billing/tripDetail";
import { periodLabel } from "@/lib/period";
import type { CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";

// ---- header -----------------------------------------------------------------

export function header(company: CompanyInfo, report: AccrualsReport): string {
  const banner =
    "Accruals statement — a monthly position of trips by billing state, NOT an invoice. " +
    "Amounts marked EST. are contract-rate estimates; issued invoice amounts govern. " +
    "All amounts CAD, excluding GST except where shown under Invoices Referenced.";
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">ACCRUALS REPORT</div>
      <div class="s">${esc(report.client.name)} · ${esc(periodLabel(report.period))} · Form NL-ACC-01</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">${esc(banner)}</div>`;
}

/** The report's degradation banners (manual billing, failed invoice fetches,
 *  unpaired legs) — printed up front so a "—" further down is never a mystery. */
export function notesBlock(report: AccrualsReport): string {
  return report.notes.map((n) => `<div class="warn">${esc(n)}</div>`).join("");
}

// ---- report details ---------------------------------------------------------

export function detailsBlock(report: AccrualsReport): string {
  const contract = report.client.activeContract;
  return (
    sectionBar("Report Details") +
    grid([field("Client", report.client.name, { wide: true })]) +
    grid([
      field("Period", periodLabel(report.period), { mono: true }),
      field("Prepared", report.today, { mono: true }),
      field("Contract rate", contract ? contractRateLabel(contract) : "No active contract"),
      field("Default PO", contract?.defaultPoNumber ?? "—", { mono: true }),
    ]) +
    grid([
      field("Budget code", contract?.budgetCode ?? "—", { mono: true }),
      field("GST", contract ? (contract.gstApplicable ? "Applies on issued invoices" : "Not applicable per contract") : "—"),
      field("", ""),
      field("", ""),
    ])
  );
}

// ---- summary ----------------------------------------------------------------

/** Bucket × round trips × actual × estimated, with a totals row — the tile row
 *  of the screen as one table. Actual and estimated stay separate columns so
 *  real dollars are never visually merged with estimates. */
export function summaryBlock(report: AccrualsReport): string {
  const rows = report.buckets
    .map(
      (b) => `<tr>
      <td>${esc(b.label)}</td>
      <td class="amt">${b.groups.length}</td>
      <td class="amt">${esc(formatInvoiceCad(b.actualCad))}</td>
      <td class="amt">${b.estimatedCad > 0 ? `${esc(formatInvoiceCad(b.estimatedCad))} EST.` : "—"}</td>
      <td class="amt">${b.unpricedCount > 0 ? b.unpricedCount : "—"}</td>
    </tr>`,
    )
    .join("");
  const totals = accrualTotals(report);
  return (
    `<div class="blk">` +
    sectionBar("Summary — Round Trips By Billing State") +
    `<table>
       <thead>
         <tr>
           <th>Bucket</th>
           <th class="amt">Round trips</th>
           <th class="amt">Actual (CAD)</th>
           <th class="amt">Estimated (CAD)</th>
           <th class="amt">Unpriced</th>
         </tr>
       </thead>
       <tbody>${rows}</tbody>
       <tfoot>
         <tr class="total">
           <td class="lbl2">Totals</td>
           <td class="amt">${totals.groupCount}</td>
           <td class="amt">${esc(formatInvoiceCad(totals.actualCad))}</td>
           <td class="amt">${totals.estimatedCad > 0 ? `${esc(formatInvoiceCad(totals.estimatedCad))} EST.` : "—"}</td>
           <td class="amt">${totals.unpricedCount > 0 ? totals.unpricedCount : "—"}</td>
         </tr>
       </tfoot>
     </table></div>`
  );
}

// ---- per-bucket detail ------------------------------------------------------

/** Trips cell: one line per leg — number, direction glyph + word, deadhead
 *  call-out. Words, not colour: the sheet prints monochrome. */
function tripsCell(g: AccrualGroup): string {
  return g.legs
    .map((l) => {
      const dir = l.direction ? ` ${esc(directionLabel(l.direction))}` : "";
      const dead = l.isEmptyLeg ? " · DEADHEAD" : "";
      return `${esc(l.tripNumber)}${dir}${dead}`;
    })
    .join("<br/>");
}

/** Amount cell — estimate suffix, or the spelled-out reason it is unpriced. */
function amountCell(g: AccrualGroup): string {
  const label = groupAmountLabel(g);
  if (label !== null) {
    return `<td class="amt">${esc(g.amountSource === "estimate" ? label.replace(" est.", " EST.") : label)}</td>`;
  }
  if (g.amountNote === "unpaired") return `<td class="flag">UNPAIRED — NOT ESTIMATED</td>`;
  if (g.amountNote === "unavailable") return `<td class="miss">AMOUNT UNAVAILABLE</td>`;
  return `<td class="amt">—</td>`;
}

function groupRow(g: AccrualGroup): string {
  return `<tr>
    <td class="ref">${esc(g.legs[0].serviceDate)}</td>
    <td class="ref">${tripsCell(g)}</td>
    <td>${esc(groupRouteLabel(g))}</td>
    <td class="ref">${esc(g.legs[0].poNumber ?? "—")}</td>
    <td class="ref">${esc(groupRefLabel(g))}</td>
    ${amountCell(g)}
  </tr>`;
}

function bucketBlock(b: AccrualBucket): string {
  const tallies = [
    `<td class="amt">${esc(formatInvoiceCad(b.actualCad))} actual</td>`,
    `<td class="amt">${b.estimatedCad > 0 ? `${esc(formatInvoiceCad(b.estimatedCad))} EST.` : "—"}</td>`,
  ].join("");
  return `<div class="blk">
    ${sectionBar(`${b.label} — ${ACCRUAL_BUCKET_META[b.id].hint}`)}
    <table>
      <thead>
        <tr>
          <th>Date</th>
          <th>Trips</th>
          <th>Route</th>
          <th>PO</th>
          <th>Ref</th>
          <th class="amt">Amount (CAD)</th>
        </tr>
      </thead>
      <tbody>${b.groups.map(groupRow).join("")}</tbody>
      <tfoot>
        <tr><td class="lbl2" colspan="4">${b.groups.length} round trip${b.groups.length === 1 ? "" : "s"}${b.unpricedCount > 0 ? ` · ${b.unpricedCount} unpriced` : ""}</td>${tallies}</tr>
      </tfoot>
    </table>
  </div>`;
}

/** Detail tables for the non-empty buckets only — the summary already carries
 *  every zero. An empty month still prints: one line says so, on purpose. */
export function bucketsBlock(report: AccrualsReport): string {
  const nonEmpty = report.buckets.filter((b) => b.groups.length > 0);
  if (nonEmpty.length === 0) {
    return (
      `<div class="blk">` +
      sectionBar("Trip Detail") +
      `<table><tbody><tr><td class="note">No trips for this client in the period.</td></tr></tbody></table></div>`
    );
  }
  return nonEmpty.map(bucketBlock).join("");
}

// ---- reconciliation ---------------------------------------------------------

/** Cancelled and written-off trips with their reasons — listed so the client's
 *  month reconciles, never counted as accruals. Always printed, even when
 *  empty: an explicit "none" is part of the statement. */
export function reconciliationBlock(report: AccrualsReport): string {
  const cancelledRows = report.cancelled
    .map(
      (t) => `<tr>
      <td class="ref">${esc(t.serviceDate)}</td>
      <td class="ref">${esc(t.tripNumber)}</td>
      <td>${esc(corridorLabel(t))}</td>
      <td>Cancelled</td>
      <td class="note">${esc(t.cancelledReason ?? "no reason recorded")}</td>
    </tr>`,
    )
    .join("");
  const writtenOffRows = report.writtenOff
    .map((g) => {
      const reason = g.legs.map((l) => l.writtenOffReason).find(Boolean) ?? "no reason recorded";
      const amount = groupAmountLabel(g);
      return `<tr>
      <td class="ref">${esc(g.legs[0].serviceDate)}</td>
      <td class="ref">${tripsCell(g)}</td>
      <td>${esc(groupRouteLabel(g))}</td>
      <td>Written off${amount ? ` ${esc(amount)}` : " — amount unavailable"}</td>
      <td class="note">${esc(reason)}</td>
    </tr>`;
    })
    .join("");
  const rows =
    cancelledRows + writtenOffRows ||
    `<tr><td class="note" colspan="5">No cancelled or written-off trips in the period.</td></tr>`;
  return `<div class="blk">
    ${sectionBar("Reconciliation — Not Counted In Accruals")}
    <table>
      <thead>
        <tr><th>Date</th><th>Trips</th><th>Route</th><th>Disposition</th><th>Reason</th></tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;
}

// ---- invoices referenced ----------------------------------------------------

/** Every fetched invoice behind the real amounts — the one place GST prints,
 *  straight off each invoice's own subtotal / GST / total. */
export function invoicesBlock(report: AccrualsReport): string {
  const rows =
    report.invoices.length === 0
      ? `<tr><td class="note" colspan="7">No issued invoices are referenced by this period's trips.</td></tr>`
      : report.invoices
          .map(
            (inv) => `<tr>
      <td class="ref">${esc(inv.invoiceNumber)}</td>
      <td class="ref">${esc(inv.qboInvoiceId ?? "—")}</td>
      <td>${esc(invoiceChip(inv).label)}</td>
      <td class="ref">${esc(invoicePeriodLabel(inv))}</td>
      <td class="amt">${esc(formatInvoiceCad(inv.subtotalCad))}</td>
      <td class="amt">${esc(formatInvoiceCad(inv.gstCad))}</td>
      <td class="amt">${esc(formatInvoiceCad(inv.totalCad))}</td>
    </tr>`,
          )
          .join("");
  return `<div class="blk">
    ${sectionBar("Invoices Referenced (GST shown here)")}
    <table>
      <thead>
        <tr>
          <th>Invoice</th>
          <th>QBO #</th>
          <th>Status</th>
          <th>Invoice period</th>
          <th class="amt">Subtotal</th>
          <th class="amt">GST</th>
          <th class="amt">Total (CAD)</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;
}

// ---- footer -----------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    ${esc(ACCRUALS_GST_NOTE)} ${esc(ACCRUALS_ESTIMATE_NOTE)}<br/>
    QuickBooks Online is the system of record for issued invoices.
  </div>`;
}
