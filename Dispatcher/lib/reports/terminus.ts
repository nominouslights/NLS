// Terminus summary — the ONE derivation that turns a period of trips into the
// distance-and-utilization picture for a single terminus community, which the
// Reports screen renders, the NL-TRM-01 sheet prints, and the clipboard export
// copies. All three consume the same TerminusReport so they can never disagree
// about a leg, a kilometre, or a seat. Sibling of lib/billing/accruals.ts, and
// built to the same rules — this one just counts vehicles and seats instead of
// dollars.
//
// This report exists to be handed to the operator at the far end of a corridor
// while negotiating billing, so every figure it prints has to survive being
// questioned line by line. That drives the ground rules:
//
//   - A leg counts for a terminus only when the terminus is one of its own
//     CORRIDOR ENDPOINTS. A leg that merely passes through the community on its
//     way somewhere else is real work, but it is not terminus work — those are
//     counted separately under "passed through" and never folded into the
//     totals.
//   - A trip's stop list is ALREADY in its own travel order, whichever
//     direction it is. Both creation paths — Trip.CreateReturn and
//     TripGenerationWorker — build the inbound leg with its stops reversed and
//     origin/destination swapped, so `direction` labels the pairing; it is not
//     an instruction to read the list backwards. Reversing inbound legs here
//     would invert every arrival and departure in the report.
//   - Only legs that ACTUALLY RAN carry figures. Cancelled trips never turned a
//     wheel, and a trip scheduled after today has not yet — both are listed for
//     reconciliation and excluded from every kilometre and every seat, so the
//     totals describe work done rather than work hoped for. A written-off trip
//     DID run: the money was lost, the distance was not, so it counts here even
//     though the accruals report excludes it.
//   - Seat utilization is measured only over legs that can carry a passenger and
//     have a capacity on file. A deadhead leg is empty BY DESIGN and a leg with
//     no recorded capacity is simply unknown — averaging either into the
//     denominator would understate the corridor and hand the other side of the
//     table a number we would have to walk back. Both exclusions are counted and
//     stated in the report's own notes rather than being silently applied.
//   - Nothing is estimated or inferred. Where a measure has no basis the figure
//     is null and prints as "not available", never as a zero.

import { formatKm } from "@/lib/api/format";
import type { StopRecord } from "@/lib/api/stops";
import { sortTrips, type TripRecord, type TripStop } from "@/lib/api/trips";
import { periodLabel, type Period } from "@/lib/period";
import type { StatusKind } from "@/lib/theme";

// ---------------------------------------------------------------------------
// The terminus being reported on
// ---------------------------------------------------------------------------

/** The venue a report is built for — a business Northern Link deals with (the
 *  Best Western in Thompson, the Lynn Inn in Lynn Lake), not the town it sits
 *  in. Sourced from the Stop catalog, but kept as its own shape so the
 *  derivation never depends on the full StopRecord. */
export interface TerminusRef {
  /** Catalog stop id — the reliable way to match a leg's snapshotted stops. */
  id: string | null;
  /** The venue itself: "Best Western Hotel & Suites". */
  name: string;
  /** The community it sits in: "Thompson". */
  community: string;
  province: string;
  /** "Thompson, MB" — where the venue is, for the sheet's sub-heading. */
  place: string;
  /** "Best Western Hotel & Suites, Thompson" — venue first, community second. */
  label: string;
  stopType: string | null;
}

export function terminusRefFor(stop: StopRecord): TerminusRef {
  return {
    id: stop.id,
    name: stop.name,
    community: stop.city,
    province: stop.province,
    place: `${stop.city}, ${stop.province}`,
    label: `${stop.name}, ${stop.city}`,
    stopType: stop.stopType,
  };
}

/** The catalog value marking a venue as a terminus — the backend's
 *  StopType.Terminus, which travels as its enum name. */
export const TERMINUS_STOP_TYPE = "Terminus";

/**
 * The venues a terminus report can be built for: active stops a dispatcher has
 * flagged Terminus. A place we merely call at — an airport standing in as an
 * origin, a by-request drop-off at a private address — is deliberately not one,
 * however often it appears on a trip, because the report describes a business
 * relationship rather than a kind of location.
 */
export function terminusStops(stops: StopRecord[]): StopRecord[] {
  return stops
    .filter((s) => s.active && s.stopType === TERMINUS_STOP_TYPE)
    // Community breaks the tie: two venues can share a name ("Town Hall"), and
    // an arbitrary order between them would shuffle the picker between loads.
    .sort((a, b) => a.name.localeCompare(b.name) || a.city.localeCompare(b.city));
}

