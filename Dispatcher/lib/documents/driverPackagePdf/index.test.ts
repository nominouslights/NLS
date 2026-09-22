import { describe, expect, it } from "vitest";
import { driverPackageHtml, type DriverPackageInput } from "./index";
import { COMPANY } from "@/lib/company";
import type { VehicleInspection } from "@/lib/api/maintenance";
import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripManifest, TripRecord, TripStop } from "@/lib/api/trips";

// The package is pure composition, so these tests are about ASSEMBLY, not
// content: all four parts present, in order, separated by real page breaks, and
// a cover that never lets a blank inspection pass for a completed one.

function stop(name: string, order: number, outbound: number): TripStop {
  return { name, order, stopId: `stop-${order}`, outboundOffsetMinutes: outbound, returnOffsetMinutes: null };
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
    routeName: "Leaf Rapids to Lynn Lake",
    origin: "Leaf Rapids",
    destination: "Lynn Lake",
    stops: [stop("Leaf Rapids", 1, 0), stop("Lynn Lake", 2, 120)],
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

function manifest(): TripManifest {
  return {
    id: "manifest-1",
    tripDate: "2026-09-20",
    tripNumber: "NL-2026-0042",
    route: "Leaf Rapids to Lynn Lake",
    direction: "Outbound",
    client: "Alamos Gold",
    passengers: [
      {
        name: "J. Bighetty",
        email: "j.bighetty@example.ca",
        phone: "(204) 555-0134",
        pickupStopId: "stop-1",
        pickupStopName: "Leaf Rapids",
        dropoffStopId: "stop-2",
        dropoffStopName: "Lynn Lake",
        idVerified: true,
        boardedOn: true,
        boardedOff: true,
        fareAmountCad: 120,
        farePaymentMethod: "Cash",
        farePaidAtUtc: "2026-09-20T06:35:00Z",
      },
    ],
    allSeatbeltsVerified: true,
    cargo: [],
    allCargoSecured: "NotApplicable",
    source: "Dispatcher",
    enteredBy: "Dispatch",
    enteredAt: "2026-09-20T06:00:00Z",
    createdAtUtc: "2026-09-20T06:00:00Z",
    faresCollectedCad: 120,
    faresPaidCount: 1,
    faresWaivedCount: 0,
  };
}

function inspection(type: "PreTrip" | "PostTrip", unit = "NL-02"): VehicleInspection {
  return {
    id: `insp-${type}`,
    type,
    source: "DriverApp",
    tripNumber: "NL-2026-0042",
    manifestId: null,
    vehicleId: "veh-2",
    unit,
    driverName: "R. Okimaw",
    enteredBy: null,
    performedAt: "2026-09-20T12:30:00Z",
    odometerKm: 184_220,
    result: "Pass",
    checklist: [{ group: "Engine Bay", item: "Engine oil", passed: true, state: "Ok", note: null }],
    defects: [],
    weather: [],
    temperatureC: null,
    roadConditions: [],
    visibility: null,
    roadAdvisories: null,
    fuelLevel: null,
    issues: [],
    attestations: [],
    driverSignatureName: "R. Okimaw",
    certifiedAt: "2026-09-20T12:45:00Z",
    fuelAdded: false,
    fuelLitres: null,
    fuelCostCad: null,
    generatedWorkOrderId: null,
    createdAtUtc: "2026-09-20T12:46:00Z",
    carrierAcknowledgedBy: null,
    carrierAcknowledgedAtUtc: null,
    carrierAcknowledgementNote: null,
    certificationStatement: null,
  };
}

function shipment(): ShipmentRecord {
  return {
    id: "ship-1",
    shipmentNumber: "SH-1001",
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
  };
}

function full(over: Partial<DriverPackageInput> = {}): DriverPackageInput {
  return {
    trip: trip(),
    manifest: manifest(),
    preTrip: inspection("PreTrip"),
    postTrip: inspection("PostTrip"),
    shipments: [shipment()],
    ...over,
  };
}

/** Where each sub-document's wrapper starts. The two inspections share the
 *  `.pti` prefix, so they are told apart by position — pre-trip first. */
function partOffsets(html: string) {
  const ptiFirst = html.indexOf('<div class="pti">');
  return {
    cover: html.indexOf('<div class="nlpkg cover">'),
    manifest: html.indexOf('<div class="tm">'),
    itinerary: html.indexOf('<div class="itin">'),
    preTrip: ptiFirst,
    postTrip: html.indexOf('<div class="pti">', ptiFirst + 1),
  };
}

describe("driverPackageHtml — assembly", () => {
  const html = driverPackageHtml(full(), COMPANY);
  const at = partOffsets(html);

  it("contains all four parts behind a cover", () => {
    for (const [part, offset] of Object.entries(at)) {
      expect(offset, `${part} missing from the package`).toBeGreaterThanOrEqual(0);
    }
  });

  it("orders them cover → manifest → itinerary → pre-trip → post-trip", () => {
    expect(at.cover).toBeLessThan(at.manifest);
    expect(at.manifest).toBeLessThan(at.itinerary);
    expect(at.itinerary).toBeLessThan(at.preTrip);
    expect(at.preTrip).toBeLessThan(at.postTrip);
  });

  it("uses each sub-document's own class prefix", () => {
    expect(html).toContain('<div class="tm">');
    expect(html).toContain('<div class="itin">');
    expect(html).toContain('<div class="pti">');
    expect(html).toContain('<div class="nlpkg cover">');
  });

  it("puts exactly three package-level breaks between the four documents", () => {
    // The cover starts the manifest on a fresh sheet with its own
    // page-break-after, so the markers separate the four documents only.
    const markers = [...html.matchAll(/class="nlpkg-brk"/g)];
    expect(markers.length).toBe(3);
    expect(html).toContain(".nlpkg-brk { page-break-before: always;");
    expect(html).toContain(".nlpkg.cover { page-break-after: always;");
  });

  it("keeps every sheet on the same US-Letter page geometry", () => {
    // @page cannot be class-scoped: concatenation emits it several times and the
    // last one wins for the whole job, so they must all be identical.
    const pages = [...html.matchAll(/@page \{[^}]*\}/g)].map((m) => m[0]);
    expect(pages.length).toBeGreaterThan(1);
    expect(new Set(pages).size).toBe(1);
    expect(pages[0]).toBe("@page { size: Letter; margin: 12mm 12mm; }");
  });

  it("does not reuse the .tm-scoped break class between documents", () => {
    // `.tm .brk` can only break INSIDE the trip manifest.
    expect(html).not.toContain('<div class="brk"></div>\n<div class="itin">');
  });
});

