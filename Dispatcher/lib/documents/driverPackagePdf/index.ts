// The Driver Package — everything a dispatcher hands a driver for one trip, as
// a single printable document, saved through the browser's Print → Save as PDF:
//
//   cover → 1 trip manifest (NL-TM-01) → 2 itinerary → 3 pre-trip inspection
//   (NL-PTI-01) → 4 en-route defect report
//
// The post-trip inspection is NOT in the package. The driver files the
// post-trip on the tablet (it still prints standalone from
// lib/documents/inspectionPdf), so a paper copy here was a second, competing
// place to record it. What the driver does need on paper during the run is
// somewhere to write down a defect found AFTER the pre-trip — the en-route
// defect report (Man. Reg. 95/2008 s.17(2)), which closes the package.
//
// PURE COMPOSITION. There is not one section builder in this folder: each part
// is the sub-document's own `*Html()` export, which is exactly why those are
// exported separately from their `print*()` siblings. If a part needs to change,
// it changes in ITS folder, so the standalone print and the package can never
// show different sheets.
//
// Every part is ALWAYS present. A missing manifest or pre-trip prints as its
// own blank form (those sub-documents take `null` and render blank paper), and
// the cover's contents list marks it `BLANK — to be completed by hand`. The
// en-route defect report has no record behind it at all, so it is always marked
// that way. Nobody may mistake a blank pre-trip for a passed one — that is the
// whole reason the cover exists.
//
// The cover's contents rows, its "N parts follow" line and the sheets are all
// derived from ONE list (`packageParts`), so they cannot describe different
// packages.
//
// See ./styles.ts for the two composition traps (`@page` cannot be class-scoped;
// the break class must be unscoped).

import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripManifest, TripRecord } from "@/lib/api/trips";
import type { VehicleInspection } from "@/lib/api/maintenance";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { esc, field, grid, sectionBar } from "../workOrderPdf/html";
import { openPrintDocument } from "../printDocument";
import { enRouteDefectReportHtml } from "../defectReportPdf";
import { inspectionReportHtml } from "../inspectionPdf";
import { itineraryHtml } from "../itineraryPdf";
import { tripManifestHtml } from "../tripManifestPdf";
import { DRIVER_PACKAGE_STYLES } from "./styles";

export interface DriverPackageInput {
  trip: TripRecord;
  manifest: TripManifest | null;
  preTrip: VehicleInspection | null;
  shipments: ShipmentRecord[];
}

/** The marker that starts a new sheet BETWEEN sub-documents. Unscoped by design. */
const BREAK = `<div class="nlpkg-brk"></div>`;

const BLANK_LABEL = "BLANK — to be completed by hand";

/** One part of the package: its cover row, and the sheet it prints. */
interface PackagePart {
  title: string;
  detail: string;
  /** Filled from a record. False prints the BLANK marker on the cover. */
  filled: boolean;
  /** Hand-completion by design — never has a record, so it never makes the
   *  cover claim a record is missing. */
  handOnly: boolean;
  html: string;
}

const COUNT_WORDS = ["No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten"];

/** The parts, in print order — the single source for the cover and the sheets. */
function packageParts(pkg: DriverPackageInput, company: CompanyInfo): PackagePart[] {
  const { trip, manifest, preTrip, shipments } = pkg;
  // The unit the inspection sheet is scoped to. A trip with no vehicle
  // assigned passes null through, which prints EVERY row — `itemsFor`'s
  // fail-safe direction on a compliance form, never a silent narrowing.
  const unit = trip.vehicleUnit;
  const tripNumber = trip.tripNumber;

  return [
    {
      title: "Trip Manifest (NL-TM-01)",
      detail: manifest
        ? `${manifest.passengers.length} passenger(s), ${manifest.cargo.length} cargo item(s)`
        : "No manifest on file",
      filled: manifest !== null,
      handOnly: false,
      html: tripManifestHtml(manifest, company),
    },
    {
      title: "Trip Itinerary",
      detail:
        shipments.length > 0
          ? `${trip.stops.length} stop(s), ${shipments.length} shipment(s)`
          : `${trip.stops.length} stop(s)`,
      filled: true,
      handOnly: false,
      html: itineraryHtml(trip, shipments, company),
    },
    {
      title: "Pre-Trip Inspection (NL-PTI-01)",
      detail: preTrip ? `Result: ${preTrip.result}` : "No pre-trip on file",
      filled: preTrip !== null,
      handOnly: false,
      html: inspectionReportHtml(preTrip, { unit, mode: "PreTrip", tripNumber }, company),
    },
    {
      title: "En-Route Defect Report",
      detail: "Defects found after the pre-trip",
      filled: false,
      handOnly: true,
      html: enRouteDefectReportHtml(trip, company),
    },
  ];
}

function stateCell(filled: boolean): string {
  return filled
    ? `<span class="state filled">✓ Filled</span>`
    : `<span class="state blank">▲ ${esc(BLANK_LABEL)}</span>`;
}

function contentsRow(n: number, part: PackagePart): string {
  return `<tr>
    <td class="num">${n}</td>
    <td>${esc(part.title)}</td>
    <td>${esc(part.detail)}</td>
    <td>${stateCell(part.filled)}</td>
  </tr>`;
}

/** "Four parts follow, one per sheet." — counted from the parts, never typed. */
export function partsFollowText(count: number): string {
  const word = COUNT_WORDS[count] ?? String(count);
  return `${word} ${count === 1 ? "part follows" : "parts follow"}, one per sheet.`;
}

function coverSheet(trip: TripRecord, parts: PackagePart[], company: CompanyInfo): string {
  const recordMissing = parts.some((p) => !p.filled && !p.handOnly);

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
      recordMissing
        ? "One or more parts of this package are BLANK FORMS. A blank inspection is not a passed inspection — it must be completed and signed by hand."
        : "The manifest, itinerary and pre-trip are filled from the system of record. Check them against the vehicle before departure, and complete the en-route defect report by hand during the run."
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
        ${parts.map((part, i) => contentsRow(i + 1, part)).join("")}
      </tbody>
    </table>

    <div class="foot">
      <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
      ${esc(partsFollowText(parts.length))} Return the completed package at the end of the run.
    </div>
  </div>
</div>`;
}

export function driverPackageHtml(pkg: DriverPackageInput, company: CompanyInfo): string {
  const parts = packageParts(pkg, company);
  // The cover carries its own page-break-after, so the markers go BETWEEN the
  // parts only — one fewer than there are parts.
  return `
<style>${DRIVER_PACKAGE_STYLES}</style>
${coverSheet(pkg.trip, parts, company)}
${parts.map((p) => p.html).join(`\n${BREAK}\n`)}`;
}

export function printDriverPackage(pkg: DriverPackageInput): void {
  openPrintDocument(
    `Driver Package ${pkg.trip.tripNumber} — ${pkg.trip.serviceDate}`,
    driverPackageHtml(pkg, COMPANY),
  );
}