// ---------------------------------------------------------------------------
// Figures
// ---------------------------------------------------------------------------

/** Distance over a set of legs. `measuredLegs` is the average's denominator —
 *  legs with no distance on file are excluded from it, not treated as zero. */
export interface DistanceFigures {
  measuredLegs: number;
  /** Legs carrying no distance — excluded above, disclosed in the notes. */
  unmeasuredLegs: number;
  totalKm: number;
  /** totalKm / measuredLegs, or null when no leg carries a distance. */
  averageKm: number | null;
}

/** Seat utilization over a set of legs. Deadhead legs and legs with no capacity
 *  on file are excluded from BOTH sides of the ratio and counted separately —
 *  see the header comment for why that is not optional. */
export interface UtilizationFigures {
  measuredLegs: number;
  /** Empty by design — excluded from the ratio, still counted in distance. */
  deadheadLegs: number;
  /** No seating capacity on file — excluded because it is unknown, not zero. */
  unmeasuredLegs: number;
  seatsOffered: number;
  seatsFilled: number;
  /** seatsFilled / seatsOffered as a 0–1 fraction, or null when unmeasurable. */
  rate: number | null;
}

/** One corridor served by the terminus, keyed by its far end — every leg whose
 *  other endpoint is that place, in either direction. */
export interface TerminusCorridor {
  /** Far-end catalog stop id, or its normalized name for a legacy free-text
   *  stop. Keying on identity rather than name keeps two same-named venues in
   *  different communities apart — a "Town Office" in each of two towns is
   *  entirely plausible here. */
  key: string;
  /** The venue at the other end: "Best Western Hotel & Suites". */
  farEndVenue: string;
  /** Its community, or null when the far stop is not in the catalog. */
  farEndCommunity: string | null;
  /** Whether the far end is itself a flagged terminus venue. False for an
   *  airport or a by-request address — worth seeing on the row, because a
   *  corridor to a place we have no relationship with is a different fact. */
  farEndIsTerminus: boolean;
  /** "Best Western Hotel & Suites, Thompson". The terminus this report is FOR
   *  is named once in the header, never repeated on every row. */
  label: string;
  /** Route names seen on this corridor; several when it is run more than one way. */
  routeNames: string[];
  legs: TripRecord[];
  /** Legs whose last stop is the terminus — traffic INTO the community. */
  arrivals: number;
  /** Legs whose first stop is the terminus — traffic OUT of it. */
  departures: number;
  /** Legs that both start and end at the terminus — counted in neither above. */
  turnarounds: number;
  distance: DistanceFigures;
  utilization: UtilizationFigures;
}

export interface TerminusReport {
  terminus: TerminusRef;
  period: Period;
  /** The day the operated / not-yet-run split was made against. */
  today: string;
  corridors: TerminusCorridor[];
  /** Legs that ran, across every corridor — the figures below describe these. */
  operatedLegs: number;
  arrivals: number;
  departures: number;
  turnarounds: number;
  distance: DistanceFigures;
  utilization: UtilizationFigures;
  /** The terminus is an intermediate stop, never an endpoint — not counted. */
  passedThrough: TripRecord[];
  /** Scheduled after `today`: real work, not yet run, so never counted. */
  upcoming: TripRecord[];
  /** Never ran — listed for reconciliation only. */
  cancelled: TripRecord[];
  /** Plain-language banners; each one explains a gap a reader would otherwise
   *  have to guess at. Rendered above the figures on screen and on the sheet. */
  notes: string[];
}

// ---------------------------------------------------------------------------
// Matching a leg to the terminus
// ---------------------------------------------------------------------------

function norm(name: string): string {
  return name.trim().toLowerCase();
}

/** A leg's stops in TRAVEL order — see the header comment: a leg's own order is
 *  already its travel order in both directions. Falls back to origin →
 *  destination for legacy free-text trips carrying no route snapshot, which is
 *  the same fallback `stopNames` makes. */
/** A leg's own snapshot of one stop: a name, and an id when it came from the
 *  catalog. It carries no community — that has to be resolved against the
 *  catalog, which is why buildTerminusReport takes the stop list. */
type LegStop = Pick<TripStop, "name" | "stopId">;

function travelStops(t: TripRecord): LegStop[] {
  const ordered = [...t.stops].sort((a, b) => a.order - b.order);
  if (ordered.length >= 2) return ordered;
  return [t.origin, t.destination].filter(Boolean).map((name) => ({ name }));
}

