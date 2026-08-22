// Invoice PREP SHEET (NL-INV-PREP) section builders. Pure string functions —
// no framework, no state. The sheet is a worksheet to KEY INTO QuickBooks
// Online by hand, NOT a customer-facing invoice, so it carries an explicit
// caption to that effect and no "bill to / remit to" language.
//
// It carries everything the clipboard exports carry: the QuickBooks entry
// record (status, QBO reference, payment, write-off) and the per-line trip &
// passenger detail. Both are derived by the SAME functions the clipboard uses
// (entryRecordFields / describeLineTrips) so the printed page and the copied
// text can never disagree about one invoice.

import type { InvoiceDetailRecord, LabelledField } from "@/lib/api/billing";
import { entryRecordFields, formatInvoiceCad, periodLabel, writeOffFields } from "@/lib/api/billing";
import {
  describeLineTrips,
  directionLabel,
  type TripDetailMap,
  type TripLegDetail,
} from "@/lib/billing/tripDetail";
import type { CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";

// ---- header -----------------------------------------------------------------

/** The banner is status-aware: the sheet prints for every status, so on an
 *  invoice already keyed it must not tell the reader to go key it again. */
function bannerText(inv: InvoiceDetailRecord): string {
  const base = "Prepared for manual entry into QuickBooks Online — not a customer invoice.";
  switch (inv.status) {
    case "Draft":
      return `${base} Key these figures into QBO, then record the QBO invoice number back in the Dispatch Console.`;
    case "Void":
      return `${base} This worksheet was voided — do not key it into QuickBooks.`;
    default:
      return `${base} Already keyed into QBO as invoice ${inv.qboInvoiceId ?? "—"} — this is the working record behind that entry.`;
  }
}

export function header(company: CompanyInfo, inv: InvoiceDetailRecord): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">INVOICE PREP SHEET</div>
      <div class="s">${esc(inv.invoiceNumber)} · Form NL-INV-PREP</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">${esc(bannerText(inv))}</div>`;
}

// ---- shared field grid ------------------------------------------------------

/** Lay labelled fields out in an even bordered grid (at most 4 across),
 *  padding the last row with blanks so the borders stay square. */
function fieldGrid(fields: LabelledField[]): string {
  if (fields.length === 0) return "";
  const cols = Math.min(fields.length, 4);
  const cells = [...fields];
  while (cells.length % cols !== 0) cells.push({ label: "", value: "" });
  return grid(
    cells.map((f) => field(f.label, f.value, { mono: f.mono })),
    cols,
  );
}

// ---- invoice details --------------------------------------------------------

export function detailsBlock(inv: InvoiceDetailRecord): string {
  return (
    sectionBar("Invoice Details") +
    grid([
      field("Client", inv.clientName, { wide: true }),
    ]) +
    grid([
      field("Billing period", periodLabel(inv), { mono: true }),
      field("PO #", inv.poNumber ?? "—", { mono: true }),
      field("Budget code", inv.budgetCode ?? "—", { mono: true }),
      field("Net terms", `Net ${inv.netTermsDays}`),
    ])
  );
}

// ---- QuickBooks entry record ------------------------------------------------

/** Status, issue date, and the recorded QBO reference / payment state. Printed
 *  for every status — on a Draft it reads "Not yet entered", so a sheet pulled
 *  before and after keying tells the same story in the same place. Status is
 *  colour-free text here by construction: the sheet prints monochrome. */
export function entryRecordBlock(inv: InvoiceDetailRecord): string {
  return sectionBar("QuickBooks Entry") + fieldGrid(entryRecordFields(inv));
}

/** The write-off record — amount, effective date, and the required reason.
 *  Renders nothing unless the invoice is written off. */
export function writeOffBlock(inv: InvoiceDetailRecord): string {
  const fields = writeOffFields(inv);
  if (!fields) return "";
  return (
    sectionBar("Write-off") +
    fieldGrid(fields) +
    `<div class="reason"><b>Reason:</b> ${esc(inv.writtenOffReason) || "No reason recorded."}</div>`
  );
}

// ---- line items -------------------------------------------------------------

function lineRef(inv: InvoiceDetailRecord["lines"][number]): string {
  return [inv.tripNumber, inv.serviceDate].filter(Boolean).join(" · ");
}

