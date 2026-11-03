import { describe, expect, it } from "vitest";
import type { TripStop } from "@/lib/api/trips";
import {
  departuresLine,
  hasTimetable,
  legDuration,
  legEntries,
  legPosition,
  minutesToHhmm,
  offsetFromReference,
  offsetLabel,
  recipientTimes,
  referencePlusOffset,
  routeStopInputs,
  stopArrivalOnTrip,
  stopDepartureOnTrip,
  stopTimetable,
  validateLeg,
  type TimetableRow,
} from "./timetable";

// The trip side reads the API's resolved times and does no arithmetic; the authoring side
// mirrors the backend's per-leg rules so the route form flags a bad leg before saving.

function stop(name: string, order: number, arrive: string | null, depart: string | null, stopId?: string): TripStop {
  return {
    name,
    order,
    ...(stopId ? { stopId } : {}),
    scheduledArrival: arrive === null ? null : `${arrive}:00`,
    scheduledDeparture: depart === null ? null : `${depart}:00`,
  };
}

const timed = {
  stops: [
    stop("Thompson", 0, "06:30", "06:30", "s-thompson"),
    stop("Leaf Rapids", 1, "07:15", "07:20", "s-leaf"),
    stop("Lynn Lake", 2, "08:30", "08:30", "s-lynn"),
  ],
};

const untimed = {
  stops: [stop("Thompson", 0, null, null, "s-thompson"), stop("Lynn Lake", 1, null, null, "s-lynn")],
};

describe("reading a trip's stored stop times", () => {
  it("returns the stored departure and arrival as HH:MM", () => {
    expect(stopDepartureOnTrip(timed, "s-leaf")).toBe("07:20");
    expect(stopArrivalOnTrip(timed, "s-leaf")).toBe("07:15");
  });

  it("finds a stop by id first, then by name (case- and space-insensitive)", () => {
    expect(stopDepartureOnTrip(timed, "unknown-id", "  leaf rapids ")).toBe("07:20");
    expect(stopDepartureOnTrip(timed, null, "Lynn Lake")).toBe("08:30");
  });

  it("returns null for a stop not on the trip", () => {
    expect(stopDepartureOnTrip(timed, "nope", "Nowhere")).toBeNull();
    expect(stopArrivalOnTrip(timed, null, null)).toBeNull();
  });

  it("returns null on an untimed leg, and on route stops that carry no resolved times", () => {
    expect(stopDepartureOnTrip(untimed, "s-thompson")).toBeNull();
    expect(stopArrivalOnTrip({ stops: [{ name: "Thompson", order: 0, stopId: "s" }] }, "s")).toBeNull();
  });

  it("hasTimetable is true only when some stop has a scheduled departure", () => {
    expect(hasTimetable(timed)).toBe(true);
    expect(hasTimetable(untimed)).toBe(false);
    expect(hasTimetable({ stops: [] })).toBe(false);
    // Offsets alone are not a timetable — the trip side never computes from them.
    expect(hasTimetable({ stops: [{ name: "A", order: 0, outboundDepartureOffsetMinutes: 0 }] })).toBe(false);
  });
});

describe("recipientTimes", () => {
  it("is the boarding stop's DEPARTURE and the alighting stop's ARRIVAL", () => {
    expect(
      recipientTimes(timed, { pickupStopId: "s-leaf", dropoffStopId: "s-lynn" }),
    ).toEqual({ pickup: "07:20", dropoff: "08:30" });
    // A mid-route drop-off is told when the vehicle arrives, not when it leaves.
    expect(recipientTimes(timed, { pickupStopId: "s-thompson", dropoffStopId: "s-leaf" })).toEqual({
      pickup: "06:30",
      dropoff: "07:15",
    });
  });

  it("falls back on stop names when the ids are missing", () => {
    expect(recipientTimes(timed, { pickupStopName: "Leaf Rapids", dropoffStopName: "Lynn Lake" })).toEqual({
      pickup: "07:20",
      dropoff: "08:30",
    });
  });

  it("is null on either side that can't be resolved, so the caller uses the trip window", () => {
    expect(recipientTimes(untimed, { pickupStopId: "s-thompson", dropoffStopId: "s-lynn" })).toEqual({
      pickup: null,
      dropoff: null,
    });
    expect(recipientTimes(timed, { pickupStopId: "s-leaf" })).toEqual({ pickup: "07:20", dropoff: null });
  });
});

