// ACCRUALS REPORT (NL-ACC-01) section builders. Pure string functions — no
// framework, no state. The sheet renders the SAME AccrualsReport the Reports
// screen and the clipboard export consume (lib/billing/accruals.ts), so the
// printed page can never disagree with the screen about a bucket or a dollar.
//
// Money rules restated where they print: estimates carry an explicit "EST."
// suffix, an unpaired group prints its one-way estimate with the flag spelled
// out beside it ("ONE-WAY RATE PER LEG — UNPAIRED, REVIEW", or "SPLIT ACROSS
// POS — PRICED AS ONE-WAY TRIPS") — the sheet is monochrome, so every caveat
// has to be words — a group with no round-trip key prints "MANUAL LINE — NOT
// ESTIMATED", and no tax appears anywhere: QuickBooks Online owns all tax
// calculation, so every amount printed here is a bare line total in CAD.
//
// Rates are PER PURCHASE ORDER (the contract rate is only the fallback), so the
// PO column prints the EFFECTIVE PO that priced each row plus any PO warning,
// and the Purchase Orders block prints each PO's authorised value against what
// this period draws down on it.
//
// Reading order is the SECTION order (upcoming work, then monies owed, then the
// settled closing line), not the bucket order — see ACCRUAL_SECTION_ORDER.

import {
  ACCRUAL_BUCKET_META,
  ACCRUALS_ESTIMATE_NOTE,
  ACCRUALS_TAX_NOTE,
  AMOUNT_FLAG_META,
  accrualHeadlines,
  accrualSectionAmountLabel,
  accrualSectionDetail,
  accrualTotals,
  groupAmountLabel,
  groupPoText,
  groupRefLabel,
  groupRouteLabel,
  type AccrualBucket,
  type AccrualGroup,
  type AccrualSection,
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
    "Accruals statement — upcoming work not yet done, then monies owed, then the month's " +
    "settled work. NOT an invoice. Amounts marked EST. are contract-rate estimates; issued " +
    "invoice amounts govern. All amounts CAD. Taxes are applied in QuickBooks; this report " +
    "contains none.";
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
 *  half-rate groups, manual-line groups) — printed up front so a "—" further
 *  down is never a mystery. */
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
      field("", ""),
      field("", ""),
      field("", ""),
    ])
  );
}

// ---- headline ---------------------------------------------------------------

/** The two figures the sheet now leads with — upcoming expenses, then monies
 *  owed — printed above Summary. Both strings come straight from the shared
 *  derivation (accrualHeadlines), so the sheet can never state a different
 *  figure from the screen, the clipboard export, or the emailed PDF. An amount
 *  keeps its EST. marking, so an estimate is never dressed up as settled money. */
export function headlineBlock(report: AccrualsReport): string {
  const cells = accrualHeadlines(report)
    .map(
      (h) => `<div class="hl">
      <div class="hl-lbl">${esc(h.label)}</div>
      <div class="hl-amt">${esc(h.amountCad.replace(" est.", " EST."))}</div>
      <div class="hl-det">${esc(h.detail.replace(" est.", " EST."))}</div>
    </div>`,
    )
    .join("");
  return `<div class="blk">${sectionBar("What This Month Costs — And What Is Owed")}<div class="hl-row">${cells}</div></div>`;
}

// ---- purchase orders --------------------------------------------------------

/** Each PO this period's work was priced under: its effective terms, its
 *  authorised value, and what this period draws down on it. The status column is
 *  words ("Over PO value by $4,000.00" / "$16,000.00 left of $96,000.00"), so a
 *  monochrome sheet carries the warning as plainly as the screen's chip does.
 *  Warnings never block — the overage also rides the notes block up top. */
