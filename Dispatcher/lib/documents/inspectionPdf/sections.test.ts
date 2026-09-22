import { describe, expect, it } from "vitest";
import { inspectionReportHtml } from "./index";
import { COMPANY } from "@/lib/company";
import { NL_PTI_01_CERTIFICATION } from "@/lib/inspectionForm";
import { esc } from "../workOrderPdf/html";
import type {
  InspectionChecklistItemWire,
  InspectionDefectWire,
  VehicleInspection,
} from "@/lib/api/maintenance";

// Form NL-PTI-01 as a printable. The assertions that matter are the ones a
// compliance sheet lives or dies by: the right box ticked on the right row,
// nothing invented on a blank form, the NSC-13/Northern-Link distinction
// visible, and not one recorded answer dropped.

function inspection(over: Partial<VehicleInspection> = {}): VehicleInspection {
  return {
    id: "insp-1",
    type: "PreTrip",
    source: "DriverApp",
    tripNumber: "NL-2026-0042",
    manifestId: null,
    vehicleId: "veh-2",
    unit: "NL-02",
    driverName: "R. Okimaw",
    enteredBy: null,
    performedAt: "2026-09-20T12:30:00Z",
    odometerKm: 184_220,
    result: "PassWithDefects",
    checklist: [],
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
    ...over,
  };
}

function item(over: Partial<InspectionChecklistItemWire> & { item: string }): InspectionChecklistItemWire {
  return { group: null, passed: true, state: "Ok", note: null, ...over };
}

function defect(over: Partial<InspectionDefectWire> & { item: string }): InspectionDefectWire {
  return { severity: "Major", note: null, ...over };
}

const NL02_CTX = { unit: "NL-02", mode: "PreTrip" as const, tripNumber: "NL-2026-0042" };

/** The three OK / Defect / N-A cells of the row whose Item cell is `label`. */
function boxesForRow(html: string, label: string): string[] {
  const escaped = label.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&");
  const row = new RegExp(`<td class="item">${escaped}</td>[\\s\\S]*?</tr>`).exec(html);
  expect(row, `no printed row for "${label}"`).not.toBeNull();
  return [...row![0].matchAll(/<td class="ck">(.*?)<\/td>/g)].map((m) => m[1]);
}

/** The Scope cell of the row whose Item cell is `label`. */
function scopeForRow(html: string, label: string): string {
  const escaped = label.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&");
  const row = new RegExp(`<td class="item">${escaped}</td>[\\s\\S]*?</tr>`).exec(html);
  expect(row, `no printed row for "${label}"`).not.toBeNull();
  return /<td class="scope">(.*?)<\/td>/.exec(row![0])![1];
}

describe("inspectionReportHtml — answers", () => {
  const filled = inspection({
    checklist: [
      item({ item: "Engine oil", state: "Ok", passed: true }),
      item({ item: "Coolant", state: "Defect", passed: false, note: "Weeping at the upper hose" }),
      item({ item: "Washer fluid", state: "NotApplicable", passed: true }),
    ],
    defects: [defect({ item: "Coolant", severity: "Major", note: "Weeping at the upper hose" })],
  });
  const html = inspectionReportHtml(filled, NL02_CTX, COMPANY);

  it("ticks OK on an Ok row", () => {
    expect(boxesForRow(html, "Engine oil")).toEqual(["☒", "☐", "☐"]);
  });

  it("ticks Defect on a Defect row", () => {
    expect(boxesForRow(html, "Coolant")).toEqual(["☐", "☒", "☐"]);
  });

  it("ticks N/A on a NotApplicable row — never as a failure", () => {
    expect(boxesForRow(html, "Washer fluid")).toEqual(["☐", "☐", "☒"]);
  });

  it("leaves a row the record does not carry unanswered", () => {
    expect(boxesForRow(html, "Horn")).toEqual(["☐", "☐", "☐"]);
  });

  it("carries the defect onto the defect log with its category", () => {
    expect(html).toContain("Defect Log");
    expect(html).toContain("Coolant — Weeping at the upper hose");
    expect(html).toContain("Major");
  });
});

describe("inspectionReportHtml — the blank form", () => {
  const html = inspectionReportHtml(null, NL02_CTX, COMPANY);

  it("prints the sheet rather than omitting it", () => {
    expect(html).toContain("DAILY PRE-TRIP / POST-TRIP INSPECTION");
    expect(html).toContain("Form NL-PTI-01");
  });

  it("leaves every checklist box empty", () => {
    const cells = [...html.matchAll(/<td class="ck">(.*?)<\/td>/g)].map((m) => m[1]);
    expect(cells.length).toBeGreaterThan(50);
    expect(cells.every((c) => c === "☐")).toBe(true);
  });

  it("still ticks the unit and the half of the form from the context", () => {
    expect(html).toContain("☒ NL-02");
    expect(html).toContain("☐ NL-01");
    expect(html).toContain("☒ Pre-Trip");
    expect(html).toContain("☐ Post-Trip");
  });
});

