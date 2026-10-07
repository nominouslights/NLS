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
    location: null,
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
      item({ item: "Engine fluid levels", state: "Ok", passed: true }),
      item({ item: "Belts, hoses & radiator", state: "Defect", passed: false, note: "Weeping at the upper hose" }),
      item({ item: "Battery, wiring & block-heater cord", state: "NotApplicable", passed: true }),
    ],
    defects: [defect({ item: "Belts, hoses & radiator", severity: "Major", note: "Weeping at the upper hose" })],
  });
  const html = inspectionReportHtml(filled, NL02_CTX, COMPANY);

  it("ticks OK on an Ok row", () => {
    expect(boxesForRow(html, "Engine fluid levels")).toEqual(["☒", "☐", "☐"]);
  });

  it("ticks Defect on a Defect row", () => {
    expect(boxesForRow(html, "Belts, hoses &amp; radiator")).toEqual(["☐", "☒", "☐"]);
  });

  it("ticks N/A on a NotApplicable row — never as a failure", () => {
    expect(boxesForRow(html, "Battery, wiring &amp; block-heater cord")).toEqual(["☐", "☐", "☒"]);
  });

  it("leaves a row the record does not carry unanswered", () => {
    expect(boxesForRow(html, "Horn")).toEqual(["☐", "☐", "☐"]);
  });

  it("carries the defect onto the defect log with its category", () => {
    expect(html).toContain("Defect Log");
    expect(html).toContain("Belts, hoses &amp; radiator — Weeping at the upper hose");
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
  it("omits the bus rows on a blank NL-01 pre-trip sheet", () => {
    const nl01 = inspectionReportHtml(null, { unit: "NL-01", mode: "PreTrip" }, COMPANY);
    expect(nl01).not.toContain("Emergency exits / windows (NL-02)");
    expect(nl01).not.toContain("Fuel / water separator (NL-02, diesel)");
    expect(nl01).not.toContain("Fitted cargo area");
    expect(nl01).not.toContain("Accessibility lift / ramp &amp; kneeling (if equipped, NL-02)");
  });

  it("keeps them on an NL-02 pre-trip sheet", () => {
    const nl02 = inspectionReportHtml(null, NL02_CTX, COMPANY);
    expect(nl02).toContain("Emergency exits / windows (NL-02)");
    expect(nl02).toContain("Fuel / water separator (NL-02, diesel)");
    expect(nl02).toContain("Accessibility lift / ramp &amp; kneeling (if equipped, NL-02)");
  });

  it("prints the rev 4 post-trip: Close-Out only, new defects go in the Defect Log", () => {
    const post = inspectionReportHtml(null, { unit: "NL-02", mode: "PostTrip" }, COMPANY);
    expect(post).not.toContain("Engine fluid levels");
    expect(post).not.toContain("Emergency exits / windows (NL-02)");
    expect(post).not.toContain("Emergency Equipment");
    expect(post).not.toContain("Wheel nuts / studs");
    expect(post).not.toContain("Defects noticed while driving");
    expect(post).toContain("Close-Out");
    expect(post).toContain("Defect Log");
  });

  it("has no Close-Out or En-Route section on a pre-trip sheet, and both on a post-trip sheet", () => {
    const pre = inspectionReportHtml(null, NL02_CTX, COMPANY);
    expect(pre).not.toContain("Close-Out");
    expect(pre).not.toContain("En-Route Observations");
    expect(inspectionReportHtml(null, { unit: "NL-02", mode: "PostTrip" }, COMPANY)).toContain("Close-Out");
  });
});

