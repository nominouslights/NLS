// NL-INV-PREP Invoice Prep Sheet — composes the printable HTML from the section
// builders. This is a worksheet for MANUAL entry into QuickBooks Online, not a
// customer-facing invoice: it carries an explicit caption to that effect and no
// remit-to / payment-terms language beyond the informational Net terms.
//
// Page assembly (US-Letter):
//   header + Invoice Details + QuickBooks Entry + (Write-off)
//   + Line Items (with Subtotal / GST / Total) + Trip & Passenger Detail + footer
//
// The sheet carries the same facts as the screen's COPY FOR QUICKBOOKS and
// per-line COPY, so it can be keyed from on its own: `details` is the loaded
// trip + passenger map the Billing detail pane already holds. Pass an empty
// map and the trip-detail section simply reports nothing loaded.

import type { InvoiceDetailRecord } from "@/lib/api/billing";
import type { TripDetailMap } from "@/lib/billing/tripDetail";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { INVOICE_PREP_STYLES } from "./styles";
import {
  detailsBlock,
  entryRecordBlock,
  footer,
  header,
  lineItemsBlock,
  tripDetailBlock,
  writeOffBlock,
} from "./sections";

export function invoicePrepHtml(
  inv: InvoiceDetailRecord,
  company: CompanyInfo,
  details: TripDetailMap,
): string {
  return `
<style>${INVOICE_PREP_STYLES}</style>
<div class="inv">
  <div class="sheet">
    ${header(company, inv)}
    ${detailsBlock(inv)}
    ${entryRecordBlock(inv)}
    ${writeOffBlock(inv)}
    ${lineItemsBlock(inv)}
    ${tripDetailBlock(inv, details)}
    ${footer(company)}
  </div>
</div>`;
}

export function printInvoicePrepSheet(inv: InvoiceDetailRecord, details: TripDetailMap): void {
  const title = `Invoice Prep Sheet ${inv.invoiceNumber} — ${inv.clientName}`;
  openPrintDocument(title, invoicePrepHtml(inv, COMPANY, details));
}