describe("driverPackageHtml — nothing on file", () => {
  const html = driverPackageHtml(
    full({ manifest: null, preTrip: null, postTrip: null, shipments: [] }),
    COMPANY,
  );
  const at = partOffsets(html);

  it("still prints all four parts, as blank forms", () => {
    expect(at.manifest).toBeGreaterThanOrEqual(0);
    expect(at.itinerary).toBeGreaterThanOrEqual(0);
    expect(at.preTrip).toBeGreaterThanOrEqual(0);
    expect(at.postTrip).toBeGreaterThan(at.preTrip);
  });

  it("marks each missing part BLANK on the cover, never silently filled", () => {
    const cover = html.slice(at.cover, at.manifest);
    const blanks = [...cover.matchAll(/BLANK — to be completed by hand/g)];
    expect(blanks.length).toBe(3); // manifest, pre-trip, post-trip
    expect(cover).toContain("No pre-trip on file");
    expect(cover).toContain("No post-trip on file");
    expect(cover).toContain("A blank inspection is not a passed inspection");
  });

  it("marks the itinerary filled — it is derived from the trip itself", () => {
    const cover = html.slice(at.cover, at.manifest);
    expect(cover).toContain("Trip Itinerary");
    expect(cover).toContain("✓ Filled");
  });

  it("omits the freight block when no shipments ride the trip", () => {
    expect(html.slice(at.itinerary, at.preTrip)).not.toContain("Received By");
  });
});

describe("driverPackageHtml — the two inspection halves", () => {
  const html = driverPackageHtml(full(), COMPANY);
  const at = partOffsets(html);
  const preTripPart = html.slice(at.preTrip, at.postTrip);
  const postTripPart = html.slice(at.postTrip);

  it("puts Close-Out on the post-trip sheet only", () => {
    expect(preTripPart).not.toContain("Close-Out");
    expect(postTripPart).toContain("Close-Out");
  });

  it("ticks the right half of each inspection sheet", () => {
    expect(preTripPart).toContain("☒ Pre-Trip");
    expect(preTripPart).toContain("☐ Post-Trip");
    expect(postTripPart).toContain("☐ Pre-Trip");
    expect(postTripPart).toContain("☒ Post-Trip");
  });
});

describe("driverPackageHtml — unit narrowing", () => {
  const nl02 = driverPackageHtml(full(), COMPANY);
  const nl01 = driverPackageHtml(
    full({
      trip: trip({ vehicleUnit: "NL-01", vehicleId: "veh-1" }),
      preTrip: inspection("PreTrip", "NL-01"),
      postTrip: inspection("PostTrip", "NL-01"),
    }),
    COMPANY,
  );

  it("includes the bus-only rows for NL-02", () => {
    expect(nl02).toContain("Emergency exits / windows (NL-02)");
    expect(nl02).toContain("Fitted cargo area — partition &amp; tie-downs (NL-02)");
  });

  it("omits them for NL-01 — that unit does not have them", () => {
    expect(nl01).not.toContain("Emergency exits / windows (NL-02)");
    expect(nl01).not.toContain("Fitted cargo area");
    expect(nl01).not.toContain("Fuel / water separator (NL-02, diesel)");
  });
});

describe("driverPackageHtml — no placeholder leaks", () => {
  // Cheap and high-value for string-built HTML: a missing field must render as
  // an empty ruled cell, never as the word "undefined" on a compliance sheet.
  const leak = /\bundefined\b|\bnull\b|\bNaN\b/;

  it("emits no literal undefined / null / NaN when fully populated", () => {
    expect(driverPackageHtml(full(), COMPANY)).not.toMatch(leak);
  });

  it("emits none when every optional part is missing either", () => {
    const html = driverPackageHtml(
      full({
        trip: trip({
          clientName: null,
          poNumber: null,
          driverName: null,
          vehicleUnit: null,
          windowEnd: null,
          direction: null,
        }),
        manifest: null,
        preTrip: null,
        postTrip: null,
        shipments: [],
      }),
      COMPANY,
    );
    expect(html).not.toMatch(leak);
  });
});
