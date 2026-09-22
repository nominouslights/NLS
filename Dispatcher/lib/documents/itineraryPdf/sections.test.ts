import { describe, expect, it } from "vitest";
import { itineraryHtml } from "./index";
import { NO_TIMETABLE_NOTE, PAPER_ACTUALS_NOTE } from "./sections";
import { COMPANY } from "@/lib/company";
import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripRecord, TripStop } from "@/lib/api/trips";

// The itinerary's two load-bearing rules: scheduled times come from the
// timetable helper (never from arithmetic here), and the actuals columns are
// blank paper because the platform holds no per-stop timestamps at all.

function stop(name: string, order: number, outbound?: number): TripStop {
  return {
    name,
    order,
    stopId: `stop-${order}`,
    ...(outbound === undefined ? {} : { outboundOffsetMinutes: outbound, returnOffsetMinutes: null }),
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
    stops: [stop("Leaf Rapids", 1, 0), stop("Granville Lake Jct", 2, 45), stop("Lynn Lake", 3, 120)],
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

/** Every Scheduled cell, in row order. */
function scheduledCells(html: string): string[] {
  return [...html.matchAll(/<td class="time">(.*?)<\/td>/g)].map((m) => m[1]);
}

describe("itineraryHtml — stops with a timetable", () => {
  const html = itineraryHtml(trip(), [], COMPANY);

  it("resolves each stop's scheduled time from the route offsets", () => {
    // 06:30 departure + 0 / 45 / 120 minutes. Resolved by stopTimeOnTrip —
    // there is no time arithmetic in the document builders.
    expect(scheduledCells(html).slice(0, 3)).toEqual(["06:30", "07:15", "08:30"]);
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

  it("prints an em dash in every Scheduled cell", () => {
    expect(scheduledCells(html).slice(0, 3)).toEqual(["—", "—", "—"]);
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
