import { describe, expect, it } from "vitest";
import { itineraryHtml } from "./index";
import { NO_TIMETABLE_NOTE, PAPER_ACTUALS_NOTE } from "./sections";
import { COMPANY } from "@/lib/company";
import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripRecord, TripStop } from "@/lib/api/trips";

// The itinerary's two load-bearing rules: scheduled times are the trip's own
// stored stop times (never arithmetic here), and the actuals columns are blank
// paper because the platform holds no per-stop timestamps at all.

/** A trip stop as the API returns it: offsets plus the times it resolved. */
function stop(name: string, order: number, times?: { arrive: string; depart: string }): TripStop {
  return {
    name,
    order,
    stopId: `stop-${order}`,
    ...(times === undefined
      ? { scheduledArrival: null, scheduledDeparture: null }
      : { scheduledArrival: `${times.arrive}:00`, scheduledDeparture: `${times.depart}:00` }),
  };
}

function trip(over: Partial<TripRecord> = {}): TripRecord {
  return {
    id: "trip-1",
    tripNumber: "NL-2026-0042",
    serviceDate: "2026-09-20",
    windowStart: "06:30:00",
    windowEnd: "11:00:00",
    serviceType: "ContractCrew",
    routeId: "route-1",
    routeName: "Leaf Rapids → Lynn Lake",
    origin: "Leaf Rapids",
    destination: "Lynn Lake",
    stops: [
      stop("Leaf Rapids", 1, { arrive: "06:30", depart: "06:30" }),
      stop("Granville Lake Jct", 2, { arrive: "07:10", depart: "07:15" }),
      stop("Lynn Lake", 3, { arrive: "08:30", depart: "08:30" }),
    ],
    distanceKm: 214,
    scheduleTemplateId: null,
    roundTripKey: null,
    direction: "Outbound",
    isEmptyLeg: false,
    clientId: "client-1",
    clientName: "Alamos Gold",
    poNumber: "PO-8841",
    driverId: "driver-1",
    driverName: "R. Okimaw",
    vehicleId: "veh-2",
    vehicleUnit: "NL-02",
    seatsCapacity: 14,
    seatsConfirmed: 9,
    seatsMinimum: null,
    demandGuaranteed: false,
    status: "Scheduled",
    manifestId: "manifest-1",
    hasPostTripInspection: false,
    operationsFinishedAtUtc: null,
    completedAtUtc: null,
    cancelledReason: null,
    writtenOffReason: null,
    createdAtUtc: "2026-09-01T10:00:00Z",
    updatedAtUtc: "2026-09-01T10:00:00Z",
    billing: null,
    ...over,
  };
}

function shipment(over: Partial<ShipmentRecord> & { shipmentNumber: string }): ShipmentRecord {
  return {
    id: `ship-${over.shipmentNumber}`,
    status: "Assigned",
    kind: "Freight",
    clientId: "client-2",
    clientName: "Incline Group",
    poNumber: null,
    consignorName: null,
    consignorContact: null,
    consigneeName: null,
    consigneeContact: null,
    originStopId: null,
    originName: "Leaf Rapids",
    destinationStopId: null,
    destinationName: "Lynn Lake",
    description: "Pallet of drill core boxes",
    pieces: 3,
    weightKg: 410,
    lengthCm: null,
    widthCm: null,
    heightCm: null,
    hazmat: false,
    declaredValueCad: null,
    specialHandling: null,
    secured: true,
    chargeCad: null,
    paymentMethod: null,
    paymentCollectedAtUtc: null,
    readyDate: null,
    requiredByDate: null,
    legs: [],
    awaitingTransfer: false,
    deliveredAtUtc: null,
    receivedBy: null,
    deliveryNote: null,
    cancelledReason: null,
    writtenOffReason: null,
    source: "Dispatcher",
    enteredBy: null,
    createdAtUtc: "2026-09-01T10:00:00Z",
    updatedAtUtc: "2026-09-01T10:00:00Z",
    ...over,
  };
}

/** Every Sched. cell, in row order: [arrive, depart] per row. */
function scheduledCells(html: string): [string, string][] {
  const cells = [...html.matchAll(/<td class="time">(.*?)<\/td>/g)].map((m) => m[1]);
  const rows: [string, string][] = [];
  for (let i = 0; i < cells.length; i += 2) rows.push([cells[i], cells[i + 1]]);
  return rows;
}

