import { describe, expect, it } from "vitest";
import type {
  DefectRepairOutcomeWire,
  DefectResolutionReasonWire,
  InspectionDefectWire,
  WorkOrderWire,
} from "./api/maintenance";
import {
  DEFECT_OUTCOME_HELP,
  DEFECT_OUTCOME_LABEL,
  DEFECT_OUTCOME_META,
  DEFECT_OUTCOMES,
  DEFECT_RESOLUTION_LABEL,
  DEFECT_RESOLUTION_META,
  inspectionDefectStatus,
  resolvedDefectChip,
  toPrintableWorkOrder,
} from "./workOrderDisplay";
import { statusMeta } from "./theme";

// Status is never colour alone: every outcome and every resolution reason must
// carry a colour, an icon and a text label.

const OUTCOMES: DefectRepairOutcomeWire[] = ["Repaired", "NoFaultFound", "Deferred"];
const REASONS: DefectResolutionReasonWire[] = [
  "RepairedUnderWorkOrder",
  "PreviouslyRepaired",
  "ReportedInError",
  "AcceptedMonitoring",
  "NoFaultFound",
];

describe("defect outcome display", () => {
  it.each(OUTCOMES)("%s has a label, a colour kind, a glyph and help copy", (o) => {
    expect(DEFECT_OUTCOME_LABEL[o]).toBeTruthy();
    expect(DEFECT_OUTCOME_META[o].kind).toBeTruthy();
    expect(DEFECT_OUTCOME_META[o].glyph).toBeTruthy();
    expect(DEFECT_OUTCOME_HELP[o]).toBeTruthy();
  });

  it("lists every outcome once", () => {
    expect([...DEFECT_OUTCOMES].sort()).toEqual([...OUTCOMES].sort());
  });

  it("Repaired and No fault found are teal but told apart by glyph; Deferred is gold ◐", () => {
    expect(DEFECT_OUTCOME_META.Repaired).toEqual({ kind: "ontime", glyph: "✓" });
    expect(DEFECT_OUTCOME_META.NoFaultFound.kind).toBe("ontime");
    expect(DEFECT_OUTCOME_META.NoFaultFound.glyph).not.toBe(DEFECT_OUTCOME_META.Repaired.glyph);
    expect(DEFECT_OUTCOME_META.Deferred).toEqual({ kind: "soon", glyph: "◐" });
    expect(statusMeta("ontime").c).toBe("#009E73");
    expect(statusMeta("soon").c).toBe("#E1B000");
  });
});

describe("defect resolution display", () => {
  it.each(REASONS)("%s has a label and a colour kind with a glyph", (r) => {
    expect(DEFECT_RESOLUTION_LABEL[r]).toBeTruthy();
    const m = DEFECT_RESOLUTION_META[r];
    expect(m.kind).toBeTruthy();
    expect(m.glyph ?? statusMeta(m.kind).g).toBeTruthy();
  });
});

describe("toPrintableWorkOrder", () => {
  const wire: WorkOrderWire = {
    id: "wo-guid",
    vehicleId: "veh-1",
    number: "WO-12",
    title: "Brakes — NL-02",
    description: "",
    status: "Open",
    priority: "Critical",
    source: "PreTripInspection",
    sourceRef: "NL-2026-0042",
    createdBy: "Dispatch",
    createdAt: "2026-10-01T12:00:00Z",
    assignedTo: null,
    dueDate: null,
    lineItems: [],
    completedAt: null,
    resolvingServiceId: null,
    shopId: null,
    authorizedLimitCad: null,
    budgetCode: null,
    dateRequiredOrOos: null,
    defects: [
      { inspectionId: "i-1", item: "Brakes", severity: "OutOfService", note: "Grinding", outcome: null, outcomeNote: null },
      { inspectionId: "i-1", item: "Horn", severity: "Minor", note: null, outcome: null, outcomeNote: null },
    ],
  };

  it("carries defect lines with the severity as its display label", () => {
    expect(toPrintableWorkOrder(wire, "NL-02").defectLines).toEqual([
      { item: "Brakes", severity: "Out-of-Service", note: "Grinding" },
      { item: "Horn", severity: "Minor", note: undefined },
    ]);
  });
});

// The live defect chip beside an inspection's frozen result chip.
describe("inspectionDefectStatus", () => {
  const d = (over: Partial<InspectionDefectWire> & { item: string }): InspectionDefectWire => ({
    severity: "Major",
    note: null,
    workOrderId: null,
    resolutionReason: null,
    resolvedAtUtc: null,
    ...over,
  });
  const resolved = { resolutionReason: "ReportedInError" as const, resolvedAtUtc: "2026-10-02T00:00:00Z" };

  it("says nothing when the inspection reported no defects", () => {
    expect(inspectionDefectStatus({ defects: [] })).toBeNull();
  });

  it("all open: counts them in the WORST severity's colour + glyph", () => {
    expect(
      inspectionDefectStatus({
        defects: [d({ item: "Wipers", severity: "Minor" }), d({ item: "Brakes", severity: "OutOfService" }), d({ item: "Mirror" })],
      }),
    ).toEqual({ kind: "over", glyph: "✕", label: "3 open defects" });
    expect(inspectionDefectStatus({ defects: [d({ item: "Mirror" }), d({ item: "Wipers", severity: "Minor" })] })).toEqual({
      kind: "over",
      glyph: undefined,
      label: "2 open defects",
    });
    expect(inspectionDefectStatus({ defects: [d({ item: "Wipers", severity: "Minor" })] })).toEqual({
      kind: "soon",
      glyph: undefined,
      label: "1 open defect",
    });
  });

  it("a defect on an open work order is still open", () => {
    expect(inspectionDefectStatus({ defects: [d({ item: "Brakes", workOrderId: "wo-1" })] })?.label).toBe("1 open defect");
  });

  it("all resolved: teal, with words", () => {
    const s = inspectionDefectStatus({
      defects: [d({ item: "Brakes", severity: "OutOfService", ...resolved }), d({ item: "Wipers", ...resolved })],
    });
    expect(s).toEqual({ kind: "ontime", label: "Defects resolved" });
    expect(statusMeta(s!.kind).g).toBeTruthy();
  });

  it("mixed: counts only the open ones, and a resolved Out-of-Service no longer sets the colour", () => {
    expect(
      inspectionDefectStatus({
        defects: [d({ item: "Brakes", severity: "OutOfService", ...resolved }), d({ item: "Wipers", severity: "Minor" })],
      }),
    ).toEqual({ kind: "soon", glyph: undefined, label: "1 open defect" });
  });
});

describe("resolvedDefectChip", () => {
  it("is teal and names the reason, with that reason's glyph", () => {
    expect(resolvedDefectChip("ReportedInError")).toEqual({
      kind: "ontime",
      glyph: undefined,
      label: "Resolved · Reported in error",
    });
    expect(resolvedDefectChip("NoFaultFound")).toEqual({
      kind: "ontime",
      glyph: "○",
      label: "Resolved · No fault found under work order",
    });
  });

  it("falls back to plain Resolved, and prints an unmapped reason as-is", () => {
    expect(resolvedDefectChip(null)).toEqual({ kind: "ontime", label: "Resolved" });
    expect(resolvedDefectChip("SomethingNew")).toEqual({ kind: "ontime", glyph: undefined, label: "Resolved · SomethingNew" });
  });
});
