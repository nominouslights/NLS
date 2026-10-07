import { describe, expect, it } from "vitest";
import { DEADHEAD_COVER_LINE, driverPackageHtml, partsFollowText, type DriverPackageInput } from "./index";
import { enRouteDefectReportHtml } from "../defectReportPdf";
import { itemsFor } from "@/lib/inspectionForm";
import { COMPANY } from "@/lib/company";
import type { VehicleInspection } from "@/lib/api/maintenance";
import type { ShipmentRecord } from "@/lib/api/shipments";
import type { TripManifest, TripRecord, TripStop } from "@/lib/api/trips";

// The package is pure composition, so most of these tests are about ASSEMBLY:
// all four parts present, in order, separated by real page breaks, and a cover
// that never lets a blank inspection pass for a completed one. The rest pin that
// the printed parts reflect the CURRENT sub-documents — NL-PTI-01 rev 3's
// catalogue and header, Bookeo-imported passengers, and the en-route defect
// report that replaced the post-trip sheet.

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
        externalRef: null,
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
    location: null,
    result: "Pass",
    checklist: [{ group: "Engine Bay", item: "Engine fluid levels", passed: true, state: "Ok", note: null }],
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
    shipments: [shipment()],
    ...over,
  };
}

/** Where each sub-document's wrapper starts. Each part has its own class prefix. */
function partOffsets(html: string) {
  return {
    cover: html.indexOf('<div class="nlpkg cover">'),
    manifest: html.indexOf('<div class="tm">'),
    itinerary: html.indexOf('<div class="itin">'),
    preTrip: html.indexOf('<div class="pti">'),
    defectReport: html.indexOf('<div class="edr">'),
  };
}

/** The four parts' HTML, sliced at their wrappers. */
function parts(html: string) {
  const at = partOffsets(html);
  return {
    cover: html.slice(at.cover, at.manifest),
    manifest: html.slice(at.manifest, at.itinerary),
    itinerary: html.slice(at.itinerary, at.preTrip),
    preTrip: html.slice(at.preTrip, at.defectReport),
    defectReport: html.slice(at.defectReport),
  };
}

/** Rows of the checklist tables — every catalogue row prints as `td.item`. */
function checklistRowCount(part: string): number {
  return [...part.matchAll(/<td class="item">/g)].length;
}

/** The value cell printed under a field label, as raw HTML. */
function fieldValue(part: string, label: string): string | null {
  const m = part.match(new RegExp(`<div class="lbl">${label}</div><div class="val">(.*?)</div>`));
  return m ? m[1] : null;
}

describe("driverPackageHtml — assembly", () => {
  const html = driverPackageHtml(full(), COMPANY);
  const at = partOffsets(html);

  it("contains all four parts behind a cover", () => {
    for (const [part, offset] of Object.entries(at)) {
      expect(offset, `${part} missing from the package`).toBeGreaterThanOrEqual(0);
    }
  });

  it("orders them cover → manifest → itinerary → pre-trip → en-route defect report", () => {
    expect(at.cover).toBeLessThan(at.manifest);
    expect(at.manifest).toBeLessThan(at.itinerary);
    expect(at.itinerary).toBeLessThan(at.preTrip);
    expect(at.preTrip).toBeLessThan(at.defectReport);
  });

  it("carries no post-trip inspection sheet", () => {
    // Exactly one NL-PTI-01 sheet, and it is the pre-trip half.
    expect([...html.matchAll(/<div class="pti">/g)].length).toBe(1);
    expect(html).toContain("☒ Pre-Trip");
    expect(html).not.toContain("☒ Post-Trip");
    // The post-trip-only groups never print.
    expect(html).not.toContain("Close-Out");
    expect(html).not.toContain("En-Route Observations");
    expect(html).not.toContain("Post-Trip Inspection (NL-PTI-01)");
    expect(html).not.toContain("No post-trip on file");
  });

  it("puts exactly three package-level breaks between the four documents", () => {
    // The cover starts the manifest on a fresh sheet with its own
    // page-break-after, so the markers separate the four documents only.
    const markers = [...html.matchAll(/class="nlpkg-brk"/g)];
    expect(markers.length).toBe(3);
    expect(html).toContain(".nlpkg-brk { page-break-before: always;");
    expect(html).toContain(".nlpkg.cover { page-break-after: always;");
  });

  it("starts each part after the cover on its own sheet", () => {
    // A break marker sits between every consecutive pair of parts.
    const order = [at.manifest, at.itinerary, at.preTrip, at.defectReport];
    for (let i = 1; i < order.length; i++) {
      const between = html.slice(order[i - 1], order[i]);
      expect(between, `no break before part ${i + 1}`).toContain('<div class="nlpkg-brk"></div>');
    }
  });

  it("keeps every sheet on the same US-Letter page geometry", () => {
    // @page cannot be class-scoped: concatenation emits it several times and the
    // last one wins for the whole job, so they must all be identical.
    const pages = [...html.matchAll(/@page \{[^}]*\}/g)].map((m) => m[0]);
    expect(pages.length).toBe(5); // package + four sub-documents
    expect(new Set(pages).size).toBe(1);
    expect(pages[0]).toBe("@page { size: Letter; margin: 12mm 12mm; }");
  });

  it("does not reuse the .tm-scoped break class between documents", () => {
    // `.tm .brk` can only break INSIDE the trip manifest.
    expect(html).not.toContain('<div class="brk"></div>\n<div class="itin">');
  });
});

