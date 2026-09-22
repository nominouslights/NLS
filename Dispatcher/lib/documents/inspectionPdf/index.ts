// Form NL-PTI-01 — Daily Pre-Trip / Post-Trip Vehicle Inspection, as a
// printable sheet. Composes the HTML from the section builders.
//
// ONE code path, like NL-TM-01: pass a VehicleInspection to render the report
// that was filed, or `null` to print the blank paper form. A missing inspection
// is a blank NL-PTI-01 to fill in by hand — never an omitted page.
//
// `inspectionReportHtml` is exported separately from `printInspectionReport` so
// the driver package can concatenate this sheet into a larger document without
// opening a print tab of its own.
//
// Page assembly (US-Letter):
//   header + Areas A/B/C + retired-form rows + Defect Log + Certification
//   + Carrier acknowledgement + Process rules + footer

import type { VehicleInspection } from "@/lib/api/maintenance";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { INSPECTION_STYLES } from "./styles";
import {
  carrierAcknowledgementBlock,
  certificationBlock,
  checklistBlocks,
  defectLogBlock,
  footer,
  header,
  processRules,
  retiredFormBlock,
  type InspectionPrintContext,
} from "./sections";

export type { InspectionPrintContext } from "./sections";

export function inspectionReportHtml(
  insp: VehicleInspection | null,
  ctx: InspectionPrintContext,
  company: CompanyInfo,
): string {
  return `
<style>${INSPECTION_STYLES}</style>
<div class="pti">
  <div class="sheet">
    ${header(company, insp, ctx)}
    ${checklistBlocks(insp, ctx)}
    ${retiredFormBlock(insp, ctx)}
    ${defectLogBlock(insp)}
    ${certificationBlock(insp)}
    ${carrierAcknowledgementBlock(insp)}
    ${processRules()}
    ${footer(company)}
  </div>
</div>`;
}

export function printInspectionReport(
  insp: VehicleInspection | null,
  ctx: InspectionPrintContext,
): void {
  const half = ctx.mode === "PreTrip" ? "Pre-Trip" : "Post-Trip";
  const title = insp
    ? `${half} Inspection ${insp.unit} — ${insp.id}`
    : `${half} Inspection — Blank Form (NL-PTI-01)`;
  openPrintDocument(title, inspectionReportHtml(insp, ctx, COMPANY));
}
