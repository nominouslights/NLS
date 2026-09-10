// NL-TRM-01 Terminus Summary — composes the printable HTML from the section
// builders. This is a per-venue record of service operated to and from a
// terminus over a period: legs, flow, distance and seat utilization, broken
// down by the corridor's far end — NOT an invoice, and it says so in its
// banner.
//
// Page assembly (US-Letter):
//   header (+ notes) + Report Details + Summary + By Corridor
//   + Not Counted + footer
//
// It renders the same TerminusReport the Reports screen shows and the clipboard
// export copies (lib/reports/terminus.ts) — one derivation, so the three can
// never disagree.

import { COMPANY, type CompanyInfo } from "@/lib/company";
import { periodLabel } from "@/lib/period";
import type { TerminusReport } from "@/lib/reports/terminus";
import { openPrintDocument } from "../printDocument";
import { TERMINUS_REPORT_STYLES } from "./styles";
import {
  corridorsBlock,
  detailsBlock,
  footer,
  header,
  notesBlock,
  reconciliationBlock,
  summaryBlock,
} from "./sections";

export function terminusReportHtml(report: TerminusReport, company: CompanyInfo): string {
  return `
<style>${TERMINUS_REPORT_STYLES}</style>
<div class="trm">
  <div class="sheet">
    ${header(company, report)}
    ${notesBlock(report)}
    ${detailsBlock(report)}
    ${summaryBlock(report)}
    ${corridorsBlock(report)}
    ${reconciliationBlock(report)}
    ${footer(company)}
  </div>
</div>`;
}

export function printTerminusSummary(report: TerminusReport): void {
  const title = `Terminus Summary — ${report.terminus.name} — ${periodLabel(report.period)}`;
  openPrintDocument(title, terminusReportHtml(report, COMPANY));
}