describe("driverPackageHtml — the cover", () => {
  const cover = parts(driverPackageHtml(full(), COMPANY)).cover;
  const rows = [...cover.matchAll(/<tr>\s*<td class="num">(\d+)<\/td>\s*<td>(.*?)<\/td>/g)].map(
    (m) => [Number(m[1]), m[2]],
  );

  it("lists the four parts, numbered in print order", () => {
    expect(rows).toEqual([
      [1, "Trip Manifest (NL-TM-01)"],
      [2, "Trip Itinerary"],
      [3, "Pre-Trip Inspection (NL-PTI-01)"],
      [4, "En-Route Defect Report"],
    ]);
  });

  it("marks the en-route defect report as always completed by hand", () => {
    const row = cover.slice(cover.indexOf("<td>En-Route Defect Report</td>"));
    expect(row.slice(0, row.indexOf("</tr>"))).toContain("BLANK — to be completed by hand");
    // Every other part was on file, so that is the only blank marker.
    expect([...cover.matchAll(/BLANK — to be completed by hand/g)].length).toBe(1);
  });

  it("does not warn of missing records when only the hand-completed part is blank", () => {
    expect(cover).not.toContain("A blank inspection is not a passed inspection");
    expect(cover).toContain("complete the en-route defect report by hand");
  });

  it("derives its parts count from the parts it lists", () => {
    expect(cover).toContain("Four parts follow, one per sheet.");
    expect(partsFollowText(rows.length)).toBe("Four parts follow, one per sheet.");
    expect(partsFollowText(1)).toBe("One part follows, one per sheet.");
  });
});

describe("driverPackageHtml — nothing on file", () => {
  const html = driverPackageHtml(full({ manifest: null, preTrip: null, shipments: [] }), COMPANY);
  const at = partOffsets(html);
  const p = parts(html);

  it("still prints all four parts, as blank forms", () => {
    expect(at.manifest).toBeGreaterThanOrEqual(0);
    expect(at.itinerary).toBeGreaterThanOrEqual(0);
    expect(at.preTrip).toBeGreaterThanOrEqual(0);
    expect(at.defectReport).toBeGreaterThan(at.preTrip);
  });

  it("marks each missing part BLANK on the cover, never silently filled", () => {
    const blanks = [...p.cover.matchAll(/BLANK — to be completed by hand/g)];
    expect(blanks.length).toBe(3); // manifest, pre-trip, en-route defect report
    expect(p.cover).toContain("No pre-trip on file");
    expect(p.cover).toContain("A blank inspection is not a passed inspection");
  });

  it("marks the itinerary filled — it is derived from the trip itself", () => {
    expect(p.cover).toContain("Trip Itinerary");
    expect(p.cover).toContain("✓ Filled");
  });

  it("omits the freight block when no shipments ride the trip", () => {
    expect(p.itinerary).not.toContain("Received By");
  });

  it("prints the blank pre-trip header as empty form fields", () => {
    for (const label of ["Date &amp; time", "Location \\(town or highway\\)", "NSC No\\.", "Odometer", "Driver"]) {
      expect(fieldValue(p.preTrip, label), label).toBe("&nbsp;");
    }
    // A blank form has not been inspected — it must not claim a clean result.
    expect(p.preTrip).not.toContain("No defects found");
  });
});