export function purchaseOrdersBlock(report: AccrualsReport): string {
  const rows =
    report.poCommitments.length === 0
      ? `<tr><td class="note" colspan="6">No purchase-order work in this period.</td></tr>`
      : report.poCommitments
          .map(
            (c) => `<tr>
      <td class="ref">${esc(c.poNumber ?? "No PO")}</td>
      <td>${esc(c.termsLabel)}</td>
      <td class="amt">${c.amountCad === null ? "—" : esc(formatInvoiceCad(c.amountCad))}</td>
      <td class="amt">${esc(formatInvoiceCad(c.invoicedCad))}</td>
      <td class="amt">${esc(formatInvoiceCad(c.upcomingCad))} EST.</td>
      <td class="${c.overageCad !== null ? "flag" : "amt"}">${esc(c.label)}</td>
    </tr>`,
          )
          .join("");
  return `<div class="blk">
    ${sectionBar("Purchase Orders — Authorised Value vs This Period's Work")}
    <table>
      <thead>
        <tr>
          <th>PO</th>
          <th>Effective terms</th>
          <th class="amt">Authorised (CAD)</th>
          <th class="amt">Invoiced (CAD)</th>
          <th class="amt">Upcoming (CAD)</th>
          <th class="amt">Status</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;
}

// ---- summary ----------------------------------------------------------------

/** Round trips × actual × estimated, grouped by section with a subtotal row per
 *  section and its buckets nested beneath, plus a whole-report totals row.
 *  Actual and estimated stay separate columns so real dollars are never
 *  visually merged with estimates. Every bucket still appears — the summary is
 *  the complete position of the month, including the settled one whose per-trip
 *  detail table is deliberately gone. */
export function summaryBlock(report: AccrualsReport): string {
  const rows = report.sections
    .map((section) => {
      const subtotal = `<tr class="sub">
      <td>${esc(section.label)}</td>
      <td class="amt">${section.groupCount}</td>
      <td class="amt">${esc(formatInvoiceCad(section.actualCad))}</td>
      <td class="amt">${section.estimatedCad > 0 ? `${esc(formatInvoiceCad(section.estimatedCad))} EST.` : "—"}</td>
      <td class="amt">${section.unpricedCount > 0 ? section.unpricedCount : "—"}</td>
    </tr>`;
      const buckets = section.buckets
        .map(
          (b) => `<tr>
      <td class="ind">${esc(b.label)}</td>
      <td class="amt">${b.groups.length}</td>
      <td class="amt">${esc(formatInvoiceCad(b.actualCad))}</td>
      <td class="amt">${b.estimatedCad > 0 ? `${esc(formatInvoiceCad(b.estimatedCad))} EST.` : "—"}</td>
      <td class="amt">${b.unpricedCount > 0 ? b.unpricedCount : "—"}</td>
    </tr>`,
        )
        .join("");
      return subtotal + buckets;
    })
    .join("");
  const totals = accrualTotals(report);
  return (
    `<div class="blk">` +
    sectionBar("Summary — Upcoming Work, Monies Owed, Settled") +
    `<table>
       <thead>
         <tr>
           <th>Section / billing state</th>
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

/** Amount cell — the figure with its EST. suffix, any amount flag printed in
 *  words BESIDE a real figure (an unpaired group has an amount: the PO's one-way
 *  rate per leg, exactly as the invoice will bill it), or the spelled-out reason
 *  it is unpriced. The flag text comes from AMOUNT_FLAG_META, so the sheet can
 *  never word a flag differently from the screen. Words, not colour. */
function amountCell(g: AccrualGroup): string {
  const label = groupAmountLabel(g);
  if (label !== null) {
    const amount = esc(g.amountSource === "estimate" ? label.replace(" est.", " EST.") : label);
    if (g.amountFlag) {
      return `<td class="flag">${amount}<br/>${esc(AMOUNT_FLAG_META[g.amountFlag].label.toUpperCase())}</td>`;
    }
    return `<td class="amt">${amount}</td>`;
  }
  if (g.amountNote === "manual") return `<td class="flag">MANUAL LINE — NOT ESTIMATED</td>`;
  if (g.amountNote === "unavailable") return `<td class="miss">AMOUNT UNAVAILABLE</td>`;
  if (g.amountNote === "counted") return `<td class="flag">COUNTED WITH THE PAIRED LEG</td>`;
  return `<td class="amt">—</td>`;
}

function groupRow(g: AccrualGroup): string {
  return `<tr>
    <td class="ref">${esc(g.legs[0].serviceDate)}</td>
    <td class="ref">${tripsCell(g)}</td>
    <td>${esc(groupRouteLabel(g))}</td>
    <td class="ref">${esc(groupPoText(g))}</td>
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

/** A section's header bar + its non-empty buckets' detail tables. */
function sectionBlock(section: AccrualSection): string {
  const bar = sectionBar(
    `${section.label} — ${accrualSectionDetail(section)} · ${accrualSectionAmountLabel(section).replace(" est.", " EST.")}`,
  );
  const nonEmpty = section.buckets.filter((b) => b.groups.length > 0);
  if (nonEmpty.length === 0) {
    return (
      `<div class="blk">` +
      bar +
      `<table><tbody><tr><td class="note">Nothing in this section for the period.</td></tr></tbody></table></div>`
    );
  }
  return `<div class="blk">${bar}</div>` + nonEmpty.map(bucketBlock).join("");
}

/** Per-trip detail, in reading order: the detail-bearing sections (upcoming
 *  work, then monies owed) with their non-empty buckets, then the settled
 *  one-liner. Settled work deliberately has NO per-trip table — it still has to
 *  reconcile the month, and the summary plus this line is how it does.
 *  An empty month still prints: one line says so, on purpose. */
export function bucketsBlock(report: AccrualsReport): string {
  if (report.buckets.every((b) => b.groups.length === 0)) {
    return (
      `<div class="blk">` +
      sectionBar("Trip Detail") +
      `<table><tbody><tr><td class="note">No trips for this client in the period.</td></tr></tbody></table></div>`
    );
  }
  const detail = report.sections.filter((s) => s.detail).map(sectionBlock).join("");
  const settled = report.sections
    .filter((s) => !s.detail)
    .map(
      (s) => `<div class="blk">
    ${sectionBar(s.label)}
    <table><tbody><tr>
      <td class="note">${esc(s.hint)} — ${esc(accrualSectionDetail(s))}</td>
      <td class="amt">${esc(accrualSectionAmountLabel(s).replace(" est.", " EST."))}</td>
    </tr></tbody></table>
  </div>`,
    )
    .join("");
  return detail + settled;
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

/** Every fetched invoice behind the real amounts — one Total (CAD) column,
 *  straight off each invoice's own line total. No tax is printed or implied. */
export function invoicesBlock(report: AccrualsReport): string {
  const rows =
    report.invoices.length === 0
      ? `<tr><td class="note" colspan="5">No issued invoices are referenced by this period's trips.</td></tr>`
      : report.invoices
          .map(
            (inv) => `<tr>
      <td class="ref">${esc(inv.invoiceNumber)}</td>
      <td class="ref">${esc(inv.qboInvoiceId ?? "—")}</td>
      <td>${esc(invoiceChip(inv).label)}</td>
      <td class="ref">${esc(invoicePeriodLabel(inv))}</td>
      <td class="amt">${esc(formatInvoiceCad(inv.totalCad))}</td>
    </tr>`,
          )
          .join("");
  return `<div class="blk">
    ${sectionBar("Invoices Referenced")}
    <table>
      <thead>
        <tr>
          <th>Invoice</th>
          <th>QBO #</th>
          <th>Status</th>
          <th>Invoice period</th>
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
    ${esc(ACCRUALS_TAX_NOTE)} ${esc(ACCRUALS_ESTIMATE_NOTE)}<br/>
    QuickBooks Online is the system of record for issued invoices.
  </div>`;
}
