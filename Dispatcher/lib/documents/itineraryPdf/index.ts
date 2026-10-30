// Trip Itinerary — the stop-by-stop sheet a driver runs the day from. Composes
// the printable HTML from the section builders.
//
// `itineraryHtml` is exported separately from `printItinerary` so the driver
// package can concatenate this sheet into a larger document without opening a
// print tab of its own.
//
// Page assembly (US-Letter):
//   header + Trip info + Stops + Freight (omitted when empty) + footer

import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripRecord } from "@/lib/api/trips";
import { COMPANY, type CompanyInfo } from "@/lib/company";
import { openPrintDocument } from "../printDocument";
import { ITINERARY_STYLES } from "./styles";
import { footer, freightBlock, header, stopsBlock, tripInfoBlock } from "./sections";

export function itineraryHtml(
  trip: TripRecord,
  shipments: ShipmentRecord[],
  company: CompanyInfo,
): string {
  return `
<style>${ITINERARY_STYLES}</style>
<div class="itin">
  <div class="sheet">
    ${header(company, trip)}
    ${tripInfoBlock(trip)}
    ${stopsBlock(trip)}
    ${freightBlock(shipments)}
    ${footer(company)}
  </div>
</div>`;
}

export function printItinerary(trip: TripRecord, shipments: ShipmentRecord[]): void {
  openPrintDocument(
    `Trip Itinerary ${trip.tripNumber} — ${trip.serviceDate}`,
    itineraryHtml(trip, shipments, COMPANY),
  );
}
