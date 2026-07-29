// Invoice PREP SHEET (NL-INV-PREP) section builders. Pure string functions —
// no framework, no state. The sheet is a worksheet to KEY INTO QuickBooks
// Online by hand, NOT a customer-facing invoice, so it carries an explicit
// caption to that effect and no "bill to / remit to" language.

import type { InvoiceDetailRecord } from "@/lib/api/billing";
import { formatInvoiceCad, periodLabel } from "@/lib/api/billing";
import type { CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";

// ---- header -----------------------------------------------------------------

export function header(company: CompanyInfo, inv: InvoiceDetailRecord): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">INVOICE PREP SHEET</div>
      <div class="s">Draft ${esc(inv.invoiceNumber)} · Form NL-INV-PREP</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">Prepared for manual entry into QuickBooks Online — not a customer invoice. Key these figures into QBO, then record the QBO invoice number back in the Dispatch Console.</div>`;
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
    ? `<tr><td class="lbl2" colspan="5">GST (${Math.round(inv.gstRate * 1000) / 10}%)</td><td class="amt">${esc(formatInvoiceCad(inv.gstCad))}</td></tr>`
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

// ---- footer -----------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    Internal worksheet — QuickBooks Online is the system of record for issued invoices.
  </div>`;
}
