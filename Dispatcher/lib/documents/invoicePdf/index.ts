// NL-INV-PREP Invoice Prep Sheet — composes the printable HTML from the section
// builders. This is a worksheet for MANUAL entry into QuickBooks Online, not a
// customer-facing invoice: it carries an explicit caption to that effect and no
// remit-to / payment-terms language beyond the informational Net terms.
//
// Page assembly (US-Letter):
//   header + Invoice Details + Line Items (with Subtotal / GST / Total) + footer

import type { InvoiceDetailRecord } from "@/lib/api/billing";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { INVOICE_PREP_STYLES } from "./styles";
import { detailsBlock, footer, header, lineItemsBlock } from "./sections";

export function invoicePrepHtml(inv: InvoiceDetailRecord, company: CompanyInfo): string {
  return `
<style>${INVOICE_PREP_STYLES}</style>
<div class="inv">
  <div class="sheet">
    ${header(company, inv)}
    ${detailsBlock(inv)}
    ${lineItemsBlock(inv)}
    ${footer(company)}
  </div>
</div>`;
}

export function printInvoicePrepSheet(inv: InvoiceDetailRecord): void {
  const title = `Invoice Prep Sheet ${inv.invoiceNumber} — ${inv.clientName}`;
  openPrintDocument(title, invoicePrepHtml(inv, COMPANY));
}