export function lineItemsBlock(inv: InvoiceDetailRecord): string {
  const rows =
    inv.lines.length === 0
      ? `<tr><td class="num"></td><td colspan="4" style="color:#777">No lines on this invoice.</td></tr>`
      : inv.lines
          .map(
            (l, i) => `<tr>
      <td class="num">${i + 1}</td>
      <td>${esc(l.description) || "&nbsp;"}</td>
      <td class="ref">${esc(lineRef(l)) || "&nbsp;"}</td>
      <td class="amt">${esc(String(l.quantity))}</td>
      <td class="amt">${esc(formatInvoiceCad(l.unitPriceCad))}</td>
      <td class="amt">${esc(formatInvoiceCad(l.amountCad))}</td>
    </tr>`,
          )
          .join("");

  const gstRow = inv.gstApplicable
    ? `<tr><td class="lbl2" colspan="5">GST (${Math.round(inv.gstRate * 1000) / 10}%) — no PST on transportation</td><td class="amt">${esc(formatInvoiceCad(inv.gstCad))}</td></tr>`
    : `<tr><td class="lbl2" colspan="5">GST — not applicable per contract</td><td class="amt">${esc(formatInvoiceCad(inv.gstCad))}</td></tr>`;

  return (
    sectionBar("Line Items") +
    `<table>
       <thead>
         <tr>
           <th class="num">#</th>
           <th>Description</th>
           <th>Trip # / Service date</th>
           <th class="amt">Qty</th>
           <th class="amt">Unit (CAD)</th>
           <th class="amt">Amount (CAD)</th>
         </tr>
       </thead>
       <tbody>${rows}</tbody>
       <tfoot>
         <tr><td class="lbl2" colspan="5">Subtotal</td><td class="amt">${esc(formatInvoiceCad(inv.subtotalCad))}</td></tr>
         ${gstRow}
         <tr class="total"><td class="lbl2" colspan="5">Total (CAD)</td><td class="amt">${esc(formatInvoiceCad(inv.totalCad))}</td></tr>
       </tfoot>
     </table>`
  );
}

// ---- trip & passenger detail ------------------------------------------------

/** One leg's row. A deadhead leg gets its own call-out: it ran empty BY
 *  DESIGN, which is not the same thing as passenger data being missing — a
 *  note ("no manifest recorded") means exactly that, and the two must never be
 *  read as each other while the money is being keyed. */
function legRow(leg: TripLegDetail): string {
  if (!leg.tripNumber) {
    return `<tr><td class="note" colspan="6">Trip details unavailable — this leg could not be loaded.</td></tr>`;
  }
  const head =
    `<td class="ref">${esc(leg.tripNumber)}</td>` +
    `<td>${esc(directionLabel(leg.direction))}</td>` +
    `<td class="ref">${esc(leg.serviceDate) || "—"}</td>`;

  if (leg.deadhead) {
    return `<tr>${head}<td class="dead" colspan="3">DEADHEAD — ran empty, no passengers</td></tr>`;
  }
  if (leg.note) {
    return `<tr>${head}<td class="note" colspan="3">${esc(leg.note)}</td></tr>`;
  }
  return (
    `<tr>${head}` +
    `<td class="amt">${leg.taken.length}</td>` +
    `<td>${esc(leg.taken.join(", ")) || "none boarded"}</td>` +
    `<td>${esc(leg.noShows.join(", ")) || "—"}</td></tr>`
  );
}

function lineDetailBlock(
  l: InvoiceDetailRecord["lines"][number],
  index: number,
  details: TripDetailMap,
): string {
  const head = `<div class="lhd">
      <span class="ln">Line ${index + 1}</span>
      <span class="ldesc">${esc(l.description) || "&nbsp;"}</span>
      <span class="lamt">${esc(String(l.quantity))} × ${esc(formatInvoiceCad(l.unitPriceCad))} = ${esc(formatInvoiceCad(l.amountCad))}</span>
    </div>`;

  if (l.tripIds.length === 0) {
    const body = l.serviceDate
      ? `Service date ${esc(l.serviceDate)} — no trips linked to this line.`
      : "No trips linked to this line.";
    return `<div class="lineblk">${head}<div class="reason">${body}</div></div>`;
  }

  const rows = describeLineTrips(l, details).map(legRow).join("");
  return `<div class="lineblk">${head}
    <table class="sub">
      <thead>
        <tr>
          <th>Trip #</th>
          <th>Direction</th>
          <th>Service date</th>
          <th class="amt">Pax</th>
          <th>Passengers taken</th>
          <th>Did not board</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;
}

/** Backup detail behind each billed line — which trips it covers, and who was
 *  actually carried. Read from the Trips API at print time, never snapshotted
 *  onto the invoice. Renders nothing when no line references a trip. */
export function tripDetailBlock(inv: InvoiceDetailRecord, details: TripDetailMap): string {
  if (!inv.lines.some((l) => l.tripIds.length > 0)) return "";
  return (
    // sectionBar escapes its own title — pass the raw ampersand.
    sectionBar("Trip & Passenger Detail") +
    `<div class="detail">${inv.lines.map((l, i) => lineDetailBlock(l, i, details)).join("")}</div>`
  );
}

// ---- footer -----------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    Internal worksheet — QuickBooks Online is the system of record for issued invoices.
  </div>`;
}
