import { describe, expect, it } from "vitest";
import {
  currentSeverityOf,
  outcomesFor,
  remainingBlockingDefects,
  validateDefectOutcomes,
  type DefectLineForCompletion,
  type DefectOutcomeDraft,
} from "./workOrderCompletion";
import { vehicleDefect } from "./defect.fixtures";

// Mirrors WorkOrder.RecordOutcomes on the backend: every line answered once, a
// note on every Deferred, no Deferred on an out-of-service line.

const lines: DefectLineForCompletion[] = [
  { inspectionId: "i-1", item: "Brakes", severity: "OutOfService" },
  { inspectionId: "i-1", item: "Wipers", severity: "Minor" },
];

const draft = (over: Partial<DefectOutcomeDraft> & { item: string }): DefectOutcomeDraft => ({
  inspectionId: "i-1",
  outcome: null,
  note: "",
  ...over,
});

describe("validateDefectOutcomes", () => {
  it("accepts one answer per line and returns the wire body in line order", () => {
    const r = validateDefectOutcomes(lines, [
      draft({ item: "Wipers", outcome: "Deferred", note: "  Parts on order " }),
      draft({ item: "Brakes", outcome: "Repaired" }),
    ]);
    expect(r).toEqual({
      ok: true,
      defectOutcomes: [
        { inspectionId: "i-1", item: "Brakes", outcome: "Repaired", note: null },
        { inspectionId: "i-1", item: "Wipers", outcome: "Deferred", note: "Parts on order" },
      ],
    });
  });

  it("validates a work order with no lines to an empty list", () => {
    expect(validateDefectOutcomes([], [])).toEqual({ ok: true, defectOutcomes: [] });
  });

  it("has no default — an unanswered line fails", () => {
    const r = validateDefectOutcomes(lines, [draft({ item: "Brakes", outcome: "Repaired" }), draft({ item: "Wipers" })]);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.issues).toEqual([{ inspectionId: "i-1", item: "Wipers", message: "Choose an outcome." }]);
  });

  it("fails a line with no draft at all", () => {
    const r = validateDefectOutcomes(lines, [draft({ item: "Brakes", outcome: "NoFaultFound" })]);
    expect(r.ok).toBe(false);
  });

  it("requires a note on Deferred", () => {
    const r = validateDefectOutcomes(lines, [
      draft({ item: "Brakes", outcome: "Repaired" }),
      draft({ item: "Wipers", outcome: "Deferred", note: "   " }),
    ]);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.issues[0].message).toMatch(/why/i);
  });

  it("never lets an out-of-service defect be deferred", () => {
    const r = validateDefectOutcomes(lines, [
      draft({ item: "Brakes", outcome: "Deferred", note: "Next week" }),
      draft({ item: "Wipers", outcome: "Repaired" }),
    ]);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.issues[0]).toMatchObject({ item: "Brakes", message: expect.stringMatching(/cannot be deferred/) });
  });

  it("rejects a duplicate and an unknown outcome", () => {
    const r = validateDefectOutcomes(lines, [
      draft({ item: "Brakes", outcome: "Repaired" }),
      draft({ item: " brakes ", outcome: "NoFaultFound" }),
      draft({ item: "Wipers", outcome: "Repaired" }),
      draft({ item: "Horn", outcome: "Repaired" }),
    ]);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.issues.map((i) => i.message)).toEqual([
      "This defect has more than one outcome.",
      "This defect is not on the work order.",
    ]);
  });

  it("matches items trimmed and case-insensitively, like the backend", () => {
    const r = validateDefectOutcomes(lines, [
      draft({ item: "BRAKES ", outcome: "Repaired" }),
      draft({ item: "wipers", outcome: "NoFaultFound" }),
    ]);
    expect(r.ok).toBe(true);
  });
});

describe("current severity (an inspection amended after the work order was raised)", () => {
  // The line snapshots severity when the work order is raised; the backend
  // judges Deferred on the defect's CURRENT severity on its inspection, falling
  // back to the snapshot only when the defect is gone.

  it("blocks Deferred on a line amended UP to out-of-service", () => {
    const current = [vehicleDefect({ inspectionId: "i-1", item: "Wipers", severity: "OutOfService" })];
    const r = validateDefectOutcomes(
      lines,
      [draft({ item: "Brakes", outcome: "Repaired" }), draft({ item: "Wipers", outcome: "Deferred", note: "Parts" })],
      current,
    );
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.issues).toEqual([
      { inspectionId: "i-1", item: "Wipers", message: "An out-of-service defect cannot be deferred." },
    ]);
  });

  it("allows Deferred on a line amended DOWN from out-of-service", () => {
    const current = [vehicleDefect({ inspectionId: "i-1", item: "brakes ", severity: "Major" })];
    const r = validateDefectOutcomes(
      lines,
      [draft({ item: "Brakes", outcome: "Deferred", note: "Next shop visit" }), draft({ item: "Wipers", outcome: "Repaired" })],
      current,
    );
    expect(r.ok).toBe(true);
  });

  it("falls back to the snapshot when the defect is not in the list", () => {
    expect(currentSeverityOf(lines[0], [])).toBe("OutOfService");
    expect(currentSeverityOf(lines[0], null)).toBe("OutOfService");
    expect(currentSeverityOf(lines[1], [vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Major" })])).toBe(
      "Minor",
    );
  });

  it("matches the defect trimmed and case-insensitively", () => {
    expect(currentSeverityOf(lines[1], [vehicleDefect({ inspectionId: "i-1", item: " WIPERS", severity: "Major" })])).toBe(
      "Major",
    );
  });
});

describe("outcomesFor", () => {
  it("does not offer Deferred for an out-of-service defect", () => {
    expect(outcomesFor("OutOfService")).toEqual(["Repaired", "NoFaultFound"]);
    expect(outcomesFor("Major")).toContain("Deferred");
    expect(outcomesFor("Minor")).toContain("Deferred");
  });
});

describe("remainingBlockingDefects", () => {
  const open = [
    vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "OutOfService" }),
    vehicleDefect({ inspectionId: "i-1", item: "Mirror", severity: "Major" }),
    vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Minor" }),
  ];

  it("subtracts what this completion cleared — the read model lags the write", () => {
    const left = remainingBlockingDefects(open, [
      { inspectionId: "i-1", item: "Brakes", outcome: "Repaired" },
      { inspectionId: "i-1", item: "Mirror", outcome: "NoFaultFound" },
    ]);
    expect(left).toEqual([]);
  });

  it("ignores Minor defects", () => {
    expect(remainingBlockingDefects([open[2]], [])).toEqual([]);
  });

  it("keeps a Deferred Major open", () => {
    const left = remainingBlockingDefects(open, [
      { inspectionId: "i-1", item: "Brakes", outcome: "Repaired" },
      { inspectionId: "i-1", item: "Mirror", outcome: "Deferred", note: "Parts" },
    ]);
    expect(left.map((d) => d.item)).toEqual(["Mirror"]);
  });

  it("keeps blocking defects that were not on this work order", () => {
    const left = remainingBlockingDefects(open, [{ inspectionId: "i-1", item: "Mirror", outcome: "Repaired" }]);
    expect(left.map((d) => d.item)).toEqual(["Brakes"]);
  });
});
