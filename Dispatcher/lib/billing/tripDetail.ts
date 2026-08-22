// Live trip + passenger detail behind an invoice line, and the ONE derivation
// that turns it into something presentable. Read from the Trips API at display
// time — never snapshotted onto the invoice, so a corrected manifest shows up
// the next time the line is copied or the prep sheet is printed.
//
// Three consumers share `describeLineTrips`, which is the point of this module:
//   - the Billing screen's expandable per-line trip blocks
//   - the per-line and whole-invoice clipboard exports (lib/api/billing.ts)
//   - the printed NL-INV-PREP prep sheet (lib/documents/invoicePdf)
// They must never disagree about what "deadhead" or "did not board" means.
//
// This module deliberately does NOT import lib/api/billing: it takes the line
// structurally (LineTripRef), which keeps the dependency one-directional —
// billing.ts imports this, not the other way round.

import type { TripDirection, TripManifest, TripRecord } from "@/lib/api/trips";

/** One trip id's fetch outcome. `error` = the trip itself failed to load;
 *  `manifestError` = the trip loaded but its manifest fetch failed. */
export interface LoadedTrip {
  trip: TripRecord | null;
  manifest: TripManifest | null;
  error: boolean;
  manifestError: boolean;
}

/** Loaded trip detail keyed by trip id, as held by the Billing detail pane. */
export type TripDetailMap = Record<string, LoadedTrip>;

/** The part of an invoice line this module needs — structural, so
 *  InvoiceLineRecord satisfies it without an import. */
export interface LineTripRef {
  tripIds: string[];
  serviceDate: string | null;
}

/**
 * One trip leg under an invoice line, resolved for display.
 *
 * `deadhead` and `note` are distinct on purpose: a deadhead leg carried no
 * passengers BY DESIGN, whereas a note ("no manifest recorded", "passengers
 * unavailable") means the passenger data is missing. Billing must never read
 * one as the other. When both are false/null, `taken` and `noShows` are
 * authoritative — including the case where every passenger no-showed.
 */
export interface TripLegDetail {
  tripId: string;
  /** null when the trip itself could not be loaded — nothing else is known. */
  tripNumber: string | null;
  direction: TripDirection | null;
  serviceDate: string | null;
  deadhead: boolean;
  /** Why passengers cannot be listed; null when taken/noShows are authoritative. */
  note: string | null;
  /** Passengers who actually boarded. */
  taken: string[];
  /** Manifested passengers who did not board. */
  noShows: string[];
}

const UNKNOWN_TRIP: Omit<TripLegDetail, "tripId"> = {
  tripNumber: null,
  direction: null,
  serviceDate: null,
  deadhead: false,
  note: "trip details unavailable",
  taken: [],
  noShows: [],
};

/** Resolve every trip id on a line into a display-ready leg, in line order. */
export function describeLineTrips(line: LineTripRef, details: TripDetailMap): TripLegDetail[] {
  return line.tripIds.map((tripId) => {
    const loaded = details[tripId];
    const trip = loaded?.trip;
    if (!trip) return { tripId, ...UNKNOWN_TRIP };

    const base = {
      tripId,
      tripNumber: trip.tripNumber,
      direction: trip.direction,
      serviceDate: trip.serviceDate,
    };

    // Ran empty by design — not missing data.
    if (trip.isEmptyLeg) {
      return { ...base, deadhead: true, note: null, taken: [], noShows: [] };
    }

    const note = passengerNote(trip, loaded);
    if (note) return { ...base, deadhead: false, note, taken: [], noShows: [] };

    const passengers = loaded.manifest?.passengers ?? [];
    return {
      ...base,
      deadhead: false,
      note: null,
      taken: passengers.filter((p) => p.boardedOn).map((p) => p.name),
      noShows: passengers.filter((p) => !p.boardedOn).map((p) => p.name),
    };
  });
}

function passengerNote(trip: TripRecord, loaded: LoadedTrip): string | null {
  if (loaded.manifestError) return "passengers unavailable";
  if (!trip.manifestId) return "no manifest recorded";
  if ((loaded.manifest?.passengers ?? []).length === 0) return "manifest has no passengers";
  return null;
}

/** Direction as a glyph + word, for print and plain text alike (never colour). */
export function directionLabel(direction: TripDirection | null): string {
  if (direction === "Outbound") return "→ Outbound";
  if (direction === "Inbound") return "← Inbound";
  return "—";
}

/** Head of a leg's clipboard row: "- NL-1042 outbound · 2026-07-03". */
export function legHeadText(leg: TripLegDetail): string {
  return `- ${leg.tripNumber}${leg.direction ? ` ${leg.direction.toLowerCase()}` : ""} · ${leg.serviceDate}`;
}

/** One leg as a single clipboard line, including its passenger roll-up. */
export function legText(leg: TripLegDetail): string {
  if (!leg.tripNumber) return "- trip details unavailable";
  const head = legHeadText(leg);
  if (leg.deadhead) return `${head} — DEADHEAD (ran empty, no passengers)`;
  if (leg.note) return `${head} — ${leg.note}`;
  let row = `${head} — passengers (${leg.taken.length}): ${leg.taken.join(", ") || "none boarded"}`;
  if (leg.noShows.length > 0) row += ` · did not board: ${leg.noShows.join(", ")}`;
  return row;
}
