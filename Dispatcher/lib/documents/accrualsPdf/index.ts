// NL-ACC-01 Accruals Report — composes the printable HTML from the section
// builders. This is a monthly per-client statement of trips by billing state
// with real invoice amounts where invoiced/paid and clearly-marked contract-
// rate estimates elsewhere — NOT an invoice, and it says so in its banner.
//
// Page assembly (US-Letter):
//   header (+ degradation notes) + Report Details + Summary
//   + per-bucket detail tables + Reconciliation + Invoices Referenced + footer
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
  invoicesBlock,
  notesBlock,
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