describe("stopTimetable and departuresLine", () => {
  it("lists stops in order, blanking the origin's arrival and the destination's departure", () => {
    const shuffled = { stops: [timed.stops[2], timed.stops[0], timed.stops[1]] };
    expect(stopTimetable(shuffled)).toEqual([
      { name: "Thompson", arrive: null, depart: "06:30" },
      { name: "Leaf Rapids", arrive: "07:15", depart: "07:20" },
      { name: "Lynn Lake", arrive: "08:30", depart: null },
    ]);
  });

  it("prints each departure and the last stop's arrival", () => {
    expect(departuresLine(timed)).toBe("06:30 Thompson › 07:20 Leaf Rapids › 08:30 Lynn Lake");
  });

  it("is null on an untimed leg", () => {
    expect(departuresLine(untimed)).toBeNull();
  });
});

describe("authoring helpers", () => {
  it("converts between clock times and offsets, across midnight", () => {
    expect(offsetFromReference("07:20", "06:30")).toBe(50);
    expect(offsetFromReference("00:30", "23:00")).toBe(90);
    expect(referencePlusOffset("23:00", 90)).toBe("00:30");
    expect(minutesToHhmm(-30)).toBe("23:30");
  });

  it("labels offsets", () => {
    expect(offsetLabel(null)).toBe("—");
    expect(offsetLabel(0)).toBe("departs");
    expect(offsetLabel(45)).toBe("+45 min");
    expect(offsetLabel(120)).toBe("+2 h");
    expect(offsetLabel(95)).toBe("+1 h 35 min");
  });

  it("places a corridor row within each leg's direction of travel", () => {
    expect([0, 1, 2].map((i) => legPosition(i, 3, "outbound"))).toEqual(["first", "middle", "last"]);
    expect([0, 1, 2].map((i) => legPosition(i, 3, "return"))).toEqual(["last", "middle", "first"]);
  });
});

/** Rows in corridor order; each tuple is [arrive, depart] for that leg (others null). */
function outboundRows(...pairs: [number | null, number | null][]): TimetableRow[] {
  return pairs.map(([a, d], i) => ({
    id: `s${i}`,
    outboundArrive: a,
    outboundDepart: d,
    returnArrive: null,
    returnDepart: null,
  }));
}
function returnRows(...pairs: [number | null, number | null][]): TimetableRow[] {
  return pairs.map(([a, d], i) => ({
    id: `s${i}`,
    outboundArrive: null,
    outboundDepart: null,
    returnArrive: a,
    returnDepart: d,
  }));
}

describe("legEntries", () => {
  it("drops the first stop's arrival and writes the last stop's arrival into its departure", () => {
    expect(legEntries(outboundRows([5, 0], [45, 50], [90, null]), "outbound")).toEqual([
      { arrival: null, departure: 0 },
      { arrival: 45, departure: 50 },
      { arrival: 90, departure: 90 },
    ]);
  });

  it("reads the return leg bottom to top", () => {
    expect(legEntries(returnRows([null, 80], [35, 40], [null, 0]), "return")).toEqual([
      { arrival: null, departure: 0 },
      { arrival: 35, departure: 40 },
      { arrival: 80, departure: 80 },
    ]);
  });

  it("uses a legacy last-stop departure as its arrival", () => {
    expect(legEntries(outboundRows([null, 0], [null, 120]), "outbound").at(-1)).toEqual({ arrival: 120, departure: 120 });
  });
});