/** Catalog ids decide it when both sides have one; a normalized name match is
 *  the fallback for legacy free-text stops that carry no id. Never the other way
 *  round — two same-named communities in different provinces must not merge. */
function isTerminus(ref: TerminusRef, stop: LegStop): boolean {
  if (ref.id && stop.stopId) return stop.stopId === ref.id;
  return norm(stop.name) === norm(ref.name);
}

/**
 * How a leg relates to the terminus. `null` means it never touched the venue —
 * which includes a leg that ran the usual corridor but SUBSTITUTED a different
 * endpoint, such as an airport pickup standing in for the hotel origin. Those
 * are omitted from the report entirely rather than listed: the venue's door saw
 * none of that traffic, and this report describes only legs that reached it.
 */
type Relation =
  | { role: "arrival" | "departure" | "turnaround"; farStop: LegStop }
  | { role: "through" }
  | null;

function relate(ref: TerminusRef, t: TripRecord): Relation {
  const stops = travelStops(t);
  if (stops.length === 0) return null;

  const first = stops[0];
  const last = stops[stops.length - 1];
  const atFirst = isTerminus(ref, first);
  const atLast = isTerminus(ref, last);

  // Starts and ends at the terminus: a local turnaround. Its "far end" is the
  // furthest point it actually reached, so it still groups under a corridor.
  if (atFirst && atLast) {
    const middle = stops.slice(1, -1);
    const furthest = middle[middle.length - 1];
    return {
      role: "turnaround",
      farStop: furthest ?? { name: ref.name, stopId: ref.id ?? undefined },
    };
  }
  if (atLast) return { role: "arrival", farStop: first };
  if (atFirst) return { role: "departure", farStop: last };

  return stops.slice(1, -1).some((s) => isTerminus(ref, s)) ? { role: "through" } : null;
}

// ---------------------------------------------------------------------------
// Figures over a set of legs
// ---------------------------------------------------------------------------

function distanceOf(legs: TripRecord[]): DistanceFigures {
  const measured = legs.filter((t) => t.distanceKm > 0);
  const totalKm = measured.reduce((sum, t) => sum + t.distanceKm, 0);
  return {
    measuredLegs: measured.length,
    unmeasuredLegs: legs.length - measured.length,
    totalKm,
    averageKm: measured.length > 0 ? totalKm / measured.length : null,
  };
}

function utilizationOf(legs: TripRecord[]): UtilizationFigures {
  const deadhead = legs.filter((t) => t.isEmptyLeg);
  const carrying = legs.filter((t) => !t.isEmptyLeg);
  const measured = carrying.filter((t) => t.seatsCapacity !== null && t.seatsCapacity > 0);

  const seatsOffered = measured.reduce((sum, t) => sum + (t.seatsCapacity ?? 0), 0);
  const seatsFilled = measured.reduce((sum, t) => sum + t.seatsConfirmed, 0);

  return {
    measuredLegs: measured.length,
    deadheadLegs: deadhead.length,
    unmeasuredLegs: carrying.length - measured.length,
    seatsOffered,
    seatsFilled,
    rate: seatsOffered > 0 ? seatsFilled / seatsOffered : null,
  };
}

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------

/**
 * Whether a leg had already run by `today`. A cancelled trip never ran and is
 * handled before this; a trip still merely Scheduled for a future date has not
 * run yet. Everything else — under way, awaiting billing, invoiced, paid, or
 * written off — put a vehicle on the road, which is what this report measures.
 */
function hasRun(t: TripRecord, today: string): boolean {
  return !(t.status === "Scheduled" && t.serviceDate > today);
}

