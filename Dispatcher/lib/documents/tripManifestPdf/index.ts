// NL-TM-01 Trip Manifest & Vehicle Inspection Report — composes the 4-page
// printable HTML from the section builders. ONE code path: pass a TripManifest
// to render a filled report, or null to print the blank paper form drivers
// carry as the device-failure fallback.
//
// Page assembly (US-Letter):
//   p1  header + §1 Trip Info + §2 Vehicle & Driver + §3 (Exterior, Safety)
//   p2  §3 continued (Interior, Comms) + §4 Weather & Road + §5 Passengers
//   p3  §6 Cargo + §7 Issues + §8 Completion Log
//   p4  §9 Post-Trip + §10 Certification + footer

import type { TripManifest } from "@/lib/api";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { TRIP_MANIFEST_STYLES } from "./styles";
import {
  cargoManifestBlock,
  certificationBlock,
  completionLogBlock,
  footer,
  header,
  issuesLogBlock,
  passengerManifestBlock,
  postTripBlock,
  preTripBlock,
  slimHeader,
  tripInfoBlock,
  vehicleDriverBlock,
  weatherRoadBlock,
} from "./sections";

export function tripManifestHtml(m: TripManifest | null, company: CompanyInfo): string {
  return `
<style>${TRIP_MANIFEST_STYLES}</style>
<div class="tm">
  <div class="sheet">
    ${header(company, m)}
    ${tripInfoBlock(m)}
    ${vehicleDriverBlock(m)}
    ${preTripBlock(m, ["exterior", "safety"], false)}

    <div class="brk">
      ${slimHeader(m, 2)}
      ${preTripBlock(m, ["interior", "comms"], true)}
      ${weatherRoadBlock(m)}
      ${passengerManifestBlock(m)}
    </div>

    <div class="brk">
      ${slimHeader(m, 3)}
      ${cargoManifestBlock(m)}
      ${issuesLogBlock(m)}
      ${completionLogBlock(m)}
    </div>

    <div class="brk">
      ${slimHeader(m, 4)}
      ${postTripBlock(m)}
      ${certificationBlock(m)}
      ${footer(company)}
    </div>
  </div>
</div>`;
}

export function printTripManifest(m: TripManifest | null): void {
  const title = m ? `Trip Manifest ${m.tripNumber} — ${m.id}` : "Trip Manifest — Blank Form (NL-TM-01)";
  openPrintDocument(title, tripManifestHtml(m, COMPANY));
}