describe("driverPackageHtml — a deadhead", () => {
  // A deadhead carries no passengers and the server refuses a manifest for it,
  // so the package leaves NL-TM-01 out rather than printing a blank one a
  // driver might fill in. Shipments may still ride it.
  const html = driverPackageHtml(
    full({ trip: trip({ isEmptyLeg: true, manifestId: null, seatsConfirmed: 0 }), manifest: null }),
    COMPANY,
  );
  const at = partOffsets(html);
  const cover = html.slice(at.cover, at.itinerary);

  it("prints no trip manifest sheet", () => {
    expect(at.manifest).toBe(-1);
    expect(html).not.toContain("Trip Manifest (NL-TM-01)");
  });

  it("still prints the itinerary, pre-trip and en-route defect report, in order", () => {
    expect(at.cover).toBeLessThan(at.itinerary);
    expect(at.itinerary).toBeLessThan(at.preTrip);
    expect(at.preTrip).toBeLessThan(at.defectReport);
    expect([...html.matchAll(/class="nlpkg-brk"/g)].length).toBe(2);
  });

  it("says it is a deadhead on the cover, and counts three parts", () => {
    expect(cover).toContain(DEADHEAD_COVER_LINE);
    expect(cover).toContain("Three parts follow, one per sheet.");
    const rows = [...cover.matchAll(/<tr>\s*<td class="num">(\d+)<\/td>\s*<td>(.*?)<\/td>/g)].map((m) => m[2]);
    expect(rows).toEqual(["Trip Itinerary", "Pre-Trip Inspection (NL-PTI-01)", "En-Route Defect Report"]);
    expect(cover).not.toContain("The manifest, itinerary and pre-trip");
  });

  it("keeps shipments on the itinerary", () => {
    expect(html.slice(at.itinerary, at.preTrip)).toContain("Pallet of drill core boxes");
  });

  it("never says deadhead on an ordinary trip's cover", () => {
    expect(driverPackageHtml(full(), COMPANY)).not.toContain(DEADHEAD_COVER_LINE);
  });
});

describe("driverPackageHtml — the pre-trip part prints NL-PTI-01 rev 3", () => {
  const nl01 = parts(
    driverPackageHtml(
      full({
        trip: trip({ vehicleUnit: "NL-01", vehicleId: "veh-1" }),
        preTrip: inspection("PreTrip", "NL-01"),
      }),
      COMPANY,
    ),
  ).preTrip;
  const nl02 = parts(driverPackageHtml(full(), COMPANY)).preTrip;
  const unknown = parts(
    driverPackageHtml(full({ trip: trip({ vehicleUnit: null, vehicleId: null }), preTrip: null }), COMPANY),
  ).preTrip;

  it("prints itemsFor(unit, 'PreTrip') — 53 rows for NL-01", () => {
    expect(checklistRowCount(nl01)).toBe(53);
  });

  it("prints 64 rows for NL-02, and for an unknown unit (every row, fail-safe)", () => {
    expect(checklistRowCount(nl02)).toBe(64);
    expect(checklistRowCount(unknown)).toBe(64);
  });

  it("matches the catalogue the console uses, row for row", () => {
    const count = (unit: string | null) =>
      itemsFor(unit, "PreTrip").reduce((n, g) => n + g.items.length, 0);
    expect(checklistRowCount(nl01)).toBe(count("NL-01"));
    expect(checklistRowCount(nl02)).toBe(count("NL-02"));
    expect(checklistRowCount(unknown)).toBe(count(null));
  });

  it("has no Interior Lights group — rev 3 removed it", () => {
    for (const part of [nl01, nl02, unknown]) {
      expect(part).not.toContain("Lights &amp; Signals — Interior");
      expect(part).not.toContain("Interior: Hazard lights");
    }
  });

  it("prints rev 3's consolidated rows", () => {
    for (const part of [nl01, nl02]) {
      expect(part).toContain('<td class="item">Engine fluid levels</td>');
      expect(part).toContain('<td class="item">Remote / winter kit</td>');
      expect(part).toContain('<td class="item">Comms &amp; navigation</td>');
      // ...and not the rows they replaced.
      expect(part).not.toContain('<td class="item">Engine oil</td>');
      expect(part).not.toContain('<td class="item">Survival kit</td>');
    }
  });

  it("includes the bus-only rows for NL-02 and omits them for NL-01", () => {
    expect(nl02).toContain("Emergency exits / windows (NL-02)");
    expect(nl02).toContain("Fitted cargo area — partition &amp; tie-downs (NL-02)");
    expect(nl01).not.toContain("Emergency exits / windows (NL-02)");
    expect(nl01).not.toContain("Fitted cargo area");
    expect(nl01).not.toContain("Fuel / water separator (NL-02, diesel)");
  });
});

