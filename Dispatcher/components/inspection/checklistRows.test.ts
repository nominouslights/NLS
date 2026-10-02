import { describe, expect, it } from "vitest";
import type {
  InspectionChecklistItemWire,
  InspectionDefectWire,
  VehicleInspection,
} from "@/lib/api/maintenance";
import { RETIRED_KEYS } from "@/lib/inspectionForm";
import { checklistWire, defectsWire, isRetiredFormRecord, rowsFor, rowsFromRecord } from "./checklistRows";

// The read-only fail-safe for saved inspections. A record holding any key outside
// today's catalogue must never be rebuilt onto current rows — whether the key is
// one rev 3 retired (and so is known, with a replacement) or one nobody has ever
// seen. Rebuilding would drop the old answers and, on save, overwrite the record.

function inspection(checklist: InspectionChecklistItemWire[], defects: InspectionDefectWire[] = []): VehicleInspection {
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
