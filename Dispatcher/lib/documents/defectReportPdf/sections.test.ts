import { describe, expect, it } from "vitest";
import { COMPANY } from "@/lib/company";
import type { TripRecord } from "@/lib/api/trips";
import { enRouteDefectReportHtml } from "./index";
import { DEFECT_REPORT_ROWS, NO_DEFECTS_LABEL, SEVERITY_INSTRUCTION } from "./sections";

// The en-route defect report is a HAND-COMPLETION sheet: only the trip's
// identity is prefilled, and nothing on it may look answered.

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
    stops: [],
    distanceKm: 214,
    scheduleTemplateId: null,
    roundTripKey: null,
    direction: "Outbound",
    isEmptyLeg: false,
    clientId: "client-1",
    clientName: "Alamos Gold",
    poNumber: null,
    driverId: "driver-1",
    driverName: "R. Okimaw",
    vehicleId: "veh-2",
    vehicleUnit: "NL-02",
    seatsCapacity: 14,
    seatsConfirmed: 9,
    seatsMinimum: null,
    demandGuaranteed: false,
    status: "Scheduled",
    manifestId: null,
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

function fieldValue(html: string, label: string): string | null {
  const m = html.match(new RegExp(`<div class="lbl">${label}</div><div class="val">(.*?)</div>`));
  return m ? m[1] : null;
}

describe("enRouteDefectReportHtml — header", () => {
  const html = enRouteDefectReportHtml(trip(), COMPANY);

  it("carries the company brand, the title and the form line", () => {
    expect(html).toContain("NORTHERN LINK <span class=\"blue\">SHUTTLE AND CARGO</span>");
    expect(html).toContain('<div class="t">EN-ROUTE DEFECT REPORT</div>');
    expect(html).toContain("Form NL-PTI-01 · Defects found after the pre-trip");
    expect(html).toContain(COMPANY.phone);
  });

  it("prints the severity instruction in the inspection sheet's warning box", () => {
    expect(SEVERITY_INSTRUCTION).toBe(
      "Major defect: stop and do not drive until it is repaired — call dispatch immediately. " +
        "Minor defect: report to the carrier before the next inspection.",
    );
    expect(html).toContain(`<div class="warn">${SEVERITY_INSTRUCTION}</div>`);
  });

  it("keeps the shared US-Letter page geometry", () => {
    expect(html).toContain("@page { size: Letter; margin: 12mm 12mm; }");
  });
});

describe("enRouteDefectReportHtml — trip fields", () => {
  const html = enRouteDefectReportHtml(trip(), COMPANY);

  it("prefills date, trip #, unit, driver and route from the trip", () => {
    expect(fieldValue(html, "Date")).toBe("2026-09-20");
    expect(fieldValue(html, "Trip #")).toBe("NL-2026-0042");
    expect(fieldValue(html, "Unit")).toBe("NL-02");
    expect(fieldValue(html, "Driver")).toBe("R. Okimaw");
    expect(fieldValue(html, "Route")).toBe("Leaf Rapids to Lynn Lake (Leaf Rapids → Lynn Lake)");
  });

  it("leaves end-of-trip odometer and arrival time for the driver", () => {
    expect(fieldValue(html, "Odometer at end of trip \\(km\\)")).toBe("&nbsp;");
    expect(fieldValue(html, "Arrival time")).toBe("&nbsp;");
  });

  it("prints an unassigned driver or unit as a ruled blank", () => {
    const blank = enRouteDefectReportHtml(trip({ driverName: null, vehicleUnit: null }), COMPANY);
    expect(fieldValue(blank, "Unit")).toBe("&nbsp;");
    expect(fieldValue(blank, "Driver")).toBe("&nbsp;");
    expect(blank).not.toMatch(/\bundefined\b|\bnull\b|\bNaN\b/);
  });

  it("escapes trip text", () => {
    const html2 = enRouteDefectReportHtml(trip({ routeName: "A <b> & B" }), COMPANY);
    expect(html2).toContain("A &lt;b&gt; &amp; B");
  });
});

describe("enRouteDefectReportHtml — the defect table", () => {
  const html = enRouteDefectReportHtml(trip(), COMPANY);

  it("has an unticked, prominent no-defects box", () => {
    expect(html).toContain(`<div class="none">☐ ${NO_DEFECTS_LABEL}</div>`);
    expect(html).not.toContain("☒");
  });

  it("has the six hand-completion columns", () => {
    const head = html.slice(html.indexOf("<thead>"), html.indexOf("</thead>"));
    const cols = [...head.matchAll(/<th[^>]*>(.*?)<\/th>/g)].map((m) => m[1]);
    expect(cols).toEqual([
      "Time noticed",
      "Location (town / highway km)",
      "Item / system",
      "Describe the defect",
      "Severity",
      "Reported to (name) &amp; time",
    ]);
  });

  it(`prints ${DEFECT_REPORT_ROWS} blank rows, each with Minor / Major boxes`, () => {
    const body = html.slice(html.indexOf("<tbody>"), html.indexOf("</tbody>"));
    expect([...body.matchAll(/<tr>/g)].length).toBe(DEFECT_REPORT_ROWS);
    expect([...body.matchAll(/☐ Minor<br\/>☐ Major/g)].length).toBe(DEFECT_REPORT_ROWS);
    expect(DEFECT_REPORT_ROWS).toBe(6);
  });
});

describe("enRouteDefectReportHtml — signatures", () => {
  const html = enRouteDefectReportHtml(trip(), COMPANY);

  it("has driver signature and date/time lines, and a received-by line", () => {
    expect(html).toContain('<div class="sigline">Driver signature</div>');
    expect(html).toContain('<div class="sigline">Received by (carrier / dispatch) — signature</div>');
    expect([...html.matchAll(/<div class="sigline">Date \/ time<\/div>/g)].length).toBe(2);
  });

  it("carries no tax wording and no money", () => {
    expect(html).not.toMatch(/\b(GST|HST|PST|tax)\b/i);
    expect(html).not.toContain("$");
  });
});
