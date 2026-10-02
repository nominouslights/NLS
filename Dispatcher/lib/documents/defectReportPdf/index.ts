// En-Route Defect Report — the hand-completion sheet a driver carries for
// defects found after the pre-trip (Man. Reg. 95/2008 s.17(2)). Composes the
// printable HTML from the section builders.
//
// `enRouteDefectReportHtml` is exported separately from
// `printEnRouteDefectReport` so the driver package can concatenate this sheet
// into a larger document without opening a print tab of its own — the same
// split every sibling sub-document makes.
//
// Page assembly (US-Letter):
//   header + instruction + Trip + no-defects box + defect table
//   + driver signature + received-by signature + footer

import type { TripRecord } from "@/lib/api/trips";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { DEFECT_REPORT_STYLES } from "./styles";
import { defectsBlock, footer, header, signatureBlock, tripBlock } from "./sections";

export function enRouteDefectReportHtml(trip: TripRecord, company: CompanyInfo): string {
  return `
<style>${DEFECT_REPORT_STYLES}</style>
<div class="edr">
  <div class="sheet">
    ${header(company)}
    ${tripBlock(trip)}
    ${defectsBlock()}
    ${signatureBlock()}
    ${footer(company)}
  </div>
</div>`;
}

export function printEnRouteDefectReport(trip: TripRecord): void {
  openPrintDocument(
    `En-Route Defect Report ${trip.tripNumber} — ${trip.serviceDate}`,
    enRouteDefectReportHtml(trip, COMPANY),
  );
}
