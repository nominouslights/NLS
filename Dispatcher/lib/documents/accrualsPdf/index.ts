// NL-ACC-01 Accruals Report — composes the printable HTML from the section
// builders. This is a monthly per-client statement that LEADS with upcoming work
// not yet done and monies owed, closes the month with one settled line, carries
// real invoice amounts where invoiced/paid and clearly-marked PO-rate
// estimates elsewhere — NOT an invoice, and it says so in its banner. Money
// prints in ONE Amount column throughout: billed and estimated dollars merge
// into a single figure that carries its own EST. marking.
//
// Page assembly (US-Letter):
//   header (+ degradation notes) + Report Details
//   + the two headline figures + Purchase Orders + Summary (section-grouped)
//   + per-section detail tables + the settled one-liner
//   + Reconciliation + Invoices Referenced + footer
//
// It renders the same AccrualsReport the Reports screen shows and the
// clipboard export copies (lib/billing/accruals.ts) — one derivation, so the
// three can never disagree.

import type { AccrualsReport } from "@/lib/billing/accruals";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { periodLabel } from "@/lib/period";
import { openPrintDocument } from "../printDocument";
import { ACCRUALS_REPORT_STYLES } from "./styles";
import {
  bucketsBlock,
  detailsBlock,
  footer,
  header,
  headlineBlock,
  invoicesBlock,
  notesBlock,
  purchaseOrdersBlock,
  reconciliationBlock,
  summaryBlock,
} from "./sections";

export function accrualsReportHtml(report: AccrualsReport, company: CompanyInfo): string {
  return `
<style>${ACCRUALS_REPORT_STYLES}</style>
<div class="acc">
  <div class="sheet">
    ${header(company, report)}
    ${notesBlock(report)}
    ${detailsBlock(report)}
    ${headlineBlock(report)}
    ${purchaseOrdersBlock(report)}
    ${summaryBlock(report)}
    ${bucketsBlock(report)}
    ${reconciliationBlock(report)}
    ${invoicesBlock(report)}
    ${footer(company)}
  </div>
</div>`;
}

export function printAccrualsReport(report: AccrualsReport): void {
  const title = `Accruals Report — ${report.client.name} — ${periodLabel(report.period)}`;
  openPrintDocument(title, accrualsReportHtml(report, COMPANY));
}
