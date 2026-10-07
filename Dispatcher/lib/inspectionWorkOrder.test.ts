import { describe, expect, it } from "vitest";
import type { InspectionDefectWire, VehicleInspection } from "./api/maintenance";
import {
  defectLineText,
  defectRefs,
  inspectionDefectWorkOrderId,
  isAttachable,
  isInspectionDefectAttachable,
  isVehicleDefectAttachable,
  settledPendingWorkOrders,
  type PendingWorkOrder,
  prefillFromDefects,
  prefillFromInspection,
} from "./inspectionWorkOrder";
import { vehicleDefect } from "./defect.fixtures";

// A work order is linked to each defect by (inspectionId, item). The prefill must
// carry exactly those refs — never the whole-inspection link — and only for open,
// unattached defects.

function defect(over: Partial<InspectionDefectWire> & { item: string }): InspectionDefectWire {
  return { severity: "Major", note: null, workOrderId: null, resolutionReason: null, resolvedAtUtc: null, ...over };
}

function inspection(defects: InspectionDefectWire[], over: Partial<VehicleInspection> = {}): VehicleInspection {
  return {
    id: "insp-9",
    type: "PostTrip",
    source: "DriverApp",
    tripNumber: "NL-2026-0101",
    manifestId: null,
    vehicleId: "veh-1",
    unit: "NL-02",
    driverName: "R. Okimaw",
    enteredBy: null,
    performedAt: "2026-10-01T12:00:00Z",
    odometerKm: null,
    location: null,
    result: "Fail",
    checklist: [],
    defects,
    weather: [],
    temperatureC: null,
    roadConditions: [],
    visibility: null,
    roadAdvisories: null,
    fuelLevel: null,
    issues: [],
    attestations: [],
    driverSignatureName: null,
    certifiedAt: null,
    fuelAdded: false,
    fuelLitres: null,
    fuelCostCad: null,
    generatedWorkOrderId: null,
    createdAtUtc: "2026-10-01T12:05:00Z",
    carrierAcknowledgedBy: null,
    carrierAcknowledgedAtUtc: null,
    carrierAcknowledgementNote: null,
    certificationStatement: null,
    ...over,
  };
}

describe("isAttachable", () => {
  it("is true only for an open defect with no active work order", () => {
    expect(isAttachable(defect({ item: "A" }))).toBe(true);
    expect(isAttachable(defect({ item: "A", workOrderId: "wo-1" }))).toBe(false);
    expect(isAttachable(defect({ item: "A", resolvedAtUtc: "2026-10-02T00:00:00Z" }))).toBe(false);
  });
});

describe("isVehicleDefectAttachable", () => {
  it("offers an open row with no work order", () => {
    expect(isVehicleDefectAttachable(vehicleDefect({ item: "A" }))).toBe(true);
  });

  it.each(["Open", "InProgress", "AwaitingParts"])("hides a row on a %s work order", (status) => {
    expect(
      isVehicleDefectAttachable(vehicleDefect({ item: "A", workOrderId: "wo-1", workOrderStatus: status })),
    ).toBe(false);
  });

  it.each(["Completed", "Cancelled"])("offers an open row whose (legacy) work order is %s", (status) => {
    expect(
      isVehicleDefectAttachable(vehicleDefect({ item: "A", workOrderId: "wo-1", workOrderStatus: status })),
    ).toBe(true);
  });

  it("never offers a resolved row", () => {
    expect(isVehicleDefectAttachable(vehicleDefect({ item: "A", resolvedAtUtc: "2026-10-02T00:00:00Z" }))).toBe(false);
  });
});

describe("prefillFromInspection", () => {
  it("defaults to the open, unattached defects and sends refs, not the inspection id", () => {
    const insp = inspection([
      defect({ item: "Brakes", severity: "OutOfService" }),
      defect({ item: "Wipers", severity: "Minor", workOrderId: "wo-1" }),
      defect({ item: "Horn", severity: "Minor", resolvedAtUtc: "2026-10-02T00:00:00Z" }),
    ]);
    const p = prefillFromInspection(insp, "NL-02");
    expect(p.defects.map((d) => d.item)).toEqual(["Brakes"]);
    expect(defectRefs(p.defects)).toEqual([{ inspectionId: "insp-9", item: "Brakes" }]);
    expect(p).not.toHaveProperty("inspectionId");
    expect(p.source).toBe("PostTripInspection");
    expect(p.priority).toBe("Critical");
    expect(p.title).toBe("Brakes — NL-02");
  });

  it("builds a paper-form line item per defect", () => {
    const p = prefillFromInspection(inspection([defect({ item: "Mirror", severity: "Major", note: "Cracked" })]), "NL-02");
    expect(p.lineItems).toEqual(["Mirror — Major: Cracked"]);
  });
});

