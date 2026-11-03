/**
 * Route timetables — reading a trip's per-stop times, and authoring a route's per-stop offsets.
 *
 * A route stores minutes-after-departure rather than clock times, so one corridor can serve a
 * 06:30 run and a 14:00 run without being duplicated. Each stop carries, per leg, an ARRIVAL and
 * a DEPARTURE offset (a null arrival means "arrives when it departs").
 *
 * Turning offsets into clock times is the API's job, not this file's: every trip stop arrives
 * with `scheduledArrival` / `scheduledDeparture` already resolved from the trip's own window
 * start and direction. The trip-side helpers below only FIND a stop and read those stored times
 * — there is no offset arithmetic for trips anywhere in the Dispatcher.
 *
 * The authoring half (bottom of the file) is the route form's: it shows offsets as clock times
 * against a throw-away reference departure and checks a leg against the same rules the backend
 * enforces, so the dispatcher sees which leg is wrong before saving.
 *
 * Everything here is pure: no API calls, no React.
 */

import type { RouteStopInput, TripStop } from "@/lib/api/trips";

/** The minimum a caller needs to read stop times — `TripRecord` satisfies it structurally. */
export interface TimetableTrip {
  stops: TripStop[];
}

/** Minutes since midnight for "HH:MM" / "HH:MM:SS", or null if unparseable. */
function toMinutes(time: string | null | undefined): number | null {
  if (!time) return null;
  const [h, m] = time.split(":");
  const hours = Number(h);
  const minutes = Number(m ?? "0");
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return null;
  return hours * 60 + minutes;
}

/** Minutes since midnight → "HH:MM", wrapping past midnight so a long overnight leg still reads. */
export function minutesToHhmm(minutes: number): string {
  const wrapped = ((minutes % 1440) + 1440) % 1440;
  const h = Math.floor(wrapped / 60);
  const m = wrapped % 60;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
}

/** A stored "HH:MM:SS" → "HH:MM"; null/absent/unparseable → null. */
function storedTime(time: string | null | undefined): string | null {
  if (!time) return null;
  return /^\d{2}:\d{2}/.test(time) ? time.slice(0, 5) : null;
}

// ---------------------------------------------------------------------------
// Reading — a trip's stored per-stop times
// ---------------------------------------------------------------------------

/**
 * The stop on this trip matching `stopId`, or — for legacy free-text stops that carry no catalog
 * id — the one matching `stopName`. Returns undefined when neither identifies a stop, which is
 * the normal case for a free-form trip.
 */
function findStop(
  trip: TimetableTrip,
  stopId: string | null | undefined,
  stopName: string | null | undefined,
): TripStop | undefined {
  if (stopId) {
    const byId = trip.stops.find((s) => s.stopId === stopId);
    if (byId) return byId;
  }
  if (stopName) {
    const wanted = stopName.trim().toLowerCase();
    return trip.stops.find((s) => s.name.trim().toLowerCase() === wanted);
  }
  return undefined;
}

/**
 * The scheduled DEPARTURE ("HH:MM") from the given stop on this trip — a passenger's pickup
 * time. Null when the stop isn't on this trip or the leg has no timetable; null is the caller's
 * cue to fall back to the trip-level time.
 */
export function stopDepartureOnTrip(
  trip: TimetableTrip,
  stopId: string | null | undefined,
  stopName?: string | null,
): string | null {
  return storedTime(findStop(trip, stopId, stopName)?.scheduledDeparture);
}

/**
 * The scheduled ARRIVAL ("HH:MM") at the given stop on this trip — a passenger's drop-off time.
 * Null under the same conditions as `stopDepartureOnTrip`.
 */
export function stopArrivalOnTrip(
  trip: TimetableTrip,
  stopId: string | null | undefined,
  stopName?: string | null,
): string | null {
  return storedTime(findStop(trip, stopId, stopName)?.scheduledArrival);
}

/** Whether this trip's leg has a timetable at all — drives whether the UI shows per-stop times. */
export function hasTimetable(trip: TimetableTrip): boolean {
  return trip.stops.some((stop) => storedTime(stop.scheduledDeparture) !== null);
}

/** The stop fields a pickup-email recipient carries (`ManifestPassenger` satisfies it). */
export interface RecipientStops {
  pickupStopId?: string | null;
  pickupStopName?: string | null;
  dropoffStopId?: string | null;
  dropoffStopName?: string | null;
}

