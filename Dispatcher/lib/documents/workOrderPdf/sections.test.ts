import { describe, expect, it } from "vitest";
import type { WorkOrder } from "@/lib/types";
import { workRequestedBlock, workRequestedItems } from "./sections";

// NL-WO-01 §5: one row per defect line ("Item — Severity: note"), then the
// free-text line items.

function wo(over: Partial<WorkOrder> = {}): WorkOrder {
  return {
    id: "WO-12",
    unit: "NL-02",
    title: "Pre-Trip defects — NL-02",
    description: "",
    status: "Open",
    k: "over",
    priority: "Critical",
    source: "Pre-Trip Inspection",
    sourceRef: "NL-2026-0042",
    createdBy: "Dispatch",
    createdAt: "2026-10-01",
    lineItems: [],
    ...over,
  };
}

describe("workRequestedItems", () => {
  it("prints defect lines first, then free-text line items", () => {
    const items = workRequestedItems(
      wo({
        defectLines: [
          { item: "Brakes", severity: "Out-of-Service", note: "Grinding" },
          { item: "Horn", severity: "Minor" },
        ],
        lineItems: ["Check tire pressures"],
      }),
    );
    expect(items).toEqual(["Brakes — Out-of-Service: Grinding", "Horn — Minor", "Check tire pressures"]);
  });

  it("prints a free-text line that repeats a defect line only once", () => {
    const items = workRequestedItems(
      wo({
        defectLines: [{ item: "Brakes", severity: "Major", note: "Soft pedal" }],
        lineItems: ["Brakes — Major: Soft pedal", "Road test"],
      }),
    );
    expect(items).toEqual(["Brakes — Major: Soft pedal", "Road test"]);
  });

  it("prints a defect once when completion changed its severity, keeping unrelated free text", () => {
    // Raised as Major; the inspection was amended to Out-of-Service and the
    // completed line carries the new severity — the free-text copy still says Major.
    const items = workRequestedItems(
      wo({
        defectLines: [{ item: "Brakes", severity: "Out-of-Service", note: "Soft pedal" }],
        lineItems: ["brakes  — Major: Soft pedal", "Brake fluid flush", "Road test"],
      }),
    );
    expect(items).toEqual(["Brakes — Out-of-Service: Soft pedal", "Brake fluid flush", "Road test"]);
    expect(items.filter((i) => /Major/.test(i))).toHaveLength(0);
  });

  it("falls back to the title with neither defects nor line items", () => {
    expect(workRequestedItems(wo())).toEqual(["Pre-Trip defects — NL-02"]);
  });

  it("is unchanged for a work order with no defect lines", () => {
    expect(workRequestedItems(wo({ lineItems: ["A", "B"] }))).toEqual(["A", "B"]);
  });
});

describe("workRequestedBlock", () => {
  it("renders each defect row, escaped, numbered in order", () => {
    const html = workRequestedBlock(
      wo({ defectLines: [{ item: "Door <rear>", severity: "Major", note: "Won't latch" }], lineItems: ["Road test"] }),
    );
    expect(html).toContain(`<td class="num">1</td><td>Door &lt;rear&gt; — Major: Won't latch</td>`);
    expect(html).toContain('<td class="num">2</td><td>Road test</td>');
  });
});
