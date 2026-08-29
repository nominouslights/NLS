/**
 * Route timetables — turning a route's per-stop offsets into the clock time a given trip
 * reaches a given stop.
 *
 * A route stores minutes-after-departure rather than clock times, so one corridor can serve a
 * 06:30 run and a 14:00 run without being duplicated. The absolute time only exists once a trip
 * pins the corridor to a departure: `trip.windowStart + offset`.
 *
 * Each stop carries BOTH legs' offsets for its whole life — including onto the trip snapshot —
 * and the trip's own direction picks the one that applies. That is why reversing a stop list for
 * an inbound leg never swaps the two fields.
 *
 * Everything here is pure: no API calls, no React.
 */

import type { TripDirection, TripStop } from "@/lib/api/trips";

/** The minimum a caller needs for a resolution — `TripRecord` satisfies it structurally. */
export interface TimetableTrip {
  stops: TripStop[];
  windowStart: string;
  direction: TripDirection | null;
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

/**
 * The offset that applies to `stop` on a leg travelling in `direction`. An Inbound leg reads the
 * return column; everything else — Outbound, and the null direction of an ad-hoc trip — reads the
 * outbound one.
 */
export function offsetForLeg(stop: TripStop, direction: TripDirection | null): number | null {
  const offset = direction === "Inbound" ? stop.returnOffsetMinutes : stop.outboundOffsetMinutes;
  return typeof offset === "number" ? offset : null;
}

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
 * The clock time ("HH:MM", 24-hour) this trip reaches the given stop, or null when it cannot be
 * resolved — the stop isn't on this trip, or this leg has no timetable. Null is the caller's cue
 * to fall back to the trip-level time, which is what every passenger got before timetables.
 */
export function stopTimeOnTrip(
  trip: TimetableTrip,
  stopId: string | null | undefined,
  stopName?: string | null,
): string | null {
  const stop = findStop(trip, stopId, stopName);
  if (!stop) return null;

  const offset = offsetForLeg(stop, trip.direction);
  if (offset === null) return null;

  const departure = toMinutes(trip.windowStart);
  if (departure === null) return null;

  return minutesToHhmm(departure + offset);
}

/** Whether this trip's leg has a timetable at all — drives whether the UI shows per-stop times. */
export function hasTimetable(trip: TimetableTrip): boolean {
  return trip.stops.some((stop) => offsetForLeg(stop, trip.direction) !== null);
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