/**
 * A passenger's own times: pickup = DEPARTURE from the boarding stop, drop-off = ARRIVAL at the
 * alighting stop. Either is null when it cannot be resolved — the caller falls back to the trip
 * window.
 */
export function recipientTimes(
  trip: TimetableTrip,
  passenger: RecipientStops,
): { pickup: string | null; dropoff: string | null } {
  return {
    pickup: stopDepartureOnTrip(trip, passenger.pickupStopId, passenger.pickupStopName),
    dropoff: stopArrivalOnTrip(trip, passenger.dropoffStopId, passenger.dropoffStopName),
  };
}

/** One row of a trip's printed/on-screen stop timetable, in stop order. */
export interface StopTimetableRow {
  name: string;
  /** "HH:MM", or null on the origin (nothing to arrive at) and on an untimed leg. */
  arrive: string | null;
  /** "HH:MM", or null on the destination (the run ends there) and on an untimed leg. */
  depart: string | null;
}

/**
 * The trip's stops in order with their stored times, origin arrival and destination departure
 * blanked — the shape the Trips pane, the itinerary sheet and the board line all print.
 */
export function stopTimetable(trip: TimetableTrip): StopTimetableRow[] {
  const stops = [...trip.stops].sort((a, b) => a.order - b.order);
  return stops.map((s, i) => ({
    name: s.name,
    arrive: i === 0 ? null : storedTime(s.scheduledArrival),
    depart: i === stops.length - 1 ? null : storedTime(s.scheduledDeparture),
  }));
}

/**
 * One line of the run: "06:30 Thompson › 07:20 Leaf Rapids › 08:30 Lynn Lake" — each stop's
 * departure, the last stop's arrival. Null when the leg is untimed.
 */
export function departuresLine(trip: TimetableTrip): string | null {
  if (!hasTimetable(trip)) return null;
  return stopTimetable(trip)
    .map((row, i, all) => {
      const time = i === all.length - 1 ? row.arrive : row.depart;
      return time ? `${time} ${row.name}` : row.name;
    })
    .join(" › ");
}

// ---------------------------------------------------------------------------
// Authoring — the route form edits clock times against a reference departure,
// then stores the offsets those imply.
// ---------------------------------------------------------------------------

/** "HH:MM" minus a reference "HH:MM", in minutes. Null if either is unparseable. */
export function offsetFromReference(time: string, reference: string): number | null {
  const at = toMinutes(time);
  const from = toMinutes(reference);
  if (at === null || from === null) return null;
  // A leg that runs past midnight arrives "earlier" on the clock; carry it to the next day so
  // an overnight corridor produces an increasing timetable rather than a negative offset.
  return at < from ? at + 1440 - from : at - from;
}

/** The inverse: the clock time an offset lands on, given the reference departure. */
export function referencePlusOffset(reference: string, offsetMinutes: number): string | null {
  const from = toMinutes(reference);
  if (from === null) return null;
  return minutesToHhmm(from + offsetMinutes);
}

/** "+95 min" / "departs" — the offset shown beside a time input so the stored value is visible. */
export function offsetLabel(offsetMinutes: number | null): string {
  if (offsetMinutes === null) return "—";
  if (offsetMinutes === 0) return "departs";
  const hours = Math.floor(offsetMinutes / 60);
  const minutes = offsetMinutes % 60;
  if (hours === 0) return `+${minutes} min`;
  return minutes === 0 ? `+${hours} h` : `+${hours} h ${minutes} min`;
}

export type Leg = "outbound" | "return";

/**
 * One selected stop in the route form, in corridor order: the catalog stop plus its four
 * offsets (minutes after the respective leg's departure; null = not entered).
 */
export interface TimetableRow {
  id: string;
  outboundArrive: number | null;
  outboundDepart: number | null;
  returnArrive: number | null;
  returnDepart: number | null;
}

/** Which inputs a row shows on a leg: the leg's first stop departs only, its last arrives only. */
export type LegPosition = "first" | "middle" | "last";

/** Position of corridor row `index` within `leg`'s direction of travel (return runs backwards). */
export function legPosition(index: number, count: number, leg: Leg): LegPosition {
  const travelIndex = leg === "outbound" ? index : count - 1 - index;
  if (travelIndex === 0) return "first";
  if (travelIndex === count - 1) return "last";
  return "middle";
}

