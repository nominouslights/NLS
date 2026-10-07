import { describe, expect, it } from "vitest";
import type {
  InspectionChecklistItemWire,
  InspectionDefectWire,
  VehicleInspection,
} from "@/lib/api/maintenance";
import { RETIRED_KEYS } from "@/lib/inspectionForm";
import {
  checklistWire,
  defectsWire,
  TRIP_ROW_START,
  inspectionResult,
  newDefectOptions,
  newDefectsFromRecord,
  newDefectsProblem,
  newDefectsWire,
  withoutUnofferedItems,
  isRetiredFormRecord,
  rowsFor,
  rowsForUnitChange,
  rowsFromRecord,
} from "./checklistRows";

// The read-only fail-safe for saved inspections. A record holding any key outside
// today's catalogue must never be rebuilt onto current rows — whether the key is
// one rev 3 retired (and so is known, with a replacement) or one nobody has ever
// seen. Rebuilding would drop the old answers and, on save, overwrite the record.

type DefectFixture = Pick<InspectionDefectWire, "item" | "severity" | "note"> & Partial<InspectionDefectWire>;

function inspection(checklist: InspectionChecklistItemWire[], defectFixtures: DefectFixture[] = []): VehicleInspection {
  // Open and unattached unless a test says otherwise.
  const defects: InspectionDefectWire[] = defectFixtures.map((d) => ({
    workOrderId: null,
    resolutionReason: null,
    resolvedAtUtc: null,
    ...d,
  }));
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
    result: "Pass",
    checklist,
    defects,
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

const ok = (item: string, group: string | null = null): InspectionChecklistItemWire => ({
  group,
  item,
  passed: true,
  state: "Ok",
  note: null,
});

describe("isRetiredFormRecord", () => {
  it("is false for a record answered entirely on the current form", () => {
    const current = rowsFor("NL-02", "PreTrip").map((r) => ok(r.itemKey, r.groupKey));
    expect(isRetiredFormRecord(inspection(current))).toBe(false);
  });

  it("is true for a rev-2 record whose keys are retired but known", () => {
    // Every one of these is in RETIRED_KEYS with a replacement row.
    const keys = ["Engine oil", "Survival kit", "Interior: Hazard lights", "Cell phone charged"];
    for (const key of keys) expect(RETIRED_KEYS.has(key)).toBe(true);
    expect(isRetiredFormRecord(inspection(keys.map((k) => ok(k))))).toBe(true);
  });

  it("is true when a single retired key sits among current ones", () => {
    const current = rowsFor("NL-02", "PreTrip").map((r) => ok(r.itemKey, r.groupKey));
    expect(isRetiredFormRecord(inspection([...current, ok("Washer fluid", "Engine Bay")]))).toBe(true);
  });

  it("is true for a record whose keys are completely unknown", () => {
    expect(isRetiredFormRecord(inspection([ok("Oil, coolant and washer levels", "Fluids (NL-TM-01)")]))).toBe(true);
  });
});

describe("rowsFromRecord never remaps a retired key", () => {
  it("leaves the replacement row unanswered and puts no old key on the wire", () => {
    // Were a caller to skip the isRetiredFormRecord guard, the rebuilt rows still
    // must not claim the replacement was answered.
    const rec = inspection(
      [ok("Engine oil", "Engine Bay"), { ...ok("Coolant", "Engine Bay"), state: "Defect", passed: false }],
      [{ item: "Coolant", severity: "Major", note: "Low" }],
    );
    const rows = rowsFromRecord(rec, "NL-02", "PreTrip");
    expect(rows.find((r) => r.itemKey === "Engine fluid levels")?.state).toBeNull();
    const wire = checklistWire(rows).map((c) => c.item);
    expect(wire).not.toContain("Engine oil");
    expect(wire).not.toContain("Coolant");
    expect(defectsWire(rows)).toEqual([]);
  });
});

describe("TRIP_ROW_START — a new trip inspection starts at OK", () => {
  it("starts every row at OK in both modes, and plain rowsFor stays unanswered", () => {
    expect(rowsFor("NL-02", "PreTrip", TRIP_ROW_START).every((r) => r.state === "Ok")).toBe(true);
    expect(rowsFor("NL-02", "PostTrip", TRIP_ROW_START).every((r) => r.state === "Ok")).toBe(true);
    expect(rowsFor("NL-02", "PreTrip").every((r) => r.state === null)).toBe(true);
  });

  it("does not touch a saved record: a row it does not carry still comes back unanswered", () => {
    const rows = rowsFromRecord(inspection([ok("Engine fluid levels", "Engine Bay")]), "NL-02", "PreTrip");
    expect(rows.find((r) => r.itemKey === "Engine fluid levels")?.state).toBe("Ok");
    expect(rows.filter((r) => r.state === null).length).toBe(rows.length - 1);
  });
});

describe("rowsForUnitChange", () => {
  const nl01Keys = new Set(rowsFor("NL-01", "PreTrip").map((r) => r.itemKey));
  const busOnly = rowsFor("NL-02", "PreTrip").filter((r) => !nl01Keys.has(r.itemKey));

  it("drops the bus-only rows when the unit narrows, so none are sent as OK unseen", () => {
    expect(busOnly.length).toBeGreaterThan(0);
    const start = rowsFor(null, "PreTrip", TRIP_ROW_START);
    const next = rowsForUnitChange(start, "NL-01", "PreTrip");
    expect(next).toHaveLength(nl01Keys.size);
    const wire = checklistWire(next).map((c) => c.item);
    for (const r of busOnly) expect(wire).not.toContain(r.itemKey);
  });

  it("keeps answers already given to rows both units share", () => {
    const start = rowsFor(null, "PreTrip", TRIP_ROW_START).map((r) =>
      r.itemKey === "Engine fluid levels" ? { ...r, state: "Defect" as const, severity: "Major" as const, note: "Low coolant" } : r,
    );
    const kept = rowsForUnitChange(start, "NL-01", "PreTrip").find((r) => r.itemKey === "Engine fluid levels");
    expect(kept).toMatchObject({ state: "Defect", severity: "Major", note: "Low coolant" });
  });

  it("brings rows back at the starting answer when the unit widens", () => {
    const start = rowsFor("NL-01", "PreTrip", TRIP_ROW_START);
    const next = rowsForUnitChange(start, "NL-02", "PreTrip");
    for (const r of busOnly) expect(next.find((x) => x.itemKey === r.itemKey)?.state).toBe("Ok");
  });
});

describe("post-trip new defects (rev 4)", () => {
  const preTrip = inspection(
    [ok("Tire condition", "Tires & Wheels")],
    [{ item: "Tire condition", severity: "Minor", note: "Worn" }],
  );

  it("offers the unit's pre-trip rows, minus the ones the pre-trip already reported", () => {
    const all = newDefectOptions("NL-02", null).map((o) => o.key);
    expect(all).toEqual(rowsFor("NL-02", "PreTrip").map((r) => r.itemKey));
    const opts = newDefectOptions("NL-02", preTrip).map((o) => o.key);
    expect(opts).not.toContain("Tire condition");
    expect(opts).toHaveLength(all.length - 1);
  });

  it("narrows by unit like the pre-trip does", () => {
    const nl01 = newDefectOptions("NL-01", null).map((o) => o.key);
    expect(nl01).toEqual(rowsFor("NL-01", "PreTrip").map((r) => r.itemKey));
    expect(nl01).not.toContain("Emergency exits / windows");
  });

  it("un-picks a new defect whose item the new unit does not have, so the save blocks", () => {
    const picked = [
      { itemKey: "Emergency exits / windows", severity: "Major" as const, note: "Jammed" },
      { itemKey: "Steering", severity: "Minor" as const, note: "Loose" },
    ];
    const after = withoutUnofferedItems(picked, newDefectOptions("NL-01", null));
    expect(after).toEqual([{ ...picked[0], itemKey: "" }, picked[1]]);
    expect(newDefectsProblem(after)).toMatch(/Pick the item/);
  });

  it("sends each new defect against its real item key, never a checklist row", () => {
    const wire = newDefectsWire([{ itemKey: "Parking brake", severity: "Major", note: " Will not hold " }]);
    expect(wire).toEqual([{ item: "Parking brake", severity: "Major", note: "Will not hold" }]);
  });

  it("blocks an unpicked item or a missing note", () => {
    expect(newDefectsProblem([{ itemKey: "", severity: "Minor", note: "x" }])).toMatch(/Pick the item/);
    expect(newDefectsProblem([{ itemKey: "Steering", severity: "Minor", note: " " }])).toMatch(/need a note/);
    expect(newDefectsProblem([{ itemKey: "Steering", severity: "Minor", note: "Loose" }])).toBeNull();
  });

  it("splits a saved post-trip's defects: Close-Out defects stay on their rows, the rest are new", () => {
    const rec = {
      ...inspection(
        [ok("Keys returned / secured", "Close-Out")],
        [
          { item: "Keys returned / secured", severity: "Minor", note: "Late" },
          { item: "Parking brake", severity: "Major", note: "Slipping" },
        ],
      ),
      type: "PostTrip" as const,
    };
    const rows = rowsFromRecord(rec, "NL-02", "PostTrip");
    expect(newDefectsFromRecord(rec, rows)).toEqual([{ itemKey: "Parking brake", severity: "Major", note: "Slipping" }]);
  });

  it("fails on a Major new defect and passes-with-defects on a Minor one", () => {
    const rows = rowsFor("NL-02", "PostTrip", TRIP_ROW_START);
    expect(inspectionResult(rows, [])).toBe("Pass");
    expect(inspectionResult(rows, [{ itemKey: "Steering", severity: "Minor", note: "x" }])).toBe("Pass with defects");
    expect(inspectionResult(rows, [{ itemKey: "Steering", severity: "Major", note: "x" }])).toBe("Fail");
  });

  it("opens an old 28-row post-trip read-only rather than rebuilding it", () => {
    const old = { ...inspection([ok("Steering", "Controls & Instruments"), ok("Defects noticed while driving", "En-Route Observations")]), type: "PostTrip" as const };
    expect(isRetiredFormRecord(old)).toBe(true);
  });
});