describe("inspectionReportHtml — unit and mode filtering", () => {
  it("omits the seven bus rows on a blank NL-01 sheet", () => {
    const nl01 = inspectionReportHtml(null, { unit: "NL-01", mode: "PostTrip" }, COMPANY);
    expect(nl01).not.toContain("Emergency exits / windows (NL-02)");
    expect(nl01).not.toContain("Fuel / water separator (NL-02, diesel)");
    expect(nl01).not.toContain("Fitted cargo area");
  });

  it("keeps them on an NL-02 sheet", () => {
    const nl02 = inspectionReportHtml(null, { unit: "NL-02", mode: "PostTrip" }, COMPANY);
    expect(nl02).toContain("Emergency exits / windows (NL-02)");
    expect(nl02).toContain("Fuel / water separator (NL-02, diesel)");
  });

  it("has no Close-Out section on a pre-trip sheet, and one on a post-trip sheet", () => {
    expect(inspectionReportHtml(null, NL02_CTX, COMPANY)).not.toContain("Close-Out");
    expect(inspectionReportHtml(null, { unit: "NL-02", mode: "PostTrip" }, COMPANY)).toContain("Close-Out");
  });
});

describe("inspectionReportHtml — the scope column", () => {
  const html = inspectionReportHtml(null, NL02_CTX, COMPANY);

  it("marks a bus-only row NL-02", () => {
    expect(scopeForRow(html, "Fuel / water separator (NL-02, diesel)")).toContain("NL-02");
  });

  it("marks a Northern Link addition NL", () => {
    expect(scopeForRow(html, "Survival kit")).toContain("<span>NL</span>");
  });

  it("leaves a plain NSC 13 row unmarked", () => {
    expect(scopeForRow(html, "Engine oil")).not.toContain("<span>");
  });

  it("prints the legend so an inspector can tell the two apart", () => {
    expect(html).toContain("beyond Manitoba Reg 95/2008 Schedule B");
    expect(html).toContain("NSC Standard 13 requirement");
  });
});

describe("inspectionReportHtml — retired-form records", () => {
  const html = inspectionReportHtml(
    inspection({
      checklist: [
        item({ item: "Engine oil" }),
        item({ group: "Fluids (NL-TM-01)", item: "Oil, coolant and washer levels", state: "Defect", passed: false }),
      ],
    }),
    NL02_CTX,
    COMPANY,
  );

  it("renders the catalogue row it recognises", () => {
    expect(boxesForRow(html, "Engine oil")).toEqual(["☒", "☐", "☐"]);
  });

  it("does not silently drop the item it does not recognise", () => {
    expect(html).toContain("Recorded under a previous form revision");
    expect(html).toContain("Oil, coolant and washer levels");
    expect(html).toContain("Fluids (NL-TM-01)");
  });

  it("says nothing about a previous revision when every item is current", () => {
    const clean = inspectionReportHtml(
      inspection({ checklist: [item({ item: "Engine oil" })] }),
      NL02_CTX,
      COMPANY,
    );
    expect(clean).not.toContain("Recorded under a previous form revision");
  });
});

describe("inspectionReportHtml — certification", () => {
  // The statement carries an "&" ("Shuttle & Cargo"), so the sheet holds its
  // HTML-escaped form — compare against that, not the raw constant.
  const CERTIFICATION = esc(NL_PTI_01_CERTIFICATION);

  it("falls back to the current statement when the record carries none", () => {
    const html = inspectionReportHtml(inspection({ certificationStatement: null }), NL02_CTX, COMPANY);
    expect(html).toContain(CERTIFICATION);
  });

  it("prints the statement the driver actually signed under, when stored", () => {
    const stored = "I certify the vehicle was inspected under the 2025 revision of this form.";
    const html = inspectionReportHtml(inspection({ certificationStatement: stored }), NL02_CTX, COMPANY);
    expect(html).toContain(stored);
    expect(html).not.toContain(CERTIFICATION);
  });

  it("prints the blank form's certification sentence too", () => {
    expect(inspectionReportHtml(null, NL02_CTX, COMPANY)).toContain(CERTIFICATION);
  });
});

describe("inspectionReportHtml — carrier acknowledgement and process rules", () => {
  it("leaves the acknowledgement lines blank when unsigned", () => {
    const html = inspectionReportHtml(inspection(), NL02_CTX, COMPANY);
    expect(html).toContain("Major defect — carrier acknowledgement");
    expect(html).toContain("Carrier representative");
  });

  it("fills them from the record when signed", () => {
    const html = inspectionReportHtml(
      inspection({ carrierAcknowledgedBy: "E. Campbell", carrierAcknowledgedAtUtc: "2026-09-20T18:05:00Z" }),
      NL02_CTX,
      COMPANY,
    );
    expect(html).toContain("E. Campbell");
  });

  it("carries the three process rules", () => {
    const html = inspectionReportHtml(null, NL02_CTX, COMPANY);
    expect(html).toContain("out of service");
    expect(html).toContain("valid for 24 hours");
    expect(html).toContain("previous day");
  });
});