describe("driverPackageHtml — the pre-trip header", () => {
  const filled = parts(
    driverPackageHtml(
      full({ preTrip: { ...inspection("PreTrip"), location: "Leaf Rapids depot" } }),
      COMPANY,
    ),
  ).preTrip;

  it("prints date & time, location and an NSC No. cell", () => {
    expect(fieldValue(filled, "Date &amp; time")).not.toBe("&nbsp;");
    expect(fieldValue(filled, "Location \\(town or highway\\)")).toBe("Leaf Rapids depot");
    expect(fieldValue(filled, "Route / Trip #")).toBe("NL-2026-0042");
  });

  it("leaves NSC No. a ruled blank while the carrier has none configured", () => {
    expect(COMPANY.nscNo).toBe("");
    expect(fieldValue(filled, "NSC No\\.")).toBe("&nbsp;");
  });

  it("prints the NSC No. once one is configured", () => {
    const withNsc = parts(driverPackageHtml(full(), { ...COMPANY, nscNo: "MB-123456" })).preTrip;
    expect(fieldValue(withNsc, "NSC No\\.")).toBe("MB-123456");
  });

  it('says "No defects found" on a clean filed pre-trip', () => {
    expect(filled).toContain("☒ No defects found");
  });

  it("ticks the Pre-Trip half", () => {
    expect(filled).toContain("☒ Pre-Trip");
    expect(filled).toContain("☐ Post-Trip");
  });
});

describe("driverPackageHtml — the manifest part", () => {
  it("prints a Bookeo-imported passenger like any other passenger", () => {
    const base = manifest();
    const bookeo = {
      ...base.passengers[0],
      name: "M. Linklater",
      email: null,
      phone: "(204) 555-0177",
      externalRef: "bookeo:88123",
    };
    const m = { ...base, passengers: [...base.passengers, bookeo] };
    const sheet = parts(driverPackageHtml(full({ manifest: m }), COMPANY)).manifest;

    expect(sheet).toContain("<td>J. Bighetty</td>");
    expect(sheet).toContain("<td>M. Linklater</td>");
    expect(sheet).toContain("<td>(204) 555-0177</td>");
    // NL-TM-01 has no notes / source column, so the import reference is not
    // printed — the row is indistinguishable from a hand-entered one.
    expect(sheet).not.toContain("88123");
    expect(sheet).not.toMatch(/bookeo/i);
  });
});

describe("driverPackageHtml — the en-route defect report part", () => {
  const part = parts(driverPackageHtml(full(), COMPANY)).defectReport;

  it("is the sub-document's own sheet, verbatim", () => {
    expect(part.trim()).toBe(enRouteDefectReportHtml(trip(), COMPANY).trim().replace(/^<style>[\s\S]*?<\/style>\s*/, ""));
  });

  it("carries the no-defects tick box and the severity columns", () => {
    expect(part).toContain("☐ No defects found during this trip");
    expect(part).toContain("<th class=\"sev\">Severity</th>");
    expect([...part.matchAll(/☐ Minor<br\/>☐ Major/g)].length).toBe(6);
  });

  it("is prefilled with the trip it rides with", () => {
    expect(fieldValue(part, "Trip #")).toBe("NL-2026-0042");
    expect(fieldValue(part, "Unit")).toBe("NL-02");
    expect(fieldValue(part, "Driver")).toBe("R. Okimaw");
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
        shipments: [],
      }),
      COMPANY,
    );
    expect(html).not.toMatch(leak);
  });
});
