// NL-TM-01 (slim) Passenger & Cargo Manifest — composes the printable HTML from
// the section builders. ONE code path: pass a TripManifest to render a filled
// manifest, or null to print the blank paper form drivers carry as the
// device-failure fallback. Pre/post-trip vehicle inspections are separate Fleet
// records and are not part of this document.
//
// Page assembly (US-Letter):
//   header + §1 Trip Info + §2 Passenger Manifest + §3 Cargo Manifest + footer

import type { TripManifest } from "@/lib/api";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { TRIP_MANIFEST_STYLES } from "./styles";
import {
  cargoManifestBlock,
  footer,
  header,
  passengerManifestBlock,
  tripInfoBlock,
} from "./sections";

export function tripManifestHtml(m: TripManifest | null, company: CompanyInfo): string {
  return `
<style>${TRIP_MANIFEST_STYLES}</style>
<div class="tm">
  <div class="sheet">
    ${header(company, m)}
    ${tripInfoBlock(m)}
    ${passengerManifestBlock(m)}
    ${cargoManifestBlock(m)}
    ${footer(company)}
  </div>
</div>`;
}

export function printTripManifest(m: TripManifest | null): void {
  const title = m ? `Passenger & Cargo Manifest ${m.tripNumber} — ${m.id}` : "Passenger & Cargo Manifest — Blank Form (NL-TM-01)";
  openPrintDocument(title, tripManifestHtml(m, COMPANY));
}