describe("itineraryHtml — stops with a timetable", () => {
  const html = itineraryHtml(trip(), [], COMPANY);

  it("prints both scheduled columns from the trip's stored stop times", () => {
    expect(html).toContain("Sched. Arrive");
    expect(html).toContain("Sched. Depart");
    // A mid-route stop shows its dwell: arrives 07:10, leaves 07:15.
    expect(scheduledCells(html)[1]).toEqual(["07:10", "07:15"]);
  });

  it("dashes the origin's arrival and the destination's departure", () => {
    const [origin, , destination] = scheduledCells(html);
    expect(origin).toEqual(["—", "06:30"]);
    expect(destination).toEqual(["08:30", "—"]);
  });

  it("does no arithmetic — a shifted stored time prints as stored", () => {
    // Offsets on the stop are deliberately inconsistent with the stored times:
    // only the stored time may reach the sheet.
    const shifted = trip({
      stops: [
        { ...stop("Leaf Rapids", 1, { arrive: "09:00", depart: "09:00" }), outboundDepartureOffsetMinutes: 0 },
        { ...stop("Lynn Lake", 2, { arrive: "11:05", depart: "11:05" }), outboundDepartureOffsetMinutes: 120 },
      ],
    });
    expect(scheduledCells(itineraryHtml(shifted, [], COMPANY)).slice(0, 2)).toEqual([
      ["—", "09:00"],
      ["11:05", "—"],
    ]);
  });

  it("prints each visit's own time when a corridor visits the same stop twice", () => {
    // A loop that passes back through Granville Lake Jct — same name, same catalog
    // id. Reading by id/name would print the first visit's time on both rows.
    const loop = trip({
      stops: [
        stop("Leaf Rapids", 1, { arrive: "06:30", depart: "06:30" }),
        { ...stop("Granville Lake Jct", 2, { arrive: "07:10", depart: "07:15" }), stopId: "stop-gl" },
        stop("Lynn Lake", 3, { arrive: "08:30", depart: "08:45" }),
        { ...stop("Granville Lake Jct", 4, { arrive: "09:55", depart: "10:00" }), stopId: "stop-gl" },
        stop("Leaf Rapids", 5, { arrive: "10:40", depart: "10:40" }),
      ],
    });
    const cells = scheduledCells(itineraryHtml(loop, [], COMPANY));
    expect(cells[1]).toEqual(["07:10", "07:15"]);
    expect(cells[3]).toEqual(["09:55", "10:00"]);
    expect(cells[4]).toEqual(["10:40", "—"]);
  });

  it("prints the stop names in route order", () => {
    const names = ["Leaf Rapids", "Granville Lake Jct", "Lynn Lake"];
    let cursor = -1;
    for (const name of names) {
      const at = html.indexOf(`<td>${name}</td>`);
      expect(at, `${name} missing or out of order`).toBeGreaterThan(cursor);
      cursor = at;
    }
  });

  it("does not print the no-timetable fallback note", () => {
    expect(html).not.toContain(NO_TIMETABLE_NOTE);
  });
});

describe("itineraryHtml — stops without a timetable", () => {
  const noOffsets = trip({
    stops: [stop("Leaf Rapids", 1), stop("Granville Lake Jct", 2), stop("Lynn Lake", 3)],
  });
  const html = itineraryHtml(noOffsets, [], COMPANY);

  it("prints an em dash in every Sched. Arrive and Sched. Depart cell", () => {
    expect(scheduledCells(html).slice(0, 3)).toEqual([
      ["—", "—"],
      ["—", "—"],
      ["—", "—"],
    ]);
  });

  it("leaves the padding rows below the stops blank", () => {
    expect(scheduledCells(html)[3]).toEqual(["&nbsp;", "&nbsp;"]);
  });

  it("says the trip window is the only scheduled time", () => {
    expect(html).toContain(NO_TIMETABLE_NOTE);
  });

  it("still prints the trip window itself", () => {
    expect(html).toContain("06:30–11:00");
  });
});

describe("itineraryHtml — the actuals columns", () => {
  const html = itineraryHtml(trip(), [], COMPANY);

  it("renders arrive / depart / pax as blank ruled cells on every row", () => {
    // Three hand-written cells per stop row, and six rows minimum.
    const handCells = [...html.matchAll(/<td class="hand blank">&nbsp;<\/td>/g)];
    expect(handCells.length).toBe(18);
  });

  it("states on the sheet that per-stop actuals are paper-only", () => {
    expect(html).toContain(PAPER_ACTUALS_NOTE);
  });
});

describe("itineraryHtml — the freight block", () => {
  it("renders from the shipments argument", () => {
    const html = itineraryHtml(
      trip({ serviceType: "Cargo" }),
      [
        shipment({ shipmentNumber: "SH-1001" }),
        shipment({ shipmentNumber: "SH-1002", description: "Propane cylinders", hazmat: true, pieces: 2, weightKg: 68 }),
      ],
      COMPANY,
    );
    expect(html).toContain("SH-1001");
    expect(html).toContain("Pallet of drill core boxes");
    expect(html).toContain("410 kg");
    expect(html).toContain("Leaf Rapids → Lynn Lake");
    expect(html).toContain("SH-1002");
    expect(html).toContain("Received By");
    // Hazmat is ticked on the propane row and only on that row.
    const hazmatCells = [...html.matchAll(/<td class="ck">(.*?)<\/td>/g)].map((m) => m[1]);
    expect(hazmatCells).toEqual(["☐", "☒"]);
  });

  it("is omitted entirely when there are no shipments — the passenger case", () => {
    const html = itineraryHtml(trip(), [], COMPANY);
    expect(html).not.toContain("Received By");
    expect(html).not.toContain("Shipment #");
  });
});

describe("itineraryHtml — header", () => {
  const html = itineraryHtml(trip(), [], COMPANY);

  it("carries the trip identity a driver needs at a glance", () => {
    expect(html).toContain("NL-2026-0042");
    expect(html).toContain("2026-09-20");
    expect(html).toContain("Alamos Gold · PO PO-8841");
    expect(html).toContain("R. Okimaw");
    expect(html).toContain("NL-02");
    expect(html).toContain("214 km");
    expect(html).toContain("☒ Outbound");
  });
});
