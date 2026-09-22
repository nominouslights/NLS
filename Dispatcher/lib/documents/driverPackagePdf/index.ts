// The Driver Package — everything a dispatcher hands a driver for one trip, as
// a single printable document: cover → trip manifest → itinerary → pre-trip
// inspection → post-trip inspection, saved through the browser's
// Print → Save as PDF.
//
// PURE COMPOSITION. There is not one section builder in this folder: each part
// is the sub-document's own `*Html()` export, which is exactly why those are
// exported separately from their `print*()` siblings. If a part needs to change,
// it changes in ITS folder, so the standalone print and the package can never
// show different sheets.
//
// Every part is ALWAYS present. A missing manifest or inspection prints as its
// own blank form (all four sub-documents take `null` and render blank paper),
// and the cover's contents list marks it `BLANK — to be completed by hand`.
// Nobody may mistake a blank pre-trip for a passed one — that is the whole
// reason the cover exists.
//
// See ./styles.ts for the two composition traps (`@page` cannot be class-scoped;
// the break class must be unscoped).

import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripManifest, TripRecord } from "@/lib/api/trips";
import type { VehicleInspection } from "@/lib/api/maintenance";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";
import { openPrintDocument } from "../printDocument";
import { inspectionReportHtml } from "../inspectionPdf";
import { itineraryHtml } from "../itineraryPdf";
import { tripManifestHtml } from "../tripManifestPdf";
import { DRIVER_PACKAGE_STYLES } from "./styles";

export interface DriverPackageInput {
  trip: TripRecord;
  manifest: TripManifest | null;
  preTrip: VehicleInspection | null;
  postTrip: VehicleInspection | null;
  shipments: ShipmentRecord[];
}

/** The marker that starts a new sheet BETWEEN sub-documents. Unscoped by design. */
const BREAK = `<div class="nlpkg-brk"></div>`;

const BLANK_LABEL = "BLANK — to be completed by hand";

function stateCell(filled: boolean): string {
  return filled
    ? `<span class="state filled">✓ Filled</span>`
    : `<span class="state blank">▲ ${esc(BLANK_LABEL)}</span>`;
}

function contentsRow(n: number, part: string, detail: string, filled: boolean): string {
  return `<tr>
    <td class="num">${n}</td>
    <td>${esc(part)}</td>
    <td>${esc(detail)}</td>
    <td>${stateCell(filled)}</td>
  </tr>`;
}

function coverSheet(pkg: DriverPackageInput, company: CompanyInfo): string {
  const { trip, manifest, preTrip, postTrip } = pkg;
  const anyBlank = !manifest || !preTrip || !postTrip;
  const paxDetail = manifest
    ? `${manifest.passengers.length} passenger(s), ${manifest.cargo.length} cargo item(s)`
    : "No manifest on file";
  const freightDetail =
    pkg.shipments.length > 0
      ? `${trip.stops.length} stop(s), ${pkg.shipments.length} shipment(s)`
      : `${trip.stops.length} stop(s)`;

  return `
<div class="nlpkg cover">
  <div class="sheet">
    <div class="head">
      <div>
        <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
        <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
      </div>
      <div class="doc-title">
        <div class="t">DRIVER PACKAGE</div>
        <div class="s">Trip ${esc(trip.tripNumber)}</div>
      </div>
    </div>
    <div class="rule"></div>
    <div class="warn">${
      anyBlank
        ? "One or more parts of this package are BLANK FORMS. A blank inspection is not a passed inspection — it must be completed and signed by hand."
        : "Every part of this package is filled from the system of record. Check it against the vehicle before departure."
    }</div>

    ${sectionBar("Trip")}
    ${grid([
      field("Trip #", trip.tripNumber, { mono: true }),
      field("Service Date", trip.serviceDate, { mono: true }),
      field("Driver", trip.driverName),
      field("Vehicle", trip.vehicleUnit, { mono: true }),
    ])}
    ${grid([
      field("Corridor", `${trip.origin} → ${trip.destination}`),
      field("Client", trip.clientName),
    ], 2)}

    ${sectionBar("Contents")}
    <table>
      <thead><tr><th class="num">#</th><th>Part</th><th>Detail</th><th>Status</th></tr></thead>
      <tbody>
        ${contentsRow(1, "Trip Manifest (NL-TM-01)", paxDetail, manifest !== null)}
        ${contentsRow(2, "Trip Itinerary", freightDetail, true)}
        ${contentsRow(3, "Pre-Trip Inspection (NL-PTI-01)", preTrip ? `Result: ${preTrip.result}` : "No pre-trip on file", preTrip !== null)}
        ${contentsRow(4, "Post-Trip Inspection (NL-PTI-01)", postTrip ? `Result: ${postTrip.result}` : "No post-trip on file", postTrip !== null)}
      </tbody>
    </table>

    <div class="foot">
      <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
      Four parts follow, one per sheet. Return the completed package at the end of the run.
    </div>
  </div>
</div>`;
}

export function driverPackageHtml(pkg: DriverPackageInput, company: CompanyInfo): string {
  const { trip, manifest, preTrip, postTrip, shipments } = pkg;
  // The unit the inspection sheets are scoped to. A trip with no vehicle
  // assigned passes null through, which prints EVERY row — `itemsFor`'s
  // fail-safe direction on a compliance form, never a silent narrowing.
  const unit = trip.vehicleUnit;
  const tripNumber = trip.tripNumber;

  return `
<style>${DRIVER_PACKAGE_STYLES}</style>
${coverSheet(pkg, company)}
${tripManifestHtml(manifest, company)}
${BREAK}
${itineraryHtml(trip, shipments, company)}
${BREAK}
${inspectionReportHtml(preTrip, { unit, mode: "PreTrip", tripNumber }, company)}
${BREAK}
${inspectionReportHtml(postTrip, { unit, mode: "PostTrip", tripNumber }, company)}`;
}

export function printDriverPackage(pkg: DriverPackageInput): void {
  openPrintDocument(
    `Driver Package ${pkg.trip.tripNumber} — ${pkg.trip.serviceDate}`,
    driverPackageHtml(pkg, COMPANY),
  );
}