export function buildTerminusReport({
  terminus,
  period,
  today,
  trips,
  stops,
}: {
  terminus: TerminusRef;
  period: Period;
  today: string;
  trips: TripRecord[];
  /** The Stop catalog. A leg's snapshotted stops carry a name and an id but no
   *  community, so the catalog is the only way to say which town a far end sits
   *  in — and whether it is itself a terminus venue rather than a place we
   *  merely call at. */
  stops: StopRecord[];
}): TerminusReport {
  const catalog = new Map(stops.map((s) => [s.id, s]));

  const passedThrough: TripRecord[] = [];
  const upcoming: TripRecord[] = [];
  const cancelled: TripRecord[] = [];
  /** Operated endpoint legs, grouped by far end. */
  const byCorridor = new Map<string, { farStop: LegStop; legs: TripRecord[]; roles: Relation[] }>();

  for (const trip of sortTrips(trips)) {
    const relation = relate(terminus, trip);
    if (relation === null) continue;
    if (relation.role === "through") {
      passedThrough.push(trip);
      continue;
    }

    if (trip.status === "Cancelled") {
      cancelled.push(trip);
      continue;
    }
    if (!hasRun(trip, today)) {
      upcoming.push(trip);
      continue;
    }

    const key = relation.farStop.stopId ?? norm(relation.farStop.name);
    const bucket = byCorridor.get(key) ?? { farStop: relation.farStop, legs: [], roles: [] };
    bucket.legs.push(trip);
    bucket.roles.push(relation);
    byCorridor.set(key, bucket);
  }

  const corridors: TerminusCorridor[] = [...byCorridor.entries()]
    .map(([key, bucket]) => {
      // The snapshot's name is what actually ran, so it wins for display; the
      // catalog only supplies what a snapshot cannot carry — the community, and
      // whether that far end is a terminus venue in its own right.
      const far = bucket.farStop.stopId ? catalog.get(bucket.farStop.stopId) : undefined;
      const community = far?.city ?? null;
      return {
        key,
        farEndVenue: bucket.farStop.name,
        farEndCommunity: community,
        farEndIsTerminus: far?.stopType === TERMINUS_STOP_TYPE,
        label: community ? `${bucket.farStop.name}, ${community}` : bucket.farStop.name,
        routeNames: [...new Set(bucket.legs.map((t) => t.routeName).filter(Boolean))].sort(),
        legs: bucket.legs,
        arrivals: bucket.roles.filter((r) => r?.role === "arrival").length,
        departures: bucket.roles.filter((r) => r?.role === "departure").length,
        turnarounds: bucket.roles.filter((r) => r?.role === "turnaround").length,
        distance: distanceOf(bucket.legs),
        utilization: utilizationOf(bucket.legs),
      };
    })
    // Busiest corridor first — that is the one the conversation starts with.
    .sort((a, b) => b.legs.length - a.legs.length || a.farEndVenue.localeCompare(b.farEndVenue));

  const operated = corridors.flatMap((c) => c.legs);
  const report: TerminusReport = {
    terminus,
    period,
    today,
    corridors,
    operatedLegs: operated.length,
    arrivals: corridors.reduce((n, c) => n + c.arrivals, 0),
    departures: corridors.reduce((n, c) => n + c.departures, 0),
    turnarounds: corridors.reduce((n, c) => n + c.turnarounds, 0),
    distance: distanceOf(operated),
    utilization: utilizationOf(operated),
    passedThrough,
    upcoming,
    cancelled,
    notes: [],
  };
  report.notes = buildNotes(report);
  return report;
}

/** The banners. Each one names a class of leg the figures deliberately leave
 *  out, so nothing above has to be taken on trust. */
function buildNotes(report: TerminusReport): string[] {
  const notes: string[] = [];
  const { utilization: u, distance: d } = report;
  const legWord = (n: number) => (n === 1 ? "leg" : "legs");

  if (report.operatedLegs === 0) {
    notes.push(
      `No leg began or ended at ${report.terminus.name} in ${periodLabel(report.period)}. ` +
        "The report can still be printed as an explicit nil statement.",
    );
  }

  if (u.deadheadLegs > 0) {
    notes.push(
      `${u.deadheadLegs} operated ${legWord(u.deadheadLegs)} ran as deadhead — empty by design. ` +
        "Their distance counts; they are excluded from seat utilization, which would otherwise " +
        "be dragged down by legs that were never meant to carry anyone.",
    );
  }

  if (u.unmeasuredLegs > 0) {
    notes.push(
      `${u.unmeasuredLegs} operated ${legWord(u.unmeasuredLegs)} carry no seating capacity on ` +
        "file and are excluded from seat utilization — unknown capacity is not the same as zero.",
    );
  }

  if (d.unmeasuredLegs > 0) {
    notes.push(
      `${d.unmeasuredLegs} operated ${legWord(d.unmeasuredLegs)} carry no distance on file and ` +
        "are excluded from the distance total and average.",
    );
  }

  if (report.operatedLegs > 0 && u.rate === null) {
    notes.push(
      "Seat utilization is not available for this period — no operated leg carries both a " +
        "seating capacity and the ability to carry passengers.",
    );
  }

  if (report.upcoming.length > 0) {
    notes.push(
      `${report.upcoming.length} ${legWord(report.upcoming.length)} in this period ${
        report.upcoming.length === 1 ? "is" : "are"
      } scheduled after ${report.today} and had not run when this report was prepared — ` +
        "listed for reference, counted in nothing.",
    );
  }

  if (report.cancelled.length > 0) {
    notes.push(
      `${report.cancelled.length} cancelled ${legWord(report.cancelled.length)} ` +
        `${report.cancelled.length === 1 ? "is" : "are"} listed for reconciliation only — ` +
        "a cancelled trip never ran and counts toward no figure here.",
    );
  }

  if (report.passedThrough.length > 0) {
    notes.push(
      `${report.passedThrough.length} further ${legWord(report.passedThrough.length)} passed ` +
        `through ${report.terminus.name} without starting or ending there. This is a terminus ` +
        "report, so they are listed separately and counted in nothing above.",
    );
  }

  return notes;
}

