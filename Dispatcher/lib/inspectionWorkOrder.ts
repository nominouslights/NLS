import type { Inspection, InspectionDefect, WorkOrderPrefill, WorkOrderPriority } from "./types";

// Build a WorkOrderPrefill from an inspection's defects — used both when
// generating a work order from all defects and from a single failed item.

export function prefillFromInspection(
  insp: Inspection,
  unit: string,
  defects: InspectionDefect[] = insp.defects,
): WorkOrderPrefill {
  const priority: WorkOrderPriority = defects.some((d) => d.severity === "Out-of-Service")
    ? "Critical"
    : defects.some((d) => d.severity === "Major")
      ? "High"
      : "Medium";
  const single = defects.length === 1;
  return {
    source: insp.type === "Pre-Trip" ? "Pre-Trip Inspection" : "Post-Trip Inspection",
    sourceRef: insp.id,
    title: single ? `${defects[0].item} — ${unit}` : `${insp.type} defects — ${unit}`,
    description: `Defect(s) recorded on ${insp.id} (${insp.type} DVIR).`,
    lineItems: defects.map((d) => `${d.item} — ${d.severity}${d.note ? `: ${d.note}` : ""}`),
    priority,
    inspectionId: insp.id,
  };
}