/** One stop's effective offsets on one leg — what gets saved. */
export interface LegEntry {
  arrival: number | null;
  departure: number | null;
}

/**
 * The leg's effective offsets in ITS direction of travel. The first stop has no arrival (it is
 * where the leg departs); the last stop's arrival is also written as its departure (the run
 * ends there, and the backend still requires a departure on every timed stop).
 */
export function legEntries(rows: TimetableRow[], leg: Leg): LegEntry[] {
  const raw = rows.map((r) =>
    leg === "outbound"
      ? { arrival: r.outboundArrive, departure: r.outboundDepart }
      : { arrival: r.returnArrive, departure: r.returnDepart },
  );
  const travel = leg === "outbound" ? raw : [...raw].reverse();
  return travel.map((e, i) => {
    if (i === 0) return { arrival: null, departure: e.departure };
    if (i === travel.length - 1) {
      const at = e.arrival ?? e.departure;
      return { arrival: at, departure: at };
    }
    return e;
  });
}

/**
 * The same rules the backend enforces on one leg (Route.Validate → ValidateLeg), as a message
 * or null. Departures: all-or-nothing, the first stop at 0, strictly increasing. Arrivals:
 * optional anywhere (null = same as departure), never on an untimed leg, never after the stop's
 * own departure, and always after the previous stop departs. The backend remains the authority.
 */
export function validateLeg(rows: TimetableRow[], leg: Leg): string | null {
  const label = leg === "outbound" ? "Outbound" : "Return";
  const entries = legEntries(rows, leg);
  if (entries.length === 0) return null;

  const departures = entries.map((e) => e.departure);
  if (departures.every((d) => d === null)) {
    // Mirrors Trips.Route.ArrivalWithoutTimetable.
    return entries.some((e) => e.arrival !== null)
      ? `${label}: an arrival time needs the leg's departure times too.`
      : null;
  }
  if (departures.some((d) => d === null)) {
    return `${label}: every stop needs a time, or none do.`;
  }
  if (departures[0] !== 0) {
    const first = leg === "outbound" ? "first" : "last";
    return `${label}: the timetable must start at the ${first} stop, where the leg departs.`;
  }
  for (let i = 1; i < departures.length; i++) {
    if ((departures[i] as number) <= (departures[i - 1] as number)) {
      return `${label}: each stop must be later than the one before it, in the direction of travel.`;
    }
  }

  for (let i = 0; i < entries.length; i++) {
    const { arrival, departure } = entries[i];
    if (arrival === null) continue;
    // Mirrors Trips.Route.ArrivalAtOrigin.
    if (i === 0 && arrival !== 0) return `${label}: the first stop is where the leg departs — it has no arrival time.`;
    // Mirrors Trips.Route.ArrivalAfterDeparture.
    if (arrival > (departure as number)) return `${label}: a stop can't arrive after it departs.`;
    // Mirrors Trips.Route.ArrivalBeforePreviousDeparture.
    if (i > 0 && arrival <= (departures[i - 1] as number)) {
      return `${label}: each stop must arrive after the stop before it departs.`;
    }
  }
  return null;
}

/** The last stop's arrival on `leg` — what the timetable implies the leg takes. Null if untimed. */
export function legDuration(rows: TimetableRow[], leg: Leg): number | null {
  return legEntries(rows, leg).at(-1)?.arrival ?? null;
}

/**
 * The route body's stops, in corridor order, carrying the four offsets. A leg switched off sends
 * nulls, which clears any timetable it had.
 */
export function routeStopInputs(rows: TimetableRow[], outboundOn: boolean, returnOn: boolean): RouteStopInput[] {
  const outbound = legEntries(rows, "outbound");
  const back = [...legEntries(rows, "return")].reverse(); // back to corridor order
  return rows.map((r, i) => ({
    stopId: r.id,
    outboundDepartureOffsetMinutes: outboundOn ? outbound[i].departure : null,
    outboundArrivalOffsetMinutes: outboundOn ? outbound[i].arrival : null,
    returnDepartureOffsetMinutes: returnOn ? back[i].departure : null,
    returnArrivalOffsetMinutes: returnOn ? back[i].arrival : null,
  }));
}