describe("validateLeg", () => {
  it("accepts an untimed leg", () => {
    expect(validateLeg(outboundRows([null, null], [null, null]), "outbound")).toBeNull();
    expect(validateLeg([], "outbound")).toBeNull();
  });

  it("accepts a valid leg with dwell, and a null arrival anywhere", () => {
    expect(validateLeg(outboundRows([null, 0], [45, 50], [90, null]), "outbound")).toBeNull();
    expect(validateLeg(outboundRows([null, 0], [null, 50], [90, null]), "outbound")).toBeNull();
  });

  it("rejects a partial timetable", () => {
    expect(validateLeg(outboundRows([null, 0], [null, null], [90, null]), "outbound")).toMatch(/every stop needs a time/);
  });

  it("rejects a leg not starting at zero, naming the return's first stop as the last", () => {
    expect(validateLeg(outboundRows([null, 10], [null, 50], [90, null]), "outbound")).toMatch(/start at the first stop/);
    expect(validateLeg(returnRows([90, null], [null, 50], [null, 10]), "return")).toMatch(/^Return: .*start at the last stop/);
  });

  it("rejects departures that don't increase", () => {
    expect(validateLeg(outboundRows([null, 0], [null, 50], [40, null]), "outbound")).toMatch(/later than the one before/);
  });

  it("rejects an arrival on an untimed leg (ArrivalWithoutTimetable)", () => {
    expect(validateLeg(outboundRows([null, null], [30, null], [null, null]), "outbound")).toMatch(
      /arrival time needs the leg's departure times/,
    );
  });

  it("rejects an arrival after its own departure (ArrivalAfterDeparture)", () => {
    expect(validateLeg(outboundRows([null, 0], [55, 50], [90, null]), "outbound")).toMatch(/can't arrive after it departs/);
  });

  it("rejects an arrival at or before the previous departure (ArrivalBeforePreviousDeparture)", () => {
    expect(validateLeg(outboundRows([null, 10], [null, 0], [90, null]), "outbound")).not.toBeNull();
    expect(validateLeg(outboundRows([null, 0], [0, 50], [90, null]), "outbound")).toMatch(
      /arrive after the stop before it departs/,
    );
  });

  it("runs the return leg's arrival rules backwards", () => {
    // Corridor order top → bottom; the return departs the bottom row.
    expect(validateLeg(returnRows([null, 80], [35, 40], [null, 0]), "return")).toBeNull();
    expect(validateLeg(returnRows([null, 80], [45, 40], [null, 0]), "return")).toMatch(/^Return: a stop can't arrive after/);
  });
});

describe("legDuration and routeStopInputs", () => {
  it("legDuration is the last stop's arrival", () => {
    expect(legDuration(outboundRows([null, 0], [45, 50], [90, null]), "outbound")).toBe(90);
    expect(legDuration(outboundRows([null, null], [null, null]), "outbound")).toBeNull();
  });

  it("sends the four offsets in corridor order, the last stop's arrival as both", () => {
    const rows: TimetableRow[] = [
      { id: "a", outboundArrive: 3, outboundDepart: 0, returnArrive: 80, returnDepart: null },
      { id: "b", outboundArrive: 45, outboundDepart: 50, returnArrive: 35, returnDepart: 40 },
      { id: "c", outboundArrive: 90, outboundDepart: null, returnArrive: 7, returnDepart: 0 },
    ];
    expect(routeStopInputs(rows, true, true)).toEqual([
      { stopId: "a", outboundDepartureOffsetMinutes: 0, outboundArrivalOffsetMinutes: null, returnDepartureOffsetMinutes: 80, returnArrivalOffsetMinutes: 80 },
      { stopId: "b", outboundDepartureOffsetMinutes: 50, outboundArrivalOffsetMinutes: 45, returnDepartureOffsetMinutes: 40, returnArrivalOffsetMinutes: 35 },
      { stopId: "c", outboundDepartureOffsetMinutes: 90, outboundArrivalOffsetMinutes: 90, returnDepartureOffsetMinutes: 0, returnArrivalOffsetMinutes: null },
    ]);
  });

  it("sends nulls for a leg that is switched off", () => {
    const rows = outboundRows([null, 0], [90, null]);
    expect(routeStopInputs(rows, false, false)).toEqual([
      { stopId: "s0", outboundDepartureOffsetMinutes: null, outboundArrivalOffsetMinutes: null, returnDepartureOffsetMinutes: null, returnArrivalOffsetMinutes: null },
      { stopId: "s1", outboundDepartureOffsetMinutes: null, outboundArrivalOffsetMinutes: null, returnDepartureOffsetMinutes: null, returnArrivalOffsetMinutes: null },
    ]);
  });
});
