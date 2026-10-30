import { describe, expect, it } from "vitest";
import type { VehicleDefect } from "./types";
import {
  RECENTLY_RESOLVED_DAYS,
  assignedVehicleId,
  assignedVehicleOpenDefects,
  defectResolutionText,
  isDefectOpen,
  openDefectWorkOrderText,
  openDefects,
  recentlyResolved,
  vehicleDefects,
  worstDefectKind,
} from "./data";

// The open-defect rule. Mirrors the server's defect lifecycle as the work-order slice defines
// it: completing a work order records an outcome PER DEFECT — Repaired and No fault found
// resolve the defect, Deferred leaves it open. So "open" means "not resolved", and a linked
// work order on its own says nothing about whether the fault is still there. The previous mock
// rule (open = workOrder === null) hid an in-the-shop defect from the driver's pre-trip view.

function defect(over: Partial<VehicleDefect>): VehicleDefect {
  return {
    id: "DF-T",
    vehicleId: "VEH-T",
    reportedOn: "2026-09-01",
    item: "Test item",
    severity: "Minor",
    note: "",
    dk: "soon",
    workOrder: null,
    resolvedOn: null,
    resolution: null,
    ...over,
  };
}

describe("isDefectOpen — open means not resolved", () => {
  it("an unresolved defect with no work order is open", () => {
    expect(isDefectOpen(defect({}))).toBe(true);
  });

  it("an unresolved defect WITH a work order is still open", () => {
    expect(isDefectOpen(defect({ workOrder: "WO-1" }))).toBe(true);
  });

  it("a Repaired defect is not open", () => {
    expect(
      isDefectOpen(defect({ workOrder: "WO-1", resolvedOn: "2026-09-10", resolution: "Repaired" })),
    ).toBe(false);
  });

  it("a No-fault-found defect is not open", () => {
    expect(
      isDefectOpen(
        defect({ workOrder: "WO-1", resolvedOn: "2026-09-10", resolution: "NoFaultFound" }),
      ),
    ).toBe(false);
  });
});

describe("the mock data honours the rule", () => {
  it("openDefects is exactly the unresolved rows", () => {
    expect(openDefects.map((d) => d.id)).toEqual(
      vehicleDefects.filter((d) => d.resolvedOn === null).map((d) => d.id),
    );
  });

  it("carries at least one open defect that has a work order, and counts it as open", () => {
    const openWithWo = vehicleDefects.filter((d) => d.workOrder !== null && d.resolvedOn === null);
    expect(openWithWo.length).toBeGreaterThan(0);
    for (const d of openWithWo) expect(openDefects).toContain(d);
  });

  it("carries resolved defects, and none of them count toward open", () => {
    const resolved = vehicleDefects.filter((d) => d.resolvedOn !== null);
    expect(resolved.some((d) => d.resolution === "Repaired")).toBe(true);
    expect(resolved.some((d) => d.resolution === "NoFaultFound")).toBe(true);
    for (const d of resolved) expect(openDefects).not.toContain(d);
  });

  it("resolution and resolvedOn are null together", () => {
    for (const d of vehicleDefects) expect(d.resolution === null).toBe(d.resolvedOn === null);
  });

  it("Today's tile counts the assigned vehicle's open defects, WO-linked ones included", () => {
    expect(assignedVehicleOpenDefects.every((d) => d.vehicleId === assignedVehicleId)).toBe(true);
    expect(assignedVehicleOpenDefects.every(isDefectOpen)).toBe(true);
    expect(assignedVehicleOpenDefects.some((d) => d.workOrder !== null)).toBe(true);
  });
});

describe("recentlyResolved", () => {
  const asOf = "2026-09-20";

  it("includes a resolution exactly RECENTLY_RESOLVED_DAYS old and excludes one a day older", () => {
    const edge = defect({ id: "A", resolvedOn: "2026-09-06", resolution: "Repaired" });
    const stale = defect({ id: "B", resolvedOn: "2026-09-05", resolution: "Repaired" });
    expect(RECENTLY_RESOLVED_DAYS).toBe(14);
    expect(recentlyResolved([edge, stale], asOf).map((d) => d.id)).toEqual(["A"]);
  });

  it("never lists an open defect, with or without a work order", () => {
    expect(recentlyResolved([defect({}), defect({ workOrder: "WO-1" })], asOf)).toEqual([]);
  });

  it("orders newest first", () => {
    const older = defect({ id: "old", resolvedOn: "2026-09-10", resolution: "Repaired" });
    const newer = defect({ id: "new", resolvedOn: "2026-09-18", resolution: "NoFaultFound" });
    expect(recentlyResolved([older, newer], asOf).map((d) => d.id)).toEqual(["new", "old"]);
  });
});

describe("display wording", () => {
  it("an open defect reads 'On WO-x' when linked, 'No WO' otherwise", () => {
    expect(openDefectWorkOrderText(defect({ workOrder: "WO-2231" }))).toBe("On WO-2231");
    expect(openDefectWorkOrderText(defect({}))).toBe("No WO");
  });

  it("matches the console's resolution wording", () => {
    expect(
      defectResolutionText(
        defect({ workOrder: "WO-2224", resolvedOn: "2026-09-11", resolution: "Repaired" }),
      ),
    ).toBe("Repaired under WO-2224 on 2026-09-11");
    expect(
      defectResolutionText(
        defect({ workOrder: "WO-2226", resolvedOn: "2026-09-08", resolution: "NoFaultFound" }),
      ),
    ).toBe("No fault found — WO-2226");
  });

  it("worstDefectKind is ontime for none, over if any defect is over, soon otherwise", () => {
    expect(worstDefectKind([])).toBe("ontime");
    expect(worstDefectKind([defect({})])).toBe("soon");
    expect(worstDefectKind([defect({}), defect({ dk: "over" })])).toBe("over");
  });
});