describe("prefillFromDefects", () => {
  it("spans inspections, keeping every (inspectionId, item) ref", () => {
    const p = prefillFromDefects(
      [
        vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "Major", tripNumber: "T-1" }),
        vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Minor", tripNumber: "T-2" }),
      ],
      "NL-02",
    );
    expect(defectRefs(p.defects)).toEqual([
      { inspectionId: "i-1", item: "Brakes" },
      { inspectionId: "i-2", item: "Wipers" },
    ]);
    expect(p.description).toContain("2 DVIRs");
    expect(p.sourceRef).toBe("T-1, T-2");
  });

  it.each([
    [["Minor", "Minor"], "Medium"],
    [["Minor", "Major"], "High"],
    [["Major", "OutOfService", "Minor"], "Critical"],
  ] as const)("takes priority from the worst severity %j → %s", (severities, priority) => {
    const rows = severities.map((s, i) => vehicleDefect({ item: `D${i}`, severity: s }));
    expect(prefillFromDefects(rows, "NL-02").priority).toBe(priority);
  });

  it("still builds line items for the paper form", () => {
    const p = prefillFromDefects([vehicleDefect({ item: "Tire", severity: "OutOfService", note: "Cord showing" })], "NL-02");
    expect(p.lineItems).toEqual([defectLineText({ item: "Tire", severity: "OutOfService", note: "Cord showing" })]);
    expect(p.lineItems[0]).toBe("Tire — Out-of-Service: Cord showing");
  });

  it("names the inspection type when all rows share one, and uses the first row's as the source", () => {
    const pre = prefillFromDefects([vehicleDefect({ item: "A" }), vehicleDefect({ item: "B" })], "NL-02");
    expect(pre.title).toBe("Pre-Trip defects — NL-02");
    expect(pre.source).toBe("PreTripInspection");

    const mixed = prefillFromDefects(
      [vehicleDefect({ item: "A", inspectionType: "PostTrip" }), vehicleDefect({ item: "B", inspectionId: "i-2" })],
      "NL-02",
    );
    expect(mixed.title).toBe("Inspection defects — NL-02");
    expect(mixed.source).toBe("PostTripInspection");
  });

  it("keeps sourceRef within the backend's 64-character column", () => {
    const rows = Array.from({ length: 8 }, (_, i) =>
      vehicleDefect({ inspectionId: `i-${i}`, item: `D${i}`, tripNumber: `NL-2026-00${10 + i}` }),
    );
    const ref = prefillFromDefects(rows, "NL-02").sourceRef;
    expect(ref).not.toBeNull();
    expect(ref!.length).toBeLessThanOrEqual(64);
  });

  it("refuses an empty selection", () => {
    expect(() => prefillFromDefects([], "NL-02")).toThrow();
  });
});

describe("inspectionDefectWorkOrderId", () => {
  const statusOf = (s: string | undefined) => () => s;

  it("prefers the defect's own work order", () => {
    expect(inspectionDefectWorkOrderId(defect({ item: "A", workOrderId: "wo-own" }), "wo-legacy", statusOf("Completed"))).toBe("wo-own");
  });

  it.each(["Open", "InProgress", "AwaitingParts", undefined])("holds an open defect on a %s legacy work order", (s) => {
    expect(inspectionDefectWorkOrderId(defect({ item: "A" }), "wo-legacy", statusOf(s))).toBe("wo-legacy");
    expect(isInspectionDefectAttachable(defect({ item: "A" }), "wo-legacy", statusOf(s))).toBe(false);
  });

  it("treats a legacy work order as holding when no status lookup is given", () => {
    expect(inspectionDefectWorkOrderId(defect({ item: "A" }), "wo-legacy")).toBe("wo-legacy");
  });

  it.each(["Completed", "Cancelled"])("releases the defect once the legacy work order is %s", (s) => {
    expect(inspectionDefectWorkOrderId(defect({ item: "A" }), "wo-legacy", statusOf(s))).toBeNull();
    expect(isInspectionDefectAttachable(defect({ item: "A" }), "wo-legacy", statusOf(s))).toBe(true);
  });

  it("never attaches a resolved defect", () => {
    const resolved = defect({ item: "A", resolvedAtUtc: "2026-10-02T00:00:00Z" });
    expect(inspectionDefectWorkOrderId(resolved, "wo-legacy", statusOf("Open"))).toBeNull();
    expect(isInspectionDefectAttachable(resolved, "wo-legacy", statusOf("Open"))).toBe(false);
  });

  it("is null with no work order at all", () => {
    expect(inspectionDefectWorkOrderId(defect({ item: "A" }), null)).toBeNull();
  });
});

describe("settledPendingWorkOrders", () => {
  const key = (d: { inspectionId: string; item: string }) => `${d.inspectionId}:${d.item}`;
  const mark = (over: Partial<PendingWorkOrder> = {}): PendingWorkOrder => ({
    workOrderId: "wo-9",
    recheckAfter: 10_000,
    ...over,
  });
  const pending = new Map([["insp-1:A", mark()]]);

  it("keeps a mark while the row still reads stale inside the window", () => {
    expect(settledPendingWorkOrders(pending, [vehicleDefect({ item: "A" })], 5_000, key)).toEqual([]);
  });

  it("settles once the row is on an open work order", () => {
    const row = vehicleDefect({ item: "A", workOrderId: "wo-9", workOrderStatus: "Open" });
    expect(settledPendingWorkOrders(pending, [row], 5_000, key)).toEqual(["insp-1:A"]);
  });

  it("settles when the row carries this work order even if it was cancelled", () => {
    const row = vehicleDefect({ item: "A", workOrderId: "wo-9", workOrderStatus: "Cancelled" });
    expect(settledPendingWorkOrders(pending, [row], 5_000, key)).toEqual(["insp-1:A"]);
  });

  it("settles a resolved or vanished row", () => {
    const resolved = vehicleDefect({ item: "A", resolvedAtUtc: "2026-10-02T00:00:00Z" });
    expect(settledPendingWorkOrders(pending, [resolved], 5_000, key)).toEqual(["insp-1:A"]);
    expect(settledPendingWorkOrders(pending, [], 5_000, key)).toEqual(["insp-1:A"]);
  });

  it("settles on any load at or after the recheck window, whatever the data says", () => {
    expect(settledPendingWorkOrders(pending, [vehicleDefect({ item: "A" })], 10_000, key)).toEqual(["insp-1:A"]);
  });

  it("does not settle on another work order's cancelled link", () => {
    const row = vehicleDefect({ item: "A", workOrderId: "wo-1", workOrderStatus: "Cancelled" });
    expect(settledPendingWorkOrders(pending, [row], 5_000, key)).toEqual([]);
  });
});