describe("inspectionReportHtml — Reg. 95/2008 s.12(1) header fields", () => {
  /** The value cell of the header field labelled `label`. */
  function fieldValue(html: string, label: string): string {
    const escaped = esc(label).replace(/[.*+?^${}()|[\]\\/]/g, "\\$&");
    const m = new RegExp(`<div class="lbl">${escaped}</div><div class="val">(.*?)</div>`).exec(html);
    expect(m, `no header field "${label}"`).not.toBeNull();
    return m![1];
  }

  it("prints the date AND the time the inspection was performed", () => {
    const html = inspectionReportHtml(inspection(), NL02_CTX, COMPANY);
    const value = fieldValue(html, "Date & time");
    expect(value).toContain("2026");
    // A time-of-day component, whatever the runner's timezone.
    expect(value).toMatch(/\d{1,2}:\d{2}/);
  });

  it("prints the recorded location", () => {
    const html = inspectionReportHtml(inspection({ location: "Leaf Rapids — PR 391 yard" }), NL02_CTX, COMPANY);
    expect(fieldValue(html, "Location (town or highway)")).toBe("Leaf Rapids — PR 391 yard");
  });

  it("leaves Location a ruled blank on an older record and on the blank form", () => {
    expect(fieldValue(inspectionReportHtml(inspection({ location: null }), NL02_CTX, COMPANY), "Location (town or highway)")).toBe("&nbsp;");
    expect(fieldValue(inspectionReportHtml(null, NL02_CTX, COMPANY), "Location (town or highway)")).toBe("&nbsp;");
  });

  it("leaves NSC No. a ruled blank for hand completion while none is configured", () => {
    expect(fieldValue(inspectionReportHtml(null, NL02_CTX, { ...COMPANY, nscNo: "" }), "NSC No.")).toBe("&nbsp;");
  });

  it("prints the carrier's NSC number once it is configured", () => {
    const html = inspectionReportHtml(null, NL02_CTX, { ...COMPANY, nscNo: "MB-TEST-0001" });
    expect(fieldValue(html, "NSC No.")).toBe("MB-TEST-0001");
  });

  it('states "No defects found" on a filled record with none', () => {
    expect(inspectionReportHtml(inspection({ defects: [] }), NL02_CTX, COMPANY)).toContain("No defects found");
  });

  it("does not claim no defects on a record that has one, nor on a blank form", () => {
    const withDefect = inspection({ defects: [defect({ item: "Coolant" })] });
    expect(inspectionReportHtml(withDefect, NL02_CTX, COMPANY)).not.toContain("No defects found");
    expect(inspectionReportHtml(null, NL02_CTX, COMPANY)).not.toContain("No defects found");
  });
});

describe("inspectionReportHtml — the scope column", () => {
  const html = inspectionReportHtml(null, NL02_CTX, COMPANY);

  it("marks a bus-only row NL-02", () => {
    expect(scopeForRow(html, "Fuel / water separator (NL-02, diesel)")).toContain("NL-02");
  });

  it("marks a Northern Link addition NL", () => {
    expect(scopeForRow(html, "Remote / winter kit")).toContain("<span>NL</span>");
    // Rev 3 retagged the engine-bay rows: Schedule 2 has no engine-fluid part.
    expect(scopeForRow(html, "Engine fluid levels")).toContain("<span>NL</span>");
  });

  it("leaves a plain NSC 13 row unmarked", () => {
    expect(scopeForRow(html, "Brake fluid reservoir (hydraulic)")).not.toContain("<span>");
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
        item({ item: "Engine fluid levels" }),
        item({ group: "Fluids (NL-TM-01)", item: "Oil, coolant and washer levels", state: "Defect", passed: false }),
      ],
    }),
    NL02_CTX,
    COMPANY,
  );

  it("renders the catalogue row it recognises", () => {
    expect(boxesForRow(html, "Engine fluid levels")).toEqual(["☒", "☐", "☐"]);
  });

  it("prints a rev-2 record's retired keys verbatim, never onto their replacement rows", () => {
    // Every key here is in RETIRED_KEYS — known, but no longer a catalogue row. The
    // replacement ("Engine fluid levels", "Remote / winter kit") must stay
    // unanswered: ticking it would claim a check the driver never answered as such.
    const rev2 = inspectionReportHtml(
      inspection({
        checklist: [
          item({ group: "Engine Bay", item: "Engine oil", state: "Ok", passed: true }),
          item({ group: "Engine Bay", item: "Coolant", state: "Defect", passed: false, note: "Low" }),
          item({ group: "Emergency Equipment", item: "Survival kit", state: "NotApplicable", passed: true }),
          item({ group: "Lights & Signals — Interior", item: "Interior: Hazard lights" }),
        ],
        defects: [defect({ item: "Coolant", severity: "Major", note: "Low" })],
      }),
      NL02_CTX,
      COMPANY,
    );
    expect(rev2).toContain("Recorded under a previous form revision");
    for (const old of ["Engine oil", "Coolant", "Survival kit", "Interior: Hazard lights"]) {
      expect(rev2).toContain(`<td>${old}</td>`);
    }
    expect(rev2).toContain("Lights &amp; Signals — Interior");
    expect(boxesForRow(rev2, "Engine fluid levels")).toEqual(["☐", "☐", "☐"]);
    expect(boxesForRow(rev2, "Remote / winter kit")).toEqual(["☐", "☐", "☐"]);
    expect(boxesForRow(rev2, "Hazard (4-way) lights")).toEqual(["☐", "☐", "☐"]);
    // The defect log keeps the stored item string.
    expect(rev2).toContain("Coolant — Low");
  });

  it("does not silently drop the item it does not recognise", () => {
    expect(html).toContain("Recorded under a previous form revision");
    expect(html).toContain("Oil, coolant and washer levels");
    expect(html).toContain("Fluids (NL-TM-01)");
  });

  it("says nothing about a previous revision when every item is current", () => {
    const clean = inspectionReportHtml(
      inspection({ checklist: [item({ item: "Engine fluid levels" })] }),
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