// ---------------------------------------------------------------------------
// Display — one wording per figure, used by the screen, the sheet and the copy
// ---------------------------------------------------------------------------

const pct = new Intl.NumberFormat("en-CA", { style: "percent", maximumFractionDigits: 0 });

/** "62%", or an em dash when nothing was measurable. Never renders 0% for
 *  "unknown" — an absent measure and an empty vehicle are different facts. */
export function utilizationRateLabel(u: UtilizationFigures): string {
  return u.rate === null ? "—" : pct.format(u.rate);
}

/** The rate with the arithmetic behind it spelled out, so the figure can be
 *  checked rather than believed. */
export function utilizationLabel(u: UtilizationFigures): string {
  if (u.rate === null) return "Not available — no measurable leg";
  const legs = u.measuredLegs === 1 ? "leg" : "legs";
  return `${pct.format(u.rate)} · ${u.seatsFilled.toLocaleString("en-CA")} of ${u.seatsOffered.toLocaleString(
    "en-CA",
  )} seats over ${u.measuredLegs} ${legs}`;
}

/**
 * Colour + glyph + words for the utilization figure. Deliberately `info` at any
 * measured rate rather than banding into good/bad: nobody has set a target
 * utilization for these corridors, and inventing a threshold would print a
 * verdict this report has no standing to make. The only judgement here is that
 * a MISSING measure is worth a caution, because it means data is absent.
 */
export function utilizationChip(u: UtilizationFigures): { kind: StatusKind; label: string } {
  return u.rate === null
    ? { kind: "soon", label: "Seat utilization not available" }
    : { kind: "info", label: `Seat utilization ${pct.format(u.rate)}` };
}

export function distanceLabel(d: DistanceFigures): string {
  return formatKm(Math.round(d.totalKm));
}

/** "367 km / leg" — the average over legs that actually carry a distance. */
export function averageKmLabel(d: DistanceFigures): string {
  return d.averageKm === null ? "—" : `${formatKm(Math.round(d.averageKm))} / leg`;
}

/** "18 in · 16 out" (· "2 turnaround" only when there are any) — the balance of
 *  flow, which is usually the first thing a terminus partner asks about. */
export function flowLabel(f: { arrivals: number; departures: number; turnarounds: number }): string {
  const parts = [`${f.arrivals} in`, `${f.departures} out`];
  if (f.turnarounds > 0) parts.push(`${f.turnarounds} turnaround`);
  return parts.join(" · ");
}

/** Plain-text export — the same figures as the screen and the sheet, shaped to
 *  paste into an email or a spreadsheet without losing the caveats. */
export function terminusClipboardText(report: TerminusReport): string {
  const lines: string[] = [
    `TERMINUS SUMMARY — ${report.terminus.name}`,
    `${report.terminus.place} · ${periodLabel(report.period)} · prepared ${report.today}`,
    "",
    `Operated legs        ${report.operatedLegs}`,
    `Flow                 ${flowLabel(report)}`,
    `Distance             ${distanceLabel(report.distance)}`,
    `Average per leg      ${averageKmLabel(report.distance)}`,
    `Seat utilization     ${utilizationLabel(report.utilization)}`,
    "",
    "BY CORRIDOR",
    ["Corridor", "Legs", "In", "Out", "Distance", "Avg/leg", "Seats"].join("\t"),
  ];

  for (const c of report.corridors) {
    lines.push(
      [
        c.label.replace(/\s+/g, " "),
        c.legs.length,
        c.arrivals,
        c.departures,
        distanceLabel(c.distance),
        averageKmLabel(c.distance),
        utilizationRateLabel(c.utilization),
      ].join("\t"),
    );
  }

  if (report.notes.length > 0) {
    lines.push("", "NOTES");
    for (const note of report.notes) lines.push(`- ${note}`);
  }

  return lines.join("\n");
}
